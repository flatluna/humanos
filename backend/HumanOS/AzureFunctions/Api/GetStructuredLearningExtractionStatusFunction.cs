using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Polls the live status of a Structured Learning Document
/// extraction run (Step 2) — same polling pattern as
/// GetTraditionalCourseStatusFunction.</summary>
public sealed class GetStructuredLearningExtractionStatusFunction
{
    private readonly StructuredLearningExtractionOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningExtractionStatusFunction(StructuredLearningExtractionOrchestrator orchestrator, HumanOsDbContext dbContext)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningExtractionStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/{runId:guid}/status")]
        HttpRequestData request,
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = _orchestrator.GetStatus(runId);
            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // The in-memory run tracker doesn't know this runId — this
            // happens whenever the Functions host restarts mid-session
            // (a recurring dev-time occurrence, see
            // /memories/repo/humanos-backend-dev-workflow.md). Since Step 2
            // is fully persisted to the DB by the time it succeeds, fall
            // back to reconstructing a "Completed" status from the document
            // row whose SessionId matches this runId, rather than forcing a
            // re-extraction.
            var document = await _dbContext.StructuredLearningDocuments
                .Include(d => d.Sections)
                .FirstOrDefaultAsync(d => d.SessionId == runId, cancellationToken);

            if (document is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "RunNotFound",
                    $"No structured-learning-extraction run found with id '{runId}'.", cancellationToken);
            }

            var result = new StructuredLearningExtractionResult
            {
                StructuredLearningDocumentId = document.StructuredLearningDocumentId,
                DocumentName = document.DocumentName,
                SectionCount = document.Sections.Count,
                TotalCharacters = document.Sections.Sum(s => s.TotalCharacters),
                TotalTokens = document.Sections.Sum(s => s.TotalTokens)
            };

            var recoveredStatus = StructuredLearningExtractionRunStatus.Completed(runId, result);
            return await FunctionResponseFactory.SuccessResponseAsync(request, recoveredStatus, cancellationToken: cancellationToken);
        }
    }
}
