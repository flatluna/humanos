using System.Net;
using HumanOS.Services.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// GET /family/academic/courses/{courseCode}
/// Retrieves the previously saved academic course document for a given CourseCode (e.g. "KIND-LIT-01"),
/// so the Academic Course Generator UI can reload an educator-approved draft instead of regenerating it.
/// </summary>
public sealed class GetAcademicCourseDocumentFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public GetAcademicCourseDocumentFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("GetAcademicCourseDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/academic/courses/{courseCode}")]
        HttpRequestData request,
        string courseCode,
        CancellationToken cancellationToken)
    {
        var document = await _capabilityService.GetAcademicCourseDocumentAsync(courseCode, cancellationToken);
        if (document is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NotFound",
                $"No saved academic course document found for CourseCode '{courseCode}'.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, document, cancellationToken: cancellationToken);
    }
}
