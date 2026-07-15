using System.Net;
using System.Text.Json;
using HumanOS.Agentic.Studio;
using HumanOS.Agents.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Submits a human reviewer's approve/reject decision for GATE 1 or GATE 2
/// of a running Human OS Studio capability-creation run, and resumes the
/// Workflow. Prototype endpoint (Postman-friendly) — the review screen in
/// humanstudio will call this same endpoint later. See
/// /memories/repo/humanstudio-multiagent-vision.md.
/// </summary>
public sealed class RespondToCapabilityCreationGateFunction
{
    private readonly CapabilityCreationOrchestrator _orchestrator;

    public RespondToCapabilityCreationGateFunction(CapabilityCreationOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public sealed class GateResponseRequest
    {
        public Guid SubjectId { get; set; }

        public bool Approved { get; set; }

        public string? Comments { get; set; }

        /// <summary>Gate 1 only: an optional edited/reduced blueprint to
        /// use instead of the one Arquitecto produced (e.g. trimmed to a
        /// couple of modules for a cheap smoke test). Ignored for Gate 2.</summary>
        public CapabilityBlueprint? RevisedBlueprint { get; set; }
    }

    [Function("RespondToCapabilityCreationGate")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "studio/capability-creation/{runId:guid}/respond")]
        HttpRequestData request,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<GateResponseRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "A gate response body is required.", cancellationToken);
        }

        try
        {
            var status = await _orchestrator.RespondAsync(
                runId, body.SubjectId, body.Approved, body.Comments, body.RevisedBlueprint, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "RunOrGateNotFound", ex.Message, cancellationToken);
        }
    }
}
