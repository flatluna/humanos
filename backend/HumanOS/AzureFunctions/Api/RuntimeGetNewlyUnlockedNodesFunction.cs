using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 5 (2026-07-17) — Endpoint: GET NEWLY UNLOCKED NODES.
/// Wraps <see cref="GraphProgressionEngine.GetNewlyUnlockedNodesAsync"/>.
/// Typically called right after CompleteNode — see
/// <see cref="RuntimeCompleteNodeFunction"/>, which already includes this
/// same data inline in its response for convenience; this standalone
/// endpoint exists so the UI (or a later re-check) can query it independently.
/// </summary>
public sealed class RuntimeGetNewlyUnlockedNodesFunction
{
    private readonly GraphProgressionEngine _graphProgressionEngine;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeGetNewlyUnlockedNodesFunction(GraphProgressionEngine graphProgressionEngine, HumanOsDbContext dbContext)
    {
        _graphProgressionEngine = graphProgressionEngine;
        _dbContext = dbContext;
    }

    [Function("RuntimeGetNewlyUnlockedNodes")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instructor-runtime/progression/newly-unlocked")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);

        if (!Guid.TryParse(query["personId"], out var personId)
            || !Guid.TryParse(query["capabilityId"], out var capabilityId)
            || !Guid.TryParse(query["completedCapabilityGraphNodeId"], out var completedCapabilityGraphNodeId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "Query parameters personId, capabilityId and completedCapabilityGraphNodeId are all required.", cancellationToken);
        }

        var newlyUnlocked = await _graphProgressionEngine.GetNewlyUnlockedNodesAsync(
            _dbContext, personId, capabilityId, completedCapabilityGraphNodeId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, GraphProgressionApiMappers.ToDto(newlyUnlocked), cancellationToken: cancellationToken);
    }
}
