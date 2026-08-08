using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio review feature (2026-08-06) — removes ONE ExampleCard
/// (by index) from a node's Deep Dive step pool. Delete counterpart to
/// AddDeepDiveCardFunction/EditDeepDiveCardFunction — no LLM call needed,
/// no request body, every OTHER card in the pool is left completely
/// untouched.
/// </summary>
public sealed class DeleteDeepDiveCardFunction
{
    private readonly BlueprintReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public DeleteDeepDiveCardFunction(BlueprintReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("DeleteDeepDiveCard")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/nodes/{capabilityGraphNodeId:guid}/blueprint/deepdive/cards/{cardIndex:int}")]
        HttpRequestData request,
        Guid capabilityGraphNodeId,
        int cardIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.DeleteExampleCardAsync(_dbContext, capabilityGraphNodeId, cardIndex, cancellationToken);
            var response = BlueprintReviewApiMappers.ToDto(updated);
            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "DeepDiveCardNotFound", ex.Message, cancellationToken);
        }
    }
}
