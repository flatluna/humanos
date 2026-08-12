using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Serves the actual image bytes of a StructuredLearningIllustration —
/// same "resolve reference in SQL, then stream blob" split as
/// GetIllustrationImageFunction/GetTraditionalIllustrationImageFunction,
/// just pointed at the Structured Learning illustrations table instead.
/// </summary>
public sealed class GetStructuredLearningIllustrationImageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public GetStructuredLearningIllustrationImageFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService illustrationStorage)
    {
        _dbContext = dbContext;
        _illustrationStorage = illustrationStorage;
    }

    [Function("GetStructuredLearningIllustrationImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "illustrations/structured-learning/{illustrationId:guid}/image")]
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

        var storagePath = await _dbContext.StructuredLearningIllustrations
            .AsNoTracking()
            .Where(i => i.StructuredLearningIllustrationId == illustrationId)
            .Select(i => i.StoragePath)
            .FirstOrDefaultAsync(cancellationToken);

        if (storagePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "IllustrationNotFound",
                $"No StructuredLearningIllustration found with id {illustrationId}.", cancellationToken);
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
