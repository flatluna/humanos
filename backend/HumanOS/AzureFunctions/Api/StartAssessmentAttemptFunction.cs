using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Assessments;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class StartAssessmentAttemptFunction
{
    private readonly AssessmentService _assessmentService;

    public StartAssessmentAttemptFunction(AssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    [Function("StartAssessmentAttempt")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/assessments/{assessmentId:guid}")]
        HttpRequestData request,
        Guid personId,
        Guid assessmentId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        try
        {
            var result = await _assessmentService.StartAttemptAsync(assessmentId, personId, cancellationToken);

            return await FunctionResponseFactory.CreatedResponseAsync(request, result!, cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NotFound", ex.Message, cancellationToken);
        }
    }
}
