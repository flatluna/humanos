using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Lets the Structured Learning Document detail page
/// (StructuredLearningDocumentDetailPage.tsx) ask "is this document's Step 2
/// (extraction/knowledge-graph build) still actively running?" — distinct
/// from GetActiveStructuredLearningPedagogyRunFunction, which only tracks
/// Step 3 (pedagogy content generation). Without this, a document whose
/// Sections rows already exist mid-extraction (e.g. while
/// "Construyendo grafo de conocimiento (relaciones)" is still running) was
/// shown the "N of N sections still need pedagogy content / Continue
/// generating" banner even though nothing has actually stalled — Step 2
/// simply hasn't finished yet (2026-10-27).
///
/// <see cref="StructuredLearningDocument.SessionId"/> IS the extraction
/// run's RunId (see StructuredLearningExtractionService's doc comments) —
/// no separate DocumentId-to-RunId mapping needs to be introduced, this
/// just looks the existing one up.</summary>
public sealed class GetActiveStructuredLearningExtractionRunForDocumentFunction
{
    private readonly StructuredLearningExtractionOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;

    public GetActiveStructuredLearningExtractionRunForDocumentFunction(
        StructuredLearningExtractionOrchestrator orchestrator, HumanOsDbContext dbContext)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
    }

    [Function("GetActiveStructuredLearningExtractionRunForDocument")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/{documentId:guid}/active-extraction-run")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => d.StructuredLearningDocumentId == documentId)
            .Select(d => new { d.SessionId })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "DocumentNotFound", $"No document found with id '{documentId}'.", cancellationToken);
        }

        try
        {
            var status = _orchestrator.GetStatus(document.SessionId);
            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // The in-process orchestrator doesn't know this run anymore —
            // either the extraction finished long ago (and the document
            // page should rely on its own section/pedagogy state instead)
            // or the Functions host restarted mid-run. Either way there is
            // nothing ACTIVELY running from this process's point of view.
            return await FunctionResponseFactory.SuccessResponseAsync(
                request, (StructuredLearningExtractionRunStatus?)null, cancellationToken: cancellationToken);
        }
    }
}
