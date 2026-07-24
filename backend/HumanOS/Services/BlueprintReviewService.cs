using System.Text;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services;

/// <summary>
/// Capability Studio review feature (2026-07-21) — "Modo Demo" / "Modo
/// Edición" for Capability Studio's "Probar como estudiante" preview. Reads
/// a node's Memory Paradox blueprint DIRECTLY (all 5 steps at once,
/// bypassing any LearningSession/runtime gating entirely) so a reviewer can
/// jump freely between Hypothesis/Teaching/Recall/Production/Assessment to
/// inspect what the AI generated, and optionally edit a step's Content (and,
/// if warranted, its illustration) via a free-text instruction before the
/// capability is approved for publishing. Completely separate from
/// InstructorRuntimeOrchestrator — never touches LearningSession/
/// LearningSessionStep/LearningEvidence, so it can never affect real student
/// progress or a student's live "Real" mode session.
/// </summary>
public sealed class BlueprintReviewService
{
    private readonly BlueprintStepEditorAgent _agent;
    private readonly GraphIllustrationImageService _imageService;
    private readonly HumanOS.Storage.CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly ILogger<BlueprintReviewService> _logger;

    public BlueprintReviewService(
        BlueprintStepEditorAgent agent,
        GraphIllustrationImageService imageService,
        HumanOS.Storage.CapabilityGraphIllustrationStorageService illustrationStorage,
        ILogger<BlueprintReviewService> logger)
    {
        _agent = agent;
        _imageService = imageService;
        _illustrationStorage = illustrationStorage;
        _logger = logger;
    }

    public sealed class IllustrationRef
    {
        public Guid IllustrationId { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public string? Caption { get; set; }
    }

    public sealed class BlueprintStepResult
    {
        public ExperienceStepType StepType { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<IllustrationRef> Illustrations { get; set; } = [];
    }

    public sealed class BlueprintResult
    {
        public Guid NodeExperienceBlueprintId { get; set; }
        public List<BlueprintStepResult> Steps { get; set; } = [];
    }

    /// <summary>Returns every step of the MOST RECENT blueprint for this node — the same "most recent wins" rule InstructorRuntimeOrchestrator.StartSessionAsync uses.</summary>
    public async Task<BlueprintResult> GetBlueprintAsync(HumanOsDbContext dbContext, Guid capabilityGraphNodeId, CancellationToken cancellationToken = default)
    {
        var blueprint = await dbContext.NodeExperienceBlueprints
            .Include(b => b.Steps)
            .Where(b => b.CapabilityGraphNodeId == capabilityGraphNodeId)
            .OrderByDescending(b => b.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (blueprint is null)
        {
            throw new InvalidOperationException($"No NodeExperienceBlueprint exists for CapabilityGraphNode {capabilityGraphNodeId}.");
        }

        var steps = new List<BlueprintStepResult>();
        foreach (var step in blueprint.Steps.OrderBy(s => s.SortOrder))
        {
            steps.Add(new BlueprintStepResult
            {
                StepType = step.StepType,
                Content = step.Content,
                Illustrations = await ResolveIllustrationsAsync(dbContext, step, cancellationToken)
            });
        }

        return new BlueprintResult { NodeExperienceBlueprintId = blueprint.NodeExperienceBlueprintId, Steps = steps };
    }

    /// <summary>
    /// Applies a reviewer's free-text instruction to ONE step of the node's
    /// most recent blueprint: regenerates its Content via
    /// BlueprintStepEditorAgent, and — only when the agent decides it's
    /// warranted — regenerates its illustration too. Overwrites the step
    /// IN PLACE (no versioning yet — this is a pre-publish review tool, see
    /// user's own scope: "no te preocupes de publicarlo, después lo vemos").
    /// </summary>
    public async Task<BlueprintStepResult> EditStepAsync(
        HumanOsDbContext dbContext,
        Guid capabilityGraphNodeId,
        ExperienceStepType stepType,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        if (!_agent.IsConfigured)
        {
            throw new InvalidOperationException("BlueprintStepEditorAgent is not configured (missing Azure OpenAI settings).");
        }

        var node = await dbContext.CapabilityGraphNodes
            .Include(n => n.Illustrations)
            .FirstOrDefaultAsync(n => n.CapabilityGraphNodeId == capabilityGraphNodeId, cancellationToken);

        if (node is null)
        {
            throw new InvalidOperationException($"CapabilityGraphNode {capabilityGraphNodeId} not found.");
        }

        var blueprint = await dbContext.NodeExperienceBlueprints
            .Include(b => b.Steps)
            .Where(b => b.CapabilityGraphNodeId == capabilityGraphNodeId)
            .OrderByDescending(b => b.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        var step = blueprint?.Steps.FirstOrDefault(s => s.StepType == stepType);
        if (blueprint is null || step is null)
        {
            throw new InvalidOperationException($"No {stepType} blueprint step exists for CapabilityGraphNode {capabilityGraphNodeId}.");
        }

        var currentIllustrations = await ResolveIllustrationsAsync(dbContext, step, cancellationToken);
        var prompt = BuildEditPrompt(node, stepType, step.Content, currentIllustrations, instruction);

        var edited = await _agent.EditStepAsync(prompt, cancellationToken);
        step.Content = edited.UpdatedContentHtml;

        // Reviewer edits can add/replace an illustration on ANY step type
        // (2026-07-21 fix — previously hard-restricted to Hypothesis/
        // Teaching only, mirroring NodeExperienceBlueprintPersistenceService's
        // MatchesStepPurpose invariant for the ORIGINAL AI-generated
        // blueprint; that restriction doesn't apply here since a reviewer
        // explicitly asking for e.g. a Recall-step illustration is a
        // deliberate, on-demand request, not a pipeline-time reuse). Only
        // Hypothesis/Teaching keep their dedicated semantic Purpose (still
        // meaningful for those two: "before" state vs. worked example) —
        // every other step type gets the generic BlueprintReviewEdit tag.
        var purpose = stepType switch
        {
            ExperienceStepType.Hypothesis => IllustrationPurpose.Hypothesis,
            ExperienceStepType.Teaching => IllustrationPurpose.Teaching,
            _ => IllustrationPurpose.BlueprintReviewEdit
        };

        if (!string.IsNullOrWhiteSpace(edited.DiagramPrompt))
        {
            var newIllustrationId = await TryGenerateIllustrationAsync(dbContext, node, purpose, edited.DiagramPrompt, cancellationToken);
            if (newIllustrationId is not null)
            {
                step.ReferencedIllustrationIdsJson = JsonSerializer.Serialize(new[] { newIllustrationId.Value });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new BlueprintStepResult
        {
            StepType = step.StepType,
            Content = step.Content,
            Illustrations = await ResolveIllustrationsAsync(dbContext, step, cancellationToken)
        };
    }

    private static async Task<List<IllustrationRef>> ResolveIllustrationsAsync(HumanOsDbContext dbContext, NodeExperienceBlueprintStep step, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(step.ReferencedIllustrationIdsJson))
        {
            return [];
        }

        var ids = JsonSerializer.Deserialize<List<Guid>>(step.ReferencedIllustrationIdsJson) ?? [];
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.CapabilityGraphNodeIllustrations
            .AsNoTracking()
            .Where(i => ids.Contains(i.CapabilityGraphNodeIllustrationId))
            .Select(i => new IllustrationRef { IllustrationId = i.CapabilityGraphNodeIllustrationId, StoragePath = i.StoragePath, Caption = i.Caption })
            .ToListAsync(cancellationToken);
    }

    private static string BuildEditPrompt(
        CapabilityGraphNode node,
        ExperienceStepType stepType,
        string currentContent,
        List<IllustrationRef> currentIllustrations,
        string instruction)
    {
        var examples = DeserializeStringList(node.ExamplesJson);
        var applications = DeserializeStringList(node.ApplicationsJson);

        var builder = new StringBuilder();
        builder.AppendLine($"Node Name: {node.Name}");
        builder.AppendLine($"Node Description: {node.Description}");
        builder.AppendLine();
        builder.AppendLine($"AcademicDefinition:\n{node.AcademicDefinition}");
        builder.AppendLine();
        builder.AppendLine($"Interpretation:\n{node.Interpretation}");
        builder.AppendLine();
        builder.AppendLine($"Examples:\n{string.Join("\n", examples.Select(e => $"- {e}"))}");
        builder.AppendLine();
        builder.AppendLine($"Applications:\n{string.Join("\n", applications.Select(a => $"- {a}"))}");
        builder.AppendLine();
        builder.AppendLine($"StepType being edited: {stepType}");
        builder.AppendLine();
        builder.AppendLine($"Current Content (HTML):\n{currentContent}");
        builder.AppendLine();
        builder.AppendLine(currentIllustrations.Count > 0
            ? $"This step currently HAS an illustration (caption: {currentIllustrations[0].Caption ?? "(none)"})."
            : "This step currently has NO illustration.");
        builder.AppendLine();
        builder.AppendLine($"Reviewer's instruction: {instruction}");

        return builder.ToString();
    }

    /// <summary>Best-effort illustration (re)generation for a reviewer's edit — never throws, logs and returns null on any failure (same contract as AdaptiveAssessmentEngine/KnowledgeExpansionService's illustration generation).</summary>
    private async Task<Guid?> TryGenerateIllustrationAsync(
        HumanOsDbContext dbContext,
        CapabilityGraphNode node,
        IllustrationPurpose purpose,
        string diagramPrompt,
        CancellationToken cancellationToken)
    {
        if (!_imageService.IsConfigured || !_illustrationStorage.IsConfigured)
        {
            _logger.LogWarning("BlueprintReview illustration edit: skipped - imageService/illustrationStorage not configured for node {NodeId}.", node.CapabilityGraphNodeId);
            return null;
        }

        var pathSeed = node.Illustrations.FirstOrDefault()?.StoragePath;
        if (pathSeed is null)
        {
            _logger.LogWarning("BlueprintReview illustration edit: skipped - node {NodeId} has no existing illustration to derive tenant/capability path from.", node.CapabilityGraphNodeId);
            return null;
        }

        var segments = pathSeed.Split('/');
        if (segments.Length < 3 || !Guid.TryParse(segments[0], out var tenantId) || !Guid.TryParse(segments[1], out var capabilityId))
        {
            return null;
        }

        try
        {
            var existingCount = await dbContext.CapabilityGraphNodeIllustrations.CountAsync(i => i.CapabilityGraphNodeId == node.CapabilityGraphNodeId, cancellationToken);
            var imageIndex = 200 + existingCount; // Base offset for reviewer-edit illustrations — never collides with the pipeline's low sequential indexes, KnowledgeExpansion's fixed 99, or Assessment's 100+ range.

            var generated = await _imageService.GenerateAsync(diagramPrompt, cancellationToken);
            using var imageStream = generated.ImageBytes.ToStream();

            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                tenantId, capabilityId, node.CapabilityGraphNodeId, imageIndex, imageStream, cancellationToken: cancellationToken);

            var illustration = new CapabilityGraphNodeIllustration
            {
                CapabilityGraphNodeIllustrationId = Guid.NewGuid(),
                CapabilityGraphNodeId = node.CapabilityGraphNodeId,
                StoragePath = storagePath,
                Prompt = diagramPrompt,
                Purpose = purpose,
                ImageModel = generated.ImageModel,
                Width = generated.Width,
                Height = generated.Height,
                CreatedDate = DateTime.UtcNow
            };

            dbContext.CapabilityGraphNodeIllustrations.Add(illustration);
            _logger.LogInformation("BlueprintReview illustration edit: generated successfully, CapabilityGraphNodeIllustrationId={IllustrationId}.", illustration.CapabilityGraphNodeIllustrationId);
            return illustration.CapabilityGraphNodeIllustrationId;
        }
        catch (Exception ex) when (ex.Message.Contains("moderation_blocked", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("BlueprintReview illustration edit: skipped - Azure OpenAI's safety system rejected the prompt (moderation_blocked) for node {NodeId}.", node.CapabilityGraphNodeId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BlueprintReview illustration edit: generation FAILED for node {NodeId}.", node.CapabilityGraphNodeId);
            return null;
        }
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
