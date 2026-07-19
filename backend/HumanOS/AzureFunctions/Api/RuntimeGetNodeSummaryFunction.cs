using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Read-only "what happened the last time I completed this node" recap,
/// shown when a person opens a node that is already Mastered on the map
/// (product decision, 2026-07-19 — reopening a Mastered node shows the
/// last attempt's results by default, it never silently starts a new
/// attempt; "practicar de nuevo" stays an explicit separate action). See
/// <see cref="InstructorRuntimeOrchestrator.GetNodeSummaryAsync"/>.
/// </summary>
public sealed class RuntimeGetNodeSummaryFunction
{
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeGetNodeSummaryFunction(InstructorRuntimeOrchestrator orchestrator, HumanOsDbContext dbContext)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
    }

    [Function("RuntimeGetNodeSummary")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instructor-runtime/nodes/summary")]
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
            var summary = await _orchestrator.GetNodeSummaryAsync(_dbContext, personId, capabilityGraphNodeId, cancellationToken);
            var response = RuntimeGraphApiMappers.ToNodeSummaryDto(summary);
            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotCompletedYet", ex.Message, cancellationToken);
        }
    }
}
