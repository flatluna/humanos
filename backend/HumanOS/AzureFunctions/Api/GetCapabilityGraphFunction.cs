using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// humanlearn (student UI), Paso 4 — Endpoint: GET FULL CAPABILITY GRAPH.
/// Wraps <see cref="GraphProgressionEngine.GetFullGraphAsync"/>. Returns
/// every node + edge of a Capability's graph, with each node's state
/// (Locked/Available/Mastered) already computed for the given person, so
/// the Capability Graph Map can render in a single request.
/// </summary>
public sealed class GetCapabilityGraphFunction
{
    private readonly GraphProgressionEngine _graphProgressionEngine;
    private readonly HumanOsDbContext _dbContext;

    public GetCapabilityGraphFunction(GraphProgressionEngine graphProgressionEngine, HumanOsDbContext dbContext)
    {
        _graphProgressionEngine = graphProgressionEngine;
        _dbContext = dbContext;
    }

    [Function("GetCapabilityGraph")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "capabilities/{capabilityId:guid}/graph")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);

        if (!Guid.TryParse(query["personId"], out var personId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "Query parameter personId is required.", cancellationToken);
        }

        var graph = await _graphProgressionEngine.GetFullGraphAsync(_dbContext, personId, capabilityId, cancellationToken);

        if (graph is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "GraphNotFound",
                $"No CapabilityGraph found for capability {capabilityId}.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, graph, cancellationToken: cancellationToken);
    }
}
