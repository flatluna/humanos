using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// GET /people/{personId}/grow-with-us-searches/{savedSearchId} — retrieves
/// one saved "Grow With Us" search's full plan, for re-opening it from the
/// left-side panel without regenerating it.
/// </summary>
public sealed class GetGrowWithUsSavedSearchFunction
{
    private readonly GrowWithUsSavedSearchService _savedSearchService;

    public GetGrowWithUsSavedSearchFunction(GrowWithUsSavedSearchService savedSearchService)
    {
        _savedSearchService = savedSearchService;
    }

    public sealed class Response
    {
        public Guid GrowWithUsSavedSearchId { get; set; }

        public string Topic { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public GrowWithUsPlanProposal Plan { get; set; } = new();
    }

    [Function("GetGrowWithUsSavedSearch")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/grow-with-us-searches/{savedSearchId:guid}")]
        HttpRequestData request,
        Guid personId,
        Guid savedSearchId,
        CancellationToken cancellationToken)
    {
        var savedSearch = await _savedSearchService.GetByIdAsync(personId, savedSearchId, cancellationToken);
        if (savedSearch is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SavedSearchNotFound", "This saved search was not found.", cancellationToken);
        }

        var plan = JsonSerializer.Deserialize<GrowWithUsPlanProposal>(
            savedSearch.PlanJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new GrowWithUsPlanProposal();

        var response = new Response
        {
            GrowWithUsSavedSearchId = savedSearch.GrowWithUsSavedSearchId,
            Topic = savedSearch.Topic,
            CreatedDate = savedSearch.CreatedDate,
            Plan = plan,
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
