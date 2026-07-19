using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Serves the actual image bytes of a CapabilityGraphNodeIllustration
/// (fixed 2026-07-18 — closes the gap where illustration StoragePath was
/// returned in JSON, e.g. by TutorAsk/GetCurrentStep, but nothing ever
/// exposed the bytes themselves for a frontend &lt;img&gt; to point at).
///
/// Looks up the row's StoragePath in SQL, then streams the blob from Azure
/// Data Lake via <see cref="CapabilityGraphIllustrationStorageService.DownloadIllustrationAsync"/> —
/// same "resolve reference, then fetch bytes" split as SynthesizeSpeech
/// (which returns raw audio/mpeg bytes, not JSON).
/// </summary>
public sealed class GetIllustrationImageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public GetIllustrationImageFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService illustrationStorage)
    {
        _dbContext = dbContext;
        _illustrationStorage = illustrationStorage;
    }

    [Function("GetIllustrationImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "illustrations/{illustrationId:guid}/image")]
        HttpRequestData request,
        Guid illustrationId,
        CancellationToken cancellationToken)
    {
        if (!_illustrationStorage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "CapabilityGraphIllustrationStorageService is not configured (missing 'DataLakeStorage' connection string).",
                cancellationToken);
        }

        var storagePath = await _dbContext.CapabilityGraphNodeIllustrations
            .AsNoTracking()
            .Where(i => i.CapabilityGraphNodeIllustrationId == illustrationId)
            .Select(i => i.StoragePath)
            .FirstOrDefaultAsync(cancellationToken);

        if (storagePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "IllustrationNotFound",
                $"No CapabilityGraphNodeIllustration found with id {illustrationId}.", cancellationToken);
        }

        try
        {
            await using var imageStream = await _illustrationStorage.DownloadIllustrationAsync(storagePath, cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "image/png");
            response.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
            await imageStream.CopyToAsync(response.Body, cancellationToken);
            return response;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ImageBlobNotFound",
                $"StoragePath '{storagePath}' has no corresponding blob in Data Lake.", cancellationToken);
        }
    }
}
