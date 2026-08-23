using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Inscribirme" (structured learning) — POST /people/{personId}/structured-learning/{documentId}/enroll.
/// See StructuredLearningDocumentEnrollmentService.EnrollAsync.
/// </summary>
public sealed class EnrollStructuredLearningDocumentFunction
{
    private readonly StructuredLearningDocumentEnrollmentService _enrollmentService;

    public EnrollStructuredLearningDocumentFunction(StructuredLearningDocumentEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    [Function("EnrollStructuredLearningDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/structured-learning/{documentId:guid}/enroll")]
        HttpRequestData request,
        Guid personId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _enrollmentService.EnrollAsync(personId, documentId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StructuredLearningDocumentNotFound", ex.Message, cancellationToken);
        }
    }
}
