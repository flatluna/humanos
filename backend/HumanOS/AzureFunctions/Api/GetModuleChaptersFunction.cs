using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Read-only chapter list for a single module — added 2026-07-16 so the
/// Runtime frontend can let a learner review a previously seen chapter
/// (e.g. "Introducción al álgebra") again, without starting a new Runtime
/// turn, touching session progress, or calling the Tutor Agent.
/// </summary>
public sealed class GetModuleChaptersFunction
{
    private readonly CapabilityService _capabilityService;

    public GetModuleChaptersFunction(CapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("GetModuleChapters")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "modules/{capabilityModuleId:guid}/chapters")]
        HttpRequestData request,
        Guid capabilityModuleId,
        CancellationToken cancellationToken)
    {
        var content = await _capabilityService.GetModuleChaptersAsync(capabilityModuleId, cancellationToken);

        if (content is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "ModuleNotFound",
                "The requested module was not found.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, content, cancellationToken: cancellationToken);
    }
}
