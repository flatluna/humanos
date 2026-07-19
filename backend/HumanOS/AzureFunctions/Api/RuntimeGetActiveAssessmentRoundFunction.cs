using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Adaptive Assessment (2026-07-18) — resume support. Returns the MOST
/// RECENT AssessmentRound for a node (so a page reload can resume
/// mid-round, or show the pass/fail summary of the last completed round),
/// or null (200 OK, empty body — same convention as
/// RuntimeGetActiveSessionFunction) if no round has ever been started for
/// this node yet, signaling the UI to call start-round.
/// </summary>
public sealed class RuntimeGetActiveAssessmentRoundFunction
{
    private readonly AdaptiveAssessmentEngine _engine;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeGetActiveAssessmentRoundFunction(AdaptiveAssessmentEngine engine, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _dbContext = dbContext;
    }

    [Function("RuntimeGetActiveAssessmentRound")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instructor-runtime/assessment/active")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);

        if (!Guid.TryParse(query["learningSessionNodeId"], out var learningSessionNodeId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Query parameter learningSessionNodeId is required.", cancellationToken);
        }

        var round = await _engine.GetActiveRoundAsync(_dbContext, learningSessionNodeId, cancellationToken);
        if (round is null)
        {
            return await FunctionResponseFactory.SuccessResponseAsync<object?>(request, null, cancellationToken: cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, AdaptiveAssessmentApiMappers.ToRoundStateDto(round), cancellationToken: cancellationToken);
    }
}
