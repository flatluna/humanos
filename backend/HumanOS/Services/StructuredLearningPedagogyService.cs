using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HumanOS.Services;

/// <summary>
/// Step 3 of the Structured Learning Document pipeline: generates the
/// pedagogical sequence (Hypothesis/Teaching/DeepDive?/Recall?/Production?/
/// Assessment) for every section of a document that isn't already
/// Completed — sections are processed with BOUNDED concurrency (never
/// unlimited, to respect Azure OpenAI rate limits, same rationale as
/// TraditionalCoursePipelineService's MaxConcurrentChapters), each result
/// checked against <see cref="StructuredLearningSectionEvaluator"/> (written
/// BEFORE this agent existed) before being persisted.
///
/// Resumable by design: only queries sections where
/// <see cref="StructuredLearningDocumentSection.PedagogyStatus"/> !=
/// Completed, so re-running after a crash/interruption (or after a Failed
/// section) never re-pays for sections that already succeeded.
/// </summary>
/// <summary>Per-section illustration-mode override for Edit mode's "Add
/// section" feature (2026-08-11) — see
/// StructuredLearningPedagogyService.GenerateIllustrationsAsync's doc
/// comment for what each Mode value does.</summary>
public sealed class SectionIllustrationOverride
{
    public Guid SectionId { get; init; }

    public string Mode { get; init; } = "AiGenerated";

    public byte[]? UploadedImageBytes { get; init; }

    public string? UploadedImageContentType { get; init; }

    /// <summary>The professor's own free-text instruction for the new
    /// section, forwarded as extra illustration guidance ONLY when
    /// <see cref="Mode"/> is "AiGeneratedFromUpload" — the section's
    /// illustration prompts are designed by StructuredLearningSectionDesignerAgent
    /// from the section's Text alone, with no visibility into the fact a
    /// reference image was supplied, so any instruction about how to use it
    /// (e.g. "put our logo on the volunteers' t-shirts") would otherwise be
    /// silently dropped.</summary>
    public string? ReferenceImageUsageInstruction { get; init; }
}

public sealed class StructuredLearningPedagogyService
{
    private const int DefaultMaxConcurrentSections = 3;

    /// <summary>Style guidance for per-scene Teaching illustrations — same
    /// wording as StructuredLearningProfessorLectureService's own constants,
    /// duplicated locally for the same reason that service documents (avoid
    /// coupling two otherwise-independent illustration pipelines together).</summary>
    private const string ProfessorLectureKidsSceneIllustrationStyle =
        "A simple, warm, child-friendly educational illustration for a K-12 learning app. " +
        "Flat design, soft cheerful colors, single clear focal scene, friendly and age-appropriate, " +
        "professional children's book / cartoon illustration style.";

    private const string ProfessorLectureStandardSceneIllustrationStyle =
        "A clean, professional educational illustration for an adult/professional learning app. " +
        "Photorealistic or clean diagrammatic style as appropriate to the subject, single clear focal composition.";

    /// <summary>Prompt for "EnhanceVideoFrame" mode (2026-11-06): the ONLY goal is a
    /// higher-quality RENDER of the exact same real video frame — never a reinterpreted
    /// or generic scene. Explicitly the opposite of the usual Teaching illustration
    /// prompts (which forbid on-screen text) because a real frame's on-screen text
    /// (code, UI labels, exercise text) IS the real content and must be preserved.</summary>
    private const string VideoFrameEnhancementPrompt =
        "This image is a real frame captured from an educational video. Produce a higher-quality, " +
        "sharper, cleaner render of this EXACT SAME frame — same composition, same subject(s), same " +
        "on-screen text/UI/code/diagrams, same layout, same colors. Do NOT invent, add, remove, move, " +
        "or reinterpret anything: no new characters, no mascots, no whiteboards, no classroom scenery " +
        "that isn't already in the frame. Preserve all visible text exactly as shown (it is real content, " +
        "not decoration) — only improve resolution/clarity/sharpness as if re-rendering the same shot " +
        "at higher fidelity.";

    private readonly StructuredLearningSectionDesignerAgent _designer;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly HumanOS.Agents.Studio.PdfImageDescriptionAgent _imageDescriptionAgent;
    private readonly StructuredLearningGenerationUsageService _usageService;
    private readonly StructuredLearningKnowledgeFactGenerationService _knowledgeFactGeneration;
    private readonly StructuredLearningProfessorLectureAgent _professorLectureAgent;
    private readonly AzureSpeechService _speechService;
    private readonly PerplexitySearchService _perplexityService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HumanOS.Storage.IProcessDocumentStorageService _processDocumentStorage;

    /// <summary>Dedicated blob-index range for per-scene ENHANCED video frames
    /// (2026-11-06 multi-frame fix) — distinct from the raw real-frame range
    /// (500+) and the single-legacy-Teaching-image index (1), so enhancing
    /// scene N never collides with/overwrites any other illustration's blob.</summary>
    private const int EnhancedFrameSceneIndexOffset = 700;

    /// <summary>Takes ONE real video frame plus its own synced transcript slice,
    /// wraps both into a single prompt envelope (frame-enhancement instructions +
    /// best-effort vision caption + the transcript text spoken at that moment),
    /// produces a higher-quality img2img render of that exact frame, uploads it
    /// as a new "Teaching" illustration, and returns its Id. Used by
    /// <see cref="HumanOS.Services.VideoLearning.VideoStructuredLearningPedagogyOrchestrator"/>
    /// to enhance EVERY real frame of a multi-frame Engram (not just the first
    /// one). Best-effort: returns null (never throws) on any failure, so the
    /// caller can fall back to the raw, unenhanced frame illustration.</summary>
    public async Task<Guid?> EnhanceVideoFrameForSceneAsync(
        Guid tenantId,
        Guid sectionId,
        int sceneIndex,
        byte[] frameBytes,
        string frameContentType,
        string? transcriptText,
        CancellationToken cancellationToken)
    {
        if (!_imageService.IsConfigured)
        {
            return null;
        }

        try
        {
            string? caption = null;
            if (_imageDescriptionAgent.IsConfigured)
            {
                try
                {
                    var described = await _imageDescriptionAgent.DescribeAsync(
                        frameBytes, frameContentType, transcriptText, cancellationToken);
                    caption = described.Description;
                }
                catch
                {
                    // Best-effort: proceed without a caption.
                }
            }

            // Envelope: enhancement instructions + what the frame shows (vision
            // caption) + what was being said at that exact moment (transcript) —
            // all three grounded in the SAME real frame, never a generic scene.
            var promptEnvelope = VideoFrameEnhancementPrompt
                + (string.IsNullOrWhiteSpace(caption) ? string.Empty : $"\n\nWhat this frame shows: {caption}")
                + (string.IsNullOrWhiteSpace(transcriptText) ? string.Empty : $"\n\nWhat the instructor was saying at this moment: {transcriptText}");

            var enhanced = await _imageService.GenerateFromReferenceAsync(promptEnvelope, frameBytes, cancellationToken);

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            using var uploadStream = new MemoryStream(enhanced.ImageBytes.ToArray());
            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                tenantId, capabilityId: sectionId, nodeId: sectionId,
                EnhancedFrameSceneIndexOffset + sceneIndex, uploadStream,
                contentType: "image/png", cancellationToken: cancellationToken);

            var illustration = new StructuredLearningIllustration
            {
                StructuredLearningIllustrationId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = sectionId,
                StoragePath = storagePath,
                Prompt = promptEnvelope,
                Caption = caption,
                Purpose = StructuredLearningIllustrationPurpose.Teaching,
                ImageModel = enhanced.ImageModel,
                Width = enhanced.Width,
                Height = enhanced.Height,
                CreatedDate = DateTime.UtcNow
            };
            dbContext.StructuredLearningIllustrations.Add(illustration);
            await dbContext.SaveChangesAsync(cancellationToken);
            return illustration.StructuredLearningIllustrationId;
        }
        catch
        {
            // Best-effort: caller falls back to the raw, unenhanced frame illustration.
            return null;
        }
    }

    public StructuredLearningPedagogyService(
        StructuredLearningSectionDesignerAgent designer,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        IConfiguration configuration,
        GraphIllustrationImageService imageService,
        HumanOS.Agents.Studio.PdfImageDescriptionAgent imageDescriptionAgent,
        StructuredLearningGenerationUsageService usageService,
        StructuredLearningKnowledgeFactGenerationService knowledgeFactGeneration,
        StructuredLearningProfessorLectureAgent professorLectureAgent,
        AzureSpeechService speechService,
        PerplexitySearchService perplexityService,
        IServiceScopeFactory scopeFactory,
        HumanOS.Storage.IProcessDocumentStorageService processDocumentStorage)
    {
        _designer = designer;
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _imageService = imageService;
        _imageDescriptionAgent = imageDescriptionAgent;
        _usageService = usageService;
        _knowledgeFactGeneration = knowledgeFactGeneration;
        _professorLectureAgent = professorLectureAgent;
        _speechService = speechService;
        _perplexityService = perplexityService;
        _scopeFactory = scopeFactory;
        _processDocumentStorage = processDocumentStorage;
        // Same rationale as TraditionalCoursePipelineService: this service
        // only needs IConfiguration, so it's constructed directly rather
        // than injected (it's registered Scoped in DI, incompatible with
        // this Singleton's lifetime).
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
    }

    public bool IsConfigured => _designer.IsConfigured;

    public int MaxConcurrentSections
    {
        get
        {
            var raw = _configuration["MaxConcurrentStructuredLearningSections"];
            return int.TryParse(raw, out var value) && value > 0 ? value : DefaultMaxConcurrentSections;
        }
    }

    /// <param name="reportProgress">(step description, completed count, total count, titles of sections currently in flight).</param>
    /// <param name="forceRegenerateAll">When true, reprocesses EVERY section
    /// regardless of PedagogyStatus (not just non-Completed ones) — needed
    /// after an agent prompt/instructions change, so already-succeeded
    /// sections pick up the new behavior instead of being skipped as
    /// already-done.</param>
    /// <param name="fromEngramNumber">Optional 1-based inclusive start position
    /// (by SortOrder) — lets a designer restart/regenerate only a specific
    /// range of engrams (e.g. "1 to 10") instead of the whole document.
    /// Null means from the first engram.</param>
    /// <param name="toEngramNumber">Optional 1-based inclusive end position
    /// (by SortOrder). Null means through the last engram.</param>
    public async Task<StructuredLearningPedagogyResult> RunAsync(
        Guid documentId,
        string languageCode,
        Action<string, int, int, IReadOnlyList<string>> reportProgress,
        CancellationToken cancellationToken,
        bool forceRegenerateAll = false,
        byte[]? uploadedImageBytes = null,
        string? uploadedImageContentType = null,
        string? referenceImageUsageInstruction = null,
        SectionIllustrationOverride? illustrationOverride = null,
        bool? usePerplexitySearch = null,
        int? fromEngramNumber = null,
        int? toEngramNumber = null,
        string? additionalInstruction = null,
        string? imageInstruction = null)
    {
        StructuredLearningDocument document;
        List<StructuredLearningDocumentSection> pendingSections;

        await using (var readContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            document = await readContext.StructuredLearningDocuments.FindAsync([documentId], cancellationToken)
                ?? throw new InvalidOperationException($"No se encontró el documento '{documentId}'.");

            var sectionsQuery = readContext.StructuredLearningDocumentSections
                .Where(s => s.StructuredLearningDocumentId == documentId);
            if (!forceRegenerateAll)
            {
                sectionsQuery = sectionsQuery.Where(s => s.PedagogyStatus != StructuredLearningPedagogyStatus.Completed);
            }

            var orderedSections = await sectionsQuery
                .OrderBy(s => s.SortOrder)
                .ToListAsync(cancellationToken);

            // Engram range filter (2026-09-19) applies AFTER ordering, using the
            // section's 1-based position in the document's own SortOrder
            // sequence — not the raw SortOrder value itself, since SortOrder can
            // start at any base or have gaps. "Engram 1 to 10" always means the
            // first ten engrams in the document, regardless of forceRegenerateAll.
            if (fromEngramNumber is > 0 || toEngramNumber is > 0)
            {
                var from = fromEngramNumber is > 0 ? fromEngramNumber.Value : 1;
                var to = toEngramNumber is > 0 ? toEngramNumber.Value : orderedSections.Count;
                pendingSections = orderedSections
                    .Select((section, index) => (section, position: index + 1))
                    .Where(x => x.position >= from && x.position <= to)
                    .Select(x => x.section)
                    .ToList();
            }
            else
            {
                pendingSections = orderedSections;
            }
        }

        var effectiveUsePerplexitySearch = usePerplexitySearch ?? ReadUsePerplexitySearch(document.ConfigSnapshotJson);

        var processVersion = "structured-learning-pedagogy-v1";
        var promptVersion = StructuredLearningSectionDesignerAgent.SourceFaithfulContractVersion;
        var configSnapshot = JsonSerializer.Serialize(new
        {
            processVersion,
            promptVersion,
            maxConcurrentSections = MaxConcurrentSections,
            forceRegenerateAll,
            documentId,
            languageCode,
            usePerplexitySearch = effectiveUsePerplexitySearch,
            pedagogicalAudience = document.PedagogicalAudience,
            includeRecallStep = document.IncludeRecallStep,
            includeProductionStep = document.IncludeProductionStep,
            includeAssessmentStep = document.IncludeAssessmentStep,
            maxDeepDiveCardsPerNode = document.MaxDeepDiveCardsPerNode,
            assessmentQuestionType = document.AssessmentQuestionType,
            assessmentQuestionsPerRound = document.AssessmentQuestionsPerRound ?? 2
        });

        await using (var writeContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await writeContext.StructuredLearningDocuments
                .Where(d => d.StructuredLearningDocumentId == documentId)
                .ExecuteUpdateAsync(
                    d => d.SetProperty(x => x.PromptVersion, promptVersion)
                          .SetProperty(x => x.ProcessVersion, processVersion)
                          .SetProperty(x => x.ModelName, "azure-openai")
                          .SetProperty(x => x.PromptHash, HashString(configSnapshot))
                          .SetProperty(x => x.ConfigSnapshotJson, configSnapshot),
                    cancellationToken);
        }

        var totalSections = pendingSections.Count;
        var completedCount = 0;
        var failedCount = 0;
        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        var activeTitles = new List<string>();
        var progressLock = new object();

        reportProgress($"Generating pedagogy for {totalSections} pending sections", 0, totalSections, []);

        if (totalSections == 0)
        {
            return new StructuredLearningPedagogyResult { StructuredLearningDocumentId = documentId, SectionsCompleted = 0, SectionsFailed = 0 };
        }

        using var semaphore = new SemaphoreSlim(MaxConcurrentSections);
        var tasks = pendingSections.Select(async section =>
        {
            await semaphore.WaitAsync(cancellationToken);
            lock (progressLock)
            {
                activeTitles.Add(section.SubchapterTitle);
                // Report progress immediately when a section STARTS, not only
                // when one finishes — otherwise a single-engram "Restart
                // engrams…" run never emits a status update containing that
                // engram's title before it completes, leaving the UI stuck on
                // its empty-list fallback the whole time (2026-11-04).
                reportProgress(
                    $"Generating: {section.SubchapterTitle}",
                    completedCount + failedCount, totalSections, [.. activeTitles]);
            }
            try
            {
                SectionIllustrationOverride? perSectionOverride = illustrationOverride;
                if (uploadedImageBytes is { Length: > 0 })
                {
                    perSectionOverride = new SectionIllustrationOverride
                    {
                        SectionId = section.StructuredLearningDocumentSectionId,
                        Mode = "AiGeneratedFromUpload",
                        UploadedImageBytes = uploadedImageBytes,
                        UploadedImageContentType = uploadedImageContentType ?? "image/png",
                        ReferenceImageUsageInstruction = referenceImageUsageInstruction,
                    };
                }

                var (succeeded, inputTokens, outputTokens) = await ProcessSectionAsync(
                    document, section, languageCode, perSectionOverride, effectiveUsePerplexitySearch, cancellationToken, additionalInstruction, imageInstruction);
                lock (progressLock)
                {
                    activeTitles.Remove(section.SubchapterTitle);
                    if (succeeded) completedCount++; else failedCount++;
                    totalInputTokens += inputTokens;
                    totalOutputTokens += outputTokens;
                    reportProgress(
                        $"{(succeeded ? "Completed" : "Failed")}: {section.SubchapterTitle}",
                        completedCount + failedCount, totalSections, [.. activeTitles]);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return new StructuredLearningPedagogyResult
        {
            StructuredLearningDocumentId = documentId,
            SectionsCompleted = completedCount,
            SectionsFailed = failedCount,
            TotalInputTokens = totalInputTokens,
            TotalOutputTokens = totalOutputTokens
        };
    }

    private async Task<(bool Succeeded, int InputTokens, int OutputTokens)> ProcessSectionAsync(
        StructuredLearningDocument document,
        StructuredLearningDocumentSection section,
        string languageCode,
        SectionIllustrationOverride? illustrationOverride,
        bool usePerplexitySearch,
        CancellationToken cancellationToken,
        string? additionalInstruction = null,
        string? imageInstruction = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Mark Generating immediately so a concurrent status poll (or a
        // resumed run started mid-flight) sees this section as in-progress,
        // not NotStarted.
        await dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.PedagogyStatus, StructuredLearningPedagogyStatus.Generating),
                cancellationToken);

        // DeepDive disabled for YoungLearner courses (2026-10-09) — its
        // interaction model (open-ended conversational walkthrough, the
        // student types/speaks a free-form reply the AI grades live) does
        // not fit a pre-reading 5-to-6-year-old; the "Learn" step's Engram
        // Cards already provide an equivalent Explanation -> Example ->
        // Practice walkthrough correctly adapted for this audience (audio
        // narration, real sound effects, tap-to-select recognition game).
        // Never affects Standard (enterprise) courses, which keep DeepDive
        // exactly as before whenever MaxDeepDiveCardsPerNode > 0.
        var includeDeepDiveStep = document.MaxDeepDiveCardsPerNode is > 0
            && document.PedagogicalAudience != "YoungLearner";
        var includeHypothesisStep = ReadIncludeHypothesisStep(document.ConfigSnapshotJson);

        // Informational-only Engram override (2026-11-07) — e.g. a course
        // welcome/orientation Engram: generate ONLY its Teaching/Learn step,
        // regardless of the document's own Hypothesis/Recall/Production/
        // Assessment toggles, since there's nothing to practice or be
        // quizzed on. DeepDive is likewise skipped (same reasoning).
        if (section.IsInformationalOnly)
        {
            includeDeepDiveStep = false;
            includeHypothesisStep = false;
        }
        var includeRecallStep = !section.IsInformationalOnly && document.IncludeRecallStep;
        var includeProductionStep = !section.IsInformationalOnly && document.IncludeProductionStep;
        var includeAssessmentStep = !section.IsInformationalOnly && document.IncludeAssessmentStep;

        // Snapshot every existing engram artifact BEFORE regenerating
        // anything (2026-09-19 "atomic restart" fix) — a restart must wipe
        // ALL of a section's prior generated content (cards/steps,
        // illustrations, hypothesis answers, assessment/recall history),
        // not just Steps, so nothing stale survives a regeneration. Ids are
        // captured now (not deleted yet) so old content stays intact if the
        // AI generation below fails — the actual delete-old+insert-new swap
        // happens together, atomically, in one transaction near the end.
        // Exclude Purpose=SourceDocument illustrations from the wipe (2026-11-xx fix):
        // those are the original, already-extracted source images (process-document
        // uploads, or real video frames for video-sourced engrams) — never
        // AI-generated, never reproduced by this method, so deleting them on every
        // regenerate/retry permanently destroys irreplaceable source material
        // instead of just clearing out the regenerated Hypothesis/Teaching/DeepDive
        // illustrations this run is about to recreate.
        var oldIllustrationIds = await dbContext.StructuredLearningIllustrations
            .Where(i => i.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId
                && i.Purpose != StructuredLearningIllustrationPurpose.SourceDocument)
            .Select(i => i.StructuredLearningIllustrationId)
            .ToListAsync(cancellationToken);
        var oldHypothesisResponseIds = await dbContext.StructuredLearningHypothesisResponses
            .Where(h => h.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
            .Select(h => h.StructuredLearningHypothesisResponseId)
            .ToListAsync(cancellationToken);
        var oldAssessmentRoundIds = await dbContext.StructuredLearningAssessmentRounds
            .Where(r => r.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
            .Select(r => r.StructuredLearningAssessmentRoundId)
            .ToListAsync(cancellationToken);
        var oldRecallAttemptIds = await dbContext.StructuredLearningRecallAttempts
            .Where(r => r.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
            .Select(r => r.StructuredLearningRecallAttemptId)
            .ToListAsync(cancellationToken);

        var sectionOverride = illustrationOverride?.SectionId == section.StructuredLearningDocumentSectionId ? illustrationOverride : null;

        // Auto-fallback to the section's own real video frame (2026-11-06): the generic
        // "Restart engrams" entry point (StartStructuredLearningPedagogyFunction) has no
        // concept of per-section overrides and always calls this with illustrationOverride
        // null, which previously made video-sourced sections silently fall back to a
        // generic AI-generated illustration (ignoring the real frame entirely) whenever a
        // designer restarted engrams from the normal Studio UI instead of the dedicated
        // video pedagogy endpoint. If this section has a real SourceDocument illustration
        // (a video frame, or a backfilled process-document image) and no override was
        // explicitly supplied, build one here so EVERY restart path — video or generic —
        // honors the real source image.
        if (sectionOverride is null)
        {
            // 2026-11-11 fix: must match BOTH the filter and ordering that
            // GetStructuredLearningDocumentFunction uses to build the "Images from the
            // original source file" strip Studio shows the reviewer (OrderBy CreatedDate,
            // excluding IsExcludedFromGeneration=true) — otherwise this fallback could pick
            // a DIFFERENT source image than the one visibly shown/kept (e.g. an image the
            // reviewer explicitly removed with the "✕" button, since that previously had no
            // effect here), or simply the wrong one when a section has more than one.
            var sourceFrame = await dbContext.StructuredLearningIllustrations
                .Where(i => i.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId
                    && i.Purpose == StructuredLearningIllustrationPurpose.SourceDocument
                    && !i.IsExcludedFromGeneration)
                .OrderBy(i => i.CreatedDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceFrame is not null)
            {
                try
                {
                    var frameBytes = await _illustrationStorage.DownloadIllustrationBytesAsync(sourceFrame.StoragePath, cancellationToken);
                    sectionOverride = new SectionIllustrationOverride
                    {
                        SectionId = section.StructuredLearningDocumentSectionId,
                        Mode = "EnhanceVideoFrame",
                        UploadedImageBytes = frameBytes.ToArray(),
                        UploadedImageContentType = "image/jpeg",
                    };
                }
                catch (Exception ex)
                {
                    // Best-effort: fall through with no override — the section still gets
                    // its pedagogical text, same as before this fix.
                    Console.Error.WriteLine($"[VideoFrameFallback] DownloadIllustrationBytesAsync failed for {sourceFrame.StoragePath}: {ex}");
                }
            }
            else
            {
                Console.Error.WriteLine($"[VideoFrameFallback] No SourceDocument illustration found for section {section.StructuredLearningDocumentSectionId}");
            }
        }
        else
        {
            Console.Error.WriteLine($"[VideoFrameFallback] sectionOverride already set to Mode={sectionOverride.Mode} before fallback check.");
        }

        // 2026-11-11: an explicit imageInstruction (e.g. from Studio's
        // "Edit source text with AI" box, no uploaded reference image
        // involved) always wins over any upload-derived guidance — it's a
        // direct reviewer request for how THIS regeneration's illustrations
        // should look.
        var illustrationGuidance = imageInstruction
            ?? (sectionOverride?.Mode == "AiGeneratedFromUpload" ? sectionOverride.ReferenceImageUsageInstruction : null);

        string? perplexityFindings = null;
        List<string> perplexityCitations = [];

        if (usePerplexitySearch && _perplexityService.IsConfigured)
        {
            try
            {
                var queryParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(section.ChapterTitle)) queryParts.Add(section.ChapterTitle);
                if (!string.IsNullOrWhiteSpace(section.SubchapterTitle) && !section.SubchapterTitle.Equals(section.ChapterTitle, StringComparison.OrdinalIgnoreCase))
                {
                    queryParts.Add(section.SubchapterTitle);
                }
                if (!string.IsNullOrWhiteSpace(section.Capability)) queryParts.Add(section.Capability);

                var searchQuery = string.Join(" - ", queryParts);
                if (string.IsNullOrWhiteSpace(searchQuery))
                {
                    searchQuery = document.DocumentName;
                }

                Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: Searching Perplexity with query: \"{searchQuery}\"");
                var searchResult = await _perplexityService.SearchAsync(searchQuery, cancellationToken);
                if (!string.IsNullOrWhiteSpace(searchResult.Answer))
                {
                    perplexityFindings = searchResult.Answer;
                    perplexityCitations = searchResult.Citations ?? [];
                    Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: Perplexity returned {perplexityCitations.Count} citations.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: Perplexity search failed, continuing without web grounding: {ex.Message}");
            }
        }

        try
        {
            var knowledgeFacts = await LoadKnowledgeFactsAsync(dbContext, section.StructuredLearningDocumentSectionId, cancellationToken);

            // Knowledge Facts are only ever a QUALITY ENHANCEMENT, never a
            // hard requirement — a designer can legitimately combine any
            // mix of options (Hypothesis on/off, Knowledge Graph on/off,
            // Recall/Production/Assessment on/off), and pedagogy must
            // still complete for every combination instead of hard-failing
            // a whole section just because one optional piece didn't come
            // through. When IncludeKnowledgeGraphEntities is false, a
            // section legitimately having zero facts is expected. When it's
            // true but facts still can't be produced (e.g. auto-recovery
            // below fails, or the section has too little content to derive
            // Core facts from), we now log a warning and fall back to
            // grounding pedagogy in the section's own Text alone — exactly
            // like before Knowledge Facts existed — rather than blocking
            // the designer's whole course (2026-08-23 fix; previously this
            // threw "No persisted Knowledge Facts..." and marked the
            // section Failed, even though Hypothesis-only, Recall-only, or
            // any other combination has nothing to do with Knowledge Facts).
            if (document.IncludeKnowledgeGraphEntities)
            {
                // Auto-recovery: the designer very often clicks "Generate
                // Pedagogy" before ever running Knowledge Facts for a
                // section — try generating them once here first, so
                // pedagogy just works as long as the section's own
                // Knowledge Graph (entities/connections) already exists.
                if (knowledgeFacts.Count == 0 && _knowledgeFactGeneration.IsConfigured)
                {
                    try
                    {
                        await _knowledgeFactGeneration.GenerateValidateAndPersistAsync(section.StructuredLearningDocumentSectionId, cancellationToken);
                        knowledgeFacts = await LoadKnowledgeFactsAsync(dbContext, section.StructuredLearningDocumentSectionId, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: Knowledge Facts auto-recovery failed, falling back to Text-only grounding: {ex.Message}");
                    }
                }

                var coreFactCount = knowledgeFacts.Count(fact => string.Equals(fact.Importance, "Core", StringComparison.OrdinalIgnoreCase));
                if (knowledgeFacts.Count == 0 || coreFactCount == 0 || knowledgeFacts.Any(fact => fact.KnowledgeFactId == Guid.Empty || fact.GraphPath.Count == 0))
                {
                    var reason = knowledgeFacts.Count == 0
                        ? "no persisted Knowledge Facts are available"
                        : coreFactCount == 0
                            ? "no persisted Core Knowledge Fact is available"
                            : "a persisted Knowledge Fact has no resolvable Graph Path";
                    Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: {reason} for this Engram; continuing with Text-only grounding instead of failing the section.");
                    knowledgeFacts = [];
                }
            }

            var coreFactCountForLog = knowledgeFacts.Count(fact => string.Equals(fact.Importance, "Core", StringComparison.OrdinalIgnoreCase));
            var pathStepCount = knowledgeFacts.Sum(fact => fact.GraphPath.Count);
            Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: loaded {knowledgeFacts.Count} Knowledge Facts ({coreFactCountForLog} Core, {knowledgeFacts.Count - coreFactCountForLog} Supporting) and {pathStepCount} GraphPathSteps from SQL.");
            var designStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Occasionally the model's structured-output JSON is malformed
            // (e.g. writes an array/object into a field the schema declares
            // as a plain string, such as a DeepDive step's Content) — that
            // throws a raw JsonException from inside DesignAsync/RunAsync
            // rather than an empty/invalid Steps list. Treat that the same
            // as any other validation failure so it feeds into the SAME
            // retry loop below instead of crashing the whole section on the
            // very first attempt.
            async Task<(StructuredLearningSectionDesignerAgent.DesignResult? Design, List<string> Failures)> TryDesignAsync()
            {
                try
                {
                    var attemptDesign = await _designer.DesignAsync(
                        section.ChapterTitle,
                        section.SubchapterTitle,
                        section.Text,
                        languageCode,
                        document.PedagogicalAudience,
                        includeDeepDiveStep,
                        document.MaxDeepDiveCardsPerNode,
                        includeHypothesisStep,
                        includeRecallStep,
                        includeProductionStep,
                        includeAssessmentStep,
                        cancellationToken,
                        illustrationGuidance,
                        knowledgeFacts,
                        perplexityFindings,
                        document.CourseLevel,
                        document.SourceCapabilityCode == "video",
                        additionalInstruction);
                    return (attemptDesign, ValidateGeneratedSteps(attemptDesign.Response.Steps, document, includeDeepDiveStep, includeHypothesisStep, includeRecallStep, includeProductionStep, includeAssessmentStep, section.Text, attemptDesign.Response.Capability));
                }
                catch (JsonException ex)
                {
                    return (null, [$"Malformed model response: {ex.Message}"]);
                }
            }

            var (design, failures) = await TryDesignAsync();
            var steps = design?.Response.Steps ?? [];

            // Retry up to 2 more times (3 attempts total) — occasionally the
            // model drops/reorders a step (e.g. skips DeepDive), writes
            // ExampleCards too short despite the explicit instructions, or
            // returns malformed JSON (see TryDesignAsync above). Some topics
            // (e.g. terse math derivations) miss on the first retry too, so
            // a single retry wasn't always enough; token usage across every
            // successful attempt is accumulated onto the final result.
            const int maxAttempts = 3;
            for (var attempt = 2; failures.Count > 0 && attempt <= maxAttempts; attempt++)
            {
                var (retryDesign, retryFailures) = await TryDesignAsync();
                if (retryDesign is null)
                {
                    failures = retryFailures;
                    continue;
                }

                design = new StructuredLearningSectionDesignerAgent.DesignResult
                {
                    Response = retryDesign.Response,
                    TokenUsage = new AgentTokenUsage
                    {
                        AgentName = retryDesign.TokenUsage.AgentName,
                        ModelName = retryDesign.TokenUsage.ModelName,
                        InputTokens = (design?.TokenUsage.InputTokens ?? 0) + retryDesign.TokenUsage.InputTokens,
                        OutputTokens = (design?.TokenUsage.OutputTokens ?? 0) + retryDesign.TokenUsage.OutputTokens,
                        CachedInputTokens = (design?.TokenUsage.CachedInputTokens ?? 0) + retryDesign.TokenUsage.CachedInputTokens
                    }
                };
                steps = retryDesign.Response.Steps;
                failures = retryFailures;
            }

            if (design is null)
            {
                await MarkFailedAsync(dbContext, section.StructuredLearningDocumentSectionId, string.Join(" | ", failures), cancellationToken);
                return (false, 0, 0);
            }

            // Deterministic fallback for the recurring "DeepDive returned
            // zero ExampleCards" failure (2026-11-04): the designer prompt
            // already explicitly says "AT LEAST ONE card — never zero", yet
            // the model still occasionally omits them (especially under
            // concurrent load across several sections at once), and the 3
            // retries above don't always fix it either. Rather than losing
            // the WHOLE Engram over just this one guardrail when every other
            // step generated fine, synthesize one card directly from this
            // section's own source text (never invented content) so the
            // section can still complete.
            if (failures is [StructuredLearningSectionEvaluator.ZeroExampleCardsFailure])
            {
                var deepDiveStep = steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.DeepDive);
                if (deepDiveStep is not null)
                {
                    var fallbackText = section.Text.Trim();
                    if (fallbackText.Length > 1500)
                    {
                        fallbackText = fallbackText[..1500].TrimEnd() + "…";
                    }
                    deepDiveStep.ExampleCards =
                    [
                        new StructuredLearningExampleCardDto
                        {
                            Title = section.SubchapterTitle,
                            Text = fallbackText,
                        },
                    ];
                    Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: model returned zero DeepDive ExampleCards after {maxAttempts} attempts — injected a fallback card built from the section's own source text instead of failing the section.");
                    failures = [];
                }
            }

            // Deterministic fallback for a Capability statement that's only
            // slightly over the atomicity length guardrail (2026-11-11):
            // observed on a rich, multi-step source text ("The Training
            // Loop...") where the designer's capability statement was 287
            // chars vs the 280 max — genuinely still ONE atomic idea, just a
            // few words too long to phrase concisely. Rather than losing the
            // whole Engram (and silently discarding the reviewer's new text
            // and image instruction) over a handful of characters, truncate
            // at the last word boundary that fits, mirroring the
            // ZeroExampleCards fallback above.
            if (design is not null && failures.Count == 1
                && failures[0].StartsWith("Capability statement is ", StringComparison.Ordinal)
                && failures[0].Contains("likely bundles more than one teachable idea", StringComparison.Ordinal))
            {
                var trimmed = design.Response.Capability.Trim();
                if (trimmed.Length > StructuredLearningSectionEvaluator.MaxAtomicCapabilityLength)
                {
                    var cut = trimmed[..StructuredLearningSectionEvaluator.MaxAtomicCapabilityLength];
                    var lastSpace = cut.LastIndexOf(' ');
                    if (lastSpace > 0)
                    {
                        cut = cut[..lastSpace];
                    }
                    design.Response.Capability = cut.TrimEnd(',', ';', ':', ' ') + ".";
                    Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: Capability statement was {trimmed.Length} chars (max {StructuredLearningSectionEvaluator.MaxAtomicCapabilityLength}) — truncated at a word boundary instead of failing the section.");
                    failures = [];
                }
            }

            designStopwatch.Stop();
            try
            {
                await _usageService.RecordAsync(
                    document.StructuredLearningDocumentId, section.StructuredLearningDocumentSectionId, "Pedagogy",
                    "StructuredLearningSectionDesigner", design.TokenUsage.ModelName, design.TokenUsage.InputTokens,
                    design.TokenUsage.OutputTokens, design.TokenUsage.CachedInputTokens, designStopwatch.ElapsedMilliseconds, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Usage logging must never break pedagogy generation itself.
            }

            if (failures.Count > 0)
            {
                await MarkFailedAsync(dbContext, section.StructuredLearningDocumentSectionId, string.Join(" | ", failures), cancellationToken);
                return (false, design.TokenUsage.InputTokens, design.TokenUsage.OutputTokens);
            }

            await GenerateIllustrationsAsync(dbContext, section.TenantId, section.StructuredLearningDocumentSectionId, steps, sectionOverride, section.Text, illustrationGuidance, cancellationToken);

            // Professor Lecture & Spoken Audio Generation (2026-09-06)
            // Creates the oral class script directly from the engram and synthesizes
            // the professor's speech to Azure Data Lake.
            await GenerateProfessorLectureAndSpeechAsync(
                dbContext, document, section, steps, languageCode, sectionOverride, cancellationToken);

            // ATOMIC SWAP (2026-09-19): a "retry"/regenerate on a section that
            // already has old content persisted (steps/cards, illustrations,
            // hypothesis answers, assessment/recall history) must delete ALL of
            // it and insert the freshly generated replacement together, as one
            // transaction — either the whole old->new swap commits, or (on any
            // failure) nothing changes and the section keeps its previous
            // content instead of being left half-empty.
            //
            // Wrapped in the DbContext's own retrying execution strategy
            // (2026-09-20 fix): SqlServer's retrying strategy refuses plain
            // user-initiated BeginTransactionAsync calls ("does not support
            // user-initiated transactions"), which was silently failing
            // EVERY section (0 tokens, immediate exception) — regenerate
            // via CreateExecutionStrategy().ExecuteAsync so the transaction
            // participates in the same retry-safe execution the rest of
            // this DbContext uses.
            var strategy = dbContext.Database.CreateExecutionStrategy();
            var processVersion = "structured-learning-pedagogy-v1";
            await strategy.ExecuteAsync(async () =>
            {
                await using var swapTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                if (oldAssessmentRoundIds.Count > 0)
                {
                    await dbContext.StructuredLearningAssessmentQuestionAttempts
                        .Where(a => oldAssessmentRoundIds.Contains(a.StructuredLearningAssessmentRoundId))
                        .ExecuteDeleteAsync(cancellationToken);
                    await dbContext.StructuredLearningAssessmentRounds
                        .Where(r => oldAssessmentRoundIds.Contains(r.StructuredLearningAssessmentRoundId))
                        .ExecuteDeleteAsync(cancellationToken);
                }
                if (oldRecallAttemptIds.Count > 0)
                {
                    await dbContext.StructuredLearningRecallAttempts
                        .Where(r => oldRecallAttemptIds.Contains(r.StructuredLearningRecallAttemptId))
                        .ExecuteDeleteAsync(cancellationToken);
                }
                if (oldHypothesisResponseIds.Count > 0)
                {
                    await dbContext.StructuredLearningHypothesisResponses
                        .Where(h => oldHypothesisResponseIds.Contains(h.StructuredLearningHypothesisResponseId))
                        .ExecuteDeleteAsync(cancellationToken);
                }
                if (oldIllustrationIds.Count > 0)
                {
                    await dbContext.StructuredLearningIllustrations
                        .Where(i => oldIllustrationIds.Contains(i.StructuredLearningIllustrationId))
                        .ExecuteDeleteAsync(cancellationToken);
                }
                // Reset any per-student "question currently in progress" state too
                // (2026-10-27 fix) — StructuredLearningPersonSectionState is NOT
                // part of the section's own rows deleted below, so without this a
                // student/preview who had already started this section's Recall
                // kept their OLD, stale CurrentRecallPrompt text (e.g. "how many
                // red blocks?") persisted from before the restart, while the
                // illustration id shown next to it got re-derived fresh from the
                // BRAND NEW regenerated bank (since the recall attempts above ARE
                // wiped, item index resets to 0) — pairing an old question with a
                // completely different new picture. Mirrors the same reset already
                // done by the single-section "Regenerate with Static Recall" flow
                // (RegenerateStructuredLearningRecallItemsFunction).
                await dbContext.StructuredLearningPersonSectionStates
                    .Where(p => p.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.CurrentRecallPrompt, (string?)null)
                              .SetProperty(x => x.CurrentRecallVisualObjectsDescription, (string?)null)
                              .SetProperty(x => x.CurrentDeepDiveCardIndex, 0)
                              .SetProperty(x => x.CurrentDeepDivePrompt, (string?)null),
                        cancellationToken);
                await dbContext.StructuredLearningSectionSteps
                    .Where(s => s.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
                    .ExecuteDeleteAsync(cancellationToken);
            var promptVersion = StructuredLearningSectionDesignerAgent.SourceFaithfulContractVersion;
            var modelName = design.TokenUsage.ModelName;
            var configSnapshot = JsonSerializer.Serialize(new
            {
                processVersion,
                promptVersion,
                includeDeepDiveStep,
                includeHypothesisStep,
                maxDeepDiveCards = document.MaxDeepDiveCardsPerNode,
                includeRecallStep,
                includeProductionStep,
                includeAssessmentStep,
                isInformationalOnly = section.IsInformationalOnly,
                usePerplexitySearch,
                hasApiData = perplexityCitations.Count > 0,
                webSearchSources = perplexityCitations,
                webSearchService = perplexityCitations.Count > 0 ? "Perplexity AI" : null,
                webSearchSnippet = perplexityFindings != null && perplexityFindings.Length > 300 ? perplexityFindings[..300] + "..." : perplexityFindings,
                languageCode,
                pedagogicalAudience = document.PedagogicalAudience,
                illustrationGuidanceProvided = !string.IsNullOrWhiteSpace(illustrationGuidance)
            });

            var statements = (design.Response.Statements ?? [])
                .Select(dto => new StructuredLearningStatement
                {
                    StatementId = Guid.NewGuid(),
                    SectionId = section.StructuredLearningDocumentSectionId,
                    StatementType = dto.StatementType,
                    Statement = dto.Statement,
                    SourceText = dto.SourceText,
                    ConditionEntity = dto.ConditionEntity,
                    ConditionAttribute = dto.ConditionAttribute,
                    ConditionOperator = dto.ConditionOperator,
                    ConditionValue = dto.ConditionValue,
                    ConditionUnit = dto.ConditionUnit,
                    ActorEntity = dto.ActorEntity,
                    ActorType = dto.ActorType,
                    Action = dto.Action,
                    TargetEntity = dto.TargetEntity,
                    Modality = dto.Modality,
                    PossibleOutcomes = dto.PossibleOutcomes
                })
                .ToList();

            await dbContext.StructuredLearningDocumentSections
                .Where(s => s.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.PromptVersion, promptVersion)
                          .SetProperty(x => x.ProcessVersion, processVersion)
                          .SetProperty(x => x.ModelName, modelName)
                          .SetProperty(x => x.ConfigSnapshotJson, configSnapshot)
                          .SetProperty(x => x.PromptHash, HashString(configSnapshot))
                          .SetProperty(x => x.Statements, statements)
                          .SetProperty(x => x.PedagogyVersion, x => x.PedagogyVersion + 1)
                          .SetProperty(x => x.PedagogyGeneratedAt, DateTime.UtcNow),
                    cancellationToken);

            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step.StepType == StructuredLearningStepType.Teaching && perplexityCitations.Count > 0)
                {
                    step.WebSearchCitations = perplexityCitations;
                }

                dbContext.StructuredLearningSectionSteps.Add(new StructuredLearningSectionStep
                {
                    StructuredLearningSectionStepId = Guid.NewGuid(),
                    StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                    TenantId = section.TenantId,
                    StepType = step.StepType,
                    SortOrder = i,
                    Content = step.Content ?? string.Empty,
                    StepDataJson = BuildStepDataJson(step),
                    PromptVersion = promptVersion,
                    ProcessVersion = processVersion,
                    ModelName = modelName,
                    PromptHash = HashString(step.Content + "|" + step.StepType),
                    ConfigSnapshotJson = configSnapshot,
                    CreatedDate = DateTime.UtcNow
                });
            }

                await dbContext.SaveChangesAsync(cancellationToken);
                await swapTransaction.CommitAsync(cancellationToken);
            });

            // Static Recall/Assessment generation-cost lever (2026-10-06) —
            // when the designer chose "Static" for either step, pre-generate
            // its up-to-3 questions right now, during authoring, so no
            // student ever triggers an LLM call for them at runtime (same
            // question persisted in SQL and served to everyone). No-op for
            // sections whose step isn't included, or whose mode is Dynamic.
            if ((includeRecallStep && document.RecallGenerationMode == StructuredLearningGenerationMode.Static)
                || (includeAssessmentStep && document.AssessmentGenerationMode == StructuredLearningGenerationMode.Static))
            {
                try
                {
                    using var staticScope = _scopeFactory.CreateScope();
                    await using var staticDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                    if (includeRecallStep && document.RecallGenerationMode == StructuredLearningGenerationMode.Static)
                    {
                        var recallEngine = staticScope.ServiceProvider.GetRequiredService<StructuredLearningRecallEngine>();
                        var recallReferenceBytes = sectionOverride?.Mode == "AiGeneratedFromUpload" ? sectionOverride.UploadedImageBytes : null;
                        await recallEngine.EnsureStaticItemsAsync(
                            staticDbContext,
                            section.StructuredLearningDocumentSectionId,
                            cancellationToken,
                            recallReferenceBytes,
                            illustrationGuidance);
                    }

                    if (includeAssessmentStep && document.AssessmentGenerationMode == StructuredLearningGenerationMode.Static)
                    {
                        var assessmentEngine = staticScope.ServiceProvider.GetRequiredService<StructuredLearningAssessmentEngine>();
                        await assessmentEngine.EnsureStaticQuestionsAsync(staticDbContext, section.StructuredLearningDocumentSectionId, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Never fail the whole section over the static-bank
                    // pre-generation step — Dynamic generation at runtime
                    // remains the safety net if this didn't finish.
                    Console.WriteLine($"[Pedagogy] Engram {section.StructuredLearningDocumentSectionId}: static Recall/Assessment generation failed, will fall back to Dynamic at runtime: {ex.Message}");
                }
            }

            // PedagogyStatus flips to Completed only now, AFTER the static
            // Recall/Assessment bank is pre-generated (2026-10-26 fix) —
            // previously this was set before the static-bank block above,
            // so any student/API call to .../recall/state that landed in
            // that window saw an empty bank and permanently fell back to a
            // Dynamic-generated first question/card, defeating Static mode
            // for that section.
            await dbContext.StructuredLearningDocumentSections
                .Where(s => s.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.PedagogyStatus, StructuredLearningPedagogyStatus.Completed)
                          .SetProperty(x => x.PedagogyErrorMessage, (string?)null),
                    cancellationToken);

            return (true, design.TokenUsage.InputTokens, design.TokenUsage.OutputTokens);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var msg = ex.InnerException != null ? $"{ex.Message} --> {ex.InnerException.Message}" : ex.Message;
            await MarkFailedAsync(dbContext, section.StructuredLearningDocumentSectionId, msg, cancellationToken);
            return (false, 0, 0);
        }
    }

    private static bool ReadIncludeHypothesisStep(string? configSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(configSnapshotJson)) return true;

        try
        {
            using var json = JsonDocument.Parse(configSnapshotJson);
            return !json.RootElement.TryGetProperty("IncludeHypothesisStep", out var value)
                || value.ValueKind != JsonValueKind.False;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool ReadUsePerplexitySearch(string? configSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(configSnapshotJson)) return false;

        try
        {
            using var json = JsonDocument.Parse(configSnapshotJson);
            if (json.RootElement.TryGetProperty("UsePerplexitySearch", out var value)
                || json.RootElement.TryGetProperty("usePerplexitySearch", out value))
            {
                return value.ValueKind == JsonValueKind.True;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static async Task MarkFailedAsync(HumanOsDbContext dbContext, Guid sectionId, string errorMessage, CancellationToken cancellationToken)
    {
        await dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.PedagogyStatus, StructuredLearningPedagogyStatus.Failed)
                      .SetProperty(x => x.PedagogyErrorMessage, errorMessage),
                cancellationToken);
    }

    private static async Task<List<StructuredLearningPedagogyKnowledgeFactDto>> LoadKnowledgeFactsAsync(
        HumanOsDbContext dbContext,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.StructuredLearningKnowledgeFacts
            .Where(fact => fact.EngramId == sectionId)
            .Select(fact => new StructuredLearningPedagogyKnowledgeFactDto
            {
                KnowledgeFactId = fact.KnowledgeFactId,
                Statement = fact.Statement,
                FactType = fact.FactType,
                Importance = fact.Importance,
                DerivationType = fact.DerivationType,
                EpistemicStatus = fact.EpistemicStatus,
                Confidence = fact.Confidence,
                GraphPath = fact.GraphPathSteps
                    .OrderBy(step => step.StepOrder)
                    .Select(step => new StructuredLearningPedagogyGraphPathStepDto
                    {
                        ConnectionId = step.ConnectionId,
                        StepOrder = step.StepOrder,
                        SourceEntityId = step.Connection.SourceEntityId,
                        SourceEntityName = step.Connection.SourceEntity.Name,
                        RelationshipCategory = step.Connection.RelationshipCategory,
                        RelationshipLabel = step.Connection.RelationshipLabel,
                        TargetEntityId = step.Connection.TargetEntityId,
                        TargetEntityName = step.Connection.TargetEntity.Name,
                        Evidence = step.Connection.Evidence,
                        EpistemicStatus = step.Connection.EpistemicStatus,
                        Confidence = step.Connection.Confidence
                    }).ToList()
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>Generates real illustrations for the Hypothesis step, the
    /// Teaching step (always present), and every DeepDive ExampleCard that
    /// has an IllustrationPrompt — mutates each step's/card's IllustrationId
    /// in place. Same "best-effort, never blocks the rest" behavior as
    /// ExampleCardIllustrationService/TraditionalCoursePipelineService's own
    /// per-card illustration step: a missing IllustrationId just means the
    /// UI shows no image (or falls back to Emoji for DeepDive cards).
    ///
    /// <paramref name="illustrationOverride"/> is only ever non-null for the
    /// ONE section a professor is adding right now via Edit mode's "Add
    /// section" feature (2026-08-11) — mirrors
    /// AddCapabilityGraphNodeFunction's 4-way IllustrationMode choice:
    /// "None" skips illustrations entirely; "UploadDirect" uses the
    /// professor's own image AS-IS for the Teaching step only (same
    /// one-record simplification Graph's own node-addition uses), captioned
    /// via <see cref="_imageDescriptionAgent"/>; "AiGeneratedFromUpload"
    /// keeps the normal AI-designed illustrations but generates them via
    /// image-edit using the professor's image as a reference instead of a
    /// blank generation; a null override (or "AiGenerated") is the default,
    /// unchanged text-to-image behavior for every other (bulk) section.</summary>
    private static string HashString(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash)[..32];
    }

    /// <summary>Decodes a <c>"data:&lt;contentType&gt;;base64,&lt;data&gt;"</c> URL — same format
    /// (and same parsing logic) as <see cref="StructuredLearningExtractionService"/>'s own
    /// TryDecodeDataUrl, duplicated locally rather than shared to avoid coupling these two
    /// otherwise-independent services together over one small static helper.</summary>
    private static (byte[] Bytes, string ContentType)? TryDecodeStepImageDataUrl(string dataUrl)
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

    private async Task GenerateProfessorLectureAndSpeechAsync(
        HumanOsDbContext dbContext,
        StructuredLearningDocument document,
        StructuredLearningDocumentSection section,
        List<StructuredLearningSectionStepDto> steps,
        string languageCode,
        SectionIllustrationOverride? illustrationOverride,
        CancellationToken cancellationToken)
    {
        var teachingStep = steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);
        if (teachingStep is null)
        {
            return;
        }

        // 0. Real source-document image(s) for this step (2026-11-03) — self-healing: if no
        // SourceDocument illustration rows exist yet for this section (e.g. the course was
        // created before this pipeline existed, or extraction found none), look up the original
        // HumanOS process document via the linked Skill and backfill them now, so "Restart
        // engrams" behaves identically regardless of when the section was first extracted.
        var sourceImages = new List<(byte[] Bytes, string ContentType)>();
        var sourceImageIds = new List<Guid>();
        try
        {
            var sourceImageRows = await dbContext.StructuredLearningIllustrations
                .Where(i => i.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId
                    && i.Purpose == StructuredLearningIllustrationPurpose.SourceDocument
                    && !i.IsExcludedFromGeneration)
                .OrderBy(i => i.CreatedDate)
                .ToListAsync(cancellationToken);

            if (sourceImageRows.Count == 0 && !string.IsNullOrWhiteSpace(document.TargetSkill))
            {
                var skill = await dbContext.Skills
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.TenantId == section.TenantId
                        && s.Name == document.TargetSkill
                        && s.SourceProcessDocumentId != null, cancellationToken);

                if (skill?.SourceProcessDocumentId is Guid processDocumentId)
                {
                    var structure = await _processDocumentStorage.GetStructuredResultAsync(
                        section.TenantId.ToString("D").ToLowerInvariant(), processDocumentId.ToString(), cancellationToken);

                    var matchedStep = structure?.Chapters
                        .FirstOrDefault(c => c.Title == section.ChapterTitle)?.Steps
                        .FirstOrDefault(st => st.StepName == section.SubchapterTitle);

                    if (matchedStep is { StepImages.Count: > 0 })
                    {
                        for (var imgIndex = 0; imgIndex < matchedStep.StepImages.Count; imgIndex++)
                        {
                            var decoded = TryDecodeStepImageDataUrl(matchedStep.StepImages[imgIndex].DataUrl);
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
                            dbContext.StructuredLearningIllustrations.Add(newRow);
                            sourceImageRows.Add(newRow);
                        }
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            foreach (var row in sourceImageRows)
            {
                try
                {
                    var bytes = await _illustrationStorage.DownloadIllustrationBytesAsync(row.StoragePath, cancellationToken);
                    sourceImages.Add((bytes.ToArray(), "image/png"));
                    sourceImageIds.Add(row.StructuredLearningIllustrationId);
                }
                catch
                {
                    // Best-effort: a missing/unreachable blob should never block lecture generation.
                }
            }
        }
        catch
        {
            // Best-effort: a missing/unreachable process document should never block pedagogy generation.
        }

        // 1. Generate generic professor spoken lecture for all students
        HumanOS.Contracts.Studio.StructuredLearningProfessorLectureResult? lecture = null;
        if (_professorLectureAgent.IsConfigured)
        {
            var lectureStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var isVideoSourced = document.SourceCapabilityCode == "video";
                var isFirstEngram = isVideoSourced && await dbContext.StructuredLearningDocumentSections
                    .Where(s => s.StructuredLearningDocumentId == document.StructuredLearningDocumentId)
                    .OrderBy(s => s.SortOrder)
                    .Select(s => s.StructuredLearningDocumentSectionId)
                    .FirstOrDefaultAsync(cancellationToken) == section.StructuredLearningDocumentSectionId;

                var context = new HumanOS.Contracts.Studio.StructuredLearningProfessorLectureContext
                {
                    CourseId = document.StructuredLearningDocumentId,
                    CourseTitle = document.DocumentName,
                    SectionId = section.StructuredLearningDocumentSectionId,
                    ChapterTitle = section.ChapterTitle,
                    SubchapterTitle = section.SubchapterTitle,
                    Capability = section.Capability,
                    TeachingContent = teachingStep.Content,
                    IllustrationDescription = teachingStep.IllustrationPrompt,
                    DomainOrTopic = document.Category ?? document.TargetSkill,
                    TargetLanguage = languageCode,
                    PedagogicalAudience = document.PedagogicalAudience,
                    SourceImages = sourceImages,
                    // Video-sourced Engrams (2026-11-06): preserve the real transcript
                    // near-verbatim instead of composing a new masterclass lecture — see
                    // StructuredLearningProfessorLectureAgent.VideoSourceInstructions.
                    IsVideoSourced = isVideoSourced,
                    VideoSourceName = document.DocumentName,
                    IsFirstEngram = isFirstEngram
                };

                teachingStep.SourceImageIds = sourceImageIds.Count > 0 ? sourceImageIds : null;

                var lectureGeneration = await _professorLectureAgent.GenerateLectureAsync(context, cancellationToken);
                lectureStopwatch.Stop();
                lecture = lectureGeneration.Result;
                teachingStep.ProfessorLecture = lecture;

                // Real LLM cost of this call (2026-10-04) — previously
                // discarded entirely (see GenerateLectureAsync's doc
                // comment), meaning the professor lecture's generation cost
                // never showed up in the Pedagogy step total on the admin
                // "Cursos" tab nor the Simulations authoring baseline.
                try
                {
                    await _usageService.RecordAsync(
                        document.StructuredLearningDocumentId, section.StructuredLearningDocumentSectionId, "Pedagogy",
                        "StructuredLearningProfessorLectureAgent", lectureGeneration.TokenUsage.ModelName,
                        lectureGeneration.TokenUsage.InputTokens, lectureGeneration.TokenUsage.OutputTokens,
                        lectureGeneration.TokenUsage.CachedInputTokens, lectureStopwatch.ElapsedMilliseconds, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Usage logging must never break pedagogy generation itself.
                }

                // Also update the teaching step's visible text to the professor's spoken lecture class
                if (!string.IsNullOrWhiteSpace(lecture.SpokenLectureScript))
                {
                    teachingStep.Content = lecture.SpokenLectureScript;
                }
            }
            catch
            {
                // Best-effort: lecture generation failure does not abort the entire section pipeline.
            }
        }

        // 2. Synthesize audio via AzureSpeechService and upload to Data Lake
        if (_speechService.IsConfigured && _illustrationStorage.IsConfigured)
        {
            try
            {
                var speechText = lecture?.SpokenLectureScript ?? teachingStep.Content;
                if (!string.IsNullOrWhiteSpace(speechText))
                {
                    // Detect language from text to ensure an English course always uses an English male voice,
                    // and a Spanish course uses a Spanish male voice, even if document metadata is inconsistent.
                    var padded = $" {speechText.ToLowerInvariant()} ";
                    string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
                    string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu "];
                    var isEnglish = englishWords.Count(w => padded.Contains(w)) > spanishWords.Count(w => padded.Contains(w));
                    var isSpanish = spanishWords.Count(w => padded.Contains(w)) > englishWords.Count(w => padded.Contains(w));

                    var voiceName = isEnglish
                        ? "en-US-AndrewNeural"
                        : isSpanish
                        ? "es-MX-JorgeNeural"
                        : (languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "es-MX-JorgeNeural" : "en-US-AndrewNeural");

                    var ttsStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var audioBytes = await _speechService.SynthesizeAsync(speechText, voiceName, cancellationToken);
                    ttsStopwatch.Stop();
                    using var audioStream = new MemoryStream(audioBytes);

                    var storagePath = await _illustrationStorage.UploadLectureAudioAsync(
                        section.TenantId,
                        document.StructuredLearningDocumentId,
                        section.StructuredLearningDocumentSectionId,
                        audioStream,
                        contentType: "audio/mpeg",
                        cancellationToken: cancellationToken);

                    teachingStep.SpeechStoragePath = storagePath;

                    // Synced image+audio Teaching player (2026-10-08): the agent splits the
                    // same lecture into ordered Scenes, each with its own ImagePrompt — when
                    // those prompts are real (not just the single-scene fallback with no
                    // prompt), generate ONE short audio clip + ONE illustration PER SCENE so
                    // the student-facing player can swap images exactly when each clip ends,
                    // with perfect sync by construction. Mirrors
                    // StructuredLearningProfessorLectureService.GenerateLectureForSectionAsync's
                    // own scene block — every Academic/YoungLearner (and Standard) course
                    // generated through THIS pedagogy pipeline now gets the same treatment,
                    // instead of only courses generated through the separate manual
                    // "/professor-lecture" endpoint.
                    // Designer-controlled toggle (2026-10-30, StructuredLearningDocument.
                    // UseScenedLecture) — the scene breakdown above is still requested from
                    // the lecture agent either way (keeps SpokenLectureScript/TTS identical),
                    // but when the designer turned scened illustrations OFF, skip the
                    // per-scene image+audio loop entirely and fall back to the single
                    // Teaching illustration (see below), avoiding up to 6 sequential
                    // image+TTS calls per section.
                    // Never AI-generate (and thereby overwrite) a Teaching illustration for a
                    // section whose real source image was forced via "UploadDirect" (2026-11-06)
                    // — e.g. every video-sourced Engram, which already has a real frame set as
                    // teachingStep.IllustrationId by GenerateIllustrationsAsync above. Without this
                    // guard, the per-scene AI illustration loop below would still run (it only
                    // checked document.UseScenedLecture) and silently replace that real frame with
                    // a generated "PROFE"-style illustration that doesn't match the real screen.
                    var hasRealScenes = document.UseScenedLecture &&
                        illustrationOverride?.Mode != "UploadDirect" && illustrationOverride?.Mode != "EnhanceVideoFrame" &&
                        lecture is not null && (lecture.Scenes.Count > 1 ||
                        (lecture.Scenes.Count == 1 && !string.IsNullOrWhiteSpace(lecture.Scenes[0].ImagePrompt)));
                    if (hasRealScenes && _imageService.IsConfigured)
                    {
                        var styleGuidance = document.PedagogicalAudience == "YoungLearner"
                            ? ProfessorLectureKidsSceneIllustrationStyle
                            : ProfessorLectureStandardSceneIllustrationStyle;
                        // Honor the designer's uploaded reference/logo image (2026-10-26
                        // fix) — this was previously ONLY passed into
                        // GenerateIllustrationsAsync's Hypothesis/DeepDive illustrations,
                        // never into these per-Scene Teaching illustrations, so a
                        // YoungLearner course's "Learn" images (the only ones a kid ever
                        // sees) silently ignored any reference image uploaded via the
                        // "Restart from first engram" modal.
                        var sceneReferenceImageBytes = illustrationOverride?.Mode == "AiGeneratedFromUpload"
                            ? illustrationOverride.UploadedImageBytes
                            : null;
                        var scenes = new List<StructuredLearningLectureSceneDto>();

                        for (var i = 0; i < lecture!.Scenes.Count; i++)
                        {
                            var scene = lecture.Scenes[i];
                            Guid? sceneIllustrationId = null;
                            string? sceneAudioPath = null;

                            try
                            {
                                var sceneAudioBytes = await _speechService.SynthesizeAsync(scene.SceneText, voiceName, cancellationToken);
                                using var sceneAudioStream = new MemoryStream(sceneAudioBytes);
                                sceneAudioPath = await _illustrationStorage.UploadLectureSceneAudioAsync(
                                    section.TenantId,
                                    document.StructuredLearningDocumentId,
                                    section.StructuredLearningDocumentSectionId,
                                    i,
                                    sceneAudioStream,
                                    contentType: "audio/mpeg",
                                    cancellationToken: cancellationToken);
                            }
                            catch
                            {
                                // Best-effort: a missing scene clip falls back to the whole-lecture audio.
                            }

                            if (!string.IsNullOrWhiteSpace(scene.ImagePrompt))
                            {
                                try
                                {
                                    var fullPrompt = $"{styleGuidance} Scene: {scene.ImagePrompt}. No text, no letters, no numbers, no words in the image.";
                                    var generated = await _imageService.GenerateAsync(fullPrompt, sceneReferenceImageBytes, cancellationToken);
                                    using var imageStream = generated.ImageBytes.ToStream();
                                    var imagePath = await _illustrationStorage.UploadIllustrationAsync(
                                        section.TenantId,
                                        document.StructuredLearningDocumentId,
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
                                    dbContext.StructuredLearningIllustrations.Add(sceneIllustrationEntity);
                                    sceneIllustrationId = sceneIllustrationEntity.StructuredLearningIllustrationId;
                                }
                                catch
                                {
                                    // Best-effort: a missing scene image falls back to no image for that scene.
                                }
                            }

                            scenes.Add(new StructuredLearningLectureSceneDto
                            {
                                SceneIndex = i,
                                Text = scene.SceneText,
                                ImagePrompt = scene.ImagePrompt,
                                IllustrationId = sceneIllustrationId,
                                AudioStoragePath = sceneAudioPath
                            });

                            if (i == 0 && sceneIllustrationId is not null)
                            {
                                // Legacy single IllustrationId: prefer Scene 0's so old
                                // consumers that only ever read IllustrationId still show
                                // something sensible.
                                teachingStep.IllustrationId = sceneIllustrationId;
                            }
                        }

                        teachingStep.Scenes = scenes;
                    }

                    // Real Azure Speech TTS cost of narrating the Learn step's
                    // lecture (2026-10-04) — previously never recorded anywhere,
                    // a genuine gap in both the admin "Cursos" cost tab and the
                    // Simulations authoring baseline. Reuses the SAME
                    // StructuredLearningGenerationUsage table with a sentinel
                    // ModelName ("azure-speech-tts") so ComputeCostForModel/
                    // TokenCostEstimator can price it by CHARACTER count (billed
                    // via AgentPricing:AzureSpeechNeuralPerMillionCharactersUsd)
                    // instead of the per-token LLM rate — InputTokens here holds
                    // the character count, not a token count.
                    try
                    {
                        await _usageService.RecordAsync(
                            document.StructuredLearningDocumentId, section.StructuredLearningDocumentSectionId, "Pedagogy",
                            "AzureSpeechService", TokenCostEstimator.AzureSpeechTtsModelSentinel, speechText.Length, 0, 0,
                            ttsStopwatch.ElapsedMilliseconds, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Usage logging must never break pedagogy generation itself.
                    }
                }
            }
            catch
            {
                // Best-effort: audio synthesis failure does not abort the section pipeline.
            }
        }
    }

    private async Task GenerateIllustrationsAsync(
        HumanOsDbContext dbContext,
        Guid tenantId,
        Guid sectionId,
        List<StructuredLearningSectionStepDto> steps,
        SectionIllustrationOverride? illustrationOverride,
        string lessonSourceText,
        string? illustrationGuidance,
        CancellationToken cancellationToken)
    {
        if (!_illustrationStorage.IsConfigured)
        {
            return;
        }

        var mode = illustrationOverride?.Mode ?? "AiGenerated";
        if (mode == "None")
        {
            return;
        }

        if ((mode == "UploadDirect" || mode == "EnhanceVideoFrame") && illustrationOverride?.UploadedImageBytes is { Length: > 0 } uploadedBytes)
        {
            var teachingStep = steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);
            if (teachingStep is null)
            {
                return;
            }

            string? caption = null;
            if (_imageDescriptionAgent.IsConfigured)
            {
                try
                {
                    var described = await _imageDescriptionAgent.DescribeAsync(
                        uploadedBytes, illustrationOverride.UploadedImageContentType ?? "image/png", teachingStep.Content, cancellationToken);
                    caption = described.Description;
                    // 2026-11-11 fix: the Caption column is nvarchar(2000) — occasionally
                    // the description agent returns far more than a short caption (observed:
                    // a full Markdown document dump), which throws a SqlException ("String
                    // or binary data would be truncated") and silently aborts the ENTIRE
                    // illustration save (via the outer best-effort catch below this block),
                    // leaving the OLD image in place even though the reviewer asked for a
                    // new one. Truncate defensively so a verbose caption never blocks saving
                    // the new illustration.
                    if (caption is { Length: > 2000 })
                    {
                        caption = caption[..2000];
                    }
                }
                catch
                {
                    // Best-effort: a missing caption never blocks using the uploaded image.
                }
            }

            // "EnhanceVideoFrame" (2026-11-06): instead of showing the real, often
            // low-resolution/compressed video frame as-is, ask the image model to
            // produce a HIGHER-QUALITY render using that real frame as the base
            // reference (img2img via GenerateFromReferenceAsync) — sharper, cleaner,
            // but depicting the EXACT same real content/composition, never a
            // reinterpreted/generic scene. Falls back to the raw frame bytes
            // (same as "UploadDirect") if enhancement isn't configured or fails,
          // so the real illustration is never lost.
            var finalBytes = uploadedBytes;
            var finalContentType = illustrationOverride.UploadedImageContentType ?? "image/png";
            var imageModelLabel = "user-upload";
            if (mode == "EnhanceVideoFrame" && _imageService.IsConfigured)
            {
                try
                {
                    // 2026-11-11: a reviewer-supplied imageInstruction (e.g. "put a woman in
                    // the image") must still be honored even for video-sourced Engrams —
                    // previously it was silently dropped here because this prompt only ever
                    // described enhancing the real frame's quality, never anything the
                    // reviewer explicitly asked to change about the depicted scene.
                    var enhancementPrompt = VideoFrameEnhancementPrompt
                        + (string.IsNullOrWhiteSpace(caption) ? string.Empty : $"\n\nWhat this frame shows: {caption}")
                        + (string.IsNullOrWhiteSpace(illustrationGuidance) ? string.Empty : $"\n\nREVIEWER-REQUESTED CHANGE TO THIS IMAGE: {illustrationGuidance.Trim()}");
                    var enhanced = await _imageService.GenerateFromReferenceAsync(
                        enhancementPrompt,
                        uploadedBytes, cancellationToken);
                    finalBytes = enhanced.ImageBytes.ToArray();
                    finalContentType = "image/png";
                    imageModelLabel = enhanced.ImageModel;
                }
                catch
                {
                    // Best-effort: keep the raw real frame if enhancement fails.
                }
            }

            try
            {
                using var uploadStream = new MemoryStream(finalBytes);
                var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                    tenantId, capabilityId: sectionId, nodeId: sectionId, 1, uploadStream,
                    contentType: finalContentType, cancellationToken: cancellationToken);

                var illustration = new StructuredLearningIllustration
                {
                    StructuredLearningIllustrationId = Guid.NewGuid(),
                    StructuredLearningDocumentSectionId = sectionId,
                    StoragePath = storagePath,
                    Prompt = mode == "EnhanceVideoFrame" ? VideoFrameEnhancementPrompt : "Imagen proporcionada por el profesor.",
                    Caption = caption,
                    Purpose = StructuredLearningIllustrationPurpose.Teaching,
                    ImageModel = imageModelLabel,
                    Width = 0,
                    Height = 0,
                    CreatedDate = DateTime.UtcNow
                };

                dbContext.StructuredLearningIllustrations.Add(illustration);
                await dbContext.SaveChangesAsync(cancellationToken);
                teachingStep.IllustrationId = illustration.StructuredLearningIllustrationId;
            }
            catch
            {
                // Best-effort: a failed upload never blocks section creation.
            }

            return;
        }

        if (!_imageService.IsConfigured)
        {
            return;
        }

        var useReference = mode == "AiGeneratedFromUpload" && illustrationOverride?.UploadedImageBytes is { Length: > 0 };
        var referenceImageBytes = useReference ? illustrationOverride!.UploadedImageBytes : null;

        var imageIndex = 0;

        foreach (var step in steps)
        {
            if (step.StepType is not (StructuredLearningStepType.Hypothesis or StructuredLearningStepType.Teaching))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(step.IllustrationPrompt))
            {
                continue;
            }

            imageIndex++;
            var purpose = step.StepType == StructuredLearningStepType.Hypothesis
                ? StructuredLearningIllustrationPurpose.Hypothesis
                : StructuredLearningIllustrationPurpose.Teaching;

            step.IllustrationId = await GenerateAndPersistIllustrationAsync(
                dbContext, tenantId, sectionId, imageIndex, step.IllustrationPrompt, caption: null, purpose, referenceImageBytes, cancellationToken);
        }

        var deepDiveStep = steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.DeepDive);
        var cards = deepDiveStep?.ExampleCards;
        if (cards is not { Count: > 0 })
        {
            return;
        }

        foreach (var card in cards)
        {
            // 2026-11-11: the designer model occasionally omits
            // IllustrationPrompt/IllustrationPrompts for a DeepDive card
            // even though its own schema doc says "always non-null" —
            // DeepDive cards are the concrete worked-example someone learns
            // from, so unlike Hypothesis/Teaching (which simply go imageless
            // when the model skips the prompt) a card must never silently
            // end up without an illustration. Synthesize one grounded 100%
            // in the card's own Title+Text (never invented content),
            // matching the same "never leave the section incomplete"
            // philosophy as the zero-ExampleCards fallback above.
            if (string.IsNullOrWhiteSpace(card.IllustrationPrompt) && card.IllustrationPrompts is not { Count: > 0 })
            {
                card.IllustrationPrompt = BuildFallbackCardIllustrationPrompt(card, lessonSourceText);
            }

            if (card.IllustrationPrompts is { Count: > 0 } stepPrompts)
            {
                var ids = new List<Guid>();
                foreach (var stepPrompt in stepPrompts)
                {
                    if (string.IsNullOrWhiteSpace(stepPrompt))
                    {
                        continue;
                    }

                    imageIndex++;
                    var id = await GenerateAndPersistIllustrationAsync(
                        dbContext, tenantId, sectionId, imageIndex, stepPrompt, card.Title, StructuredLearningIllustrationPurpose.DeepDiveCard, referenceImageBytes, cancellationToken);
                    if (id is not null)
                    {
                        ids.Add(id.Value);
                    }
                }

                if (ids.Count > 0)
                {
                    card.IllustrationIds = ids;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(card.IllustrationPrompt))
            {
                continue;
            }

            imageIndex++;
            card.IllustrationId = await GenerateAndPersistIllustrationAsync(
                dbContext, tenantId, sectionId, imageIndex, card.IllustrationPrompt, card.Title, StructuredLearningIllustrationPurpose.DeepDiveCard, referenceImageBytes, cancellationToken);
        }
    }

    /// <summary>Builds a plain illustration prompt straight from a DeepDive
    /// card's own Title+Text when the designer model didn't supply one —
    /// deliberately simple (no invented details) since this only exists to
    /// guarantee every card gets SOME real illustration instead of none.</summary>
    private static string BuildFallbackCardIllustrationPrompt(StructuredLearningExampleCardDto card, string lessonSourceText)
    {
        var text = card.Text.Trim();
        if (text.Length > 900)
        {
            text = text[..900].TrimEnd() + "…";
        }

        var lessonText = (lessonSourceText ?? string.Empty).Trim();
        if (lessonText.Length > 4000)
        {
            lessonText = lessonText[..4000].TrimEnd() + "…";
        }

        // 2026-11-11: a one-line generic prompt ("a simple illustration of
        // this example") was consistently producing images that dropped
        // every concrete concept/step the card's own Text actually talks
        // about. This fallback must carry the SAME source-faithful,
        // concrete-visual-subject decision the main designer prompt uses
        // (see SourceFaithfulContractVersion above): if the text is a
        // process/sequence of steps, draw it as a real flow/process diagram
        // with labeled boxes and arrows in that exact order; if it's an
        // equation/formula, depict the mathematical object itself; if it's
        // a list of distinct, named concepts, draw a labeled
        // hub-and-spoke/relationship diagram connecting exactly those
        // concepts (no invented ones); otherwise depict the concrete
        // scene/object the text describes. Pass BOTH the card's FULL text
        // AND the whole lesson's source text (not just the card) so every
        // concept the LESSON talks about — not just the one worked
        // example — has a chance to actually show up in the image.
        return $"""
            Create an educational illustration for this worked example titled "{card.Title}". Base it ENTIRELY on the card text and the full lesson text below — every concept, step, equation, or named item mentioned must be represented, nothing invented and nothing omitted.

            Decide the visual form from the text's own structure:
            - If it describes a sequence of steps/a process/a workflow: draw a labeled flow diagram (boxes + directional arrows) showing that exact order.
            - If it describes an equation/formula/mathematical object: depict the equation/object itself, clearly labeled.
            - If it lists several distinct named concepts/items: draw a labeled hub-and-spoke or relationship diagram connecting exactly those items (use their own names as labels).
            - If it describes a measurable comparison (before/after, data, categories): draw a simple labeled chart with the real values/categories from the text.
            - Otherwise: depict the concrete scene or object the text is actually about.

            Card text (the primary worked example to depict):
            {text}

            Full lesson text (for context — make sure the image stays consistent with and, where it fits the card's own subject, reflects these concepts too):
            {lessonText}

            Style: clean, polished, textbook-quality educational diagram or scene — no watermark, no logo, minimal decorative filler.
            """;
    }

    private async Task<Guid?> GenerateAndPersistIllustrationAsync(
        HumanOsDbContext dbContext,
        Guid tenantId,
        Guid sectionId,
        int imageIndex,
        string illustrationPrompt,
        string? caption,
        StructuredLearningIllustrationPurpose purpose,
        byte[]? referenceImageBytes,
        CancellationToken cancellationToken)
    {
        try
        {
#pragma warning disable OPENAI001 // GeneratedImageQuality.LowQuality is an experimental SDK member (2.9.1) — explicitly opted into for speed.
            var generated = referenceImageBytes is { Length: > 0 }
                ? await _imageService.GenerateFromReferenceAsync(illustrationPrompt, referenceImageBytes, cancellationToken, quality: OpenAI.Images.GeneratedImageQuality.LowQuality)
                : await _imageService.GenerateAsync(illustrationPrompt, logoImageBytes: null, cancellationToken, quality: OpenAI.Images.GeneratedImageQuality.LowQuality);
#pragma warning restore OPENAI001
            using var stream = generated.ImageBytes.ToStream();
            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                tenantId, capabilityId: sectionId, nodeId: sectionId, imageIndex, stream, cancellationToken: cancellationToken);

            var processVersion = "structured-learning-pedagogy-v1";
            var promptVersion = StructuredLearningSectionDesignerAgent.SourceFaithfulContractVersion;
            var illustration = new StructuredLearningIllustration
            {
                StructuredLearningIllustrationId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = sectionId,
                StoragePath = storagePath,
                Prompt = illustrationPrompt,
                PromptVersion = promptVersion,
                ProcessVersion = processVersion,
                PromptHash = HashString(illustrationPrompt),
                ConfigSnapshotJson = JsonSerializer.Serialize(new { processVersion, promptVersion, purpose, imageModel = generated.ImageModel }),
                Caption = caption,
                Purpose = purpose,
                ImageModel = generated.ImageModel,
                Width = generated.Width,
                Height = generated.Height,
                CreatedDate = DateTime.UtcNow
            };

            dbContext.StructuredLearningIllustrations.Add(illustration);
            await dbContext.SaveChangesAsync(cancellationToken);

            return illustration.StructuredLearningIllustrationId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort — see GenerateIllustrationsAsync's doc comment.
            return null;
        }
    }

    /// <summary>Runs the quality guardrails (structural + content) written
    /// in <see cref="StructuredLearningSectionEvaluator"/> BEFORE this agent
    /// existed. Returns an empty list when everything passes.</summary>
    private static List<string> ValidateGeneratedSteps(
        List<StructuredLearningSectionStepDto> steps,
        StructuredLearningDocument document,
        bool includeDeepDiveStep,
        bool includeHypothesisStep,
        bool includeRecallStep,
        bool includeProductionStep,
        bool includeAssessmentStep,
        string sectionSourceText,
        string capabilityStatement)
    {
        var failures = new List<string>();

        var structural = StructuredLearningSectionEvaluator.ValidateStructure(
            steps.Select(s => (s.StepType, ContentForStructuralCheck(s))).ToList(),
            includeDeepDiveStep,
            includeRecallStep,
            includeProductionStep,
            includeAssessmentStep,
            includeHypothesisStep);
        if (!structural.Passed)
        {
            failures.AddRange(structural.Failures);
        }

        var deepDiveStep = steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.DeepDive);
        failures.AddRange(StructuredLearningSectionEvaluator.ValidateDeepDiveCards(
            includeDeepDiveStep,
            deepDiveStep?.ExampleCards?.Select(c => (c.Title, c.Text)).ToList()));

        // Atomicity (2026-08-17, Paso 4) — catches an engram whose
        // Capability statement bundles more than one teachable idea, which
        // the chapter/engram-extraction splitters should have separated
        // already; failing here triggers the same retry-once path as any
        // other structural failure.
        var atomicity = StructuredLearningSectionEvaluator.CheckCapabilityAtomicity(capabilityStatement);
        if (!atomicity.Passed)
        {
            failures.Add(atomicity.Reason ?? "Capability statement is not atomic.");
        }

        return failures;
    }

    /// <summary>DeepDive uses its own type-specific list instead of Content
    /// — build a representative string from it so the shared "no step is
    /// empty" structural check still applies meaningfully. Recall/Assessment
    /// (2026-08-10) intentionally carry NO design-time content at all
    /// (their real questions are generated live) — see
    /// <see cref="StructuredLearningSectionEvaluator.ValidateStructure"/>,
    /// which skips the empty-content check for those two step types.</summary>
    private static string ContentForStructuralCheck(StructuredLearningSectionStepDto step) => step.StepType switch
    {
        StructuredLearningStepType.DeepDive => string.Join(" ", step.ExampleCards?.Select(c => c.Text) ?? []),
        _ => step.Content
    };

    public static string? BuildStepDataJson(StructuredLearningSectionStepDto step) => step.StepType switch
    {
        StructuredLearningStepType.DeepDive when step.ExampleCards is { Count: > 0 } => JsonSerializer.Serialize(step.ExampleCards),
        StructuredLearningStepType.Hypothesis when step.HypothesisQuestions is { Count: > 0 } => JsonSerializer.Serialize(
            step.HypothesisQuestions.Select(q => new
            {
                question = q.Question,
                options = q.Options,
                correctIndex = q.CorrectIndex ?? 0
            })),
        StructuredLearningStepType.Teaching when step.ProfessorLecture is not null || step.IllustrationId is not null || (step.WebSearchCitations is { Count: > 0 }) =>
            JsonSerializer.Serialize(new
            {
                step.IllustrationId,
                step.SpeechStoragePath,
                ProfessorLecture = step.ProfessorLecture,
                WebSearchCitations = step.WebSearchCitations ?? [],
                Scenes = step.Scenes
            }),
        StructuredLearningStepType.Hypothesis when step.IllustrationId is not null =>
            JsonSerializer.Serialize(new { step.IllustrationId }),
        _ => null
    };
}
