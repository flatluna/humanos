using System.Net;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningEntityDto
{
    public Guid EntityId { get; set; }

    public string CanonicalName { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> Aliases { get; set; } = [];

    public string SourceText { get; set; } = string.Empty;

    public int? StartPosition { get; set; }

    public int? EndPosition { get; set; }
}

/// <summary>
/// Knowledge Graph (2026-08-14) — returns every entity extracted for one
/// Engram (StructuredLearningDocumentSection), with its per-occurrence
/// source evidence. Purely additive/read-only endpoint.
/// </summary>
public sealed class GetStructuredLearningEntitiesForSectionFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningEntitiesForSectionFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningEntitiesForSection")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/entities")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var entities = await _dbContext.StructuredLearningEntitySections
            .Where(l => l.StructuredLearningDocumentSectionId == sectionId)
            .Select(l => new StructuredLearningEntityDto
            {
                EntityId = l.StructuredLearningEntityId,
                CanonicalName = l.Entity.Name,
                EntityType = l.Entity.EntityType,
                Description = l.Entity.Description,
                Aliases = l.Entity.Aliases,
                SourceText = l.SourceText,
                StartPosition = l.StartPosition,
                EndPosition = l.EndPosition
            })
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, entities, cancellationToken: cancellationToken);
    }
}

public sealed class StructuredLearningEngramSummaryDto
{
    public Guid EngramId { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid SchemaId { get; set; }

    public string SchemaName { get; set; } = string.Empty;

    public string SourceText { get; set; } = string.Empty;
}

/// <summary>Reverse lookup: every Engram (section) a given entity occurs
/// in, across every Schema (document) — same evidence preserved.</summary>
public sealed class GetStructuredLearningEngramsForEntityFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningEngramsForEntityFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningEngramsForEntity")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/entities/{entityId:guid}/engrams")]
        HttpRequestData request,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var engrams = await _dbContext.StructuredLearningEntitySections
            .Where(l => l.StructuredLearningEntityId == entityId)
            .Select(l => new StructuredLearningEngramSummaryDto
            {
                EngramId = l.StructuredLearningDocumentSectionId,
                Title = l.Section.SubchapterTitle,
                SchemaId = l.Section.StructuredLearningDocumentId,
                SchemaName = l.Section.Document.DocumentName,
                SourceText = l.SourceText
            })
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, engrams, cancellationToken: cancellationToken);
    }
}

/// <summary>Search the tenant-wide entity catalog by name/alias substring
/// (case-insensitive) — used by Studio's own entity browser UI, distinct
/// from the agent-facing semantic retrieval package below.</summary>
public sealed class SearchStructuredLearningEntitiesFunction
{
    private readonly HumanOsDbContext _dbContext;

    public SearchStructuredLearningEntitiesFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("SearchStructuredLearningEntities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/entities/search")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var tenantIdRaw = query["tenantId"];
        var term = query["q"] ?? string.Empty;

        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'tenantId' query parameter is required.", cancellationToken);
        }

        var normalizedTerm = term.Trim().ToLowerInvariant();
        // Aliases is a JSON-converted column (List<string>) — can't be
        // translated into SQL, so the alias filter runs client-side after
        // a tenant-scoped fetch (bounded catalog size, same rationale as
        // other Studio catalog reads).
        var tenantEntities = await _dbContext.StructuredLearningEntities
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var entities = tenantEntities
            .Where(e => normalizedTerm == "" ||
                        e.NormalizedValue.Contains(normalizedTerm) ||
                        e.Aliases.Any(a => a.ToLowerInvariant().Contains(normalizedTerm)))
            .Select(e => new StructuredLearningEntityDto
            {
                EntityId = e.StructuredLearningEntityId,
                CanonicalName = e.Name,
                EntityType = e.EntityType,
                Description = e.Description,
                Aliases = e.Aliases
            })
            .ToList();

        return await FunctionResponseFactory.SuccessResponseAsync(request, entities, cancellationToken: cancellationToken);
    }
}
