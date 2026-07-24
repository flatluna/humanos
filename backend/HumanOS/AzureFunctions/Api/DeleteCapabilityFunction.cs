using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Permanently deletes a capability and all its content. Exposed in
/// humanstudio's Capability Library ("⋮" menu -> Delete, behind a
/// type-to-confirm warning modal — see DeleteCapabilityModal.tsx). My
/// Courses (the student-facing app) does not expose this in the UI.
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
