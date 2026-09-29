using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// GET /people/{personId}/grow-with-us-searches — lists a person's saved
/// "Grow With Us" searches (most recent first), for the left-side panel
/// on the Advance Your Knowledge page. See <see cref="HumanOS.Services.GrowWithUsSavedSearchService"/>.
/// </summary>
public sealed class ListGrowWithUsSavedSearchesFunction
{
    private readonly GrowWithUsSavedSearchService _savedSearchService;

    public ListGrowWithUsSavedSearchesFunction(GrowWithUsSavedSearchService savedSearchService)
    {
        _savedSearchService = savedSearchService;
    }

    [Function("ListGrowWithUsSavedSearches")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/grow-with-us-searches")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var results = await _savedSearchService.GetListAsync(personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, results, cancellationToken: cancellationToken);
    }
}
