using System.Linq;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services;

/// <summary>
/// Orchestrates the YoungLearner-only "Aprende" Engram Card experience
/// (2026-10-08): gathers one section's already-approved
/// <see cref="StructuredLearningEntity"/>/<see cref="StructuredLearningKnowledgeFact"/>
/// rows, calls <see cref="StructuredLearningEngramCardGenerationAgent"/> to
/// propose the locked sequence, generates each card's illustration + short
/// "Profe" audio clip, and persists the result under the Teaching step's
/// StepDataJson "Cards" key.
///
/// ONLY ever runs for documents whose
/// <see cref="StructuredLearningDocument.PedagogicalAudience"/> is
/// "YoungLearner" — <see cref="GenerateCardsForSectionAsync"/> is a deliberate
/// no-op (returns null, touches nothing) for every other document, so
/// "Standard" (enterprise) courses are entirely unaffected by this service
/// existing. Mirrors <see cref="StructuredLearningProfessorLectureService"/>'s
/// Scenes generation (same storage conventions, same illustration style
/// gate), but is a fully separate, additive pipeline — it never modifies
/// Scenes, SpeechStoragePath, or any other existing StepDataJson key.
/// </summary>
public sealed class StructuredLearningEngramCardService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly StructuredLearningEngramCardGenerationAgent _agent;
    private readonly AzureSpeechService _speechService;
    private readonly ElevenLabsSoundEffectService _soundEffectService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly GraphIllustrationImageService _imageService;
    private readonly ILogger<StructuredLearningEngramCardService> _logger;

    /// <summary>Same wording as ExampleCardIllustrationService/
    /// StructuredLearningProfessorLectureService's own KidsIllustrationStyle
    /// — kept duplicated locally rather than shared, same reasoning as that
    /// service's own comment: avoids coupling otherwise-independent
    /// illustration pipelines together.</summary>
    private const string KidsCardIllustrationStyle =
        "A simple, warm, child-friendly educational illustration for a K-12 learning app. " +
        "Flat design, soft cheerful colors, single clear focal scene, friendly and age-appropriate, " +
        "professional children's book / cartoon illustration style.";

    public StructuredLearningEngramCardService(
        HumanOsDbContext dbContext,
        StructuredLearningEngramCardGenerationAgent agent,
        AzureSpeechService speechService,
        ElevenLabsSoundEffectService soundEffectService,
        CapabilityGraphIllustrationStorageService illustrationStorage,
        GraphIllustrationImageService imageService,
        ILogger<StructuredLearningEngramCardService> logger)
    {
        _dbContext = dbContext;
        _agent = agent;
        _speechService = speechService;
        _soundEffectService = soundEffectService;
        _illustrationStorage = illustrationStorage;
        _imageService = imageService;
        _logger = logger;
    }

    /// <summary>Generates (or regenerates) the YoungLearner Engram Card
    /// sequence for one section's Teaching step, media included, and
    /// persists it under StepDataJson's "Cards" key. Returns null — doing
    /// nothing at all — when the owning document is not PedagogicalAudience
    /// "YoungLearner", or when the Teaching step/Entities/Facts required to
    /// build cards are missing.</summary>
    public async Task<List<EngramCard>?> GenerateCardsForSectionAsync(
        Guid sectionId,
        CancellationToken cancellationToken = default)
    {
        var section = await _dbContext.StructuredLearningDocumentSections
            .Include(s => s.Document)
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken);

        if (section?.Document is null || section.Document.PedagogicalAudience != "YoungLearner")
        {
            // Hard gate (2026-10-08): never generate/alter anything for
            // "Standard" (enterprise) documents or sections missing a
            // document reference.
            return null;
        }

        var teachingStepEntity = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);
        if (teachingStepEntity is null)
        {
            _logger.LogInformation("Section {SectionId} has no Teaching step yet; skipping Engram Card generation.", sectionId);
            return null;
        }

        // 1. Already-approved Entities mentioned in this section. Projected to
        // scalar fields only (no Embedding) BEFORE Distinct — SqlVector<float>
        // cannot be compared/DISTINCT-ed at the SQL level.
        var entityRows = await _dbContext.StructuredLearningEntitySections
            .AsNoTracking()
            .Where(es => es.StructuredLearningDocumentSectionId == sectionId)
            .Select(es => new
            {
                es.Entity.StructuredLearningEntityId,
                es.Entity.Name,
                es.Entity.EntityType,
                es.Entity.Description
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (entityRows.Count == 0)
        {
            _logger.LogInformation("Section {SectionId} has no extracted Entities yet; skipping Engram Card generation.", sectionId);
            return null;
        }

        // 2. Already-approved Knowledge Facts for this section, resolved to
        // the Entities they are about via their GraphPath's Connections —
        // same provenance chain Studio's reviewer already approved.
        var factRows = await _dbContext.StructuredLearningKnowledgeFacts
            .AsNoTracking()
            .Include(f => f.GraphPathSteps)
                .ThenInclude(step => step.Connection)
            .Where(f => f.EngramId == sectionId)
            .ToListAsync(cancellationToken);

        var entityIds = entityRows.Select(e => e.StructuredLearningEntityId).ToHashSet();
        var factInputs = factRows.Select(fact => new EngramCardFactInput
        {
            KnowledgeFactId = fact.KnowledgeFactId,
            Statement = fact.Statement,
            FactType = fact.FactType,
            RelatedEntityIds = fact.GraphPathSteps
                .SelectMany(step => new[] { step.Connection.SourceEntityId, step.Connection.TargetEntityId })
                .Where(entityIds.Contains)
                .Distinct()
                .ToList()
        }).ToList();

        var resolvedLanguage = ResolveLanguage(section.Document.ConfigSnapshotJson);

        // Same fallback chain StructuredLearningProfessorLectureService uses for its own
        // TeachingContent — prefer the Teaching step's own narration (already written in
        // Profe's voice) over the section's raw source text.
        // Concatenate BOTH sources (2026-10-09, fixed) — previously this preferred ONLY the
        // Teaching step's rewritten "Profe" narration when present, which discards explicit
        // structural annotations (e.g. "Sonido a reproducir:"/"Sound to play:") that
        // ProfesorExpertAgent's course markdown embeds per card — StructuredLearningProfessorLectureAgent
        // rewrites everything into continuous warm prose and does not preserve them. Without the
        // raw section Text, SoundEffectLabel extraction below silently always came back null even
        // for cards whose own source clearly named a real sound (bell/drum/clap). Passing both
        // keeps the narration's grounding value AND restores access to the literal sound labels.
        var sourceText = !string.IsNullOrWhiteSpace(teachingStepEntity.Content)
            ? $"{teachingStepEntity.Content}\n\n---\nRAW SECTION SOURCE (for explicit sound-effect labels only):\n{section.Text}"
            : section.Text;

        var request = new EngramCardGenerationRequest
        {
            EngramId = sectionId,
            EngramName = section.SubchapterTitle,
            Language = resolvedLanguage,
            SourceText = sourceText,
            Entities = entityRows.Select(e => new EngramCardEntityInput
            {
                EntityId = e.StructuredLearningEntityId,
                CanonicalName = e.Name,
                EntityType = e.EntityType,
                Description = e.Description
            }).ToList(),
            Facts = factInputs
        };

        _logger.LogInformation(
            "Invoking StructuredLearningEngramCardGenerationAgent for section {SectionId} ({EngramName}) with {EntityCount} entities and {FactCount} facts",
            sectionId, section.SubchapterTitle, request.Entities.Count, request.Facts.Count);

        var generation = await _agent.GenerateAsync(request, cancellationToken);

        // 3. Generate media (illustration + short "Profe" audio) per card —
        // best-effort: a media failure degrades that one card gracefully
        // (text-only) instead of failing the whole batch, same philosophy
        // as StructuredLearningProfessorLectureService's Scenes loop.
        var cards = new List<EngramCard>();
        for (var i = 0; i < generation.Cards.Count; i++)
        {
            var generated = generation.Cards[i];
            Guid? illustrationId = null;
            string? audioStoragePath = null;
            string? soundEffectStoragePath = null;

            if (!string.IsNullOrWhiteSpace(generated.ImagePrompt) && _imageService.IsConfigured)
            {
                try
                {
                    var fullPrompt = $"{KidsCardIllustrationStyle} Scene: {generated.ImagePrompt}. No text, no letters, no numbers, no words in the image.";
                    var generatedImage = await _imageService.GenerateAsync(fullPrompt, null, cancellationToken);
                    using var imageStream = generatedImage.ImageBytes.ToStream();
                    var imagePath = await _illustrationStorage.UploadCardIllustrationAsync(
                        section.TenantId,
                        section.StructuredLearningDocumentId,
                        section.StructuredLearningDocumentSectionId,
                        i,
                        imageStream,
                        cancellationToken: cancellationToken);

                    var illustrationEntity = new StructuredLearningIllustration
                    {
                        StructuredLearningIllustrationId = Guid.NewGuid(),
                        StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                        StoragePath = imagePath,
                        Prompt = fullPrompt,
                        Caption = generated.Title,
                        Purpose = StructuredLearningIllustrationPurpose.Teaching,
                        ImageModel = generatedImage.ImageModel,
                        Width = generatedImage.Width,
                        Height = generatedImage.Height,
                        CreatedDate = DateTime.UtcNow
                    };
                    _dbContext.StructuredLearningIllustrations.Add(illustrationEntity);
                    illustrationId = illustrationEntity.StructuredLearningIllustrationId;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate Engram Card {CardIndex} illustration for section {SectionId}", i, sectionId);
                }
            }

            if (!string.IsNullOrWhiteSpace(generated.ProfessorLine) && _speechService.IsConfigured && _illustrationStorage.IsConfigured)
            {
                try
                {
                    var voiceName = string.Equals(resolvedLanguage, "es", StringComparison.OrdinalIgnoreCase)
                        ? "es-MX-JorgeNeural"
                        : "en-US-AndrewNeural";
                    var narrationBytes = await _speechService.SynthesizeAsync(generated.ProfessorLine, voiceName, cancellationToken);

                    // Real, non-human sound effect (2026-10-09) — generated via ElevenLabs and
                    // stored as its OWN separate clip, never byte-concatenated onto the narration:
                    // Azure Speech TTS and ElevenLabs output different MPEG encode parameters
                    // (sample rate/channels), so naive concatenation produced a file whose second
                    // segment silently failed to decode in the browser (the bell was never heard).
                    // The frontend player (EngramCardSequencePlayer) plays AudioStoragePath then
                    // SoundEffectStoragePath as two sequential <audio> elements instead.
                    if (!string.IsNullOrWhiteSpace(generated.SoundEffectLabel) && _soundEffectService.IsConfigured)
                    {
                        try
                        {
                            // A label naming MULTIPLE sounds (e.g. "Bell chime, Drum beat, Bell
                            // chime" for a 3-beat sequence card) must become THREE separate,
                            // distinguishable clips, never one blended ~3s clip generated from the
                            // whole comma-joined description — ElevenLabs cannot reliably render
                            // "three distinct sounds in order" from a single text prompt. Each
                            // individual sound IS safe to concatenate with the others (same
                            // encoder/format, unlike the narration+effect case above).
                            var soundParts = generated.SoundEffectLabel
                                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Where(part => part.Length > 0)
                                .ToList();
                            if (soundParts.Count == 0)
                            {
                                soundParts = [generated.SoundEffectLabel];
                            }

                            var effectSegments = new List<byte[]>();
                            foreach (var part in soundParts)
                            {
                                effectSegments.Add(await _soundEffectService.GenerateAsync(part, 2.0, cancellationToken));
                            }
                            var effectBytes = effectSegments.Count == 1
                                ? effectSegments[0]
                                : effectSegments.SelectMany(b => b).ToArray();

                            using var effectStream = new MemoryStream(effectBytes);
                            soundEffectStoragePath = await _illustrationStorage.UploadCardSoundEffectAudioAsync(
                                section.TenantId,
                                section.StructuredLearningDocumentId,
                                section.StructuredLearningDocumentSectionId,
                                i,
                                effectStream,
                                cancellationToken: cancellationToken);
                        }
                        catch (Exception sfxEx)
                        {
                            _logger.LogWarning(sfxEx, "Failed to generate Engram Card {CardIndex} sound effect '{SoundEffectLabel}' for section {SectionId}", i, generated.SoundEffectLabel, sectionId);
                        }
                    }

                    using var audioStream = new MemoryStream(narrationBytes);
                    audioStoragePath = await _illustrationStorage.UploadLectureCardAudioAsync(
                        section.TenantId,
                        section.StructuredLearningDocumentId,
                        section.StructuredLearningDocumentSectionId,
                        i,
                        audioStream,
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to synthesize Engram Card {CardIndex} audio for section {SectionId}", i, sectionId);
                }
            }

            string? recognitionCueAudioPath = null;
            if (!string.IsNullOrWhiteSpace(generated.RecognitionCue) && _speechService.IsConfigured && _illustrationStorage.IsConfigured)
            {
                try
                {
                    var voiceName = string.Equals(resolvedLanguage, "es", StringComparison.OrdinalIgnoreCase)
                        ? "es-MX-JorgeNeural"
                        : "en-US-AndrewNeural";
                    var cueAudioBytes = await _speechService.SynthesizeAsync(generated.RecognitionCue, voiceName, cancellationToken);
                    using var cueAudioStream = new MemoryStream(cueAudioBytes);
                    recognitionCueAudioPath = await _illustrationStorage.UploadCardRecognitionCueAudioAsync(
                        section.TenantId,
                        section.StructuredLearningDocumentId,
                        section.StructuredLearningDocumentSectionId,
                        i,
                        cueAudioStream,
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to synthesize Engram Card {CardIndex} RecognitionCue audio for section {SectionId}", i, sectionId);
                }
            }

            cards.Add(new EngramCard
            {
                Id = generated.Id,
                Title = generated.Title,
                Type = generated.Type,
                Sound = generated.Sound,
                Verb = generated.Verb,
                RecognitionCue = generated.RecognitionCue,
                RecognitionCueAudioStoragePath = recognitionCueAudioPath,
                SoundEffectLabel = generated.SoundEffectLabel,
                SoundEffectStoragePath = soundEffectStoragePath,
                ImagePrompt = generated.ImagePrompt,
                IllustrationId = illustrationId,
                AudioStoragePath = audioStoragePath,
                ProfessorLine = generated.ProfessorLine,
                Explanation = generated.Explanation,
                Example = generated.Example,
                Facts = generated.Facts,
                Question = generated.Question,
                Children = generated.Children,
                SourceEntityId = generated.SourceEntityId,
                SourceKnowledgeFactIds = generated.SourceKnowledgeFactIds,
                IsComposedExample = generated.IsComposedExample,
                PatternSourceFactId = generated.PatternSourceFactId,
                Evidence = generated.Evidence
            });
        }

        // 4. Persist — merge the "Cards" key into StepDataJson without
        // disturbing any existing key (Scenes/IllustrationId/
        // SpeechStoragePath/WebSearchCitations) already written by
        // StructuredLearningProfessorLectureService for this same step.
        var merged = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(teachingStepEntity.StepDataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(teachingStepEntity.StepDataJson);
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    merged[property.Name] = JsonSerializer.Deserialize<object?>(property.Value.GetRawText());
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Existing StepDataJson for section {SectionId} was not valid JSON; it will be replaced.", sectionId);
            }
        }
        merged["Cards"] = cards;
        teachingStepEntity.StepDataJson = JsonSerializer.Serialize(merged);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return cards;
    }

    private static string ResolveLanguage(string? configSnapshotJson)
    {
        if (!string.IsNullOrWhiteSpace(configSnapshotJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(configSnapshotJson);
                if (doc.RootElement.TryGetProperty("TargetLanguage", out var langProp) && langProp.ValueKind == JsonValueKind.String)
                {
                    var lang = langProp.GetString();
                    if (!string.IsNullOrWhiteSpace(lang)) return lang;
                }
            }
            catch (JsonException)
            {
                // Fall through to default below.
            }
        }

        return "es";
    }
}
