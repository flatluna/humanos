using System.Net;
using System.Web;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Streams the synthesized workplace application audio file (MP3) from Azure Data Lake.
/// </summary>
public sealed class GetStructuredLearningWorkplaceApplicationAudioFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storageService;

    public GetStructuredLearningWorkplaceApplicationAudioFunction(
        HumanOsDbContext dbContext,
        CapabilityGraphIllustrationStorageService storageService)
    {
        _dbContext = dbContext;
        _storageService = storageService;
    }

    [Function("GetStructuredLearningWorkplaceApplicationAudio")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/workplace-application/audio")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_storageService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured", "Data Lake is not configured.", cancellationToken);
        }

        var query = HttpUtility.ParseQueryString(request.Url.Query);
        Guid? personId = null;
        if (Guid.TryParse(query["personId"] ?? query["studentId"] ?? query["employeeId"], out var parsedId))
        {
            personId = parsedId;
        }

        var section = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Select(s => new { s.StructuredLearningDocumentSectionId, s.TenantId, s.StructuredLearningDocumentId })
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken);

        if (section is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", "Section was not found.", cancellationToken);
        }

        if (!personId.HasValue)
        {
            var defaultPerson = await _dbContext.PersonProfiles.AsNoTracking()
                .Where(pp => (pp.DisplayName != null && pp.DisplayName.Contains("Alejandro")) || (pp.FirstName != null && pp.FirstName.Contains("Alejandro")))
                .Select(pp => (Guid?)pp.PersonId)
                .FirstOrDefaultAsync(cancellationToken);

            personId = defaultPerson ?? await _dbContext.People.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.CreatedDate)
                .Select(p => (Guid?)p.PersonId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var storagePath = _storageService.GetWorkplaceAudioStoragePath(
            section.TenantId, section.StructuredLearningDocumentId, sectionId, personId ?? Guid.Empty);

        try
        {
            await using var stream = await _storageService.DownloadIllustrationAsync(storagePath, cancellationToken);
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "audio/mpeg");
            response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            await stream.CopyToAsync(response.Body, cancellationToken);
            return response;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "AudioNotFound", "Workplace audio not found for this student.", cancellationToken);
        }
    }
}
