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

    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public ExampleCardIllustrationService(GraphIllustrationImageService imageService, IConfiguration configuration)
    {
        _imageService = imageService;
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
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || exampleCards.Count == 0)
        {
            return 0;
        }

        var generatedCount = 0;

        var cardsToIllustrate = exampleCards
            .Where(c => c.SuggestedForIllustration)
            .Take(MaxIllustratedCardsPerNode)
            .ToList();

        foreach (var card in cardsToIllustrate)
        {
            try
            {
                var plainText = Regex.Replace(card.Text, "<[^>]+>", " ").Trim();
                var prompt =
                    "A simple, warm, child-friendly educational illustration for a K-12 learning app. " +
                    $"Scene: {card.Title} — {plainText}. " +
                    "Flat design, soft cheerful colors, no text, no letters, no numbers, no words, " +
                    "single clear focal scene, friendly and age-appropriate, professional children's " +
                    "book illustration style.";

                var generated = await _imageService.GenerateAsync(prompt, cancellationToken);
                using var stream = generated.ImageBytes.ToStream();
                var imageIndex = existingIllustrationCount + generatedCount + 1;
                var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                    tenantId, capabilityId, capabilityGraphNodeId, imageIndex, stream, cancellationToken: cancellationToken);

                var illustration = new CapabilityGraphNodeIllustration
                {
                    CapabilityGraphNodeIllustrationId = Guid.NewGuid(),
                    CapabilityGraphNodeId = capabilityGraphNodeId,
                    StoragePath = storagePath,
                    Prompt = prompt,
                    Caption = card.Title,
                    Purpose = IllustrationPurpose.Example,
                    ImageModel = generated.ImageModel,
                    Width = generated.Width,
                    Height = generated.Height,
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
