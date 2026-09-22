using System.Net;
using System.Web;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Streams one of the two CQI Type 6 ("The Verifier") extra example illustrations
/// (imageIndex 1 or 2) from Azure Data Lake.
/// </summary>
public sealed class GetStructuredLearningWorkplaceApplicationCqiExampleImageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storageService;

    public GetStructuredLearningWorkplaceApplicationCqiExampleImageFunction(
        HumanOsDbContext dbContext,
        CapabilityGraphIllustrationStorageService storageService)
    {
        _dbContext = dbContext;
        _storageService = storageService;
    }

    [Function("GetStructuredLearningWorkplaceApplicationCqiExampleImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/workplace-application/cqi-example-image/{imageIndex:int}")]
        HttpRequestData request,
        Guid sectionId,
        int imageIndex,
        CancellationToken cancellationToken)
    {
        if (!_storageService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured", "Data Lake is not configured.", cancellationToken);
        }

        var query = HttpUtility.ParseQueryString(request.Url.Query);
        if (!Guid.TryParse(query["personId"], out var personId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'personId' is required.", cancellationToken);
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

        var storagePath = _storageService.GetWorkplaceExampleImageStoragePath(
            section.TenantId, section.StructuredLearningDocumentId, sectionId, personId, imageIndex);

        try
        {
            await using var stream = await _storageService.DownloadIllustrationAsync(storagePath, cancellationToken);
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "image/png");
            response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            await stream.CopyToAsync(response.Body, cancellationToken);
            return response;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ImageNotFound", "Example image not found for this student.", cancellationToken);
        }
    }
}
