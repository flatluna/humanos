using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Contracts.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services;

/// <summary>
/// Service that orchestrates gathering all Engram information and the student's complete
/// professional context to generate a personalized spoken lecture via <see cref="StructuredLearningProfessorLectureAgent"/>.
/// </summary>
public sealed class StructuredLearningProfessorLectureService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly EmployeeScoringProfileService _profileService;
    private readonly StructuredLearningProfessorLectureAgent _agent;
    private readonly AzureSpeechService _speechService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly GraphIllustrationImageService _imageService;
    private readonly StructuredLearningLessonNarrationSyncAgent _narrationSyncAgent;
    private readonly HumanOS.Storage.IProcessDocumentStorageService _processDocumentStorage;
    private readonly ILogger<StructuredLearningProfessorLectureService> _logger;

    /// <summary>Style guidance for scene illustrations when the owning document's
    /// PedagogicalAudience is "YoungLearner" — same convention/wording as
    /// <see cref="ExampleCardIllustrationService"/>'s KidsIllustrationStyle, kept
    /// duplicated locally rather than shared to avoid coupling these two otherwise
    /// independent illustration pipelines together.</summary>
    private const string KidsSceneIllustrationStyle =
        "A simple, warm, child-friendly educational illustration for a K-12 learning app. " +
        "Flat design, soft cheerful colors, single clear focal scene, friendly and age-appropriate, " +
        "professional children's book / cartoon illustration style.";

    private const string StandardSceneIllustrationStyle =
        "A clean, professional educational illustration for an adult/professional learning app. " +
        "Photorealistic or clean diagrammatic style as appropriate to the subject, single clear focal composition.";

    public StructuredLearningProfessorLectureService(
        HumanOsDbContext dbContext,
        EmployeeScoringProfileService profileService,
        StructuredLearningProfessorLectureAgent agent,
        AzureSpeechService speechService,
        CapabilityGraphIllustrationStorageService illustrationStorage,
        GraphIllustrationImageService imageService,
        StructuredLearningLessonNarrationSyncAgent narrationSyncAgent,
        HumanOS.Storage.IProcessDocumentStorageService processDocumentStorage,
        ILogger<StructuredLearningProfessorLectureService> logger)
    {
        _dbContext = dbContext;
        _profileService = profileService;
        _agent = agent;
        _speechService = speechService;
        _illustrationStorage = illustrationStorage;
        _imageService = imageService;
        _narrationSyncAgent = narrationSyncAgent;
        _processDocumentStorage = processDocumentStorage;
        _logger = logger;
    }

    /// <summary>
    /// Generates a personalized spoken lecture for an Engram (section) tailored to a specific employee/student.
    /// </summary>
    public async Task<StructuredLearningProfessorLectureResult> GenerateLectureForSectionAsync(
        Guid sectionId,
        Guid? employeeId = null,
        string? targetLanguage = null,
        CancellationToken cancellationToken = default)
    {
        var section = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Include(s => s.Document)
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken);

        if (section is null)
        {
            throw new KeyNotFoundException($"Structured learning section with ID '{sectionId}' was not found.");
        }

        // 1. Identify the Teaching content (the core lesson of the engram)
        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);
        var teachingContent = !string.IsNullOrWhiteSpace(teachingStep?.Content)
            ? teachingStep.Content
            : section.Text;

        if (string.IsNullOrWhiteSpace(teachingContent))
        {
            teachingContent = $"Lesson on {section.SubchapterTitle} ({section.ChapterTitle}).";
        }

        // 2. Identify Visual/Illustration elements (whiteboard, diagram, etc.)
        var illustration = await _dbContext.StructuredLearningIllustrations
            .AsNoTracking()
            .Where(i => i.StructuredLearningDocumentSectionId == sectionId)
            .OrderByDescending(i => i.Purpose == StructuredLearningIllustrationPurpose.Teaching)
            .FirstOrDefaultAsync(cancellationToken);

        string? illustrationDescription = null;
        if (illustration is not null)
        {
            illustrationDescription = !string.IsNullOrWhiteSpace(illustration.Caption)
                ? illustration.Caption
                : illustration.Prompt;
        }

        // 2b. Real source-document images (2026-11-03) — there can be more than one per
        // Engram; download every one so the agent genuinely SEES them (vision input),
        // not just a cached text description.
        var sourceImages = new List<(byte[] Bytes, string ContentType)>();
        var sourceImageRows = await _dbContext.StructuredLearningIllustrations
            .Where(i => i.StructuredLearningDocumentSectionId == sectionId && i.Purpose == StructuredLearningIllustrationPurpose.SourceDocument)
            .OrderBy(i => i.StoragePath)
            .ToListAsync(cancellationToken);

        // Self-healing backfill (2026-11-03): a section created BEFORE the image pipeline
        // existed (or whose extraction simply found no images for other reasons) has no
        // SourceDocument rows yet — re-fetch the original process document's images for
        // this exact step (by ChapterTitle/SubchapterTitle) and persist them now, so
        // "Restart engrams" always behaves consistently regardless of when the section's
        // text was originally extracted, instead of only ever working for brand-new courses.
        if (sourceImageRows.Count == 0 && section.Document is not null && !string.IsNullOrWhiteSpace(section.Document.TargetSkill))
        {
            try
            {
                var skill = await _dbContext.Skills
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.TenantId == section.TenantId
                        && s.Name == section.Document.TargetSkill
                        && s.SourceProcessDocumentId != null, cancellationToken);

                if (skill?.SourceProcessDocumentId is Guid processDocumentId)
                {
                    var structure = await _processDocumentStorage.GetStructuredResultAsync(
                        section.TenantId.ToString("D").ToLowerInvariant(), processDocumentId.ToString(), cancellationToken);

                    var step = structure?.Chapters
                        .FirstOrDefault(c => c.Title == section.ChapterTitle)?.Steps
                        .FirstOrDefault(st => st.StepName == section.SubchapterTitle);

                    if (step is { StepImages.Count: > 0 })
                    {
                        for (var imgIndex = 0; imgIndex < step.StepImages.Count; imgIndex++)
                        {
                            var stepImage = step.StepImages[imgIndex];
                            var decoded = TryDecodeDataUrl(stepImage.DataUrl);
                            if (decoded is null) continue;

                            await using var imgStream = new MemoryStream(decoded.Value.Bytes);
                            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                                section.TenantId, section.StructuredLearningDocumentSectionId, section.StructuredLearningDocumentSectionId,
                                200 + imgIndex, imgStream, decoded.Value.ContentType, cancellationToken);

                            var newRow = new StructuredLearningIllustration
                            {
                                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                                StoragePath = storagePath,
                                Prompt = "Real source-document image (HumanOS process step) — backfilled",
                                Caption = section.SubchapterTitle,
                                Purpose = StructuredLearningIllustrationPurpose.SourceDocument,
                                ImageModel = "source-document",
                                CreatedDate = DateTime.UtcNow
                            };
                            _dbContext.StructuredLearningIllustrations.Add(newRow);
                            sourceImageRows.Add(newRow);
                        }
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Source-image backfill failed for section {SectionId}; continuing without real images", sectionId);
            }
        }

        foreach (var row in sourceImageRows)
        {
            try
            {
                var bytes = await _illustrationStorage.DownloadIllustrationBytesAsync(row.StoragePath, cancellationToken);
                sourceImages.Add((bytes.ToArray(), "image/png"));
            }
            catch
            {
                // Best-effort: a missing/unreachable blob should never block lecture generation.
            }
        }

        // 3. Resolve Target Language
        var resolvedLanguage = targetLanguage;
        if (string.IsNullOrWhiteSpace(resolvedLanguage))
        {
            resolvedLanguage = ResolveLanguageCode(section.Document?.ConfigSnapshotJson, teachingContent);
        }

        // 4. Construct Universal Context for the Agent (Generic for all learners)
        var context = new StructuredLearningProfessorLectureContext
        {
            CourseId = section.StructuredLearningDocumentId,
            CourseTitle = section.Document?.DocumentName ?? string.Empty,
            SectionId = section.StructuredLearningDocumentSectionId,
            ChapterTitle = section.ChapterTitle,
            SubchapterTitle = section.SubchapterTitle,
            Capability = section.Capability,
            TeachingContent = teachingContent,
            IllustrationDescription = illustrationDescription,
            SourceImages = sourceImages,
            DomainOrTopic = section.Document?.Category ?? section.Document?.TargetSkill,
            TargetLanguage = resolvedLanguage,
            PedagogicalAudience = section.Document?.PedagogicalAudience
        };

        // 5. Invoke the Agent
        _logger.LogInformation(
            "Invoking StructuredLearningProfessorLectureAgent for section {SectionId} ({SubchapterTitle}) in language '{Language}'",
            sectionId, section.SubchapterTitle, resolvedLanguage);

        var lectureGeneration = await _agent.GenerateLectureAsync(context, cancellationToken);
        var lectureResult = lectureGeneration.Result;

        // Synthesize spoken audio and persist to Data Lake and section Teaching step
        if (_speechService.IsConfigured && _illustrationStorage.IsConfigured && !string.IsNullOrWhiteSpace(lectureResult.SpokenLectureScript))
        {
            try
            {
                var speechText = lectureResult.SpokenLectureScript;
                var padded = $" {speechText.ToLowerInvariant()} ";
                string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
                string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu "];
                var isEnglish = englishWords.Count(w => padded.Contains(w)) > spanishWords.Count(w => padded.Contains(w));

                var voiceName = isEnglish
                    ? "en-US-AndrewNeural"
                    : "es-MX-JorgeNeural";

                var audioBytes = await _speechService.SynthesizeAsync(speechText, voiceName, cancellationToken);
                using var audioStream = new MemoryStream(audioBytes);

                var storagePath = await _illustrationStorage.UploadLectureAudioAsync(
                    section.TenantId,
                    section.StructuredLearningDocumentId,
                    section.StructuredLearningDocumentSectionId,
                    audioStream,
                    contentType: "audio/mpeg",
                    cancellationToken: cancellationToken);

                // Synced image+audio Teaching player (2026-10-08): the agent splits the
                // same lecture into ordered Scenes, each with its own ImagePrompt — when
                // those prompts are real (not just the single-scene fallback with an empty
                // prompt), generate ONE short audio clip + ONE illustration PER SCENE so the
                // student-facing player can swap images exactly when each clip ends, with
                // perfect sync by construction (no word/sentence-boundary timestamps needed).
                var sceneDtos = new List<object>();
                Guid? firstSceneIllustrationId = null;
                var hasRealScenes = lectureResult.Scenes.Count > 1 ||
                    (lectureResult.Scenes.Count == 1 && !string.IsNullOrWhiteSpace(lectureResult.Scenes[0].ImagePrompt));

                if (hasRealScenes && _imageService.IsConfigured)
                {
                    var styleGuidance = section.Document?.PedagogicalAudience == "YoungLearner"
                        ? KidsSceneIllustrationStyle
                        : StandardSceneIllustrationStyle;

                    for (var i = 0; i < lectureResult.Scenes.Count; i++)
                    {
                        var scene = lectureResult.Scenes[i];
                        Guid? sceneIllustrationId = null;
                        string? sceneAudioPath = null;

                        try
                        {
                            var sceneAudioBytes = await _speechService.SynthesizeAsync(scene.SceneText, voiceName, cancellationToken);
                            using var sceneAudioStream = new MemoryStream(sceneAudioBytes);
                            sceneAudioPath = await _illustrationStorage.UploadLectureSceneAudioAsync(
                                section.TenantId,
                                section.StructuredLearningDocumentId,
                                section.StructuredLearningDocumentSectionId,
                                i,
                                sceneAudioStream,
                                contentType: "audio/mpeg",
                                cancellationToken: cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to synthesize scene {SceneIndex} audio for section {SectionId}", i, sectionId);
                        }

                        if (!string.IsNullOrWhiteSpace(scene.ImagePrompt))
                        {
                            try
                            {
                                var fullPrompt = $"{styleGuidance} Scene: {scene.ImagePrompt}. No text, no letters, no numbers, no words in the image.";
                                var generated = await _imageService.GenerateAsync(fullPrompt, null, cancellationToken);
                                using var imageStream = generated.ImageBytes.ToStream();
                                var imagePath = await _illustrationStorage.UploadIllustrationAsync(
                                    section.TenantId,
                                    section.StructuredLearningDocumentId,
                                    section.StructuredLearningDocumentSectionId,
                                    i,
                                    imageStream,
                                    cancellationToken: cancellationToken);

                                var sceneIllustrationEntity = new StructuredLearningIllustration
                                {
                                    StructuredLearningIllustrationId = Guid.NewGuid(),
                                    StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                                    StoragePath = imagePath,
                                    Prompt = fullPrompt,
                                    Caption = scene.SceneText,
                                    Purpose = StructuredLearningIllustrationPurpose.Teaching,
                                    ImageModel = generated.ImageModel,
                                    Width = generated.Width,
                                    Height = generated.Height,
                                    CreatedDate = DateTime.UtcNow
                                };
                                _dbContext.StructuredLearningIllustrations.Add(sceneIllustrationEntity);
                                sceneIllustrationId = sceneIllustrationEntity.StructuredLearningIllustrationId;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to generate scene {SceneIndex} illustration for section {SectionId}", i, sectionId);
                            }
                        }

                        sceneDtos.Add(new
                        {
                            SceneIndex = i,
                            Text = scene.SceneText,
                            ImagePrompt = scene.ImagePrompt,
                            IllustrationId = sceneIllustrationId,
                            AudioStoragePath = sceneAudioPath
                        });

                        if (i == 0)
                        {
                            firstSceneIllustrationId = sceneIllustrationId;
                        }
                    }
                }

                // Update the Teaching step in database with ProfessorLecture and SpeechStoragePath
                var teachingStepEntity = await _dbContext.StructuredLearningSectionSteps
                    .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Teaching, cancellationToken);

                if (teachingStepEntity is not null)
                {
                    if (!string.IsNullOrWhiteSpace(lectureResult.SpokenLectureScript))
                    {
                        teachingStepEntity.Content = lectureResult.SpokenLectureScript;
                    }

                    // Legacy single IllustrationId: prefer Scene 0's (so old consumers that
                    // only ever read IllustrationId/SpeechStoragePath still show something
                    // sensible), falling back to whatever was already recorded before.
                    Guid? illustrationId = firstSceneIllustrationId;
                    List<string> existingCitations = [];
                    if (!string.IsNullOrWhiteSpace(teachingStepEntity.StepDataJson))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(teachingStepEntity.StepDataJson);
                            if (illustrationId is null && doc.RootElement.TryGetProperty("IllustrationId", out var ilProp) && ilProp.ValueKind == JsonValueKind.String)
                            {
                                if (Guid.TryParse(ilProp.GetString(), out var parsedId)) illustrationId = parsedId;
                            }
                            if (doc.RootElement.TryGetProperty("WebSearchCitations", out var citProp) && citProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var c in citProp.EnumerateArray())
                                {
                                    var str = c.GetString();
                                    if (!string.IsNullOrWhiteSpace(str)) existingCitations.Add(str);
                                }
                            }
                        }
                        catch { }
                    }

                    teachingStepEntity.StepDataJson = JsonSerializer.Serialize(new
                    {
                        IllustrationId = illustrationId,
                        SpeechStoragePath = storagePath,
                        Scenes = sceneDtos,
                        ProfessorLecture = lectureResult,
                        WebSearchCitations = existingCitations,
                        // Real source-document image(s) (2026-11-03) — shown alongside the
                        // professor's own generated illustration/scenes in the student player,
                        // never replacing them, so the student sees both the real figure and
                        // the professor's explanation of it.
                        SourceImageIds = sourceImageRows.Select(r => r.StructuredLearningIllustrationId).ToList()
                    });

                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to synthesize/upload professor lecture audio for section {SectionId}", sectionId);
            }
        }

        return lectureResult;
    }

    /// <summary>
    /// "Change image/voice" escape hatch for the Synced Teaching scene player
    /// (2026-11-07) — regenerates ONLY the requested media (image and/or voice) for
    /// ONE scene, reusing that scene's own stored Text/ImagePrompt (never re-inventing
    /// the lecture script). Meant for when a reviewer/student spots a mismatched
    /// illustration or a bad voice take for a single scene and wants a quick redo
    /// without re-running the whole lecture generation.
    /// </summary>
    public async Task<(Guid? IllustrationId, Guid? AlternateIllustrationId, string? AudioStoragePath)> RegenerateSceneAsync(
        Guid sectionId,
        int sceneIndex,
        bool regenerateImage,
        bool regenerateVoice,
        CancellationToken cancellationToken = default)
    {
        if (!regenerateImage && !regenerateVoice)
        {
            throw new InvalidOperationException("Nothing to regenerate: set regenerateImage and/or regenerateVoice.");
        }

        var section = await _dbContext.StructuredLearningDocumentSections
            .Include(s => s.Document)
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Structured learning section with ID '{sectionId}' was not found.");

        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching)
            ?? throw new InvalidOperationException("This section has no Teaching step yet.");

        if (string.IsNullOrWhiteSpace(teachingStep.StepDataJson))
        {
            throw new InvalidOperationException("This section has no scenes to regenerate.");
        }

        using var doc = JsonDocument.Parse(teachingStep.StepDataJson);
        if (!doc.RootElement.TryGetProperty("Scenes", out var scenesProp) || scenesProp.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("This section has no scenes to regenerate.");
        }

        var scenes = JsonSerializer.Deserialize<List<StructuredLearningLectureSceneDto>>(scenesProp.GetRawText()) ?? [];
        var scene = scenes.FirstOrDefault(s => s.SceneIndex == sceneIndex)
            ?? throw new KeyNotFoundException($"Scene {sceneIndex} was not found for section '{sectionId}'.");

        var languageCode = ResolveLanguageCode(section.Document?.ConfigSnapshotJson, scene.Text);
        var voiceName = languageCode == "en" ? "en-US-AndrewNeural" : "es-MX-JorgeNeural";

        if (regenerateVoice)
        {
            if (!_speechService.IsConfigured || !_illustrationStorage.IsConfigured)
            {
                throw new InvalidOperationException("Audio synthesis is not configured (missing Azure Speech / storage settings).");
            }

            var sceneAudioBytes = await _speechService.SynthesizeAsync(scene.Text, voiceName, cancellationToken);
            using var sceneAudioStream = new MemoryStream(sceneAudioBytes);
            scene.AudioStoragePath = await _illustrationStorage.UploadLectureSceneAudioAsync(
                section.TenantId,
                section.StructuredLearningDocumentId,
                section.StructuredLearningDocumentSectionId,
                sceneIndex,
                sceneAudioStream,
                contentType: "audio/mpeg",
                cancellationToken: cancellationToken);
        }

        if (regenerateImage)
        {
            if (!_imageService.IsConfigured)
            {
                throw new InvalidOperationException("Image generation is not configured.");
            }

            // Use the real professor-at-the-blackboard photo (extracted from the
            // original source video/document, Purpose=SourceDocument) as a visual
            // reference/base for the AI illustration — not just a blind text prompt
            // (2026-11-07, "pasarle la imagen del profesor en el pizarron como
            // base"). Picks the source photo whose ordinal position best matches
            // this scene's position in the lecture (proportional mapping, since
            // there's no per-scene timestamp link in this flow), falling back to
            // plain text-to-image if no source photos exist or the download fails.
            var referenceImageBytes = await TryGetSceneReferenceImageAsync(section.StructuredLearningDocumentSectionId, sceneIndex, scenes.Count, cancellationToken);

            // Older scenes (generated before ImagePrompt was persisted per-scene) only
            // have the narration Text — instead of failing, or handing the raw
            // transcript straight to the image model (which tends to render literal
            // text/words on screen), wrap it in an instruction telling the model to
            // produce an EDUCATIONAL ILLUSTRATION/DIAGRAM of the underlying concept —
            // WITH real supporting data (axes, curves, labels, annotations, arrows),
            // not a decorative scene — the same way a real ImagePrompt would (2026-11-07).
            // When a reference photo IS available, keep the professor from it IN the
            // scene instead of excluding them — the user explicitly asked for the
            // professor to appear too ("que ponga la persona tambien", 2026-11-07),
            // standing/pointing next to the diagram, not just a bare diagram.
            var effectiveImagePrompt = !string.IsNullOrWhiteSpace(scene.ImagePrompt)
                ? scene.ImagePrompt
                : referenceImageBytes is not null
                    ? $"Using the attached reference photo as the starting point, create an IMPROVED, COMPLETED version " +
                      $"of that same blackboard/whiteboard scene — do not just reproduce or copy it as-is. Keep the " +
                      $"SAME professor from the photo (same appearance/clothing), standing next to or pointing at the " +
                      $"board, but ENRICH the board's content: add any missing axes, curves, labeled data points, " +
                      $"numeric examples, arrows and short captions needed to fully and clearly illustrate and " +
                      $"COMPLETE the concept/example being taught in this narration (finish any half-drawn diagram or " +
                      $"partial example visible in the photo, and add concrete supporting data). Then, like a " +
                      $"professor drawing attention with a colored marker, draw ONE clear annotation (a circled/" +
                      $"highlighted region, or a curved arrow) in a bright contrasting color (e.g. red or yellow) " +
                      $"pointing at or encircling the SPECIFIC part of the board (the exact curve, point, axis, or " +
                      $"formula) that this narration is actually talking about right now — not the whole board, " +
                      $"just that one specific element: \"{scene.Text}\""
                    : $"Create a clear educational diagram (with axes, curves, labeled data points, arrows and short " +
                      $"captions as needed) that visually explains the concept the professor is teaching in this " +
                      $"narration, rather than depicting the professor or classroom: \"{scene.Text}\"";
            if (string.IsNullOrWhiteSpace(effectiveImagePrompt))
            {
                throw new InvalidOperationException("This scene has no text or image prompt to regenerate from.");
            }

            var styleGuidance = section.Document?.PedagogicalAudience == "YoungLearner"
                ? KidsSceneIllustrationStyle
                : StandardSceneIllustrationStyle;
            // Scenes WITH a real stored ImagePrompt keep the original "no text/labels"
            // restriction (a purely visual illustration); the diagram fallback above
            // explicitly needs labels/data, so it skips that restriction (2026-11-07).
            var noTextRestriction = !string.IsNullOrWhiteSpace(scene.ImagePrompt)
                ? " No text, no letters, no numbers, no words in the image."
                : string.Empty;
            // When a scene HAS a real stored ImagePrompt (so the branch above didn't
            // already mention the professor) but a reference photo is also available,
            // still tell the model to keep that same professor visible in the scene
            // instead of silently dropping them (2026-11-07).
            var keepProfessorInstruction = referenceImageBytes is not null && !string.IsNullOrWhiteSpace(scene.ImagePrompt)
                ? " Use the attached reference photo as a starting point, not a copy to reproduce: keep the same " +
                  "professor visible (same appearance/clothing), standing near or interacting with the scene, but " +
                  "ENRICH and COMPLETE the visual with additional supporting data/labels/examples that make the " +
                  "concept clearer than the photo alone. Also draw ONE clear annotation (a circled/highlighted " +
                  "region, or a curved arrow) in a bright contrasting color pointing at the specific part of the " +
                  "board this narration is about, like a professor drawing attention with a marker. " +
                  "concept clearer than the photo alone."
                : string.Empty;
            var fullPrompt = $"{styleGuidance} Scene: {effectiveImagePrompt}.{noTextRestriction}{keepProfessorInstruction}";

            var generated = referenceImageBytes is not null
                ? await _imageService.GenerateFromReferenceAsync(fullPrompt, referenceImageBytes, cancellationToken)
                : await _imageService.GenerateAsync(fullPrompt, null, cancellationToken);
            using var imageStream = generated.ImageBytes.ToStream();
            var imagePath = await _illustrationStorage.UploadIllustrationAsync(
                section.TenantId,
                section.StructuredLearningDocumentId,
                section.StructuredLearningDocumentSectionId,
                sceneIndex,
                imageStream,
                cancellationToken: cancellationToken);

            var illustrationEntity = new StructuredLearningIllustration
            {
                StructuredLearningIllustrationId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                StoragePath = imagePath,
                Prompt = fullPrompt,
                Caption = scene.Text,
                Purpose = StructuredLearningIllustrationPurpose.Teaching,
                ImageModel = generated.ImageModel,
                Width = generated.Width,
                Height = generated.Height,
                CreatedDate = DateTime.UtcNow
            };
            _dbContext.StructuredLearningIllustrations.Add(illustrationEntity);
            // Saved ALONGSIDE the existing illustration, never replacing it — capped
            // at one alternate per scene (2026-11-07): generating again just swaps
            // out this alternate, it never grows into a third image.
            scene.AlternateIllustrationId = illustrationEntity.StructuredLearningIllustrationId;
        }

        // Rewrite StepDataJson, replacing only "Scenes" — every other field (speech
        // path, ProfessorLecture, citations, source images, legacy top-level
        // IllustrationId) is preserved as-is.
        var updatedFields = new Dictionary<string, object?>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            updatedFields[prop.Name] = prop.Value.Clone();
        }
        updatedFields["Scenes"] = scenes;

        teachingStep.StepDataJson = JsonSerializer.Serialize(updatedFields);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (scene.IllustrationId, scene.AlternateIllustrationId, scene.AudioStoragePath);
    }

    /// <summary>
    /// Best-effort lookup of a real source-document photo (e.g. the professor at the
    /// blackboard) to use as a visual reference/base when regenerating a scene's
    /// illustration (2026-11-07). These rows (Purpose=SourceDocument) aren't tagged
    /// with a per-scene timestamp in this flow, so picks the one whose ordinal
    /// position proportionally matches <paramref name="sceneIndex"/>'s position among
    /// <paramref name="totalScenes"/> — e.g. scene 0 of 10 maps to the first photo,
    /// the last scene maps to the last photo. Returns null (never throws) if there
    /// are no source photos or the blob can't be downloaded, so callers can safely
    /// fall back to plain text-to-image generation.
    /// </summary>
    private async Task<byte[]?> TryGetSceneReferenceImageAsync(
        Guid sectionId,
        int sceneIndex,
        int totalScenes,
        CancellationToken cancellationToken)
    {
        try
        {
            var sourceImageRows = await _dbContext.StructuredLearningIllustrations
                .AsNoTracking()
                .Where(i => i.StructuredLearningDocumentSectionId == sectionId && i.Purpose == StructuredLearningIllustrationPurpose.SourceDocument)
                .OrderBy(i => i.StoragePath)
                .ToListAsync(cancellationToken);

            if (sourceImageRows.Count == 0)
            {
                return null;
            }

            var proportionalIndex = totalScenes > 1
                ? (int)Math.Round((double)sceneIndex / (totalScenes - 1) * (sourceImageRows.Count - 1))
                : 0;
            proportionalIndex = Math.Clamp(proportionalIndex, 0, sourceImageRows.Count - 1);

            var chosenRow = sourceImageRows[proportionalIndex];
            var bytes = await _illustrationStorage.DownloadIllustrationBytesAsync(chosenRow.StoragePath, cancellationToken);
            return bytes.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load a source reference image for scene {SceneIndex}; falling back to text-to-image", sceneIndex);
            return null;
        }
    }

    /// <summary>
    /// Removes ONE scene from the Synced Teaching scene player's lecture — a hard
    /// delete (2026-11-07): drops it from the "Scenes" array and renumbers the
    /// remaining scenes' SceneIndex so they stay consecutive (0, 1, 2…). The scene's
    /// audio/illustration blobs are left in storage (unreferenced, same tradeoff as
    /// the source-frame soft-delete) rather than deleted outright, since other scenes
    /// could in theory still reference the same illustration row. Always keeps at
    /// least ONE scene — deleting the last remaining one is rejected so the lecture
    /// never ends up with an empty player.
    /// </summary>
    public async Task<int> DeleteSceneAsync(Guid sectionId, int sceneIndex, CancellationToken cancellationToken = default)
    {
        var section = await _dbContext.StructuredLearningDocumentSections
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Structured learning section with ID '{sectionId}' was not found.");

        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching)
            ?? throw new InvalidOperationException("This section has no Teaching step yet.");

        if (string.IsNullOrWhiteSpace(teachingStep.StepDataJson))
        {
            throw new InvalidOperationException("This section has no scenes to delete.");
        }

        using var doc = JsonDocument.Parse(teachingStep.StepDataJson);
        if (!doc.RootElement.TryGetProperty("Scenes", out var scenesProp) || scenesProp.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("This section has no scenes to delete.");
        }

        var scenes = JsonSerializer.Deserialize<List<StructuredLearningLectureSceneDto>>(scenesProp.GetRawText()) ?? [];
        var sceneToRemove = scenes.FirstOrDefault(s => s.SceneIndex == sceneIndex)
            ?? throw new KeyNotFoundException($"Scene {sceneIndex} was not found for section '{sectionId}'.");

        if (scenes.Count <= 1)
        {
            throw new InvalidOperationException("Can't delete the only remaining scene — the lecture needs at least one.");
        }

        scenes.Remove(sceneToRemove);
        scenes.Sort((a, b) => a.SceneIndex.CompareTo(b.SceneIndex));
        for (var i = 0; i < scenes.Count; i++)
        {
            scenes[i].SceneIndex = i;
        }

        var updatedFields = new Dictionary<string, object?>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            updatedFields[prop.Name] = prop.Value.Clone();
        }
        updatedFields["Scenes"] = scenes;

        teachingStep.StepDataJson = JsonSerializer.Serialize(updatedFields);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return scenes.Count;
    }

    /// <summary>
    /// Regenerates the Teaching step's single lesson illustration (the
    /// "Learn" image shown in the no-Scenes/no-Cards "listen" layout,
    /// 2026-11-11) from a reviewer-supplied prompt — NEVER just the
    /// reviewer's own words alone: the final prompt always starts from this
    /// Engram's ORIGINAL lesson illustration prompt (the same
    /// source-faithful prompt the designer agent generated from the full
    /// class text when this Engram was first created, persisted on the
    /// current <see cref="StructuredLearningIllustration.Prompt"/> row — or,
    /// if that row can't be found, a fallback built straight from the
    /// step's own Content), plus the full lesson text again for grounding,
    /// with the reviewer's instructions appended on top. This keeps a
    /// manual "make it simpler"/"change the colors" tweak from silently
    /// discarding everything the original image was supposed to depict.
    /// <paramref name="logoImageBytes"/> (2026-11-11), when supplied, is a
    /// real company brand logo PNG the reviewer just uploaded — passed
    /// straight through to GraphIllustrationImageService.GenerateAsync,
    /// which switches to its image-edit path and instructs the model to
    /// weave the ACTUAL uploaded logo (not a guessed/hallucinated one)
    /// somewhere natural into the scene.
    /// <paramref name="referenceSourceIllustrationId"/> (2026-11-12), when
    /// supplied, must be one of this section's own SourceDocument
    /// illustrations (e.g. a real extracted video frame) — the model then
    /// uses it as an image-to-image starting point via
    /// GraphIllustrationImageService.GenerateFromReferenceAsync instead of
    /// generating purely from text. Takes precedence over
    /// <paramref name="logoImageBytes"/> if both are supplied.
    /// </summary>
    public async Task<(Guid IllustrationId, string Content)> RegenerateLessonIllustrationAsync(
        Guid sectionId,
        string instructions,
        byte[]? logoImageBytes = null,
        Guid? referenceSourceIllustrationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw new InvalidOperationException("Instructions are required to regenerate the lesson illustration.");
        }

        if (!_imageService.IsConfigured)
        {
            throw new InvalidOperationException("Image generation is not configured.");
        }

        var section = await _dbContext.StructuredLearningDocumentSections
            .Include(s => s.Document)
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Structured learning section with ID '{sectionId}' was not found.");

        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching)
            ?? throw new InvalidOperationException("This section has no Teaching step yet.");

        Guid? currentIllustrationId = null;
        var existingFields = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(teachingStep.StepDataJson))
        {
            using var doc = JsonDocument.Parse(teachingStep.StepDataJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                existingFields[prop.Name] = prop.Value.Clone();
            }
            if (doc.RootElement.TryGetProperty("IllustrationId", out var ilProp) && ilProp.ValueKind == JsonValueKind.String
                && Guid.TryParse(ilProp.GetString(), out var parsedId))
            {
                currentIllustrationId = parsedId;
            }
        }

        string? originalPrompt = null;
        if (currentIllustrationId is not null)
        {
            originalPrompt = await _dbContext.StructuredLearningIllustrations
                .Where(i => i.StructuredLearningIllustrationId == currentIllustrationId)
                .Select(i => i.Prompt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var lessonText = (teachingStep.Content ?? string.Empty).Trim();
        if (lessonText.Length > 6000)
        {
            lessonText = lessonText[..6000].TrimEnd() + "…";
        }

        var basePrompt = !string.IsNullOrWhiteSpace(originalPrompt)
            ? originalPrompt
            : $"Create a clear educational illustration for this lesson, based entirely on its full text below — every concept mentioned must be represented, nothing invented and nothing omitted.\n\nFull lesson text:\n{lessonText}";

        var fullPrompt =
            $"{basePrompt}\n\n" +
            $"Full lesson text (for grounding/context — the image must stay faithful to this):\n{lessonText}\n\n" +
            $"Additional instructions from the reviewer (apply these on top of everything above, without contradicting the lesson's own content):\n{instructions.Trim()}";

        // Reviewer may pick ONE of this Engram's own real source images (e.g. a
        // video frame used to originally generate the lesson) to use as a visual
        // reference for the regenerated illustration (2026-11-12), instead of a
        // pure text-to-image guess. Only source images actually belonging to this
        // section are honored.
        byte[]? referenceImageBytes = null;
        if (referenceSourceIllustrationId is not null)
        {
            var referenceRow = await _dbContext.StructuredLearningIllustrations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    i => i.StructuredLearningIllustrationId == referenceSourceIllustrationId
                        && i.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId
                        && i.Purpose == StructuredLearningIllustrationPurpose.SourceDocument,
                    cancellationToken)
                ?? throw new InvalidOperationException("The selected reference image was not found among this Engram's source images.");

            referenceImageBytes = (await _illustrationStorage.DownloadIllustrationBytesAsync(referenceRow.StoragePath, cancellationToken)).ToArray();
        }

        var generated = referenceImageBytes is not null
            ? await _imageService.GenerateFromReferenceAsync(fullPrompt, referenceImageBytes, cancellationToken)
            : await _imageService.GenerateAsync(fullPrompt, logoImageBytes, cancellationToken);
        using var imageStream = generated.ImageBytes.ToStream();
        var imagePath = await _illustrationStorage.UploadIllustrationAsync(
            section.TenantId,
            section.StructuredLearningDocumentId,
            section.StructuredLearningDocumentSectionId,
            imageIndex: 0,
            imageStream,
            cancellationToken: cancellationToken);

        var illustrationEntity = new StructuredLearningIllustration
        {
            StructuredLearningIllustrationId = Guid.NewGuid(),
            StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
            StoragePath = imagePath,
            Prompt = fullPrompt,
            Caption = lessonText.Length > 2000 ? lessonText[..2000] : lessonText,
            Purpose = StructuredLearningIllustrationPurpose.Teaching,
            ImageModel = generated.ImageModel,
            Width = generated.Width,
            Height = generated.Height,
            CreatedDate = DateTime.UtcNow
        };
        _dbContext.StructuredLearningIllustrations.Add(illustrationEntity);

        existingFields["IllustrationId"] = illustrationEntity.StructuredLearningIllustrationId;
        teachingStep.StepDataJson = JsonSerializer.Serialize(existingFields);

        // Keep the spoken narration in sync with the new scene (2026-11-11): a
        // regenerated image almost always depicts a different concrete scene than the
        // one the narration was originally written to describe (e.g. a different room,
        // a different professor), so patch ONLY the narration's scene-descriptive
        // sentences to match — never touching any pedagogical content — then
        // re-synthesize the audio from the updated text. A poor/low-quality image is
        // never a reason to change the lesson's substance; if the sync agent isn't
        // configured or the call fails, we deliberately keep the OLD narration/audio
        // rather than risk losing/altering teaching content.
        if (_narrationSyncAgent.IsConfigured && !string.IsNullOrWhiteSpace(teachingStep.Content))
        {
            try
            {
                var syncedContent = await _narrationSyncAgent.SyncNarrationAsync(teachingStep.Content, instructions.Trim(), cancellationToken);
                if (!string.IsNullOrWhiteSpace(syncedContent))
                {
                    teachingStep.Content = syncedContent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync Teaching step narration to the regenerated lesson illustration for section {SectionId}; keeping the existing narration/audio.", sectionId);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_speechService.IsConfigured && _illustrationStorage.IsConfigured)
        {
            try
            {
                await RegenerateAudioFromCurrentContentAsync(sectionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to re-synthesize audio after regenerating the lesson illustration for section {SectionId}.", sectionId);
            }
        }

        return (illustrationEntity.StructuredLearningIllustrationId, teachingStep.Content ?? string.Empty);
    }

    /// <summary>
    /// Re-synthesizes ONLY the audio for a section's Teaching step, using its CURRENT
    /// Content verbatim (no rewrite via <see cref="StructuredLearningProfessorLectureAgent"/>) —
    /// the escape hatch for when a reviewer edited the Content manually or via
    /// StructuredLearningStepEditorAgent (e.g. translated it, fixed a mistake) and just wants
    /// the audio to match the text now on screen, instead of the agent re-inventing a new
    /// script from it (which could silently drift language/wording again).
    /// </summary>
    public async Task<string> RegenerateAudioFromCurrentContentAsync(Guid sectionId, CancellationToken cancellationToken = default)
    {
        if (!_speechService.IsConfigured || !_illustrationStorage.IsConfigured)
        {
            throw new InvalidOperationException("Audio synthesis is not configured (missing Azure Speech / storage settings).");
        }

        var section = await _dbContext.StructuredLearningDocumentSections
            .Include(s => s.Document)
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Structured learning section with ID '{sectionId}' was not found.");

        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching)
            ?? throw new InvalidOperationException("This section has no Teaching step yet.");

        if (string.IsNullOrWhiteSpace(teachingStep.Content))
        {
            throw new InvalidOperationException("The Teaching step has no text to synthesize.");
        }

        var languageCode = ResolveLanguageCode(section.Document?.ConfigSnapshotJson, teachingStep.Content);
        var voiceName = languageCode == "en" ? "en-US-AndrewNeural" : "es-MX-JorgeNeural";

        var audioBytes = await _speechService.SynthesizeAsync(teachingStep.Content, voiceName, cancellationToken);
        using var audioStream = new MemoryStream(audioBytes);

        var storagePath = await _illustrationStorage.UploadLectureAudioAsync(
            section.TenantId,
            section.StructuredLearningDocumentId,
            section.StructuredLearningDocumentSectionId,
            audioStream,
            contentType: "audio/mpeg",
            cancellationToken: cancellationToken);

        Guid? illustrationId = null;
        List<string> existingCitations = [];
        List<Guid> existingSourceImageIds = [];
        JsonElement? existingProfessorLecture = null;
        if (!string.IsNullOrWhiteSpace(teachingStep.StepDataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(teachingStep.StepDataJson);
                if (doc.RootElement.TryGetProperty("IllustrationId", out var ilProp) && ilProp.ValueKind == JsonValueKind.String
                    && Guid.TryParse(ilProp.GetString(), out var parsedId))
                {
                    illustrationId = parsedId;
                }
                if (doc.RootElement.TryGetProperty("WebSearchCitations", out var citProp) && citProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in citProp.EnumerateArray())
                    {
                        var str = c.GetString();
                        if (!string.IsNullOrWhiteSpace(str)) existingCitations.Add(str);
                    }
                }
                if (doc.RootElement.TryGetProperty("SourceImageIds", out var srcProp) && srcProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in srcProp.EnumerateArray())
                    {
                        var str = s.GetString();
                        if (Guid.TryParse(str, out var srcId)) existingSourceImageIds.Add(srcId);
                    }
                }
                if (doc.RootElement.TryGetProperty("ProfessorLecture", out var lectureProp))
                {
                    existingProfessorLecture = lectureProp.Clone();
                }
            }
            catch (JsonException)
            {
                // Same graceful-skip rationale as GenerateLectureForSectionAsync's own StepDataJson parsing.
            }
        }

        teachingStep.StepDataJson = JsonSerializer.Serialize(new
        {
            IllustrationId = illustrationId,
            SpeechStoragePath = storagePath,
            ProfessorLecture = existingProfessorLecture,
            WebSearchCitations = existingCitations,
            SourceImageIds = existingSourceImageIds
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return storagePath;
    }

    private static string ResolveLanguageCode(string? configSnapshotJson, string sampleText)
    {
        if (!string.IsNullOrWhiteSpace(configSnapshotJson))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<JsonElement>(configSnapshotJson);
                foreach (var prop in snapshot.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "languageCode", StringComparison.OrdinalIgnoreCase) &&
                        prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var code = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            return code.Trim().ToLowerInvariant().StartsWith("es") ? "es" : "en";
                        }
                    }
                }
            }
            catch { }
        }

        return DetectLanguageCode(sampleText);
    }

    private static string DetectLanguageCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "es";
        }

        var padded = $" {text.ToLowerInvariant()} ";
        string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
        string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu "];

        var englishHits = englishWords.Count(w => padded.Contains(w));
        var spanishHits = spanishWords.Count(w => padded.Contains(w));

        return englishHits > spanishHits ? "en" : "es";
    }

    private sealed class HypothesisQuestionData
    {
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = [];
        public int? CorrectIndex { get; set; }
    }

    public static string FormatHypothesisIntuitions(
        List<StructuredLearningHypothesisResponse> responses,
        string? stepDataJson)
    {
        List<HypothesisQuestionData> questions = [];
        if (!string.IsNullOrWhiteSpace(stepDataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(stepDataJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    questions = JsonSerializer.Deserialize<List<HypothesisQuestionData>>(stepDataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("HypothesisQuestions", out var arrayProp))
                {
                    questions = JsonSerializer.Deserialize<List<HypothesisQuestionData>>(arrayProp.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                }
            }
            catch { }
        }

        var parts = new List<string>();
        foreach (var r in responses)
        {
            var qText = (r.QuestionIndex >= 0 && r.QuestionIndex < questions.Count)
                ? questions[r.QuestionIndex].Question
                : $"Question {r.QuestionIndex + 1}";

            var optText = (r.QuestionIndex >= 0 && r.QuestionIndex < questions.Count &&
                           r.SelectedOptionIndex >= 0 && r.SelectedOptionIndex < questions[r.QuestionIndex].Options.Count)
                ? questions[r.QuestionIndex].Options[r.SelectedOptionIndex]
                : $"Option {r.SelectedOptionIndex}";

            var correctness = r.IsCorrect.HasValue
                ? (r.IsCorrect.Value ? "Correct prediction" : "Incorrect initial guess")
                : "Submitted intuition";

            parts.Add($"{qText} -> Selected: \"{optText}\" ({correctness})");
        }

        return string.Join("; ", parts);
    }

    /// <summary>Decodes a <c>"data:&lt;contentType&gt;;base64,&lt;data&gt;"</c> URL — same format
    /// (and same parsing logic) as <see cref="StructuredLearningExtractionService"/>'s own
    /// TryDecodeDataUrl, duplicated locally rather than shared to avoid coupling these two
    /// otherwise-independent services together over one small static helper.</summary>
    private static (byte[] Bytes, string ContentType)? TryDecodeDataUrl(string dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var commaIndex = dataUrl.IndexOf(',');
        if (commaIndex < 0)
        {
            return null;
        }

        var header = dataUrl[5..commaIndex];
        var contentType = header.Split(';')[0];
        if (string.IsNullOrWhiteSpace(contentType))
        {
            contentType = "image/png";
        }

        try
        {
            var bytes = Convert.FromBase64String(dataUrl[(commaIndex + 1)..]);
            return (bytes, contentType);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
