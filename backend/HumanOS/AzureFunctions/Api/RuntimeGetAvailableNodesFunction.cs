using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 5 (2026-07-17) — Endpoint: GET AVAILABLE NODES.
/// Wraps <see cref="GraphProgressionEngine.GetAvailableNodesAsync"/>.
/// </summary>
public sealed class RuntimeGetAvailableNodesFunction
{
    private readonly GraphProgressionEngine _graphProgressionEngine;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeGetAvailableNodesFunction(GraphProgressionEngine graphProgressionEngine, HumanOsDbContext dbContext)
    {
        _graphProgressionEngine = graphProgressionEngine;
        _dbContext = dbContext;
    }

    [Function("RuntimeGetAvailableNodes")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instructor-runtime/progression/available-nodes")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);

        if (!Guid.TryParse(query["personId"], out var personId) || !Guid.TryParse(query["capabilityId"], out var capabilityId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "Query parameters personId and capabilityId are both required.", cancellationToken);
        }

        var available = await _graphProgressionEngine.GetAvailableNodesAsync(_dbContext, personId, capabilityId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, GraphProgressionApiMappers.ToDto(available), cancellationToken: cancellationToken);
    }
}
