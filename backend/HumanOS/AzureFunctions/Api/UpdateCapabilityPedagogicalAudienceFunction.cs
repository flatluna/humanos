using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio — lets a professor change an EXISTING capability's
/// tone/persona directive (Standard vs. "YoungLearner") after creation,
/// without needing direct DB access. Only affects FUTURE generated content
/// (new nodes added via AddCapabilityGraphNodeFunction) — never rewrites
/// already-persisted blueprint step text. See
/// <see cref="HumanOS.Models.Capabilities.Capability.PedagogicalAudience"/>.
/// </summary>
public sealed class UpdateCapabilityPedagogicalAudienceFunction
{
    private readonly CapabilityService _capabilityService;

    public UpdateCapabilityPedagogicalAudienceFunction(CapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    public sealed class UpdateRequest
    {
        /// <summary>Null/empty resets to "Standard". The only other
        /// supported value today is "YoungLearner".</summary>
        public string? PedagogicalAudience { get; set; }
    }

    [Function("UpdateCapabilityPedagogicalAudience")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "patch",
            Route = "capabilities/{capabilityId:guid}/pedagogical-audience")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<UpdateRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken) ?? new UpdateRequest();

        if (!string.IsNullOrWhiteSpace(body.PedagogicalAudience) &&
            !string.Equals(body.PedagogicalAudience, "YoungLearner", StringComparison.OrdinalIgnoreCase))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidPedagogicalAudience",
                "'pedagogicalAudience' must be null/empty (Standard) or 'YoungLearner'.", cancellationToken);
        }

        var updated = await _capabilityService.UpdatePedagogicalAudienceAsync(
            capabilityId, body.PedagogicalAudience, cancellationToken);

        if (!updated)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "CapabilityNotFound",
                "The requested capability was not found.", cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
