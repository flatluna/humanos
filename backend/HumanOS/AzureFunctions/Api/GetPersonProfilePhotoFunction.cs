using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetPersonProfilePhotoFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly PersonProfilePhotoStorageService _photoStorage;

    public GetPersonProfilePhotoFunction(HumanOsDbContext dbContext, PersonProfilePhotoStorageService photoStorage)
    {
        _dbContext = dbContext;
        _photoStorage = photoStorage;
    }

    [Function("GetPersonProfilePhoto")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/profile/photo")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        if (!_photoStorage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "PersonProfilePhotoStorageService is not configured (missing 'DataLakeStorage' connection string).",
                cancellationToken);
        }

        var storagePath = await _dbContext.PersonProfiles
            .AsNoTracking()
            .Where(p => p.PersonId == personId)
            .Select(p => p.ProfilePhotoUrl)
            .FirstOrDefaultAsync(cancellationToken);

        if (storagePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PhotoNotFound",
                $"No profile photo found for person {personId}.", cancellationToken);
        }

        try
        {
            await using var imageStream = await _photoStorage.DownloadPhotoAsync(storagePath, cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            var contentType = storagePath.EndsWith(".jpg") ? "image/jpeg"
                : storagePath.EndsWith(".webp") ? "image/webp"
                : "image/png";
            response.Headers.Add("Content-Type", contentType);
            response.Headers.Add("Cache-Control", "no-cache");
            await imageStream.CopyToAsync(response.Body, cancellationToken);
            return response;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PhotoBlobNotFound",
                $"StoragePath '{storagePath}' has no corresponding blob in Data Lake.", cancellationToken);
        }
    }
}
