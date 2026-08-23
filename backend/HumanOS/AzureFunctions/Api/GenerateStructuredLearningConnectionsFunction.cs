using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class GenerateStructuredLearningConnectionsResponseDto
{
    public Guid SectionId { get; set; }
    public int ConnectionsCreated { get; set; }
}

/// <summary>Manual "Regenerar Knowledge Graph" trigger for one Engram
/// (Studio Edit mode) — replays StructuredLearningConnectionExtractionService
/// on demand (e.g. after tightening the extraction agent's own instructions),
/// without re-running the whole pipeline. REPLACES (2026-08-17) this
/// section's existing connections and Knowledge Facts with a fresh
/// extraction — see StructuredLearningConnectionExtractionService's doc
/// comment for why this is no longer purely additive.</summary>
public sealed class GenerateStructuredLearningConnectionsFunction
{
    private readonly StructuredLearningConnectionExtractionService _extractionService;
    private readonly HumanOsDbContext _dbContext;

    public GenerateStructuredLearningConnectionsFunction(
        StructuredLearningConnectionExtractionService extractionService, HumanOsDbContext dbContext)
    {
        _extractionService = extractionService;
        _dbContext = dbContext;
    }

    [Function("GenerateStructuredLearningConnections")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/knowledge-graph/generate")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_extractionService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "ConnectionExtractionNotConfigured",
                "Knowledge Graph connection extraction is not configured.", cancellationToken);
        }

        var sectionExists = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .AnyAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken);
        if (!sectionExists)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", "No StructuredLearningDocumentSection was found with that id.", cancellationToken);
        }

        int created;
        try
        {
            created = await _extractionService.ExtractForSectionAsync(sectionId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.UnprocessableEntity, "ConnectionExtractionFailed", exception.Message, cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, new GenerateStructuredLearningConnectionsResponseDto
        {
            SectionId = sectionId,
            ConnectionsCreated = created
        }, cancellationToken: cancellationToken);
    }
}
