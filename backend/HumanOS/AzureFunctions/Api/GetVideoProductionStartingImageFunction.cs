using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetVideoProductionStartingImageResponse
{
    public string Base64 { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/png";
}

/// <summary>Returns the wizard's own saved step-4 starting image
/// (2026-09-24) — lets reopening a project restore the exact image
/// preview instead of always resetting step 4 to "no image".</summary>
public sealed class GetVideoProductionStartingImageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storage;

    public GetVideoProductionStartingImageFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    [Function("GetVideoProductionStartingImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/video-productions/{videoProductionId:guid}/starting-image")]
        HttpRequestData request,
        Guid videoProductionId,
        CancellationToken cancellationToken)
    {
        var production = await _dbContext.VideoProductions
            .AsNoTracking()
            .Where(p => p.VideoProductionId == videoProductionId)
            .Select(p => new { p.StartingImageStoragePath, p.StartingImageContentType })
            .FirstOrDefaultAsync(cancellationToken);

        if (production?.StartingImageStoragePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StartingImageNotFound", "No starting image was saved for this project.", cancellationToken);
        }

        var bytes = await _storage.DownloadIllustrationBytesAsync(production.StartingImageStoragePath, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new GetVideoProductionStartingImageResponse
            {
                Base64 = Convert.ToBase64String(bytes.ToArray()),
                ContentType = production.StartingImageContentType ?? "image/png",
            },
            cancellationToken: cancellationToken);
    }
}
