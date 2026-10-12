using System.Net;
using HumanOS.Services.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// DELETE /family/academic/courses/{courseCode}
/// Deletes a saved academic course document — lets the "Catálogo Académico" page
/// clean up test/broken drafts (2026-10-08).
/// </summary>
public sealed class DeleteAcademicCourseDocumentFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public DeleteAcademicCourseDocumentFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("DeleteAcademicCourseDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "family/academic/courses/{courseCode}")]
        HttpRequestData request,
        string courseCode,
        CancellationToken cancellationToken)
    {
        await _capabilityService.DeleteAcademicCourseDocumentAsync(courseCode, cancellationToken);
        var response = request.CreateResponse(HttpStatusCode.NoContent);
        return response;
    }
}
