using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /people/{personId}/grow-with-us-searches/search — semantic search
/// over a person's saved "Grow With Us" searches (embeds the query and
/// ranks past searches by cosine distance; falls back to substring match
/// if embeddings aren't configured). See <see cref="GrowWithUsSavedSearchService.SearchAsync"/>.
/// </summary>
public sealed class SearchGrowWithUsSavedSearchesFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly GrowWithUsSavedSearchService _savedSearchService;

    public SearchGrowWithUsSavedSearchesFunction(GrowWithUsSavedSearchService savedSearchService)
    {
        _savedSearchService = savedSearchService;
    }

    public sealed class Request
    {
        public string Query { get; set; } = string.Empty;
    }

    [Function("SearchGrowWithUsSavedSearches")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/grow-with-us-searches/search")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        Request? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<Request>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Query))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "QueryRequired", "A 'query' is required.", cancellationToken);
        }

        var results = await _savedSearchService.SearchAsync(personId, body.Query, cancellationToken: cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, results, cancellationToken: cancellationToken);
    }
}
