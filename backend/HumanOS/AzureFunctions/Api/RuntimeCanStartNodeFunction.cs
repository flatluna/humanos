using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 5 (2026-07-17) — Endpoint: CAN START NODE.
/// Wraps <see cref="GraphProgressionEngine.CanStartNodeAsync"/>. Generic
/// across any Capability domain — never assumes math/language/etc.
/// </summary>
public sealed class RuntimeCanStartNodeFunction
{
    private readonly GraphProgressionEngine _graphProgressionEngine;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeCanStartNodeFunction(GraphProgressionEngine graphProgressionEngine, HumanOsDbContext dbContext)
    {
        _graphProgressionEngine = graphProgressionEngine;
        _dbContext = dbContext;
    }

    [Function("RuntimeCanStartNode")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instructor-runtime/progression/can-start-node")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);

        if (!Guid.TryParse(query["personId"], out var personId)
            || !Guid.TryParse(query["capabilityId"], out var capabilityId)
            || !Guid.TryParse(query["capabilityGraphNodeId"], out var capabilityGraphNodeId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "Query parameters personId, capabilityId and capabilityGraphNodeId are all required.", cancellationToken);
        }

        var result = await _graphProgressionEngine.CanStartNodeAsync(
            _dbContext, personId, capabilityId, capabilityGraphNodeId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, GraphProgressionApiMappers.ToDto(result), cancellationToken: cancellationToken);
    }
}
