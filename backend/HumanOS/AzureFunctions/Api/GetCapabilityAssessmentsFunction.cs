using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetCapabilityAssessmentsFunction
{
    private readonly AssessmentService _assessmentService;

    public GetCapabilityAssessmentsFunction(AssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    [Function("GetCapabilityAssessments")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "capabilities/{capabilityCode}/assessments")]
        HttpRequestData request,
        string capabilityCode,
        CancellationToken cancellationToken)
    {
        var assessments = await _assessmentService.GetActiveByCapabilityCodeAsync(capabilityCode, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, assessments, cancellationToken: cancellationToken);
    }
}
