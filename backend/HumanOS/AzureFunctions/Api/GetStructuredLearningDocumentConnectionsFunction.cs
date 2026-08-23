using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningConnectionDto
{
    public Guid ConnectionId { get; set; }

    public Guid SourceEntityId { get; set; }

    public string SourceEntityName { get; set; } = string.Empty;

    public Guid TargetEntityId { get; set; }

    public string TargetEntityName { get; set; } = string.Empty;

    public string RelationshipCategory { get; set; } = string.Empty;

    public string RelationshipLabel { get; set; } = string.Empty;

    public string? Evidence { get; set; }

    public Guid? SourceSectionId { get; set; }

    public string EpistemicStatus { get; set; } = "explicit";

    public double? Confidence { get; set; }
}

/// <summary>
/// Knowledge Graph edges (2026-08-14) — document-wide view of every
/// relationship extracted between entities in one Structured Learning
/// Document. Read-only, purely additive; mirrors
/// <see cref="GetStructuredLearningDocumentEntitiesFunction"/>'s exact
/// conventions.
/// </summary>
public sealed class GetStructuredLearningDocumentConnectionsFunction
{
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningDocumentConnectionsFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningDocumentConnections")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/{documentId:guid}/connections")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var connections = await _dbContext.StructuredLearningConnections
            .Where(c => c.StructuredLearningDocumentId == documentId)
            .Select(c => new StructuredLearningConnectionDto
            {
                ConnectionId = c.StructuredLearningConnectionId,
                SourceEntityId = c.SourceEntityId,
                SourceEntityName = c.SourceEntity.Name,
                TargetEntityId = c.TargetEntityId,
                TargetEntityName = c.TargetEntity.Name,
                RelationshipCategory = c.RelationshipCategory,
                RelationshipLabel = c.RelationshipLabel,
                Evidence = c.Evidence,
                SourceSectionId = c.SourceSectionId,
                EpistemicStatus = c.EpistemicStatus,
                Confidence = c.Confidence
            })
            .OrderBy(c => c.RelationshipCategory).ThenBy(c => c.SourceEntityName)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, connections, cancellationToken: cancellationToken);
    }
}
