using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio — professor/author tool: permanently deletes ONE node
/// from an already-persisted CapabilityGraph (its edges, illustrations,
/// blueprint(s), knowledge chunks/expansion cache, and any per-node learning
/// progress — see CapabilityGraphPersistenceService.DeleteNodeAsync for the
/// exact cascade/delete order). Added 2026-07-31 so a professor can clean up
/// a node whose content generation never finished (e.g. left half-built by a
/// backend crash) without having to delete the entire capability. Exposed
/// behind a type-to-confirm warning modal in the UI (DeleteGraphNodeModal.tsx)
/// — irreversible, never called without explicit user confirmation.
/// </summary>
public sealed class DeleteCapabilityGraphNodeFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphPersistenceService _persistenceService;

    public DeleteCapabilityGraphNodeFunction(
        HumanOsDbContext dbContext,
        CapabilityGraphPersistenceService persistenceService)
    {
        _dbContext = dbContext;
        _persistenceService = persistenceService;
    }

    [Function("DeleteCapabilityGraphNode")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "delete",
            Route = "capabilities/{capabilityId:guid}/graph/nodes/{nodeId:guid}")]
        HttpRequestData request,
        Guid capabilityId,
        Guid nodeId,
        CancellationToken cancellationToken)
    {
        var nodeBelongsToCapability = await _dbContext.CapabilityGraphNodes
            .AsNoTracking()
            .AnyAsync(
                n => n.CapabilityGraphNodeId == nodeId && n.CapabilityGraph!.CapabilityId == capabilityId,
                cancellationToken);

        if (!nodeBelongsToCapability)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "NodeNotFound",
                $"No node {nodeId} found in capability {capabilityId}'s graph.",
                cancellationToken);
        }

        var deleted = await _persistenceService.DeleteNodeAsync(_dbContext, nodeId, cancellationToken);

        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "NodeNotFound",
                "The requested node was not found.",
                cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
