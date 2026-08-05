using System.Text.RegularExpressions;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Storage;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Generates ONE real illustration per ExampleCard of a node's optional
/// Examples step (2026-07-29 — upgrade from the original emoji-only cards;
/// see <see cref="ExampleCard"/>'s doc comment, which anticipated exactly
/// this). Must run AFTER <see cref="ExperienceDesignerAgent.DesignBlueprintAsync"/>
/// returns (the cards' Title/Text only exist at that point — unlike
/// Hypothesis/Teaching illustrations, whose prompts GraphArchitectAgent
/// writes up-front) and BEFORE
/// <see cref="NodeExperienceBlueprintPersistenceService.PersistAsync"/> (so
/// each card's <see cref="ExampleCardDto.IllustrationId"/> is already
/// resolved when the blueprint gets serialized to ExampleCardsJson).
///
/// Reuses the same gpt-image (LowQuality) pipeline as
/// GraphIllustrationImageService, tagged with
/// <see cref="IllustrationPurpose.Example"/> so these rows are clearly
/// distinguishable from Hypothesis/Teaching illustrations in the DB.
///
/// Best-effort per card: a single failed image never blocks the others or
/// the overall blueprint persistence — a card with no image simply falls
/// back to its existing Emoji (or nothing) in the UI.
/// </summary>
public sealed class ExampleCardIllustrationService
{
    /// <summary>
    /// Hard cost safety net (2026-07-29 — coverage-pool feature): even if
    /// ExperienceDesignerAgent marks more cards SuggestedForIllustration
    /// than intended, never generate more than this many real images for a
    /// single node's Examples step. Cards beyond this cap simply keep their
    /// Emoji-only fallback.
    /// </summary>
    private const int MaxIllustratedCardsPerNode = 10;

    /// <summary>
    /// Style guidance used ONLY when the owning Capability's
    /// PedagogicalAudience is "YoungLearner" (K-12/early-childhood courses)
    /// — see <see cref="Models.Capabilities.Capability.PedagogicalAudience"/>.
    /// Children may appear in the scene when the card's own content actually
    /// calls for it (e.g. a classroom, counting kids), but the overall style
    /// stays a warm, cartoon-style children's-book illustration either way.
    /// </summary>
    private const string KidsIllustrationStyle =
        "A simple, warm, child-friendly educational illustration for a K-12 learning app. " +
        "Flat design, soft cheerful colors, single clear focal scene, friendly and age-appropriate, " +
        "professional children's book illustration style. Children may appear in the scene only if " +
        "the content described below genuinely calls for it.";

    /// <summary>
    /// Default style for every other capability (2026-08-05 fix — every
    /// card illustration used to be forced into the cartoon/children's-book
    /// style above regardless of subject/audience, which was wrong for
    /// adult/professional courses). Deliberately does NOT force a person
    /// into the scene (2026-08-05 follow-up fix — the old wording forced
    /// "real adult people" into EVERY card, so a card about a complex
    /// number or a country's flag rendered as a generic photo of someone
    /// gesturing at a whiteboard instead of the actual content). The real
    /// subject accuracy comes from the card's own DiagramPrompt/MathDiagram
    /// (see ExperienceDesignerAgent) — this constant only sets rendering
    /// quality/realism, not WHO/WHAT appears in the scene.
    /// </summary>
    private const string RealisticAdultIllustrationStyle =
        "A realistic, professional educational illustration/photo for an adult learning app. " +
        "Photorealistic or clean diagrammatic style as appropriate to the subject, natural lighting when a real " +
        "scene is depicted, single clear focal composition. If people appear, they must be real adults, never a " +
        "cartoon and never a child.";

    private readonly GraphIllustrationImageService _imageService;
    private readonly MathDiagramRenderService _mathDiagramRenderService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public ExampleCardIllustrationService(
        GraphIllustrationImageService imageService,
        MathDiagramRenderService mathDiagramRenderService,
        IConfiguration configuration)
    {
        _imageService = imageService;
        _mathDiagramRenderService = mathDiagramRenderService;
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
    }

    public bool IsConfigured => _imageService.IsConfigured && _illustrationStorage.IsConfigured;

    /// <summary>
    /// Mutates each <paramref name="exampleCards"/> entry in place (sets
    /// IllustrationId on success). Returns how many images were actually
    /// generated — callers fold this into their own illustration-count/cost
    /// telemetry, since these rows are created directly here rather than
    /// through the batch illustration-persistence path Hypothesis/Teaching
    /// use.
    /// </summary>
    public async Task<int> GenerateAsync(
        HumanOsDbContext dbContext,
        Guid tenantId,
        Guid capabilityId,
        Guid capabilityGraphNodeId,
        int existingIllustrationCount,
        List<ExampleCardDto> exampleCards,
        CancellationToken cancellationToken = default,
        byte[]? brandLogoBytes = null,
        string? pedagogicalAudience = null)
    {
        if (!IsConfigured || exampleCards.Count == 0)
        {
            return 0;
        }

        var generatedCount = 0;
        var styleGuidance = pedagogicalAudience == "YoungLearner" ? KidsIllustrationStyle : RealisticAdultIllustrationStyle;

        var cardsToIllustrate = exampleCards
            .Where(c => c.SuggestedForIllustration)
            .Take(MaxIllustratedCardsPerNode)
            .ToList();

        foreach (var card in cardsToIllustrate)
        {
            try
            {
                // Prefer the agent's own DiagramPrompt/MathDiagram (2026-08-05
                // fix — content-accurate illustrations, e.g. an actual Argand-
                // plane point for a complex number) over the old fallback of
                // synthesizing a generic prompt from Title/Text, which forced
                // every card into the same "person explaining" stock-photo
                // shape regardless of subject matter.
                string? diagramPrompt = card.DiagramPrompt;
                if (card.MathDiagram is null && string.IsNullOrWhiteSpace(diagramPrompt))
                {
                    var plainText = Regex.Replace(card.Text, "<[^>]+>", " ").Trim();
                    diagramPrompt =
                        $"{styleGuidance} " +
                        $"Scene: {card.Title} — {plainText}. " +
                        "No text, no letters, no numbers, no words in the image.";
                }
                else if (diagramPrompt is not null)
                {
                    diagramPrompt = $"{styleGuidance} {diagramPrompt}";
                }

                object generated = card.MathDiagram is not null
                    ? await _mathDiagramRenderService.RenderAsync(card.MathDiagram, cancellationToken)
                    : await _imageService.GenerateAsync(diagramPrompt!, brandLogoBytes, cancellationToken);
                var (imageBytes, imageModel, width, height) = generated switch
                {
                    MathDiagramRenderService.RenderedDiagram r => (r.ImageBytes, r.ImageModel, r.Width, r.Height),
                    GraphIllustrationImageService.GeneratedIllustration g => (g.ImageBytes, g.ImageModel, g.Width, g.Height),
                    _ => throw new InvalidOperationException("Unreachable")
                };
                using var stream = imageBytes.ToStream();
                var imageIndex = existingIllustrationCount + generatedCount + 1;
                var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                    tenantId, capabilityId, capabilityGraphNodeId, imageIndex, stream, cancellationToken: cancellationToken);

                var illustration = new CapabilityGraphNodeIllustration
                {
                    CapabilityGraphNodeIllustrationId = Guid.NewGuid(),
                    CapabilityGraphNodeId = capabilityGraphNodeId,
                    StoragePath = storagePath,
                    Prompt = card.MathDiagram is not null
                        ? (card.MathDiagram.EquationLabel ?? "MathDiagram")
                        : diagramPrompt!,
                    Caption = card.Title,
                    Purpose = IllustrationPurpose.Example,
                    ImageModel = imageModel,
                    Width = width,
                    Height = height,
                    CreatedDate = DateTime.UtcNow
                };

                dbContext.CapabilityGraphNodeIllustrations.Add(illustration);
                await dbContext.SaveChangesAsync(cancellationToken);

                card.IllustrationId = illustration.CapabilityGraphNodeIllustrationId;
                generatedCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — see class doc comment. Leave IllustrationId
                // null; the UI falls back to the card's Emoji.
            }
        }

        return generatedCount;
    }
}
