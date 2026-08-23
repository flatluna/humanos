using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>One-off backfill (2026-08-21) for Traditional Courses created
/// before the executive-summary feature existed — generates it from the
/// course's own already-persisted content, never invented.</summary>
public sealed class BackfillTraditionalCourseExecutiveSummaryFunction
{
    private readonly TraditionalCoursePipelineService _pipeline;

    public BackfillTraditionalCourseExecutiveSummaryFunction(TraditionalCoursePipelineService pipeline)
    {
        _pipeline = pipeline;
    }

    public sealed class BackfillResponse
    {
        public string ExecutiveSummary { get; set; } = string.Empty;
    }

    [Function("BackfillTraditionalCourseExecutiveSummary")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/traditional-course/{capabilityId:guid}/executive-summary/backfill")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var executiveSummary = await _pipeline.BackfillExecutiveSummaryAsync(capabilityId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(
                request, new BackfillResponse { ExecutiveSummary = executiveSummary }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "BackfillFailed", ex.Message, cancellationToken);
        }
    }
}
