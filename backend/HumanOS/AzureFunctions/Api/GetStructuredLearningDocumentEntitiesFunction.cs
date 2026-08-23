using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningEntityOccurrenceDto
{
    public Guid EngramId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string SourceText { get; set; } = string.Empty;
}

public sealed class StructuredLearningDocumentEntityDto
{
    public Guid EntityId { get; set; }

    public string CanonicalName { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? OntologicalClass { get; set; }

    public List<string> Aliases { get; set; } = [];

    public List<StructuredLearningEntityOccurrenceDto> Occurrences { get; set; } = [];
}

/// <summary>
/// Knowledge Graph (2026-08-14) — document-wide view of every entity
/// mentioned anywhere in one Structured Learning Document, grouped by
/// canonical entity with all its per-section occurrences/evidence. Used by
/// the Studio UI's post-Step-2 "Grafo de conocimiento" results viewer.
/// Read-only, purely additive.
/// </summary>
public sealed class GetStructuredLearningDocumentEntitiesFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningDocumentEntitiesFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningDocumentEntities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/{documentId:guid}/entities")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var links = await _dbContext.StructuredLearningEntitySections
            .Where(l => l.Section.StructuredLearningDocumentId == documentId)
            .Select(l => new
            {
                l.StructuredLearningEntityId,
                l.Entity.Name,
                l.Entity.EntityType,
                l.Entity.Description,
                l.Entity.OntologicalClass,
                l.Entity.Aliases,
                EngramId = l.StructuredLearningDocumentSectionId,
                Title = l.Section.SubchapterTitle,
                l.SourceText
            })
            .ToListAsync(cancellationToken);

        var entities = links
            .GroupBy(l => l.StructuredLearningEntityId)
            .Select(g => new StructuredLearningDocumentEntityDto
            {
                EntityId = g.Key,
                CanonicalName = g.First().Name,
                EntityType = g.First().EntityType,
                Description = g.First().Description,
                OntologicalClass = g.First().OntologicalClass,
                Aliases = g.First().Aliases,
                Occurrences = g
                    .Select(l => new StructuredLearningEntityOccurrenceDto { EngramId = l.EngramId, Title = l.Title, SourceText = l.SourceText })
                    .ToList()
            })
            .OrderBy(e => e.EntityType).ThenBy(e => e.CanonicalName)
            .ToList();

        return await FunctionResponseFactory.SuccessResponseAsync(request, entities, cancellationToken: cancellationToken);
    }
}
