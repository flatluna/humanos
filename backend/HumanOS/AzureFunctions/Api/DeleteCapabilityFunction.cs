using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Permanently deletes a capability and all its content. Used for admin/
/// test cleanup today (e.g. removing smoke-test runs) — My Courses does
/// not expose this in the UI yet (no real "delete" action there, see
/// /memories/repo/frontend-backend-integration-validated.md).
/// </summary>
public sealed class DeleteCapabilityFunction
{
    private readonly CapabilityService _capabilityService;

    public DeleteCapabilityFunction(CapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("DeleteCapability")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "delete",
            Route = "capabilities/{capabilityId:guid}")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var deleted = await _capabilityService.DeleteAsync(capabilityId, cancellationToken);

        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "CapabilityNotFound",
                "The requested capability was not found.",
                cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
