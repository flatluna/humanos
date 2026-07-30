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
    private readonly ExampleCardEditorAgent _cardAgent;
    private readonly GraphIllustrationImageService _imageService;
    private readonly MathDiagramRenderService _mathDiagramRenderService;
    private readonly HumanOS.Storage.CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly ILogger<BlueprintReviewService> _logger;

    public BlueprintReviewService(
        BlueprintStepEditorAgent agent,
        ExampleCardEditorAgent cardAgent,
        GraphIllustrationImageService imageService,
        MathDiagramRenderService mathDiagramRenderService,
        HumanOS.Storage.CapabilityGraphIllustrationStorageService illustrationStorage,
        ILogger<BlueprintReviewService> logger)
    {
        _agent = agent;
        _cardAgent = cardAgent;
        _imageService = imageService;
        _mathDiagramRenderService = mathDiagramRenderService;
        _illustrationStorage = illustrationStorage;
        _logger = logger;
    }

    public sealed class IllustrationRef
    {
        public Guid IllustrationId { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public string? Caption { get; set; }

        /// <summary>Raw generation-model tag stored on the illustration —
        /// either "deterministic-skiasharp-v1" (see
        /// <see cref="MathDiagramRenderService"/>, no LLM involved) or a
        /// generative image model name (e.g. "gpt-image-2"). Surfaced to
        /// Capability Studio so reviewers can see, per-image, exactly which
        /// pipeline produced it — never inferred/guessed in the frontend.</summary>
        public string ImageModel { get; set; } = string.Empty;
    }

    public sealed class BlueprintStepResult
    {
        public ExperienceStepType StepType { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<IllustrationRef> Illustrations { get; set; } = [];

        /// <summary>Only populated when StepType == Examples — deserialized
        /// from NodeExperienceBlueprintStep.ExampleCardsJson.</summary>
        public List<ExampleCard>? ExampleCards { get; set; }
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
                Illustrations = await ResolveIllustrationsAsync(dbContext, step, cancellationToken),
                ExampleCards = string.IsNullOrWhiteSpace(step.ExampleCardsJson)
                    ? null
                    : JsonSerializer.Deserialize<List<ExampleCard>>(step.ExampleCardsJson)
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

        if (!string.IsNullOrWhiteSpace(edited.DiagramPrompt) || edited.MathDiagram is not null)
        {
            var newIllustrationId = await TryGenerateIllustrationAsync(dbContext, node, purpose, edited.DiagramPrompt, edited.MathDiagram, cancellationToken);
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
            Illustrations = await ResolveIllustrationsAsync(dbContext, step, cancellationToken),
            // Deep Dive's ExampleCards are UNTOUCHED by this generic
            // Content-only edit flow — echo them back unchanged so the
            // frontend's merge never wipes them from view (2026-07-30 fix:
            // this response used to leave ExampleCards null, which looked
            // to a reviewer like editing the Deep Dive step's intro text
            // had deleted every card, even though the DB row itself was
            // never touched). See EditExampleCardAsync/AddExampleCardAsync
            // below for the real per-card editing flow.
            ExampleCards = step.StepType == ExperienceStepType.DeepDive && !string.IsNullOrWhiteSpace(step.ExampleCardsJson)
                ? JsonSerializer.Deserialize<List<ExampleCard>>(step.ExampleCardsJson)
                : null
        };
    }

    /// <summary>
    /// Applies a reviewer's free-text instruction to ONE ExampleCard (by
    /// index) of a node's Deep Dive step, via ExampleCardEditorAgent — every
    /// OTHER card in the pool is read from the persisted JSON, left
    /// completely untouched, and written straight back. This is the
    /// card-scoped counterpart to <see cref="EditStepAsync"/>, which only
    /// ever edits the step's plain Content and has no concept of
    /// ExampleCards at all.
    /// </summary>
    public async Task<BlueprintStepResult> EditExampleCardAsync(
        HumanOsDbContext dbContext,
        Guid capabilityGraphNodeId,
        int cardIndex,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        if (!_cardAgent.IsConfigured)
        {
            throw new InvalidOperationException("ExampleCardEditorAgent is not configured (missing Azure OpenAI settings).");
        }

        var (node, _, step, cards) = await LoadDeepDiveStepAsync(dbContext, capabilityGraphNodeId, cancellationToken);

        if (cardIndex < 0 || cardIndex >= cards.Count)
        {
            throw new InvalidOperationException($"CardIndex {cardIndex} is out of range — this Deep Dive step has {cards.Count} card(s).");
        }

        var target = cards[cardIndex];
        var otherTitles = cards.Where((_, i) => i != cardIndex).Select(c => c.Title).ToList();
        var prompt = BuildCardPrompt(node, otherTitles, target, instruction);

        var edited = await _cardAgent.EditCardAsync(prompt, cancellationToken);
        target.Title = edited.Title;
        target.Text = edited.Text;
        target.Emoji = edited.Emoji;

        if (!string.IsNullOrWhiteSpace(edited.DiagramPrompt) || edited.MathDiagram is not null)
        {
            var newIllustrationId = await TryGenerateIllustrationAsync(
                dbContext, node, IllustrationPurpose.Example, edited.DiagramPrompt, edited.MathDiagram, cancellationToken);
            if (newIllustrationId is not null)
            {
                target.IllustrationId = newIllustrationId;
            }
        }

        step.ExampleCardsJson = JsonSerializer.Serialize(cards);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new BlueprintStepResult
        {
            StepType = step.StepType,
            Content = step.Content,
            Illustrations = await ResolveIllustrationsAsync(dbContext, step, cancellationToken),
            ExampleCards = cards
        };
    }

    /// <summary>
    /// Appends a brand-new ExampleCard to a node's Deep Dive step pool, via
    /// ExampleCardEditorAgent — every existing card is read, left completely
    /// untouched, and written straight back alongside the new one.
    /// </summary>
    public async Task<BlueprintStepResult> AddExampleCardAsync(
        HumanOsDbContext dbContext,
        Guid capabilityGraphNodeId,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        if (!_cardAgent.IsConfigured)
        {
            throw new InvalidOperationException("ExampleCardEditorAgent is not configured (missing Azure OpenAI settings).");
        }

        var (node, _, step, cards) = await LoadDeepDiveStepAsync(dbContext, capabilityGraphNodeId, cancellationToken);

        var existingTitles = cards.Select(c => c.Title).ToList();
        var prompt = BuildCardPrompt(node, existingTitles, currentCard: null, instruction);

        var created = await _cardAgent.EditCardAsync(prompt, cancellationToken);
        var newCard = new ExampleCard { Title = created.Title, Text = created.Text, Emoji = created.Emoji };

        if (!string.IsNullOrWhiteSpace(created.DiagramPrompt) || created.MathDiagram is not null)
        {
            var newIllustrationId = await TryGenerateIllustrationAsync(
                dbContext, node, IllustrationPurpose.Example, created.DiagramPrompt, created.MathDiagram, cancellationToken);
            if (newIllustrationId is not null)
            {
                newCard.IllustrationId = newIllustrationId;
            }
        }

        cards.Add(newCard);
        step.ExampleCardsJson = JsonSerializer.Serialize(cards);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new BlueprintStepResult
        {
            StepType = step.StepType,
            Content = step.Content,
            Illustrations = await ResolveIllustrationsAsync(dbContext, step, cancellationToken),
            ExampleCards = cards
        };
    }

    /// <summary>Shared load path for both card-level operations — resolves
    /// the node (with Illustrations, needed for tenant/capability path
    /// derivation), the most recent blueprint's DeepDive step, and its
    /// already-persisted card list (or an empty list if the step has none
    /// yet).</summary>
    private static async Task<(CapabilityGraphNode Node, NodeExperienceBlueprint Blueprint, NodeExperienceBlueprintStep Step, List<ExampleCard> Cards)> LoadDeepDiveStepAsync(
        HumanOsDbContext dbContext,
        Guid capabilityGraphNodeId,
        CancellationToken cancellationToken)
    {
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

        var step = blueprint?.Steps.FirstOrDefault(s => s.StepType == ExperienceStepType.DeepDive);
        if (blueprint is null || step is null)
        {
            throw new InvalidOperationException($"No DeepDive blueprint step exists for CapabilityGraphNode {capabilityGraphNodeId}.");
        }

        var cards = string.IsNullOrWhiteSpace(step.ExampleCardsJson)
            ? []
            : JsonSerializer.Deserialize<List<ExampleCard>>(step.ExampleCardsJson) ?? [];

        return (node, blueprint, step, cards);
    }

    /// <summary>Builds the reviewer-instruction prompt for ExampleCardEditorAgent — pass <paramref name="currentCard"/> as null for the ADD-new-card case.</summary>
    private static string BuildCardPrompt(
        CapabilityGraphNode node,
        List<string> otherCardTitles,
        ExampleCard? currentCard,
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
        builder.AppendLine(otherCardTitles.Count > 0
            ? $"Titles of every OTHER existing card in this pool (avoid duplicating these): {string.Join(", ", otherCardTitles)}"
            : "There are no other existing cards in this pool yet.");
        builder.AppendLine();

        if (currentCard is null)
        {
            builder.AppendLine("Mode: ADD — there is no current card, invent a brand-new one.");
        }
        else
        {
            builder.AppendLine("Mode: EDIT — this card already exists.");
            builder.AppendLine($"Current Title: {currentCard.Title}");
            builder.AppendLine($"Current Text (HTML):\n{currentCard.Text}");
            builder.AppendLine($"Current Emoji: {currentCard.Emoji ?? "(none)"}");
            builder.AppendLine(currentCard.IllustrationId is not null
                ? "This card currently HAS a generated illustration."
                : "This card currently has NO generated illustration.");
        }

        builder.AppendLine();
        builder.AppendLine($"Reviewer's instruction: {instruction}");

        return builder.ToString();
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
            .Select(i => new IllustrationRef { IllustrationId = i.CapabilityGraphNodeIllustrationId, StoragePath = i.StoragePath, Caption = i.Caption, ImageModel = i.ImageModel })
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
        string? diagramPrompt,
        MathDiagramSpec? mathDiagram,
        CancellationToken cancellationToken)
    {
        if (!_illustrationStorage.IsConfigured || (mathDiagram is null && !_imageService.IsConfigured))
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

            // Two mutually-exclusive rendering paths, decided by
            // BlueprintStepEditorAgent (LLM proposes, code just dispatches):
            // precise coordinate-plane graphs go through the deterministic
            // SkiaSharp renderer, everything else through the generative
            // image model as before. See MathDiagramRenderService's doc comment.
            object generated = mathDiagram is not null
                ? await _mathDiagramRenderService.RenderAsync(mathDiagram, cancellationToken)
                : await _imageService.GenerateAsync(diagramPrompt!, cancellationToken);
            var (imageBytes, imageModel, width, height) = generated switch
            {
                MathDiagramRenderService.RenderedDiagram r => (r.ImageBytes, r.ImageModel, r.Width, r.Height),
                GraphIllustrationImageService.GeneratedIllustration g => (g.ImageBytes, g.ImageModel, g.Width, g.Height),
                _ => throw new InvalidOperationException("Unreachable")
            };
            using var imageStream = imageBytes.ToStream();

            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                tenantId, capabilityId, node.CapabilityGraphNodeId, imageIndex, imageStream, cancellationToken: cancellationToken);

            var illustration = new CapabilityGraphNodeIllustration
            {
                CapabilityGraphNodeIllustrationId = Guid.NewGuid(),
                CapabilityGraphNodeId = node.CapabilityGraphNodeId,
                StoragePath = storagePath,
                Prompt = mathDiagram is not null ? (mathDiagram.EquationLabel ?? "MathDiagram") : diagramPrompt!,
                Purpose = purpose,
                ImageModel = imageModel,
                Width = width,
                Height = height,
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
