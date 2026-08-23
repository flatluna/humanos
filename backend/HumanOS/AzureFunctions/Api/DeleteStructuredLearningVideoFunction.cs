using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Deletes one Google Veo video (2026-08-21) — removes both the
/// StructuredLearningIllustration row and its .mp4 blob, freeing up one of
/// the <see cref="Services.StructuredLearningVideoOrchestrator.MaxVideosPerSection"/>
/// slots for that section so a new video can be generated.</summary>
public sealed class DeleteStructuredLearningVideoFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public DeleteStructuredLearningVideoFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService illustrationStorage)
    {
        _dbContext = dbContext;
        _illustrationStorage = illustrationStorage;
    }

    [Function("DeleteStructuredLearningVideo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/structured-learning/videos/{illustrationId:guid}")]
        HttpRequestData request,
        Guid illustrationId,
        CancellationToken cancellationToken)
    {
        var illustration = await _dbContext.StructuredLearningIllustrations
            .FirstOrDefaultAsync(i => i.StructuredLearningIllustrationId == illustrationId && i.MediaType == "Video", cancellationToken);

        if (illustration is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "VideoNotFound",
                $"No video illustration found with id {illustrationId}.", cancellationToken);
        }

        try
        {
            await _illustrationStorage.DeleteIllustrationAsync(illustration.StoragePath, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort — a blob that's already gone (or storage briefly
            // unavailable) must never block freeing up the DB slot/count.
        }

        _dbContext.StructuredLearningIllustrations.Remove(illustration);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { deleted = true }, cancellationToken: cancellationToken);
    }
}
