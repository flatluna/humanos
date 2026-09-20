using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Uploads a person's own "Mi Perfil" profile photo — takes the raw image
/// bytes as the request body (Content-Type header identifies the
/// format), same "no multipart parsing" simplicity as
/// UploadProgramLogoFunction. Overwrites any existing photo. Requires a
/// PersonProfile row to already exist (created via CreatePersonProfile).
/// </summary>
public sealed class UploadPersonProfilePhotoFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly PersonProfilePhotoStorageService _photoStorage;

    public UploadPersonProfilePhotoFunction(HumanOsDbContext dbContext, PersonProfilePhotoStorageService photoStorage)
    {
        _dbContext = dbContext;
        _photoStorage = photoStorage;
    }

    [Function("UploadPersonProfilePhoto")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/profile/photo")]
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

        var profile = await _dbContext.PersonProfiles.SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        if (profile is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonProfileNotFound",
                $"No profile found for person {personId}.", cancellationToken);
        }

        var tenantId = await _dbContext.People
            .Where(p => p.PersonId == personId)
            .Select(p => (Guid?)p.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        if (tenantId is null || tenantId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonNotFound",
                $"No person found for {personId}.", cancellationToken);
        }

        var contentType = request.Headers.TryGetValues("Content-Type", out var values)
            ? values.First()
            : "image/png";

        var storagePath = await _photoStorage.UploadPhotoAsync(tenantId.Value, personId, request.Body, contentType, cancellationToken);

        // PersonProfile.ProfilePhotoUrl holds the raw blob StoragePath (same
        // convention as Program.LogoStoragePath) — the frontend never reads
        // this value directly, it always requests the stable
        // GET /people/{personId}/profile/photo route below.
        profile.ProfilePhotoUrl = storagePath;
        profile.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new { personId, photoUrl = $"/people/{personId}/profile/photo" }, cancellationToken: cancellationToken);
    }
}
