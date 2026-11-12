using System.Net;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Free (no-AI-cost) way to re-test Recall on a section (2026-10-26) —
/// unlike regenerating/"Redoing" the engram (which re-runs pedagogy
/// generation, including paid AI calls for questions/images), this just
/// deletes the student's previous <c>StructuredLearningRecallAttempts</c>
/// rows for the section so the next GET .../recall/state starts the
/// SAME already-generated question bank over from zero mastery, without
/// spending anything. Intended for Studio testers, not real students.
/// </summary>
public sealed class ResetStructuredLearningRecallStateFunction
{
    private readonly HumanOsDbContext _dbContext;

    public ResetStructuredLearningRecallStateFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("ResetStructuredLearningRecallState")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/recall/reset")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        Guid? personId = Guid.TryParse(query["personId"], out var parsedPersonId) ? parsedPersonId : null;

        var attemptsQuery = _dbContext.StructuredLearningRecallAttempts
            .Where(r => r.StructuredLearningDocumentSectionId == sectionId);
        if (personId is not null)
        {
            attemptsQuery = attemptsQuery.Where(r => r.PersonId == personId);
        }

        var deletedCount = await attemptsQuery.ExecuteDeleteAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { deletedCount }, cancellationToken: cancellationToken);
    }
}
