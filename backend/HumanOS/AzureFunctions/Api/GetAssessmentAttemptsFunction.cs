using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetAssessmentAttemptsFunction
{
    private readonly AssessmentService _assessmentService;

    public GetAssessmentAttemptsFunction(AssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    [Function("GetAssessmentAttempts")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/assessments")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        Guid? assessmentId = Guid.TryParse(query["assessmentId"], out var aid) ? aid : null;

        var attempts = await _assessmentService.GetPersonAttemptsAsync(personId, assessmentId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, attempts, cancellationToken: cancellationToken);
    }
}
