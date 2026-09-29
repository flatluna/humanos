using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class BuddyWebSearchRequest
{
    public string? Query { get; set; }
}

/// <summary>
/// Buddy (2026-09-28) — POST /humanos/buddy/web-search: backs the Realtime voice
/// session's <c>search_the_web</c> tool (see BuddyVoiceSessionFunction/BuddyContextBuilder).
/// The browser can't call Perplexity directly (no API key reaches the client), so the
/// Realtime data-channel tool call is answered by the frontend POSTing the model's own
/// search query here, and this backend does the real lookup via
/// <see cref="PerplexitySearchService"/> (Sonar model) — the same live-web-grounding
/// service already used by the Structured Learning pipeline — never invented, never
/// proxying anything the model can't already justify from real search results.
/// </summary>
public sealed class BuddyWebSearchFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly PerplexitySearchService _perplexityService;

    public BuddyWebSearchFunction(PerplexitySearchService perplexityService)
    {
        _perplexityService = perplexityService;
    }

    [Function("BuddyWebSearch")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "humanos/buddy/web-search")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_perplexityService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "WebSearchNotConfigured",
                "Buddy's web search is not configured (missing PerplexityApiKey setting).",
                cancellationToken);
        }

        BuddyWebSearchRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<BuddyWebSearchRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Query))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "QueryRequired", "A search query is required.", cancellationToken);
        }

        try
        {
            var result = await _perplexityService.SearchAsync(body.Query.Trim(), cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                new { text = string.IsNullOrWhiteSpace(result.Answer) ? "No relevant web results were found." : result.Answer },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "WebSearchFailed", "The web search could not be completed: " + ex.Message, cancellationToken);
        }
    }
}

