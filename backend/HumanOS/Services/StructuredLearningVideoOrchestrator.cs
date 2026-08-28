using System.Collections.Concurrent;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>Status of one in-flight/completed Veo video generation run —
/// same in-memory start/poll shape as StructuredLearningPedagogyOrchestrator,
/// its own separate run store since video jobs are independent of pedagogy
/// runs and can take several minutes each.</summary>
public sealed class StructuredLearningVideoRunStatus
{
    public Guid RunId { get; init; }
    public string Stage { get; init; } = "Running";
    public string? ErrorMessage { get; init; }
    public Guid? IllustrationId { get; init; }

    public static StructuredLearningVideoRunStatus Running(Guid runId) =>
        new() { RunId = runId, Stage = "Running" };

    public static StructuredLearningVideoRunStatus Completed(Guid runId, Guid illustrationId) =>
        new() { RunId = runId, Stage = "Completed", IllustrationId = illustrationId };

    public static StructuredLearningVideoRunStatus Failed(Guid runId, string errorMessage) =>
        new() { RunId = runId, Stage = "Failed", ErrorMessage = errorMessage };
}

/// <summary>
/// Owns Google Veo video generation for Structured Learning "Learn"
/// sections (2026-08-21) — one 8-second video per Hypothesis/Teaching/
/// Production step or DeepDive card, hard-capped at
/// <see cref="MaxVideosPerSection"/> per section/chapter (a real cost:
/// each Veo call is billed, so this is a deliberate spend guard, not just a
/// UX limit). Same async start/poll pattern as
/// <see cref="StructuredLearningPedagogyOrchestrator"/>.
/// </summary>
public sealed class StructuredLearningVideoOrchestrator
{
    public const int MaxVideosPerSection = 10;

    private readonly GoogleVeoVideoService _veoService;
    private readonly GoogleOmniFlashVideoService _omniService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly ConcurrentDictionary<Guid, RunSession> _runs = new();

    public StructuredLearningVideoOrchestrator(
        GoogleVeoVideoService veoService,
        GoogleOmniFlashVideoService omniService,
        IConfiguration configuration,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _veoService = veoService;
        _omniService = omniService;
        // Same rationale as StructuredLearningPedagogyService: this Singleton
        // cannot directly inject the Scoped CapabilityGraphIllustrationStorageService,
        // so it's constructed directly here (it only needs IConfiguration).
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
        _dbContextFactory = dbContextFactory;
    }

    public bool IsConfigured => _veoService.IsConfigured && _illustrationStorage.IsConfigured;

    /// <summary>Starts a video generation job for one section. Throws
    /// InvalidOperationException immediately (no background job started)
    /// if the section already has <see cref="MaxVideosPerSection"/> videos —
    /// callers should surface this synchronously as a 400, not as a failed
    /// async run.</summary>
    public async Task<StructuredLearningVideoRunStatus> StartAsync(
        Guid sectionId, string prompt, StructuredLearningIllustrationPurpose purpose, string videoModel = "veo",
        byte[]? referenceImageBytes = null, string? referenceImageContentType = null, CancellationToken cancellationToken = default)
    {
        await using (var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var existingVideoCount = await dbContext.StructuredLearningIllustrations
                .Where(i => i.StructuredLearningDocumentSectionId == sectionId && i.MediaType == "Video")
                .CountAsync(cancellationToken);

            if (existingVideoCount >= MaxVideosPerSection)
            {
                throw new InvalidOperationException(
                    $"This section already has {existingVideoCount} video(s) — the maximum allowed per Learn section/chapter is {MaxVideosPerSection}.");
            }

            var section = await dbContext.StructuredLearningDocumentSections
                .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
                ?? throw new InvalidOperationException($"StructuredLearningDocumentSection {sectionId} not found.");

            var runId = Guid.NewGuid();
            var session = new RunSession();
            _runs[runId] = session;
            session.LatestStatus = StructuredLearningVideoRunStatus.Running(runId);

            session.BackgroundTask = Task.Run(async () => await RunVideoJobAsync(runId, session, sectionId, section.TenantId, prompt, purpose, videoModel, referenceImageBytes, referenceImageContentType), CancellationToken.None);

            return session.LatestStatus;
        }
    }

    private async Task RunVideoJobAsync(
        Guid runId, RunSession session, Guid sectionId, Guid tenantId, string prompt, StructuredLearningIllustrationPurpose purpose,
        string videoModel, byte[]? referenceImageBytes, string? referenceImageContentType)
    {
        try
        {
            byte[]? videoBytes;
            string imageModelName;

            if (videoModel == "omni")
            {
                var omniResult = await _omniService.GenerateAsync(prompt, referenceImageBytes, referenceImageContentType);
                if (omniResult.ErrorMessage is not null || omniResult.VideoBytes is null)
                {
                    session.LatestStatus = StructuredLearningVideoRunStatus.Failed(runId, omniResult.ErrorMessage ?? "Omni Flash returned no video bytes.");
                    return;
                }
                videoBytes = omniResult.VideoBytes;
                imageModelName = "gemini-omni-flash-preview";
            }
            else
            {
                var operationName = await _veoService.StartGenerationAsync(prompt, referenceImageBytes, referenceImageContentType);

                GoogleVeoVideoService.PollResult result;
                var deadline = DateTime.UtcNow.AddMinutes(8);
                do
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    result = await _veoService.PollAsync(operationName);
                }
                while (!result.Done && DateTime.UtcNow < deadline);

                if (!result.Done)
                {
                    session.LatestStatus = StructuredLearningVideoRunStatus.Failed(runId, "Timed out waiting for Veo video generation (over 8 minutes).");
                    return;
                }

                if (result.ErrorMessage is not null || result.VideoBytes is null)
                {
                    session.LatestStatus = StructuredLearningVideoRunStatus.Failed(runId, result.ErrorMessage ?? "Veo returned no video bytes.");
                    return;
                }

                videoBytes = result.VideoBytes;
                imageModelName = "veo-3.1-fast-generate-preview";
            }

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Path uses this illustration's OWN new Guid (2026-08-22 fix) —
            // NOT a COUNT-based index, which raced: two videos generated
            // around the same time (Veo takes minutes) both computed the
            // same count before either row was inserted, producing the
            // SAME blob path and silently overwriting one video's bytes
            // with the other's.
            var illustrationId = Guid.NewGuid();
            using var stream = new MemoryStream(videoBytes);
            var storagePath = await _illustrationStorage.UploadVideoAsync(tenantId, sectionId, sectionId, illustrationId, stream);

            var illustration = new StructuredLearningIllustration
            {
                StructuredLearningIllustrationId = illustrationId,
                StructuredLearningDocumentSectionId = sectionId,
                StoragePath = storagePath,
                Prompt = prompt,
                Purpose = purpose,
                ImageModel = imageModelName,
                MediaType = "Video",
                Width = 0,
                Height = 0,
                CreatedDate = DateTime.UtcNow
            };

            dbContext.StructuredLearningIllustrations.Add(illustration);
            await dbContext.SaveChangesAsync();

            session.LatestStatus = StructuredLearningVideoRunStatus.Completed(runId, illustration.StructuredLearningIllustrationId);
        }
        catch (Exception ex)
        {
            session.LatestStatus = StructuredLearningVideoRunStatus.Failed(runId, ex.Message);
        }
    }

    public StructuredLearningVideoRunStatus GetStatus(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var session))
        {
            throw new InvalidOperationException($"No structured-learning-video run found with id '{runId}'.");
        }

        return session.LatestStatus;
    }

    /// <summary>Merges 2+ already-generated videos (in the caller-supplied
    /// order) into ONE new video via ffmpeg concat (2026-08-22) — a local,
    /// unbilled operation (no Veo/Omni API call), so it runs synchronously
    /// rather than through the async start/poll run store. The merged
    /// result counts toward <see cref="MaxVideosPerSection"/> like any
    /// other video; the source clips are left untouched (the reviewer can
    /// delete them separately if they only want the merged one).</summary>
    public async Task<Guid> MergeAsync(Guid sectionId, IReadOnlyList<Guid> orderedIllustrationIds, CancellationToken cancellationToken = default)
    {
        if (orderedIllustrationIds.Count < 2)
        {
            throw new InvalidOperationException("Select at least 2 videos to merge.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingVideoCount = await dbContext.StructuredLearningIllustrations
            .Where(i => i.StructuredLearningDocumentSectionId == sectionId && i.MediaType == "Video")
            .CountAsync(cancellationToken);
        if (existingVideoCount >= MaxVideosPerSection)
        {
            throw new InvalidOperationException(
                $"This section already has {existingVideoCount} video(s) — the maximum allowed per Learn section/chapter is {MaxVideosPerSection}.");
        }

        var section = await dbContext.StructuredLearningDocumentSections
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new InvalidOperationException($"StructuredLearningDocumentSection {sectionId} not found.");

        var illustrationsById = await dbContext.StructuredLearningIllustrations
            .Where(i => i.StructuredLearningDocumentSectionId == sectionId && i.MediaType == "Video" && orderedIllustrationIds.Contains(i.StructuredLearningIllustrationId))
            .ToDictionaryAsync(i => i.StructuredLearningIllustrationId, cancellationToken);

        var orderedClips = new List<byte[]>();
        foreach (var id in orderedIllustrationIds)
        {
            if (!illustrationsById.TryGetValue(id, out var illustration))
            {
                throw new InvalidOperationException($"Video illustration {id} not found in this section.");
            }
            var content = await _illustrationStorage.DownloadIllustrationBytesAsync(illustration.StoragePath, cancellationToken);
            orderedClips.Add(content.ToArray());
        }

        var mergedBytes = await Mp4Concat.ConcatenateAsync(orderedClips, cancellationToken);
        mergedBytes = await Mp4FastStart.TryApplyAsync(mergedBytes, cancellationToken);

        var mergedIllustrationId = Guid.NewGuid();
        using var stream = new MemoryStream(mergedBytes);
        var storagePath = await _illustrationStorage.UploadVideoAsync(section.TenantId, sectionId, sectionId, mergedIllustrationId, stream);

        var mergedIllustration = new StructuredLearningIllustration
        {
            StructuredLearningIllustrationId = mergedIllustrationId,
            StructuredLearningDocumentSectionId = sectionId,
            StoragePath = storagePath,
            Prompt = $"Merged {orderedIllustrationIds.Count} videos: " + string.Join(" + ", orderedIllustrationIds.Select(id => illustrationsById[id].Prompt)),
            Purpose = illustrationsById[orderedIllustrationIds[0]].Purpose,
            ImageModel = "merged",
            MediaType = "Video",
            Width = 0,
            Height = 0,
            CreatedDate = DateTime.UtcNow
        };

        dbContext.StructuredLearningIllustrations.Add(mergedIllustration);
        await dbContext.SaveChangesAsync(cancellationToken);

        return mergedIllustration.StructuredLearningIllustrationId;
    }

    /// <summary>Trims one video's start/end (in seconds) in place — e.g. to
    /// cut a slow first second or a trailing pause so it syncs better once
    /// merged with other clips (2026-08-22). Re-encodes via <see cref="Mp4Trim"/>
    /// (frame-accurate, unlike stream-copy trims), then overwrites the SAME
    /// blob/StoragePath — no new illustration row, no change to the video
    /// count.</summary>
    public async Task TrimAsync(Guid illustrationId, double startSeconds, double? endSeconds, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var illustration = await dbContext.StructuredLearningIllustrations
            .FirstOrDefaultAsync(i => i.StructuredLearningIllustrationId == illustrationId && i.MediaType == "Video", cancellationToken)
            ?? throw new InvalidOperationException($"Video illustration {illustrationId} not found.");

        var originalBytes = (await _illustrationStorage.DownloadIllustrationBytesAsync(illustration.StoragePath, cancellationToken)).ToArray();
        var trimmedBytes = await Mp4Trim.TrimAsync(originalBytes, startSeconds, endSeconds, cancellationToken);

        await BackupBeforeOverwriteAsync(illustration.StoragePath, originalBytes, cancellationToken);
        using var stream = new MemoryStream(trimmedBytes);
        await _illustrationStorage.OverwriteVideoAsync(illustration.StoragePath, stream, cancellationToken);
    }

    /// <summary>Cuts a chunk out of the MIDDLE of a video (2026-08-22) —
    /// unlike <see cref="TrimAsync"/> (which only shortens the start/end),
    /// this keeps a caller-supplied set of disjoint [start,end) ranges and
    /// splices them back together, letting a reviewer delete a specific
    /// unwanted segment (e.g. a bad frame/stumble in the middle) while
    /// keeping everything before and after it. Each kept range is
    /// re-encoded via <see cref="Mp4Trim"/> for frame-accurate cuts, then
    /// stitched with <see cref="Mp4Concat"/> if there's more than one —
    /// overwrites the SAME blob/illustration, same as TrimAsync.</summary>
    public async Task CutSegmentsAsync(Guid illustrationId, IReadOnlyList<(double Start, double End)> keepRanges, CancellationToken cancellationToken = default)
    {
        if (keepRanges.Count == 0)
        {
            throw new InvalidOperationException("At least one range to keep is required.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var illustration = await dbContext.StructuredLearningIllustrations
            .FirstOrDefaultAsync(i => i.StructuredLearningIllustrationId == illustrationId && i.MediaType == "Video", cancellationToken)
            ?? throw new InvalidOperationException($"Video illustration {illustrationId} not found.");

        var originalBytes = (await _illustrationStorage.DownloadIllustrationBytesAsync(illustration.StoragePath, cancellationToken)).ToArray();

        var keptClips = new List<byte[]>();
        foreach (var (start, end) in keepRanges)
        {
            keptClips.Add(await Mp4Trim.TrimAsync(originalBytes, start, end, cancellationToken));
        }

        var finalBytes = keptClips.Count == 1
            ? keptClips[0]
            : await Mp4Concat.ConcatenateAsync(keptClips, cancellationToken);

        await BackupBeforeOverwriteAsync(illustration.StoragePath, originalBytes, cancellationToken);
        using var stream = new MemoryStream(finalBytes);
        await _illustrationStorage.OverwriteVideoAsync(illustration.StoragePath, stream, cancellationToken);
    }

    /// <summary>Restores the single most recent Trim/CutSegments edit for
    /// one video (2026-08-22) — one level of undo, no history stack. The
    /// pre-edit bytes are saved to a sibling "{StoragePath}.bak" blob right
    /// before every overwrite (see <see cref="BackupBeforeOverwriteAsync"/>);
    /// this just copies that back over the live blob and clears the
    /// backup so a second Undo doesn't redo the same restore.</summary>
    public async Task UndoLastEditAsync(Guid illustrationId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var illustration = await dbContext.StructuredLearningIllustrations
            .FirstOrDefaultAsync(i => i.StructuredLearningIllustrationId == illustrationId && i.MediaType == "Video", cancellationToken)
            ?? throw new InvalidOperationException($"Video illustration {illustrationId} not found.");

        var backupPath = illustration.StoragePath + ".bak";
        byte[] backupBytes;
        try
        {
            backupBytes = (await _illustrationStorage.DownloadIllustrationBytesAsync(backupPath, cancellationToken)).ToArray();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException("There is no previous version of this video to undo to.");
        }

        using var stream = new MemoryStream(backupBytes);
        await _illustrationStorage.OverwriteVideoAsync(illustration.StoragePath, stream, cancellationToken);
        await _illustrationStorage.DeleteIllustrationAsync(backupPath, cancellationToken);
    }

    private async Task BackupBeforeOverwriteAsync(string storagePath, byte[] currentBytes, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(currentBytes);
        await _illustrationStorage.OverwriteVideoAsync(storagePath + ".bak", stream, cancellationToken);
    }

    /// <summary>Last frame of an already-generated clip as JPEG bytes
    /// (2026-08-25/26) — used both for multi-clip continuity (wizard) and
    /// exposed via <see cref="GetStructuredLearningVideoLastFrameFunction"/>.</summary>
    public async Task<byte[]> GetLastFrameAsync(Guid illustrationId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var illustration = await dbContext.StructuredLearningIllustrations
            .FirstOrDefaultAsync(i => i.StructuredLearningIllustrationId == illustrationId && i.MediaType == "Video", cancellationToken)
            ?? throw new InvalidOperationException($"Video illustration {illustrationId} not found.");

        var videoBytes = (await _illustrationStorage.DownloadIllustrationBytesAsync(illustration.StoragePath, cancellationToken)).ToArray();
        return await Mp4LastFrame.ExtractAsync(videoBytes, cancellationToken);
    }

    /// <summary>Starts a "Regenerate" job for one existing video (2026-08-26)
    /// — reuses that video's OWN stored Prompt and its OWN first frame as
    /// the starting reference image, so the reviewer can re-roll a clip
    /// that came out wrong while keeping the same visual continuity/
    /// subject it started from. Does not delete the old video — the
    /// caller can delete it after confirming the new one looks better.</summary>
    public async Task<StructuredLearningVideoRunStatus> RegenerateAsync(
        Guid illustrationId, string videoModel = "veo", CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var illustration = await dbContext.StructuredLearningIllustrations
            .FirstOrDefaultAsync(i => i.StructuredLearningIllustrationId == illustrationId && i.MediaType == "Video", cancellationToken)
            ?? throw new InvalidOperationException($"Video illustration {illustrationId} not found.");

        var videoBytes = (await _illustrationStorage.DownloadIllustrationBytesAsync(illustration.StoragePath, cancellationToken)).ToArray();
        var firstFrame = await Mp4FirstFrame.ExtractAsync(videoBytes, cancellationToken);

        return await StartAsync(
            illustration.StructuredLearningDocumentSectionId, illustration.Prompt, illustration.Purpose, videoModel,
            firstFrame, "image/jpeg", cancellationToken);
    }

    private sealed class RunSession
    {
        private readonly object _lock = new();
        private StructuredLearningVideoRunStatus _latestStatus = null!;

        public Task? BackgroundTask { get; set; }

        public StructuredLearningVideoRunStatus LatestStatus
        {
            get { lock (_lock) { return _latestStatus; } }
            set { lock (_lock) { _latestStatus = value; } }
        }
    }
}
