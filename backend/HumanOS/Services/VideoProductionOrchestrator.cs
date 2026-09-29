using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Orchestrates the standalone "Video Productions" module (2026-09-23) —
/// deliberately does NOT use FilmProductionOrchestrator/FilmSceneContentAgent
/// (the slow, per-scene image-generation pipeline). Clips are generated
/// directly from a text prompt (already produced by the fast,
/// text-only <see cref="HumanOS.Agents.Studio.StructuredLearningVideoPromptAgent"/>
/// on the frontend's "Generate prompt sequence" call) straight into a
/// video via Google Veo/Omni Flash — same services
/// StructuredLearningVideoOrchestrator/FilmProductionVideoOrchestrator
/// already use, just without any intermediate still-image step.
///
/// Scoped, same rationale as FilmProductionOrchestrator: depends on the
/// Scoped CapabilityGraphIllustrationStorageService.
/// </summary>
public sealed class VideoProductionOrchestrator
{
    private readonly GoogleVeoVideoService _veoService;
    private readonly GoogleOmniFlashVideoService _omniService;
    private readonly KlingVideoService _klingService;
    private readonly CapabilityGraphIllustrationStorageService _storage;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public VideoProductionOrchestrator(
        GoogleVeoVideoService veoService,
        GoogleOmniFlashVideoService omniService,
        KlingVideoService klingService,
        CapabilityGraphIllustrationStorageService storage,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _veoService = veoService;
        _omniService = omniService;
        _klingService = klingService;
        _storage = storage;
        _dbContextFactory = dbContextFactory;
    }

    public bool IsConfigured => _veoService.IsConfigured && _storage.IsConfigured;

    /// <summary>Generates ONE clip directly from a text prompt (optionally
    /// image-to-video from <paramref name="referenceImageBytes"/> — e.g. the
    /// user's chosen starting image for clip 0, or a previous clip's last
    /// frame for continuity). Creates and persists the clip row itself —
    /// callers don't need a separate "create" step.</summary>
    public async Task<VideoProductionClip> GenerateClipAsync(
        Guid videoProductionId, int sortOrder, string prompt, string videoModel,
        byte[]? referenceImageBytes, string? referenceImageContentType, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("VideoProductionOrchestrator is not configured.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var production = await dbContext.VideoProductions
            .FirstOrDefaultAsync(p => p.VideoProductionId == videoProductionId, cancellationToken)
            ?? throw new InvalidOperationException($"VideoProduction {videoProductionId} not found.");

        var clip = new VideoProductionClip
        {
            VideoProductionId = videoProductionId,
            SortOrder = sortOrder,
            Prompt = prompt,
            Status = VideoProductionClipStatus.Processing,
        };
        dbContext.VideoProductionClips.Add(clip);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Persists the exact starting frame used (2026-09-24) — either
        // clip 0's own uploaded/edited image or the previous clip's last
        // frame carried over for continuity — so reopening the project
        // later still shows/lets the reviewer verify it, instead of it
        // only ever existing for the original browser session.
        if (referenceImageBytes is not null && _storage.IsConfigured)
        {
            using var anchorStream = new MemoryStream(referenceImageBytes);
            clip.AnchorImageStoragePath = await _storage.UploadIllustrationAsync(
                production.TenantId, videoProductionId, clip.VideoProductionClipId, 0, anchorStream,
                referenceImageContentType ?? "image/png", cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        try
        {
            byte[]? videoBytes;
            string modelName;

            if (videoModel == "omni")
            {
                var result = await _omniService.GenerateAsync(prompt, referenceImageBytes, referenceImageContentType, cancellationToken);
                if (result.ErrorMessage is not null || result.VideoBytes is null)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "Omni Flash returned no video bytes.");
                }
                videoBytes = result.VideoBytes;
                modelName = "gemini-omni-flash-preview";
            }
            else if (videoModel == "kling")
            {
                // Cheaper third-party alternative to Google Veo (2026-09-24
                // — Veo's per-second billing was running $3-5/clip even on
                // the "-fast" tier). See KlingVideoService's own doc
                // comment for the API shape.
                var taskId = await _klingService.StartGenerationAsync(prompt, referenceImageBytes, referenceImageContentType, cancellationToken);

                KlingVideoService.PollResult result;
                var deadline = DateTime.UtcNow.AddMinutes(8);
                do
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    result = await _klingService.PollAsync(taskId, cancellationToken);
                }
                while (!result.Done && DateTime.UtcNow < deadline);

                if (!result.Done)
                {
                    throw new InvalidOperationException("Timed out waiting for Kling video generation (over 8 minutes).");
                }
                if (result.ErrorMessage is not null || result.VideoBytes is null)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "Kling returned no video bytes.");
                }

                videoBytes = result.VideoBytes;
                modelName = "kling-3.0";
            }
            else
            {
                // Switched to the "-fast" tier at 720p (2026-09-24, cost
                // reduction — the standard 1080p tier was running
                // $3-5/clip). Same defaults GoogleVeoVideoService already
                // uses for Structured Learning/Film Production. "veo-lite"
                // opts into the even cheaper Lite tier (still has native
                // audio per Google's docs).
                var veoModelOverride = videoModel == "veo-lite" ? GoogleVeoVideoService.LiteModelName : null;
                var operationName = await _veoService.StartGenerationAsync(
                    prompt, referenceImageBytes, referenceImageContentType, cancellationToken, veoModelOverride);

                GoogleVeoVideoService.PollResult result;
                var deadline = DateTime.UtcNow.AddMinutes(8);
                do
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    result = await _veoService.PollAsync(operationName, cancellationToken);
                }
                while (!result.Done && DateTime.UtcNow < deadline);

                if (!result.Done)
                {
                    throw new InvalidOperationException("Timed out waiting for Veo video generation (over 8 minutes).");
                }
                if (result.ErrorMessage is not null || result.VideoBytes is null)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "Veo returned no video bytes.");
                }

                videoBytes = result.VideoBytes;
                modelName = veoModelOverride ?? GoogleVeoVideoService.FastModelName;
            }

            using var stream = new MemoryStream(videoBytes);
            var storagePath = await _storage.UploadVideoAsync(
                production.TenantId, videoProductionId, clip.VideoProductionClipId, clip.VideoProductionClipId, stream, cancellationToken: cancellationToken);

            clip.StoragePath = storagePath;
            clip.VideoModel = modelName;
            clip.Status = VideoProductionClipStatus.Completed;
        }
        catch (Exception ex)
        {
            clip.Status = VideoProductionClipStatus.Failed;
            clip.ErrorMessage = ex.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return clip;
    }

    /// <summary>Downloads a completed clip's video bytes — used both to
    /// serve it directly and to extract its last frame for the next
    /// clip's continuity anchor (handled by the caller/frontend).</summary>
    public async Task<byte[]> DownloadClipVideoAsync(Guid clipId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var clip = await dbContext.VideoProductionClips.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VideoProductionClipId == clipId, cancellationToken)
            ?? throw new InvalidOperationException($"VideoProductionClip {clipId} not found.");

        if (clip.StoragePath is null)
        {
            throw new InvalidOperationException("This clip has no generated video yet.");
        }

        return (await _storage.DownloadIllustrationBytesAsync(clip.StoragePath, cancellationToken)).ToArray();
    }

    /// <summary>Last frame of an already-generated clip as JPEG bytes
    /// (2026-09-23) — same continuity purpose as
    /// StructuredLearningVideoOrchestrator.GetLastFrameAsync: the frontend
    /// uses this as the next clip's own starting-frame anchor image AND
    /// displays it under that next clip so the reviewer can see exactly
    /// where the previous clip left off.</summary>
    public async Task<byte[]> GetLastFrameAsync(Guid clipId, CancellationToken cancellationToken)
    {
        var videoBytes = await DownloadClipVideoAsync(clipId, cancellationToken);
        return await Mp4LastFrame.ExtractAsync(videoBytes, cancellationToken);
    }

    /// <summary>Concatenates the caller-selected clips, IN THE ORDER given
    /// (2026-09-23, mirrors StructuredLearningVideoOrchestrator.MergeAsync)
    /// into ONE final video — local ffmpeg stream-copy, no billed API call.
    /// Source clips are left untouched; the reviewer can delete them
    /// separately via <see cref="DeleteClipAsync"/>.</summary>
    public async Task<VideoProduction> MergeAsync(Guid videoProductionId, IReadOnlyList<Guid> orderedClipIds, CancellationToken cancellationToken)
    {
        if (orderedClipIds.Count < 2)
        {
            throw new InvalidOperationException("Select at least 2 clips to merge.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var production = await dbContext.VideoProductions.FirstOrDefaultAsync(p => p.VideoProductionId == videoProductionId, cancellationToken)
            ?? throw new InvalidOperationException($"VideoProduction {videoProductionId} not found.");

        var clipsById = await dbContext.VideoProductionClips
            .Where(c => c.VideoProductionId == videoProductionId && orderedClipIds.Contains(c.VideoProductionClipId))
            .ToDictionaryAsync(c => c.VideoProductionClipId, cancellationToken);

        var orderedClips = new List<byte[]>();
        foreach (var id in orderedClipIds)
        {
            if (!clipsById.TryGetValue(id, out var clip) || clip.Status != VideoProductionClipStatus.Completed || clip.StoragePath is null)
            {
                throw new InvalidOperationException($"Clip {id} was not found, or isn't a completed video, in this production.");
            }
            var bytes = await _storage.DownloadIllustrationBytesAsync(clip.StoragePath, cancellationToken);
            orderedClips.Add(bytes.ToArray());
        }

        var mergedBytes = await Mp4Concat.ConcatenateAsync(orderedClips, cancellationToken);
        mergedBytes = await Mp4FastStart.TryApplyAsync(mergedBytes, cancellationToken);

        using var stream = new MemoryStream(mergedBytes);
        var storagePath = await _storage.UploadVideoAsync(
            production.TenantId, videoProductionId, videoProductionId, Guid.NewGuid(), stream, cancellationToken: cancellationToken);

        production.FinalVideoStoragePath = storagePath;
        await dbContext.SaveChangesAsync(cancellationToken);
        return production;
    }

    /// <summary>Deletes one clip's row and its .mp4 blob (2026-09-23,
    /// mirrors DeleteStructuredLearningVideoFunction) — blob deletion is
    /// best-effort so an already-gone/unreachable blob never blocks
    /// removing the DB row.</summary>
    public async Task DeleteClipAsync(Guid clipId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var clip = await dbContext.VideoProductionClips.FirstOrDefaultAsync(c => c.VideoProductionClipId == clipId, cancellationToken)
            ?? throw new InvalidOperationException($"VideoProductionClip {clipId} not found.");

        if (clip.StoragePath is not null)
        {
            try
            {
                await _storage.DeleteIllustrationAsync(clip.StoragePath, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — see doc comment.
            }
        }

        dbContext.VideoProductionClips.Remove(clip);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Deletes an entire project — the VideoProduction row, every
    /// one of its VideoProductionClip rows, and every associated blob
    /// (each clip's own .mp4 plus the merged final video, if any)
    /// (2026-09-24). Blob deletion is best-effort, same rationale as
    /// <see cref="DeleteClipAsync"/> — an already-gone/unreachable blob
    /// must never block removing the DB rows the reviewer asked to
    /// delete.</summary>
    public async Task DeleteProductionAsync(Guid videoProductionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var production = await dbContext.VideoProductions.FirstOrDefaultAsync(p => p.VideoProductionId == videoProductionId, cancellationToken)
            ?? throw new InvalidOperationException($"VideoProduction {videoProductionId} not found.");

        var clips = await dbContext.VideoProductionClips
            .Where(c => c.VideoProductionId == videoProductionId)
            .ToListAsync(cancellationToken);

        foreach (var clip in clips)
        {
            if (clip.StoragePath is null) continue;
            try
            {
                await _storage.DeleteIllustrationAsync(clip.StoragePath, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — see doc comment.
            }
        }

        if (production.FinalVideoStoragePath is not null)
        {
            try
            {
                await _storage.DeleteIllustrationAsync(production.FinalVideoStoragePath, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — see doc comment.
            }
        }

        dbContext.VideoProductionClips.RemoveRange(clips);
        dbContext.VideoProductions.Remove(production);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
