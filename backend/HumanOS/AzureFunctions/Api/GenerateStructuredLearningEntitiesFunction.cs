using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class GenerateStructuredLearningEntitiesResponseDto
{
    public Guid SectionId { get; set; }
    public int LinksCreated { get; set; }
}

/// <summary>Manual "Regenerar entidades" trigger for one Engram (Studio
/// Edit mode) — replays StructuredLearningEntityExtractionService on
/// demand, e.g. when a section shows "No entities extracted for this
/// section" (a transient earlier failure — see
/// StructuredLearningEntityExtractionService.ExtractForSectionAsync's own
/// idempotency guarantee). Connections/Facts regeneration both need
/// entities to already exist as candidates, so this should run BEFORE
/// GenerateStructuredLearningConnectionsFunction for an empty section.</summary>
public sealed class GenerateStructuredLearningEntitiesFunction
{
    private readonly StructuredLearningEntityExtractionService _entityExtraction;
    private readonly HumanOsDbContext _dbContext;

    public GenerateStructuredLearningEntitiesFunction(
        StructuredLearningEntityExtractionService entityExtraction, HumanOsDbContext dbContext)
    {
        _entityExtraction = entityExtraction;
        _dbContext = dbContext;
    }

    [Function("GenerateStructuredLearningEntities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/entities/generate")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_entityExtraction.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "EntityExtractionNotConfigured",
                "Knowledge Graph entity extraction is not configured.", cancellationToken);
        }

        var sectionExists = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .AnyAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken);
        if (!sectionExists)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", "No StructuredLearningDocumentSection was found with that id.", cancellationToken);
        }

        var linksCreated = await _entityExtraction.ExtractForSectionAsync(sectionId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new GenerateStructuredLearningEntitiesResponseDto { SectionId = sectionId, LinksCreated = linksCreated }, cancellationToken: cancellationToken);
    }
}
