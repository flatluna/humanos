using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Turns a completed FilmScene's still image + GeneratedPrompt into a short
/// review/edit-ready video clip (2026-08-24, Fase 2) — reuses Gemini Omni
/// Flash's synchronous image-to-video mode (<see cref="GoogleOmniFlashVideoService"/>,
/// same one used by StructuredLearningVideoOrchestrator) so a single call
/// returns the finished .mp4 inline, no async start/poll run store needed.
/// Uses the scene's own FinalImageStoragePath as the video's first frame,
/// so the literal-motion instructions baked into GeneratedPrompt animate
/// starting from the exact composited still the reviewer already approved.
///
/// Scoped, same rationale as FilmProductionOrchestrator: depends on the
/// Scoped CapabilityGraphIllustrationStorageService.
/// </summary>
public sealed class FilmProductionVideoOrchestrator
{
    private readonly GoogleOmniFlashVideoService _omniService;
    private readonly CapabilityGraphIllustrationStorageService _storage;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public FilmProductionVideoOrchestrator(
        GoogleOmniFlashVideoService omniService,
        CapabilityGraphIllustrationStorageService storage,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _omniService = omniService;
        _storage = storage;
        _dbContextFactory = dbContextFactory;
    }

    public bool IsConfigured => _omniService.IsConfigured && _storage.IsConfigured;

    /// <summary>Generates (or regenerates) the video for ONE scene. Requires
    /// the scene to already be Completed with a final image + prompt.
    /// <paramref name="promptOverride"/> lets a caller retry with different
    /// wording (2026-08-24) — e.g. when Omni Flash rejects the stored
    /// GeneratedPrompt for referencing a real person's name/likeness —
    /// without needing to regenerate the scene's image/content.</summary>
    public async Task<FilmScene> GenerateSceneVideoAsync(Guid sceneId, string? promptOverride, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("FilmProductionVideoOrchestrator is not configured.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var scene = await dbContext.FilmScenes.FirstOrDefaultAsync(s => s.FilmSceneId == sceneId, cancellationToken)
            ?? throw new InvalidOperationException($"FilmScene {sceneId} not found.");

        if (scene.Status != FilmSceneStatus.Completed || scene.FinalImageStoragePath is null || string.IsNullOrWhiteSpace(scene.GeneratedPrompt))
        {
            throw new InvalidOperationException("This scene must have a completed image and prompt before generating a video.");
        }

        var production = await dbContext.FilmProductions.FirstOrDefaultAsync(p => p.FilmProductionId == scene.FilmProductionId, cancellationToken)
            ?? throw new InvalidOperationException($"FilmProduction {scene.FilmProductionId} not found.");

        scene.VideoStatus = FilmSceneStatus.Processing;
        scene.VideoErrorMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var effectivePrompt = string.IsNullOrWhiteSpace(promptOverride) ? scene.GeneratedPrompt! : promptOverride;
            var imageBytes = (await _storage.DownloadIllustrationBytesAsync(scene.FinalImageStoragePath, cancellationToken)).ToArray();
            var result = await _omniService.GenerateAsync(effectivePrompt, imageBytes, "image/png", cancellationToken);

            if (result.ErrorMessage is not null || result.VideoBytes is null)
            {
                scene.VideoStatus = FilmSceneStatus.Failed;
                scene.VideoErrorMessage = result.ErrorMessage ?? "Video generation returned no video bytes.";
            }
            else
            {
                var videoId = Guid.NewGuid();
                using var stream = new MemoryStream(result.VideoBytes);
                var storagePath = await _storage.UploadVideoAsync(production.TenantId, scene.FilmProductionId, scene.FilmSceneId, videoId, stream, cancellationToken: cancellationToken);

                scene.FinalVideoStoragePath = storagePath;
                scene.VideoModel = "gemini-omni-flash-preview";
                scene.VideoStatus = FilmSceneStatus.Completed;
            }
        }
        catch (Exception ex)
        {
            scene.VideoStatus = FilmSceneStatus.Failed;
            scene.VideoErrorMessage = ex.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return scene;
    }

    /// <summary>Generates videos for every Completed scene in the production
    /// that doesn't already have one, sequentially (never parallel — same
    /// rationale as ProcessScriptAsync, avoids contending for the same
    /// video-generation quota and keeps a partial failure recoverable).</summary>
    public async Task<List<FilmScene>> GenerateAllPendingAsync(Guid filmProductionId, CancellationToken cancellationToken)
    {
        List<Guid> sceneIds;
        await using (var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            sceneIds = await dbContext.FilmScenes
                .Where(s => s.FilmProductionId == filmProductionId
                    && s.Status == FilmSceneStatus.Completed
                    && s.FinalVideoStoragePath == null)
                .OrderBy(s => s.SortOrder)
                .Select(s => s.FilmSceneId)
                .ToListAsync(cancellationToken);
        }

        var results = new List<FilmScene>();
        foreach (var sceneId in sceneIds)
        {
            results.Add(await GenerateSceneVideoAsync(sceneId, promptOverride: null, cancellationToken));
        }

        return results;
    }

    /// <summary>Concatenates every scene's video (in SortOrder) into ONE
    /// final film (2026-08-24) — local ffmpeg stream-copy via
    /// <see cref="Mp4Concat"/>/<see cref="Mp4FastStart"/>, no billed API
    /// call. Requires every scene to already have a video (see
    /// <see cref="GenerateAllPendingAsync"/>); throws listing which scenes
    /// are still missing one rather than silently skipping them, so the
    /// merged film never has a gap.</summary>
    public async Task<FilmProduction> MergeAllScenesAsync(Guid filmProductionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var production = await dbContext.FilmProductions.FirstOrDefaultAsync(p => p.FilmProductionId == filmProductionId, cancellationToken)
            ?? throw new InvalidOperationException($"FilmProduction {filmProductionId} not found.");

        var scenes = await dbContext.FilmScenes
            .Where(s => s.FilmProductionId == filmProductionId && s.Status == FilmSceneStatus.Completed)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        if (scenes.Count < 2)
        {
            throw new InvalidOperationException("At least 2 completed scenes are required to merge a film.");
        }

        var missingVideo = scenes.Where(s => s.FinalVideoStoragePath is null).Select(s => s.SortOrder).ToList();
        if (missingVideo.Count > 0)
        {
            throw new InvalidOperationException(
                $"Scene(s) {string.Join(", ", missingVideo)} don't have a video yet — generate all videos before merging.");
        }

        var orderedClips = new List<byte[]>();
        foreach (var scene in scenes)
        {
            var bytes = await _storage.DownloadIllustrationBytesAsync(scene.FinalVideoStoragePath!, cancellationToken);
            orderedClips.Add(bytes.ToArray());
        }

        var mergedBytes = await Mp4Concat.ConcatenateAsync(orderedClips, cancellationToken);
        mergedBytes = await Mp4FastStart.TryApplyAsync(mergedBytes, cancellationToken);

        using var stream = new MemoryStream(mergedBytes);
        var storagePath = await _storage.UploadVideoAsync(production.TenantId, filmProductionId, filmProductionId, Guid.NewGuid(), stream, cancellationToken: cancellationToken);

        production.FinalFilmStoragePath = storagePath;
        await dbContext.SaveChangesAsync(cancellationToken);
        return production;
    }
}
