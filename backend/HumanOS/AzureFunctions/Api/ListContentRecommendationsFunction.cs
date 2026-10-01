using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /people/{personId}/content-recommendations — list a person's saved
/// content-recommendation searches, newest first, for the left-side panel (2026-09-29).</summary>
public sealed class ListContentRecommendationsFunction
{
    private readonly ContentRecommendationService _service;

    public ListContentRecommendationsFunction(ContentRecommendationService service)
    {
        _service = service;
    }

    [Function("ListContentRecommendations")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/content-recommendations")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var list = await _service.GetListAsync(personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, list, cancellationToken: cancellationToken);
    }
}

/// <summary>GET /people/{personId}/content-recommendations/{id} — reopen one past
/// recommendation report.</summary>
public sealed class GetContentRecommendationFunction
{
    private readonly ContentRecommendationService _service;

    public GetContentRecommendationFunction(ContentRecommendationService service)
    {
        _service = service;
    }

    [Function("GetContentRecommendation")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/content-recommendations/{savedSearchId:guid}")]
        HttpRequestData request,
        Guid personId,
        Guid savedSearchId,
        CancellationToken cancellationToken)
    {
        var saved = await _service.GetByIdAsync(personId, savedSearchId, cancellationToken);
        if (saved is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SavedSearchNotFound", "This saved search was not found.", cancellationToken);
        }

        var proposal = System.Text.Json.JsonSerializer.Deserialize<HumanOS.Agents.ContentRecommendationProposal>(saved.ResultJson)
            ?? new HumanOS.Agents.ContentRecommendationProposal();

        return await FunctionResponseFactory.SuccessResponseAsync(request, proposal, cancellationToken: cancellationToken);
    }
}
