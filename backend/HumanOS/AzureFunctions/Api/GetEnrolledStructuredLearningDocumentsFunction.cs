using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Mis Engramas" (structured learning) — GET /people/{personId}/enrolled-structured-learning.
/// Sibling endpoint to GetEnrolledCapabilitiesFunction. See
/// StructuredLearningDocumentEnrollmentService.GetEnrolledDocumentsAsync.
/// </summary>
public sealed class GetEnrolledStructuredLearningDocumentsFunction
{
    private readonly StructuredLearningDocumentEnrollmentService _enrollmentService;

    public GetEnrolledStructuredLearningDocumentsFunction(StructuredLearningDocumentEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    [Function("GetEnrolledStructuredLearningDocuments")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/enrolled-structured-learning")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var enrolledDocuments = await _enrollmentService.GetEnrolledDocumentsAsync(personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, enrolledDocuments, cancellationToken: cancellationToken);
    }
}
