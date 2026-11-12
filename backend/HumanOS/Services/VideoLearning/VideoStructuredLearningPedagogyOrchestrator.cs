using System.Collections.Concurrent;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services.VideoLearning;

/// <summary>
/// Step 3 (pedagogy generation) entry point EXCLUSIVE to video-sourced
/// Structured Learning Documents (2026-11-05). Completely separate from
/// <see cref="StructuredLearningPedagogyOrchestrator"/>/
/// <see cref="StartStructuredLearningPedagogyFunction"/> used by the
/// traditional PDF pipeline — this class does not modify, call into, or
/// share any mutable state with that run store, so the traditional flow's
/// behavior is provably unaffected by this file's existence.
///
/// Why a separate orchestrator instead of reusing the existing one: the
/// traditional flow always lets <see cref="StructuredLearningPedagogyService"/>
/// generate a brand-new AI illustration per section. For video-sourced
/// documents we instead want the Teaching step's illustration to BE the
/// real frame already extracted from the source video (persisted earlier
/// by <see cref="VideoStructuredLearningIngestionService"/> as a
/// <see cref="StructuredLearningIllustrationPurpose.SourceDocument"/>
/// illustration) — never a new AI-generated image. The underlying
/// <see cref="StructuredLearningPedagogyService.RunAsync"/> already supports
/// this exact need via its (unmodified, pre-existing) per-section
/// <see cref="SectionIllustrationOverride"/> "UploadDirect" mode — originally
/// built for Edit mode's "Add section" feature — so this orchestrator simply
/// calls that SAME shared pedagogy engine once per engram, each time scoped
/// (via fromEngramNumber/toEngramNumber) to exactly one section and supplying
/// THAT section's own already-stored real frame bytes as the forced,
/// no-AI-generation illustration. No new agent/model code — pure
/// composition of existing building blocks.
/// </summary>
public sealed class VideoStructuredLearningPedagogyOrchestrator
{
    private readonly StructuredLearningPedagogyService _pedagogy;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly AzureSpeechService _speechService;
    private readonly HumanOS.Agents.Studio.VideoFrameNarrationAgent _frameNarrationAgent;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<Guid, RunSession> _runs = new();

    /// <summary>2026-11-07 fix: this orchestrator used to generate Engrams one at a
    /// time (plain foreach), unlike the PDF pipeline's <see cref="StructuredLearningPedagogyService"/>
    /// which already ran sections in parallel via a semaphore. Restored the same
    /// bounded-parallelism pattern here — each Engram still gets its own
    /// <see cref="IDbContextFactory{TContext}"/>-scoped DbContext (already the case for
    /// every DB access in this class), so running several at once is safe.</summary>
    private const int DefaultMaxConcurrentVideoSections = 3;

    private int MaxConcurrentVideoSections
    {
        get
        {
            var raw = _configuration["MaxConcurrentStructuredLearningSections"];
            return int.TryParse(raw, out var value) && value > 0 ? value : DefaultMaxConcurrentVideoSections;
        }
    }

    private readonly object _activeRunLock = new();
    private Guid? _activeRunId;

    public VideoStructuredLearningPedagogyOrchestrator(
        StructuredLearningPedagogyService pedagogy,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        IConfiguration configuration,
        AzureSpeechService speechService,
        HumanOS.Agents.Studio.VideoFrameNarrationAgent frameNarrationAgent)
    {
        _pedagogy = pedagogy;
        _dbContextFactory = dbContextFactory;
        _frameNarrationAgent = frameNarrationAgent;
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
        _speechService = speechService;
        _configuration = configuration;
    }

    public bool IsConfigured => _pedagogy.IsConfigured;

    public sealed class ActiveRunConflictException(Guid activeRunId) : Exception(
        $"Ya hay una generación de pedagogía de video en curso (RunId: {activeRunId}). Espera a que termine antes de iniciar otra.")
    {
        public Guid ActiveRunId { get; } = activeRunId;
    }

    private void ClaimActiveRunSlotOrThrow(Guid runId)
    {
        lock (_activeRunLock)
        {
            if (_activeRunId is Guid existingRunId &&
                _runs.TryGetValue(existingRunId, out var existingSession) &&
                existingSession.LatestStatus.Stage == StructuredLearningPedagogyStage.Running)
            {
                throw new ActiveRunConflictException(existingRunId);
            }

            _activeRunId = runId;
        }
    }

    private void ReleaseActiveRunSlot(Guid runId)
    {
        lock (_activeRunLock)
        {
            if (_activeRunId == runId)
            {
                _activeRunId = null;
            }
        }
    }

    public StructuredLearningPedagogyRunStatus GetStatus(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var session))
        {
            throw new InvalidOperationException($"No video-pedagogy run found with id '{runId}'.");
        }

        return session.LatestStatus;
    }

    /// <summary>Returns the currently in-progress video-pedagogy run for the
    /// given document, if any — same recovery pattern as
    /// StructuredLearningPedagogyOrchestrator.GetActiveRun, so the run
    /// progress page can find its way back to an already-started run even
    /// after the browser reloaded/navigated away and lost the in-memory
    /// RunId (2026-11-06).</summary>
    public StructuredLearningPedagogyRunStatus? GetActiveRun(Guid documentId)
    {
        lock (_activeRunLock)
        {
            if (_activeRunId is Guid runId &&
                _runs.TryGetValue(runId, out var session) &&
                session.LatestStatus.Stage == StructuredLearningPedagogyStage.Running &&
                session.LatestStatus.DocumentId == documentId)
            {
                return session.LatestStatus;
            }

            return null;
        }
    }

    /// <summary>Starts (in the background) generating the pedagogical
    /// sequence for every real-frame-backed section of a video-sourced
    /// document, one engram at a time, each forced to reuse its own
    /// already-extracted frame instead of generating a new illustration.</summary>
    public StructuredLearningPedagogyRunStatus Start(
        Guid documentId,
        string languageCode,
        bool forceRegenerateAll = false,
        bool? usePerplexitySearch = null,
        int? fromEngramNumber = null,
        int? toEngramNumber = null)
    {
        var runId = Guid.NewGuid();
        ClaimActiveRunSlotOrThrow(runId);

        var session = new RunSession();
        _runs[runId] = session;
        session.UpdateStatus(StructuredLearningPedagogyRunStatus.Running(
            runId, documentId, null, "Starting", null, 0, null, fromEngramNumber, toEngramNumber));

        session.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                var result = await RunAsync(
                    runId, documentId, languageCode, session, forceRegenerateAll, usePerplexitySearch,
                    fromEngramNumber, toEngramNumber, CancellationToken.None);
                session.UpdateStatus(StructuredLearningPedagogyRunStatus.Completed(runId, documentId, result));
            }
            catch (Exception ex)
            {
                session.UpdateStatus(StructuredLearningPedagogyRunStatus.Failed(runId, documentId, ex.Message));
            }
            finally
            {
                ReleaseActiveRunSlot(runId);
            }
        }, CancellationToken.None);

        return session.LatestStatus;
    }

    private async Task<StructuredLearningPedagogyResult> RunAsync(
        Guid runId,
        Guid documentId,
        string languageCode,
        RunSession session,
        bool forceRegenerateAll,
        bool? usePerplexitySearch,
        int? fromEngramNumber,
        int? toEngramNumber,
        CancellationToken cancellationToken)
    {
        string? documentName;
        List<SectionWithSourceImage> orderedSections;

        await using (var readContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            documentName = await readContext.StructuredLearningDocuments
                .Where(d => d.StructuredLearningDocumentId == documentId)
                .Select(d => d.DocumentName)
                .FirstOrDefaultAsync(cancellationToken);

            var sections = await readContext.StructuredLearningDocumentSections
                .Where(s => s.StructuredLearningDocumentId == documentId)
                .OrderBy(s => s.SortOrder)
                .Select(s => new { s.StructuredLearningDocumentSectionId, s.TenantId, s.SubchapterTitle, s.PedagogyStatus, s.Text })
                .ToListAsync(cancellationToken);

            // Ordered by CreatedDate == the order frames were uploaded in,
            // which VideoStructuredLearningIngestionService always does in
            // time order (TimestampSeconds ascending) — so this list is
            // already chronological, no extra sort key needed.
            var sourceFrames = await readContext.StructuredLearningIllustrations
                .Where(i => sections.Select(s => s.StructuredLearningDocumentSectionId).Contains(i.StructuredLearningDocumentSectionId)
                    && i.Purpose == StructuredLearningIllustrationPurpose.SourceDocument
                    && !i.IsExcludedFromGeneration)
                .OrderBy(i => i.CreatedDate)
                .Select(i => new
                {
                    i.StructuredLearningDocumentSectionId,
                    i.StructuredLearningIllustrationId,
                    i.StoragePath,
                    i.Caption
                })
                .ToListAsync(cancellationToken);

            var sourceFramesBySection = sourceFrames
                .GroupBy(i => i.StructuredLearningDocumentSectionId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<SourceFrame>)g
                        .Select(i => new SourceFrame(i.StructuredLearningIllustrationId, i.StoragePath, i.Caption))
                        .ToList());

            orderedSections = sections
                .Select((s, index) => new SectionWithSourceImage(
                    s.StructuredLearningDocumentSectionId,
                    s.TenantId,
                    s.SubchapterTitle,
                    s.PedagogyStatus,
                    Position: index + 1,
                    sourceFramesBySection.GetValueOrDefault(s.StructuredLearningDocumentSectionId, Array.Empty<SourceFrame>()),
                    s.Text))
                .ToList();
        }

        // Video-only, idempotent toggle (does NOT touch any PDF-sourced document —
        // this orchestrator is never invoked for those): the shared pedagogy
        // service's per-scene lecture illustrations (document.UseScenedLecture,
        // defaults to true) are generated with their OWN AI image call per scene,
        // completely bypassing the per-section SectionIllustrationOverride
        // "UploadDirect" mechanism used below. Forcing it off here makes the
        // shared service fall back to the single Teaching illustration
        // (teachingStep.IllustrationId), which DOES honor "UploadDirect" — so the
        // real extracted video frame actually ends up as the Teaching image
        // instead of a brand-new AI-generated whiteboard illustration.
        await using (var toggleContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await toggleContext.StructuredLearningDocuments
                .Where(d => d.StructuredLearningDocumentId == documentId && d.UseScenedLecture)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.UseScenedLecture, false), cancellationToken);
        }

        var inRange = orderedSections.Where(s =>
            (fromEngramNumber is not > 0 || s.Position >= fromEngramNumber) &&
            (toEngramNumber is not > 0 || s.Position <= toEngramNumber));

        var targetSections = (forceRegenerateAll ? inRange : inRange.Where(s => s.PedagogyStatus != StructuredLearningPedagogyStatus.Completed))
            .ToList();

        var totalSections = targetSections.Count;
        var completedCount = 0;
        var failedCount = 0;
        var totalInputTokens = 0;
        var totalOutputTokens = 0;

        session.UpdateStatus(StructuredLearningPedagogyRunStatus.Running(
            runId, documentId, documentName, $"Generating pedagogy for {totalSections} pending video engrams",
            totalSections, 0, [], fromEngramNumber, toEngramNumber));

        var activeTitles = new List<string>();
        var progressLock = new object();

        using var semaphore = new SemaphoreSlim(MaxConcurrentVideoSections);
        var tasks = targetSections.Select(async section =>
        {
            await semaphore.WaitAsync(cancellationToken);
            lock (progressLock)
            {
                activeTitles.Add(section.SubchapterTitle);
                session.UpdateStatus(StructuredLearningPedagogyRunStatus.Running(
                    runId, documentId, documentName, $"Generating: {section.SubchapterTitle}",
                    totalSections, completedCount + failedCount, [.. activeTitles], fromEngramNumber, toEngramNumber));
            }

            try
            {
                SectionIllustrationOverride? illustrationOverride = null;
                var firstFrame = section.SourceFrames.Count > 0 ? section.SourceFrames[0] : null;
                if (firstFrame is not null)
                {
                    try
                    {
                        var bytes = await _illustrationStorage.DownloadIllustrationBytesAsync(firstFrame.StoragePath, cancellationToken);
                        illustrationOverride = new SectionIllustrationOverride
                        {
                            SectionId = section.SectionId,
                            // "EnhanceVideoFrame" (2026-11-06): generates a higher-quality render of this
                            // EXACT real frame (img2img), instead of showing the raw often-compressed
                            // frame as-is or (the old "UploadDirect" bug) letting the unrelated per-scene
                            // AI illustration loop silently replace it with a generic scene.
                            Mode = "EnhanceVideoFrame",
                            UploadedImageBytes = bytes.ToArray(),
                            UploadedImageContentType = "image/jpeg",
                        };
                    }
                    catch
                    {
                        // Best-effort: if the real frame can't be downloaded for some
                        // reason, fall through with illustrationOverride = null — the
                        // section still gets its pedagogical text, just without a
                        // forced real-frame illustration (same as a PDF section would).
                    }
                }

                StructuredLearningPedagogyResult sectionResult;
                try
                {
                    sectionResult = await _pedagogy.RunAsync(
                        documentId,
                        languageCode,
                        reportProgress: (_, _, _, _) => { },
                        cancellationToken,
                        forceRegenerateAll: true,
                        uploadedImageBytes: null,
                        uploadedImageContentType: null,
                        referenceImageUsageInstruction: null,
                        illustrationOverride: illustrationOverride,
                        usePerplexitySearch: usePerplexitySearch,
                        fromEngramNumber: section.Position,
                        toEngramNumber: section.Position);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    lock (progressLock)
                    {
                        failedCount++;
                    }
                    return;
                }

                lock (progressLock)
                {
                    completedCount += sectionResult.SectionsCompleted;
                    failedCount += sectionResult.SectionsFailed;
                    totalInputTokens += sectionResult.TotalInputTokens;
                    totalOutputTokens += sectionResult.TotalOutputTokens;
                }

                // Multi-frame Engram (2026-11-xx): the shared pedagogy service
                // only ever applies ONE forced illustration (the first real
                // frame above). When this Engram actually has several real
                // frames, build the Teaching step's Scenes list directly from
                // ALL of them — reusing each frame's own already-uploaded
                // illustration (never a new AI image) paired with its own
                // synced transcript slice (Caption) — so the student player
                // shows every real picture in sequence instead of just the
                // first one.
                if (sectionResult.SectionsFailed == 0 && section.SourceFrames.Count > 1)
                {
                    try
                    {
                        await ApplyMultiFrameScenesAsync(section, languageCode, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // Best-effort: the section already has its single
                        // Teaching illustration from the override above even if
                        // this enrichment step fails — never let it fail the run.
                        // Logged (2026-11-06) so a silent failure here (which
                        // leaves StepDataJson.Scenes stale/unrefreshed even
                        // though Content was updated) is actually diagnosable.
                        DebugLog($"ApplyMultiFrameScenesAsync failed for section {section.SectionId}: {ex}");
                    }
                }
            }
            finally
            {
                lock (progressLock)
                {
                    activeTitles.Remove(section.SubchapterTitle);
                    session.UpdateStatus(StructuredLearningPedagogyRunStatus.Running(
                        runId, documentId, documentName, $"Progress update",
                        totalSections, completedCount + failedCount, [.. activeTitles], fromEngramNumber, toEngramNumber));
                }
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
            TotalOutputTokens = totalOutputTokens,
        };
    }

    // Lightweight error-only diagnostic log (2026-11-06): Console.Error
    // output from the isolated worker process isn't visible in the func
    // host console, so failures here are written to a file instead so a
    // per-frame alignment error (which otherwise silently falls back to
    // the frame's raw caption) is never invisible again.
    private static readonly object DebugLogLock = new();
    private static void DebugLog(string message)
    {
        try
        {
            lock (DebugLogLock)
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "video-pedagogy-debug.log"),
                    $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // best-effort diagnostic only
        }
    }

    /// <summary>Replaces the Teaching step's Scenes with one scene per real
    /// video frame already stored for this section, each paired with its
    /// own synced transcript slice and (best-effort) its own short TTS
    /// clip — all built from data already persisted by
    /// <see cref="VideoStructuredLearningIngestionService"/>, no AI image
    /// generation involved.</summary>
    private async Task ApplyMultiFrameScenesAsync(SectionWithSourceImage section, string languageCode, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var teachingStepRow = await dbContext.StructuredLearningSectionSteps
            .Where(s => s.StructuredLearningDocumentSectionId == section.SectionId
                && s.StepType == StructuredLearningStepType.Teaching)
            .OrderByDescending(s => s.CreatedDate)
            .Select(s => new { s.Content, s.StepDataJson })
            .FirstOrDefaultAsync(cancellationToken);

        if (teachingStepRow?.StepDataJson is null)
        {
            return;
        }

        ExistingTeachingStepData? existing;
        try
        {
            existing = JsonSerializer.Deserialize<ExistingTeachingStepData>(teachingStepRow.StepDataJson);
        }
        catch (JsonException)
        {
            return;
        }

        // Preferred path (2026-11-05): explicitly ask a vision-grounded LLM
        // call, made ONCE PER FRAME IN ORDER (2026-11-06 sequential redesign):
        // each call is given the full approved script, every PRIOR frame's
        // own already-decided narration (for continuity/duplicate
        // detection), and ONLY the current frame's image — so the model can
        // either write a chunk genuinely grounded in THIS exact image, or
        // SKIP the frame entirely (never fabricating a caption) when it is
        // a near-duplicate of the previous frame or shows only a person
        // with no informative visual content. Skipped frames produce no
        // Scene at all (never shown, never deleted from storage). Falls
        // back to the old regrouping/sentence-split chain (keeping EVERY
        // frame, no skipping) only when the agent isn't configured or the
        // approved script is missing.
        var scenes = new List<StructuredLearningLectureSceneDto>(section.SourceFrames.Count);
        // 2026-11-07 fix: use the RAW ORIGINAL transcript (section.SectionText)
        // as the source of truth for alignment, never the Teaching step's
        // already-translated/already-summarized Content — using Content here
        // silently compounded the Designer's own summarization loss with a
        // second truncation (splitting an already-short translated paragraph
        // across every frame), which is how most of the original lecture
        // ended up missing from the final Scenes.
        var canUseSequentialAlignment = _frameNarrationAgent.IsConfigured && !string.IsNullOrWhiteSpace(section.SectionText);

        if (canUseSequentialAlignment)
        {
            var priorFrameTexts = new List<string>(section.SourceFrames.Count);
            var nextSceneIndex = 0;
            var totalFrames = section.SourceFrames.Count;

            for (var frameIndex = 0; frameIndex < section.SourceFrames.Count; frameIndex++)
            {
                var frame = section.SourceFrames[frameIndex];
                string? sceneText;
                try
                {
                    var rawBytes = await _illustrationStorage.DownloadIllustrationBytesAsync(frame.StoragePath, cancellationToken);
                    var decision = await _frameNarrationAgent.AlignFrameToScriptAsync(
                        section.SectionText!, priorFrameTexts, rawBytes.ToArray(), "image/jpeg", frame.SceneText, languageCode, cancellationToken,
                        frameIndex, totalFrames);

                    if (decision.Skip || string.IsNullOrWhiteSpace(decision.Text))
                    {
                        // Skipped (duplicate of previous frame, or a person
                        // with no informative content) — no Scene is added
                        // for this frame; it simply never appears, per the
                        // 2026-11-06 design decision. Does not count toward
                        // priorFrameTexts since there's no new narration.
                        continue;
                    }

                    sceneText = decision.Text;
                }
                catch (Exception ex)
                {
                    // Best-effort: this single frame's sequential alignment
                    // failed (download/model error) — fall back to its own
                    // raw synced transcript slice rather than losing it.
                    // Logged (2026-11-06) to diagnose silent fallback.
                    DebugLog($"AlignFrameToScriptAsync failed for frame seq={frame.SceneText?.Substring(0, Math.Min(30, frame.SceneText.Length))}: {ex}");
                    sceneText = frame.SceneText;
                }

                if (string.IsNullOrWhiteSpace(sceneText))
                {
                    continue;
                }

                priorFrameTexts.Add(sceneText);

                var illustrationId = await EnhanceFrameForSceneAsync(section, frame, nextSceneIndex, sceneText, cancellationToken);
                var audioStoragePath = await SynthesizeSceneAudioAsync(section, languageCode, nextSceneIndex, sceneText, cancellationToken);

                scenes.Add(new StructuredLearningLectureSceneDto
                {
                    SceneIndex = nextSceneIndex,
                    Text = sceneText,
                    IllustrationId = illustrationId,
                    AudioStoragePath = audioStoragePath,
                });
                nextSceneIndex++;
            }
        }
        else
        {
            // Fallback (agent not configured / no approved script): narrate
            // the ALREADY GENERATED, already-approved, LLM-expanded lecture —
            // using the SAME per-beat "Scenes" breakdown the Professor
            // Lecture Agent itself already produced (see
            // StructuredLearningProfessorLectureAgent's rule 8), regrouped to
            // match the frame count. No skipping in this fallback — every
            // frame keeps its own Scene, same as before the 2026-11-06
            // sequential redesign.
            var lectureScenes = existing?.ProfessorLecture?.Scenes;
            var sourceTexts = lectureScenes is { Count: > 0 }
                ? lectureScenes.Select(s => s.SceneText).Where(t => !string.IsNullOrWhiteSpace(t)).ToList()
                : null;
            var sceneTexts = sourceTexts is { Count: > 0 }
                ? RegroupTextsAcrossFrames(sourceTexts, section.SourceFrames.Count)
                : DistributeContentAcrossFrames(teachingStepRow.Content, section.SourceFrames.Count);

            for (var i = 0; i < section.SourceFrames.Count; i++)
            {
                var frame = section.SourceFrames[i];
                var sceneText = sceneTexts[i];

                var illustrationId = await EnhanceFrameForSceneAsync(section, frame, i, sceneText, cancellationToken);
                var audioStoragePath = await SynthesizeSceneAudioAsync(section, languageCode, i, sceneText, cancellationToken);

                scenes.Add(new StructuredLearningLectureSceneDto
                {
                    SceneIndex = i,
                    Text = sceneText,
                    IllustrationId = illustrationId,
                    AudioStoragePath = audioStoragePath,
                });
            }
        }

        var stepDto = new StructuredLearningSectionStepDto
        {
            StepType = StructuredLearningStepType.Teaching,
            IllustrationId = existing?.IllustrationId,
            SpeechStoragePath = existing?.SpeechStoragePath,
            ProfessorLecture = existing?.ProfessorLecture,
            WebSearchCitations = existing?.WebSearchCitations,
            Scenes = scenes,
        };

        var newJson = StructuredLearningPedagogyService.BuildStepDataJson(stepDto);

        await dbContext.StructuredLearningSectionSteps
            .Where(s => s.StructuredLearningDocumentSectionId == section.SectionId
                && s.StepType == StructuredLearningStepType.Teaching)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StepDataJson, newJson), cancellationToken);
    }

    /// <summary>Regroups an ordered list of already-rich, already-written
    /// text beats (here: the Professor Lecture Agent's own per-scene
    /// SceneText segments — each a complete, substantial, grounded
    /// narration unit, never a 1-sentence fragment) into exactly
    /// <paramref name="frameCount"/> contiguous, in-order chunks, one per
    /// real video frame. When there are more source beats than frames,
    /// adjacent beats are merged (joined with a space); when there are
    /// fewer, trailing frames reuse the previous frame's chunk instead of
    /// going silent. Never drops, reorders, or rewrites any beat's text.</summary>
    private static List<string> RegroupTextsAcrossFrames(IReadOnlyList<string> texts, int frameCount)
    {
        var result = new List<string>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            var startIndex = (int)((long)i * texts.Count / frameCount);
            var endIndex = (int)((long)(i + 1) * texts.Count / frameCount);

            if (endIndex <= startIndex)
            {
                result.Add(result.Count > 0 ? result[^1] : texts[0]);
            }
            else
            {
                result.Add(string.Join(" ", texts.Skip(startIndex).Take(endIndex - startIndex)));
            }
        }

        return result;
    }

    /// <summary>Fallback only (when no ProfessorLecture.Scenes were
    /// persisted at all): splits the Teaching step's already-generated
    /// lecture content into <paramref name="frameCount"/> contiguous,
    /// in-order SENTENCE groups — one per real frame — so every scene's audio
    /// narrates a real slice of the SAME approved text, never a literal/raw
    /// transcript fragment and never the exact same text repeated. Splits
    /// on sentence boundaries (same heuristic the frontend itself already
    /// uses to break this text into paragraphs for display — Content is
    /// stored as one continuous string, NOT newline-separated) rather than
    /// on '\n', since a naive newline split collapses to a single "paragraph"
    /// and was making every scene narrate the identical whole lecture.
    /// When there are fewer sentences than frames, the trailing frames
    /// reuse the last non-empty chunk instead of getting silence.</summary>
    private static List<string> DistributeContentAcrossFrames(string? content, int frameCount)
    {
        var sentences = System.Text.RegularExpressions.Regex
            .Matches(content ?? string.Empty, @"[^.!?]+[.!?]+(?=\s|$)")
            .Select(m => m.Value.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        // Fallback for content with no sentence-ending punctuation at all
        // (rare, but still real text) — treat the whole thing as one chunk
        // instead of discarding it.
        if (sentences.Count == 0 && !string.IsNullOrWhiteSpace(content))
        {
            sentences.Add(content.Trim());
        }

        var result = new List<string>(frameCount);
        if (sentences.Count == 0)
        {
            for (var i = 0; i < frameCount; i++)
            {
                result.Add(string.Empty);
            }

            return result;
        }

        for (var i = 0; i < frameCount; i++)
        {
            var startIndex = (int)((long)i * sentences.Count / frameCount);
            var endIndex = (int)((long)(i + 1) * sentences.Count / frameCount);

            if (endIndex <= startIndex)
            {
                // More frames than sentences — this frame's own narration
                // chunk would be empty, so it reuses the previous frame's
                // (real, already-assigned) text instead of going silent.
                result.Add(result.Count > 0 ? result[^1] : sentences[0]);
            }
            else
            {
                result.Add(string.Join(" ", sentences.Skip(startIndex).Take(endIndex - startIndex)));
            }
        }

        return result;
    }

    /// <summary>Enhances (img2img) ONE real frame's already-uploaded
    /// illustration using its own scene text as prompt grounding — shared
    /// helper between the sequential (vision-aligned) and fallback
    /// (regrouped-text) Scene-building paths so neither duplicates this
    /// logic. Best-effort: returns the frame's raw, unenhanced illustration
    /// Id on any failure.</summary>
    private async Task<Guid> EnhanceFrameForSceneAsync(
        SectionWithSourceImage section, SourceFrame frame, int sceneIndex, string sceneText, CancellationToken cancellationToken)
    {
        try
        {
            var rawBytes = await _illustrationStorage.DownloadIllustrationBytesAsync(frame.StoragePath, cancellationToken);
            var enhancedId = await _pedagogy.EnhanceVideoFrameForSceneAsync(
                section.TenantId, section.SectionId, sceneIndex, rawBytes.ToArray(), "image/jpeg", sceneText, cancellationToken);
            return enhancedId ?? frame.IllustrationId;
        }
        catch
        {
            // Best-effort: keep the raw, unenhanced frame illustration.
            return frame.IllustrationId;
        }
    }

    /// <summary>Synthesizes ONE Scene's own short TTS audio clip — shared
    /// helper between the sequential and fallback Scene-building paths.
    /// Best-effort: returns null on any failure (the player falls back to
    /// the whole-lecture audio for pacing).</summary>
    private async Task<string?> SynthesizeSceneAudioAsync(
        SectionWithSourceImage section, string languageCode, int sceneIndex, string sceneText, CancellationToken cancellationToken)
    {
        if (!_speechService.IsConfigured || !_illustrationStorage.IsConfigured || string.IsNullOrWhiteSpace(sceneText))
        {
            return null;
        }

        try
        {
            var voiceName = languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                ? "es-MX-JorgeNeural"
                : "en-US-AndrewNeural";
            var audioBytes = await _speechService.SynthesizeAsync(sceneText, voiceName, cancellationToken);
            using var audioStream = new MemoryStream(audioBytes);
            return await _illustrationStorage.UploadLectureSceneAudioAsync(
                section.TenantId,
                section.SectionId,
                section.SectionId,
                sceneIndex: sceneIndex,
                audioStream,
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Best-effort — a scene without its own audio clip still shows
            // its real image and transcript text.
            return null;
        }
    }

    private sealed record SourceFrame(Guid IllustrationId, string StoragePath, string? SceneText);

    private sealed record ExistingTeachingStepData
    {
        public Guid? IllustrationId { get; init; }
        public string? SpeechStoragePath { get; init; }
        public HumanOS.Contracts.Studio.StructuredLearningProfessorLectureResult? ProfessorLecture { get; init; }
        public List<string>? WebSearchCitations { get; init; }
    }

    private sealed record SectionWithSourceImage(
        Guid SectionId,
        Guid TenantId,
        string SubchapterTitle,
        StructuredLearningPedagogyStatus PedagogyStatus,
        int Position,
        IReadOnlyList<SourceFrame> SourceFrames,
        string? SectionText);

    private sealed class RunSession
    {
        private readonly object _lock = new();
        private StructuredLearningPedagogyRunStatus _latestStatus = null!;

        public Task? BackgroundTask { get; set; }

        public StructuredLearningPedagogyRunStatus LatestStatus
        {
            get { lock (_lock) { return _latestStatus; } }
        }

        public void UpdateStatus(StructuredLearningPedagogyRunStatus status)
        {
            lock (_lock) { _latestStatus = status; }
        }
    }
}
