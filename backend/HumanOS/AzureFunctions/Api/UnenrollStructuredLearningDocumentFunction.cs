using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Quitar" (structured learning) — DELETE /people/{personId}/structured-learning/{documentId}/enroll.
/// See StructuredLearningDocumentEnrollmentService.UnenrollAsync.
/// </summary>
public sealed class UnenrollStructuredLearningDocumentFunction
{
    private readonly StructuredLearningDocumentEnrollmentService _enrollmentService;

    public UnenrollStructuredLearningDocumentFunction(StructuredLearningDocumentEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    [Function("UnenrollStructuredLearningDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "people/{personId:guid}/structured-learning/{documentId:guid}/enroll")]
        HttpRequestData request,
        Guid personId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var result = await _enrollmentService.UnenrollAsync(personId, documentId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
