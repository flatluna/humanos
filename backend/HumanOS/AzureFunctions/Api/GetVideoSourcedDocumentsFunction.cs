using System.Net;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Lets Studio's "Clases en Proceso" list (2026-11-07) tell apart a video
/// that is merely indexed in Azure AI Video Indexer from one whose Engrams
/// were actually generated into a Structured Learning course — by returning
/// every (SourceVideoId -> StructuredLearningDocumentId/DocumentName) pair
/// recorded so far. "Indexado" vs real "Listo" was being inferred purely
/// from Azure VI's own State field before, which only reflects indexing,
/// not course creation.
/// </summary>
public sealed class GetVideoSourcedDocumentsFunction
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public GetVideoSourcedDocumentsFunction(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Function("GetVideoSourcedDocuments")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/video/by-source-video")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var documents = await dbContext.StructuredLearningDocuments
            .Where(d => d.SourceVideoId != null)
            .Select(d => new VideoSourcedDocumentSummary(d.SourceVideoId!, d.StructuredLearningDocumentId, d.DocumentName))
            .ToListAsync(cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(documents, cancellationToken);
        return response;
    }
}

public sealed record VideoSourcedDocumentSummary(string SourceVideoId, Guid StructuredLearningDocumentId, string DocumentName);
