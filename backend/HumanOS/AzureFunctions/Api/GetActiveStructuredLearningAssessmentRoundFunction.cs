using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Real-time Assessment (2026-08-10) — returns the MOST RECENT round for a
/// Structured Learning section (so a page reload can resume mid-round, or
/// show the last completed round's pass/fail summary), or a null body if
/// no round has ever been started (caller should then call
/// StartStructuredLearningAssessmentRoundFunction).
/// </summary>
public sealed class GetActiveStructuredLearningAssessmentRoundFunction
{
    private readonly StructuredLearningAssessmentEngine _engine;
    private readonly HumanOsDbContext _dbContext;

    public GetActiveStructuredLearningAssessmentRoundFunction(StructuredLearningAssessmentEngine engine, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _dbContext = dbContext;
    }

    [Function("GetActiveStructuredLearningAssessmentRound")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/assessment/active-round")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var round = await _engine.GetActiveRoundAsync(_dbContext, sectionId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(
            request, round is null ? null : StructuredLearningAssessmentApiMappers.ToDto(round), cancellationToken: cancellationToken);
    }
}
