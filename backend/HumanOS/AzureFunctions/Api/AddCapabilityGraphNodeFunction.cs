using System.Net;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.Capabilities;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio — professor/author tool: inserts ONE new learning node
/// into an ALREADY-PERSISTED CapabilityGraph, from a free-text prompt plus an
/// explicit choice of which existing nodes it requires (prerequisites) and/or
/// which existing nodes should now require it (dependents).
///
/// "Nivel" in the Studio UI is never a stored column — it's the node's depth
/// in the Requires-edge DAG (see capabilitystudio/PreviewGraphPage.tsx's
/// groupIntoLevels). Connecting the new node to require the right
/// prerequisite nodes (and, optionally, being required by the right
/// dependent nodes) is what actually places it at the desired level.
/// </summary>
public sealed class AddCapabilityGraphNodeFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly GraphNodeAdditionAgent _agent;
    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly CapabilityGraphPersistenceService _persistenceService;
    private readonly ExperienceDesignerAgent _experienceDesignerAgent;
    private readonly ExampleCardIllustrationService _exampleCardIllustrationService;
    private readonly NodeExperienceBlueprintPersistenceService _blueprintPersistenceService;
    private readonly BlueprintValidatorAgent _blueprintValidatorAgent;
    private readonly BlueprintValidationPersistenceService _blueprintValidationPersistenceService;
    private readonly PdfImageDescriptionAgent _imageDescriptionAgent;

    public AddCapabilityGraphNodeFunction(
        HumanOsDbContext dbContext,
        GraphNodeAdditionAgent agent,
        GraphIllustrationImageService imageService,
        IConfiguration configuration,
        CapabilityGraphPersistenceService persistenceService,
        ExperienceDesignerAgent experienceDesignerAgent,
        ExampleCardIllustrationService exampleCardIllustrationService,
        NodeExperienceBlueprintPersistenceService blueprintPersistenceService,
        BlueprintValidatorAgent blueprintValidatorAgent,
        BlueprintValidationPersistenceService blueprintValidationPersistenceService,
        PdfImageDescriptionAgent imageDescriptionAgent)
    {
        _dbContext = dbContext;
        _agent = agent;
        _imageService = imageService;
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
        _persistenceService = persistenceService;
        _experienceDesignerAgent = experienceDesignerAgent;
        _exampleCardIllustrationService = exampleCardIllustrationService;
        _blueprintPersistenceService = blueprintPersistenceService;
        _blueprintValidatorAgent = blueprintValidatorAgent;
        _blueprintValidationPersistenceService = blueprintValidationPersistenceService;
        _imageDescriptionAgent = imageDescriptionAgent;
    }

    [Function("AddCapabilityGraphNode")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "capabilities/{capabilityId:guid}/graph/nodes")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotImplemented, "NotConfigured",
                "The Graph Node Addition agent is not configured on this environment.", cancellationToken);
        }

        var body = await request.ReadFromJsonAsync<AddCapabilityGraphNodeRequest>(cancellationToken)
            ?? new AddCapabilityGraphNodeRequest();

        if (string.IsNullOrWhiteSpace(body.Prompt))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Prompt is required.", cancellationToken);
        }

        var illustrationMode = string.IsNullOrWhiteSpace(body.IllustrationMode) ? "AiGenerated" : body.IllustrationMode;
        var needsUploadedImage = illustrationMode is "UploadDirect" or "AiGeneratedFromUpload";

        byte[]? uploadedImageBytes = null;
        if (!string.IsNullOrWhiteSpace(body.UploadedImageBase64))
        {
            try
            {
                uploadedImageBytes = Convert.FromBase64String(body.UploadedImageBase64);
            }
            catch (FormatException)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidBase64", "'UploadedImageBase64' is not valid base64.", cancellationToken);
            }
        }

        if (needsUploadedImage && uploadedImageBytes is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "UploadedImageBase64 is required when IllustrationMode is 'UploadDirect' or 'AiGeneratedFromUpload'.",
                cancellationToken);
        }

        var graph = await _dbContext.CapabilityGraphs
            .Include(g => g.Nodes).ThenInclude(n => n.Illustrations)
            .Include(g => g.Edges)
            .FirstOrDefaultAsync(g => g.CapabilityId == capabilityId, cancellationToken);

        if (graph is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "GraphNotFound",
                $"No CapabilityGraph found for capability {capabilityId}.", cancellationToken);
        }

        // Read the owning Capability's language + tone directives so a node
        // added here matches the rest of its capability's content — without
        // this, every "add node" call silently defaulted to Spanish/Standard
        // tone regardless of the capability's own ContentLanguageCode/
        // PedagogicalAudience (2026-07-26 fix, K-12 lesson course feature).
        var capabilityMeta = await _dbContext.Capabilities
            .Where(c => c.CapabilityId == capabilityId)
            .Select(c => new { c.ContentLanguageCode, c.PedagogicalAudience, c.IncludeExamplesStep, c.IncludeProductionStep, c.MaxDeepDiveCardsPerNode })
            .FirstOrDefaultAsync(cancellationToken);
        var languageCode = capabilityMeta?.ContentLanguageCode ?? "es";
        var pedagogicalAudience = capabilityMeta?.PedagogicalAudience;
        var includeExamplesStep = capabilityMeta?.IncludeExamplesStep ?? true;
        var includeProductionStep = capabilityMeta?.IncludeProductionStep ?? true;
        var maxDeepDiveCardsPerNode = capabilityMeta?.MaxDeepDiveCardsPerNode;

        var nodesById = graph.Nodes.ToDictionary(n => n.CapabilityGraphNodeId);

        var unknownIds = body.RequiresNodeIds.Concat(body.RequiredByNodeIds).Where(id => !nodesById.ContainsKey(id)).ToList();
        if (unknownIds.Count > 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "UnknownNodeIds",
                "One or more RequiresNodeIds/RequiredByNodeIds do not belong to this capability's graph.",
                cancellationToken);
        }

        // Deterministic DAG-safety check BEFORE calling the LLM: simulate the
        // new node with a placeholder id and verify the chosen wiring cannot
        // create a circular prerequisite chain.
        var placeholderNodeId = Guid.NewGuid();
        var proposedEdges = body.RequiresNodeIds.Select(id => (Source: placeholderNodeId, Target: id))
            .Concat(body.RequiredByNodeIds.Select(id => (Source: id, Target: placeholderNodeId)));
        var existingEdges = graph.Edges.Select(e => (Source: e.SourceNodeId, Target: e.TargetNodeId));

        if (GraphCycleValidator.WouldCreateCycle(existingEdges, proposedEdges))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "WouldCreateCycle",
                "Connecting the new node this way would create a circular prerequisite chain. " +
                "Revisa los nodos elegidos como 'requiere' / 'es requerido por'.", cancellationToken);
        }

        var requiresNodes = body.RequiresNodeIds
            .Select(id => (nodesById[id].Name, nodesById[id].Description)).ToList();
        var requiredByNodes = body.RequiredByNodeIds
            .Select(id => (nodesById[id].Name, nodesById[id].Description)).ToList();
        var existingNodeNames = graph.Nodes.Select(n => n.Name).ToList();

        GraphNodeAdditionAgent.NodeAdditionResult result;
        try
        {
            result = await _agent.GenerateNodeAsync(
                graph.Name, graph.Description, existingNodeNames, requiresNodes, requiredByNodes,
                body.Prompt, languageCode, pedagogicalAudience, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "GenerationFailed",
                $"Failed to generate the new node: {ex.Message}", cancellationToken);
        }

        var node = result.Node;

        if (existingNodeNames.Any(n => string.Equals(n, node.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "DuplicateNodeName",
                $"El nodo generado ('{node.Name}') ya existe en este grafo. Intenta con un prompt más específico.",
                cancellationToken);
        }

        // Best-effort illustrations (never block node creation on failure).
        // tenantId is parsed from an existing illustration's StoragePath —
        // same technique KnowledgeExpansionService uses (2026-07-20) — since
        // neither Capability nor CapabilityGraph stores a TenantId column.
        var illustrationRecords = new List<CapabilityGraphPersistenceService.NodeIllustrationRecord>();
        if (illustrationMode != "None" && _illustrationStorage.IsConfigured)
        {
            var tenantId = TryParseTenantIdFromExistingIllustration(graph.Nodes);
            if (tenantId is not null && illustrationMode == "UploadDirect" && uploadedImageBytes is not null)
            {
                // Option 1: use the professor's own image AS-IS, no AI image
                // generation at all — vision AI best-effort extracts a Caption
                // describing its content so it isn't just a bare, undescribed image.
                string? caption = null;
                if (_imageDescriptionAgent.IsConfigured)
                {
                    try
                    {
                        var described = await _imageDescriptionAgent.DescribeAsync(
                            uploadedImageBytes, body.UploadedImageContentType ?? "image/png", node.Description, cancellationToken);
                        caption = described.Description;
                    }
                    catch
                    {
                        // Best-effort: a missing caption never blocks using the uploaded image.
                    }
                }

                try
                {
                    using var uploadStream = new MemoryStream(uploadedImageBytes);
                    var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                        tenantId.Value, capabilityId, node.NodeId, 1, uploadStream,
                        contentType: body.UploadedImageContentType ?? "image/png", cancellationToken: cancellationToken);

                    illustrationRecords.Add(new CapabilityGraphPersistenceService.NodeIllustrationRecord
                    {
                        NodeId = node.NodeId,
                        StoragePath = storagePath,
                        Prompt = "Imagen proporcionada por el profesor.",
                        Caption = caption,
                        ImageModel = "user-upload",
                        Width = 0,
                        Height = 0,
                        Purpose = IllustrationPurpose.Teaching
                    });
                }
                catch
                {
                    // Best-effort: a failed upload never blocks node creation.
                }
            }
            else if (tenantId is not null && _imageService.IsConfigured && node.IllustrationPrompts.Count > 0)
            {
                // Options 2/3: the AI generates the illustration(s) from
                // IllustrationPrompts — either plain text-to-image guided by the
                // capability's own brand logo ("AiGenerated", unchanged default
                // behavior), or an image-edit call referencing the professor's own
                // uploaded image instead ("AiGeneratedFromUpload").
                var useUploadedReference = illustrationMode == "AiGeneratedFromUpload" && uploadedImageBytes is not null;
                var brandLogoBytes = useUploadedReference
                    ? null
                    : await _illustrationStorage.TryGetBrandLogoBytesAsync(_dbContext, capabilityId, cancellationToken);

                foreach (var illustrationPrompt in node.IllustrationPrompts)
                {
                    try
                    {
                        var generated = useUploadedReference
                            ? await _imageService.GenerateFromReferenceAsync(illustrationPrompt.Prompt, uploadedImageBytes!, cancellationToken)
                            : await _imageService.GenerateAsync(illustrationPrompt.Prompt, brandLogoBytes, cancellationToken);
                        using var stream = generated.ImageBytes.ToStream();
                        var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                            tenantId.Value, capabilityId, node.NodeId, illustrationRecords.Count + 1, stream,
                            cancellationToken: cancellationToken);

                        illustrationRecords.Add(new CapabilityGraphPersistenceService.NodeIllustrationRecord
                        {
                            NodeId = node.NodeId,
                            StoragePath = storagePath,
                            Prompt = illustrationPrompt.Prompt,
                            ImageModel = generated.ImageModel,
                            Width = generated.Width,
                            Height = generated.Height,
                            Purpose = illustrationPrompt.Purpose
                        });
                    }
                    catch
                    {
                        // Best-effort: a single failed illustration never blocks node creation.
                    }
                }
            }
        }

        var nextSortOrder = (graph.Nodes.Count > 0 ? graph.Nodes.Max(n => n.SortOrder) : 0) + 1;

        var savedNode = await _persistenceService.AddNodeAsync(
            _dbContext, graph.CapabilityGraphId, node, body.RequiresNodeIds, body.RequiredByNodeIds,
            nextSortOrder, illustrationRecords, body.TargetChapterIndex, cancellationToken);

        // A node with no NodeExperienceBlueprint cannot be previewed or
        // taught at all (GetNodeBlueprintFunction 404s) — so this is NOT
        // best-effort/optional the way illustrations are: it mirrors Paso 3
        // (ExperienceDesigner) + Paso 4 (BlueprintValidator) of the main
        // capability-creation pipeline for this one new node.
        bool blueprintGenerated = false;
        string? blueprintWarning = null;
        var exampleIllustrationsGenerated = 0;
        try
        {
            var savedIllustrations = await _dbContext.CapabilityGraphNodeIllustrations
                .Where(i => i.CapabilityGraphNodeId == savedNode.CapabilityGraphNodeId)
                .OrderBy(i => i.CreatedDate)
                .ToListAsync(cancellationToken);

            var availableIllustrations = savedIllustrations
                .Select((illustration, index) => new AvailableIllustrationDto
                {
                    Index = index + 1,
                    Prompt = illustration.Prompt,
                    Caption = illustration.Caption,
                    Purpose = illustration.Purpose
                })
                .ToList();

            var exampleTenantId = TryParseTenantIdFromExistingIllustration(graph.Nodes);
            var exampleBrandLogoBytes = exampleTenantId is not null
                ? await _illustrationStorage.TryGetBrandLogoBytesAsync(_dbContext, capabilityId, cancellationToken)
                : null;

            // Bounded retry (2026-08-06 fix — see /memories/repo notes on the
            // "Control de Edición" orphaned-node bug): this used to run ONCE —
            // any transient failure (LLM hiccup, an image-quota burst
            // outlasting GraphIllustrationImageService's own retry budget)
            // permanently left the node with no blueprint at all. Retrying the
            // FULL design+illustrate+persist step from scratch is safe:
            // nothing is persisted until PersistAsync succeeds.
            NodeExperienceBlueprint blueprintEntity = null!;
            (blueprintEntity, exampleIllustrationsGenerated) = await HumanOS.Common.TransientRetry.ExecuteAsync(
                async () =>
                {
                    var designResult = await _experienceDesignerAgent.DesignBlueprintAsync(
                        savedNode, availableIllustrations, webFindings: null, languageCode: languageCode,
                        pedagogicalAudience: pedagogicalAudience, includeExamplesStep: includeExamplesStep,
                        maxDeepDiveCardsPerNode: maxDeepDiveCardsPerNode,
                        includeProductionStep: includeProductionStep, cancellationToken: cancellationToken);

                    // Real illustrations for the Examples step's cards (2026-07-29):
                    // must happen AFTER the agent invents the cards' Title/Text, and
                    // BEFORE persisting, so each card's IllustrationId is already
                    // resolved in the JSON blob. Best-effort per card, same
                    // tenantId-lookup technique as the node illustrations above.
                    var thisAttemptExampleIllustrations = 0;
                    if (includeExamplesStep)
                    {
                        var examplesStepDto = designResult.Blueprint.Steps
                            .FirstOrDefault(s => s.StepType == ExperienceStepType.DeepDive);
                        if (examplesStepDto?.ExampleCards is { Count: > 0 } && exampleTenantId is not null)
                        {
                            thisAttemptExampleIllustrations = await _exampleCardIllustrationService.GenerateAsync(
                                _dbContext, exampleTenantId.Value, capabilityId, savedNode.CapabilityGraphNodeId,
                                savedIllustrations.Count, examplesStepDto.ExampleCards, cancellationToken, exampleBrandLogoBytes,
                                pedagogicalAudience);
                        }
                    }

                    var persisted = await _blueprintPersistenceService.PersistAsync(
                        _dbContext, savedNode.CapabilityGraphNodeId, designResult.Blueprint, savedIllustrations,
                        includeExamplesStep: includeExamplesStep, includeProductionStep: includeProductionStep, cancellationToken: cancellationToken);

                    return (persisted, thisAttemptExampleIllustrations);
                },
                maxAttempts: 3,
                cancellationToken);

            blueprintGenerated = true;

            if (_blueprintValidatorAgent.IsConfigured)
            {
                try
                {
                    var validationResult = await _blueprintValidatorAgent.ValidateAsync(
                        savedNode, blueprintEntity, savedIllustrations.Count, cancellationToken);

                    await _blueprintValidationPersistenceService.PersistAsync(
                        _dbContext, blueprintEntity.NodeExperienceBlueprintId, validationResult.Validation,
                        validationResult.TokenUsage, cancellationToken);
                }
                catch
                {
                    // Validation is a quality-gate record, not a hard
                    // requirement for the node to be previewable/teachable —
                    // never fail node creation just because the validator run failed.
                }
            }
        }
        catch (Exception ex)
        {
            blueprintWarning = $"El nodo se creó pero no se pudo generar su contenido pedagógico (blueprint): {ex.Message}";
        }

        return await FunctionResponseFactory.CreatedResponseAsync(
            request,
            new AddCapabilityGraphNodeResponse
            {
                CapabilityGraphNodeId = savedNode.CapabilityGraphNodeId,
                Name = savedNode.Name,
                Description = savedNode.Description,
                SortOrder = savedNode.SortOrder,
                IllustrationsGenerated = illustrationRecords.Count + exampleIllustrationsGenerated,
                BlueprintGenerated = blueprintGenerated,
                BlueprintWarning = blueprintWarning
            },
            cancellationToken);
    }

    private static Guid? TryParseTenantIdFromExistingIllustration(IEnumerable<CapabilityGraphNode> nodes)
    {
        foreach (var node in nodes)
        {
            var illustration = node.Illustrations.FirstOrDefault();
            if (illustration is null) continue;

            var segments = illustration.StoragePath.Split('/');
            if (segments.Length > 0 && Guid.TryParse(segments[0], out var tenantId))
            {
                return tenantId;
            }
        }

        return null;
    }
}
