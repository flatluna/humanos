using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Uploads a Program's logo — takes the raw image bytes as the request
/// body (Content-Type header identifies the format), same "no multipart
/// parsing" simplicity as this app's other single-file upload endpoints.
/// Overwrites any existing logo.
/// </summary>
public sealed class UploadProgramLogoFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly ProgramLogoStorageService _logoStorage;

    public UploadProgramLogoFunction(HumanOsDbContext dbContext, ProgramLogoStorageService logoStorage)
    {
        _dbContext = dbContext;
        _logoStorage = logoStorage;
    }

    [Function("UploadProgramLogo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programs/{programId:guid}/logo")]
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

        var program = await _dbContext.Programs.SingleOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);
        if (program is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProgramNotFound",
                $"No program found with id {programId}.", cancellationToken);
        }

        var contentType = request.Headers.TryGetValues("Content-Type", out var values)
            ? values.First()
            : "image/png";

        var storagePath = await _logoStorage.UploadLogoAsync(programId, request.Body, contentType, cancellationToken);

        program.LogoStoragePath = storagePath;
        program.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new { programId, logoUrl = $"/programs/{programId}/logo" }, cancellationToken: cancellationToken);
    }
}
