using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Serves the actual image bytes of a Capability's course-level cover image
/// (CapabilityGraph.CoverImageStoragePath, 2026-07-21) — same "resolve
/// reference in SQL, then stream the blob from Data Lake" split as
/// <see cref="GetIllustrationImageFunction"/>, but keyed by CapabilityId
/// instead of an illustration id (there is exactly one cover image per
/// capability).
/// </summary>
public sealed class GetCapabilityCoverImageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public GetCapabilityCoverImageFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService illustrationStorage)
    {
        _dbContext = dbContext;
        _illustrationStorage = illustrationStorage;
    }

    [Function("GetCapabilityCoverImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "capabilities/{capabilityId:guid}/cover-image")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        if (!_illustrationStorage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "CapabilityGraphIllustrationStorageService is not configured (missing 'DataLakeStorage' connection string).",
                cancellationToken);
        }

        var storagePath = await _dbContext.CapabilityGraphs
            .AsNoTracking()
            .Where(g => g.CapabilityId == capabilityId)
            .Select(g => g.CoverImageStoragePath)
            .FirstOrDefaultAsync(cancellationToken);

        if (storagePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "CoverImageNotFound",
                $"No cover image found for capability {capabilityId}.", cancellationToken);
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
