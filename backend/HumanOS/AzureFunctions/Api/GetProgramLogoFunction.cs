using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetProgramLogoFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly ProgramLogoStorageService _logoStorage;

    public GetProgramLogoFunction(HumanOsDbContext dbContext, ProgramLogoStorageService logoStorage)
    {
        _dbContext = dbContext;
        _logoStorage = logoStorage;
    }

    [Function("GetProgramLogo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programs/{programId:guid}/logo")]
        HttpRequestData request,
        Guid programId,
        CancellationToken cancellationToken)
    {
        if (!_logoStorage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "ProgramLogoStorageService is not configured (missing 'DataLakeStorage' connection string).",
                cancellationToken);
        }

        var storagePath = await _dbContext.Programs
            .AsNoTracking()
            .Where(p => p.ProgramId == programId)
            .Select(p => p.LogoStoragePath)
            .FirstOrDefaultAsync(cancellationToken);

        if (storagePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "LogoNotFound",
                $"No logo found for program {programId}.", cancellationToken);
        }

        try
        {
            await using var imageStream = await _logoStorage.DownloadLogoAsync(storagePath, cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            var contentType = storagePath.EndsWith(".svg") ? "image/svg+xml"
                : storagePath.EndsWith(".jpg") ? "image/jpeg"
                : storagePath.EndsWith(".webp") ? "image/webp"
                : "image/png";
            response.Headers.Add("Content-Type", contentType);
            response.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
            await imageStream.CopyToAsync(response.Body, cancellationToken);
            return response;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "LogoBlobNotFound",
                $"StoragePath '{storagePath}' has no corresponding blob in Data Lake.", cancellationToken);
        }
    }
}
