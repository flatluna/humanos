using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>DELETE /responsibilities/{responsibilityId} — hard-deletes a catalog Responsibility.</summary>
public sealed class DeleteResponsibilityFunction
{
    private readonly ResponsibilityService _responsibilityService;

    public DeleteResponsibilityFunction(ResponsibilityService responsibilityService)
    {
        _responsibilityService = responsibilityService;
    }

    [Function("DeleteResponsibility")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "responsibilities/{responsibilityId:guid}")]
        HttpRequestData request,
        Guid responsibilityId,
        CancellationToken cancellationToken)
    {
        var deleted = await _responsibilityService.DeleteAsync(responsibilityId, cancellationToken);

        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ResponsibilityNotFound", "The requested responsibility was not found.", cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
