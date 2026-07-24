using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio review feature (2026-07-21) — returns EVERY step of a
/// node's Memory Paradox blueprint at once (Hypothesis/Teaching/Recall/
/// Production/Assessment), completely independent of any LearningSession.
/// Powers Capability Studio's "Demo" preview mode, where a reviewer can jump
/// freely between all 5 steps of any node (no locks, no sequential gate) to
/// inspect/approve the AI-generated content before publishing.
/// </summary>
public sealed class GetNodeBlueprintFunction
{
    private readonly BlueprintReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public GetNodeBlueprintFunction(BlueprintReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("GetNodeBlueprint")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/nodes/{capabilityGraphNodeId:guid}/blueprint")]
        HttpRequestData request,
        Guid capabilityGraphNodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var blueprint = await _service.GetBlueprintAsync(_dbContext, capabilityGraphNodeId, cancellationToken);
            var response = BlueprintReviewApiMappers.ToDto(capabilityGraphNodeId, blueprint);
            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "BlueprintNotFound", ex.Message, cancellationToken);
        }
    }
}
