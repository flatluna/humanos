using System.Net;
using System.Text.Json;
using HumanOS.Agentic.Studio;
using HumanOS.Agents.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Starts a new Human OS Studio capability-creation run: Curador -&gt;
/// Arquitecto, then pauses at GATE 1 for human review. Prototype endpoint
/// (Postman-friendly) — see
/// /memories/repo/humanstudio-multiagent-vision.md.
/// </summary>
public sealed class StartCapabilityCreationFunction
{
    private readonly CapabilityCreationOrchestrator _orchestrator;

    public StartCapabilityCreationFunction(CapabilityCreationOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public sealed class StartRequest
    {
        public Guid CapabilityDomainId { get; set; }

        public string CapabilityGoal { get; set; } = string.Empty;

        public List<RawMaterialItem> RawMaterials { get; set; } = [];
    }

    [Function("StartCapabilityCreation")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/capability-creation/start")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "StudioAgentsNotConfigured",
                "The Human OS Studio pipeline agents are not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        var body = await JsonSerializer.DeserializeAsync<StartRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (body is null || body.CapabilityDomainId == Guid.Empty ||
            string.IsNullOrWhiteSpace(body.CapabilityGoal) || body.RawMaterials.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidRequest",
                "'capabilityDomainId', 'capabilityGoal' and at least one 'rawMaterials' item are required.",
                cancellationToken);
        }

        var status = await _orchestrator.StartAsync(
            body.CapabilityDomainId, body.CapabilityGoal, body.RawMaterials, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
    }
}
