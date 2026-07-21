using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Orchestrates the on-demand "Profundizar" (Knowledge Expansion) feature
/// (2026-07-20): given a CapabilityGraphNodeId, returns a cached expansion
/// if one already exists, otherwise generates one (Bing Grounding + LLM
/// knowledge + optional diagram) and persists it so subsequent learners
/// reuse the same result — same "generate once, serve many" pattern as
/// node illustrations.
///
/// Singleton, same rationale as PdfCapabilityGraphPipelineService: holds no
/// per-request state, constructs its own stateless
/// CapabilityGraphIllustrationStorageService directly (rather than via DI)
/// so it stays Singleton-safe.
/// </summary>
public sealed class KnowledgeExpansionService
{
    private readonly KnowledgeExpansionAgent _agent;
    private readonly WebGroundingService _webGrounding;
    private readonly GraphIllustrationImageService _imageService;
    private readonly NodeKnowledgeIndexService _knowledgeIndexService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    /// <summary>Fixed image index for the (at most one) diagram cached per
    /// node's knowledge expansion — never collides with the low sequential
    /// indexes (1, 2, ...) the Studio creation pipeline uses for
    /// Hypothesis/Teaching illustrations.</summary>
    private const int DiagramImageIndex = 99;

    public KnowledgeExpansionService(
        KnowledgeExpansionAgent agent,
        WebGroundingService webGrounding,
        GraphIllustrationImageService imageService,
        NodeKnowledgeIndexService knowledgeIndexService,
        IConfiguration configuration)
    {
        _agent = agent;
        _webGrounding = webGrounding;
        _imageService = imageService;
        _knowledgeIndexService = knowledgeIndexService;
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
    }

    public bool IsConfigured => _agent.IsConfigured;

    /// <summary>
    /// Returns the cached expansion for <paramref name="nodeId"/> if one
    /// already exists, otherwise generates and persists a new one.
    /// </summary>
    public async Task<CapabilityGraphNodeKnowledgeExpansion> GetOrCreateAsync(
        HumanOsDbContext dbContext,
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.CapabilityGraphNodeKnowledgeExpansions
            .FirstOrDefaultAsync(e => e.CapabilityGraphNodeId == nodeId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (!_agent.IsConfigured)
        {
            throw new InvalidOperationException(
                "KnowledgeExpansionAgent is not configured. Set 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName'.");
        }

        var node = await dbContext.CapabilityGraphNodes
            .Include(n => n.Illustrations)
            .FirstOrDefaultAsync(n => n.CapabilityGraphNodeId == nodeId, cancellationToken);

        if (node is null)
        {
            throw new InvalidOperationException($"CapabilityGraphNode '{nodeId}' not found.");
        }

        // Base content the expansion must NOT repeat — same field used for
        // the Teaching step, falling back progressively if unset.
        var baseContent = !string.IsNullOrWhiteSpace(node.Interpretation)
            ? node.Interpretation
            : (!string.IsNullOrWhiteSpace(node.AcademicDefinition)
                ? node.AcademicDefinition
                : node.Description ?? node.Name);

        // Always attempted if configured — this is an explicit, deliberate
        // learner action (unlike the Studio pipeline's NeedsCurrentInfo
        // gating), so the search cost is always justified. Best-effort: a
        // Bing Grounding failure must never block the expansion itself.
        string? webFindingsText = null;
        if (_webGrounding.IsConfigured)
        {
            try
            {
                var webResult = await _webGrounding.SearchAsync(node.Name, node.Description ?? node.Name, cancellationToken);
                webFindingsText = webResult.Text;
            }
            catch (Exception)
            {
                webFindingsText = null;
            }
        }

        var expansion = await _agent.ExpandAsync(node.Name, baseContent, webFindingsText, cancellationToken);

        var diagramIllustrationId = await TryGenerateDiagramAsync(dbContext, node, expansion.DiagramPrompt, cancellationToken);

        var entity = new CapabilityGraphNodeKnowledgeExpansion
        {
            CapabilityGraphNodeKnowledgeExpansionId = Guid.NewGuid(),
            CapabilityGraphNodeId = node.CapabilityGraphNodeId,
            Content = expansion.ExpandedContentHtml,
            DiagramIllustrationId = diagramIllustrationId,
            CreatedDate = DateTime.UtcNow
        };

        dbContext.CapabilityGraphNodeKnowledgeExpansions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Best-effort: add this freshly-generated expansion as extra RAG
        // chunks (2026-07-20) on top of the node's base content, so a
        // future Tutor question elsewhere in the same graph can retrieve
        // this deeper material too. Never blocks returning the expansion
        // itself to the caller.
        try
        {
            await _knowledgeIndexService.IndexKnowledgeExpansionAsync(
                dbContext, node.CapabilityGraphNodeId, node.CapabilityGraphId, entity.Content, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort — see comment above.
        }

        return entity;
    }

    /// <summary>
    /// Best-effort diagram generation: never throws, never blocks the text
    /// expansion from being returned/persisted. Derives the tenantId/
    /// capabilityId needed for the blob storage path from one of the
    /// node's EXISTING illustrations (they were uploaded with the real
    /// values at capability-creation time) rather than re-deriving tenancy
    /// context here — if the node has none yet, the diagram is skipped.
    /// </summary>
    private async Task<Guid?> TryGenerateDiagramAsync(
        HumanOsDbContext dbContext,
        CapabilityGraphNode node,
        string? diagramPrompt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(diagramPrompt) || !_imageService.IsConfigured || !_illustrationStorage.IsConfigured)
        {
            return null;
        }

        var pathSeed = node.Illustrations.FirstOrDefault()?.StoragePath;
        if (pathSeed is null)
        {
            return null;
        }

        var segments = pathSeed.Split('/');
        if (segments.Length < 3 || !Guid.TryParse(segments[0], out var tenantId) || !Guid.TryParse(segments[1], out var capabilityId))
        {
            return null;
        }

        try
        {
            var generated = await _imageService.GenerateAsync(diagramPrompt, cancellationToken);
            using var imageStream = generated.ImageBytes.ToStream();

            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                tenantId, capabilityId, node.CapabilityGraphNodeId, DiagramImageIndex, imageStream,
                cancellationToken: cancellationToken);

            var illustration = new CapabilityGraphNodeIllustration
            {
                CapabilityGraphNodeIllustrationId = Guid.NewGuid(),
                CapabilityGraphNodeId = node.CapabilityGraphNodeId,
                StoragePath = storagePath,
                Prompt = diagramPrompt,
                Purpose = IllustrationPurpose.KnowledgeExpansion,
                ImageModel = generated.ImageModel,
                Width = generated.Width,
                Height = generated.Height,
                CreatedDate = DateTime.UtcNow
            };

            dbContext.CapabilityGraphNodeIllustrations.Add(illustration);
            return illustration.CapabilityGraphNodeIllustrationId;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
