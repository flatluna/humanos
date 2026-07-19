using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 4 (2026-07-17) — Endpoint 7: COMPLETE NODE.
/// Closes out a node once its Assessment step is done, via
/// <see cref="InstructorRuntimeOrchestrator.CompleteNodeAsync"/>.
/// Deliberately does NOT touch <c>LearningSession.Status</c> — that is
/// explicitly out of scope / known deferred tech debt for this Paso, per
/// the spec.
///
/// Runtime V1, Paso 5 (2026-07-17) addition: after completing the node, this
/// endpoint also calls <see cref="GraphProgressionEngine.GetNewlyUnlockedNodesAsync"/>
/// and includes the result inline in the response — the natural, single
/// place the "graph unlocked something new" signal should surface, since it
/// is a direct consequence of THIS completion. Purely additive: existing
/// consumers that only read <c>success</c> are unaffected.
/// </summary>
public sealed class RuntimeCompleteNodeFunction
{
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly GraphProgressionEngine _graphProgressionEngine;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeCompleteNodeFunction(
        InstructorRuntimeOrchestrator orchestrator,
        GraphProgressionEngine graphProgressionEngine,
        HumanOsDbContext dbContext)
    {
        _orchestrator = orchestrator;
        _graphProgressionEngine = graphProgressionEngine;
        _dbContext = dbContext;
    }

    [Function("RuntimeCompleteNode")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/nodes/complete")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        CompleteRuntimeNodeRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<CompleteRuntimeNodeRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.LearningSessionNodeId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "LearningSessionNodeId is required.", cancellationToken);
        }

        try
        {
            await _orchestrator.CompleteNodeAsync(_dbContext, body.LearningSessionNodeId, cancellationToken);

            var completedNode = await _dbContext.LearningSessionNodes
                .AsNoTracking()
                .Include(n => n.LearningSession)
                .FirstOrDefaultAsync(n => n.LearningSessionNodeId == body.LearningSessionNodeId, cancellationToken);

            var newlyUnlocked = completedNode?.LearningSession is null
                ? new List<GraphProgressionEngine.GraphNodeInfo>()
                : await _graphProgressionEngine.GetNewlyUnlockedNodesAsync(
                    _dbContext,
                    completedNode.LearningSession.PersonId,
                    completedNode.LearningSession.CapabilityId,
                    completedNode.CapabilityGraphNodeId,
                    cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                new { success = true, newlyUnlockedNodes = GraphProgressionApiMappers.ToDto(newlyUnlocked) },
                cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotCompletable", ex.Message, cancellationToken);
        }
    }
}
