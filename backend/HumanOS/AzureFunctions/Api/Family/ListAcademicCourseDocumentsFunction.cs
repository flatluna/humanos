using HumanOS.Services.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// GET /family/academic/courses
/// Lists every saved academic course document (lightweight summary, no full
/// markdown) — powers the "Catálogo Académico" sidebar page in Capability Studio.
/// </summary>
public sealed class ListAcademicCourseDocumentsFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public ListAcademicCourseDocumentsFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("ListAcademicCourseDocuments")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/academic/courses")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var documents = await _capabilityService.ListAcademicCourseDocumentsAsync(cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, documents, cancellationToken: cancellationToken);
    }
}
