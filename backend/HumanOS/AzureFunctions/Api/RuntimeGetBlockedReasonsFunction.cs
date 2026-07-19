using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 5 (2026-07-17) — Endpoint: GET BLOCKED REASONS.
/// Wraps <see cref="GraphProgressionEngine.GetBlockedReasonsAsync"/>.
/// CapabilityId is intentionally NOT a query parameter — it is resolved
/// internally from the CapabilityGraphNode itself, per spec.
/// </summary>
public sealed class RuntimeGetBlockedReasonsFunction
{
    private readonly GraphProgressionEngine _graphProgressionEngine;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeGetBlockedReasonsFunction(GraphProgressionEngine graphProgressionEngine, HumanOsDbContext dbContext)
    {
        _graphProgressionEngine = graphProgressionEngine;
        _dbContext = dbContext;
    }

    [Function("RuntimeGetBlockedReasons")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instructor-runtime/progression/blocked-reasons")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);

        if (!Guid.TryParse(query["personId"], out var personId)
            || !Guid.TryParse(query["capabilityGraphNodeId"], out var capabilityGraphNodeId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "Query parameters personId and capabilityGraphNodeId are both required.", cancellationToken);
        }

        try
        {
            var reasons = await _graphProgressionEngine.GetBlockedReasonsAsync(
                _dbContext, personId, capabilityGraphNodeId, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, new { blockedReasons = reasons }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotFound", ex.Message, cancellationToken);
        }
    }
}
