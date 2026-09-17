using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services;

public sealed class PerplexitySearchResult
{
    public string Answer { get; set; } = string.Empty;

    public List<string> Citations { get; set; } = [];
}

/// <summary>
/// Service to perform real-time web searches using the Perplexity API (Sonar model).
/// Used during AI edit/review to ground and enrich lesson content, cards, and diagrams with live web knowledge.
/// </summary>
public sealed class PerplexitySearchService
{
    private static readonly HttpClient HttpClient = new();
    private readonly string? _apiKey;
    private readonly ILogger<PerplexitySearchService> _logger;

    public PerplexitySearchService(IConfiguration configuration, ILogger<PerplexitySearchService> logger)
    {
        _apiKey = configuration["PerplexityApiKey"];
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    private sealed class PerplexityMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class PerplexityChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "sonar";

        [JsonPropertyName("messages")]
        public List<PerplexityMessage> Messages { get; set; } = [];

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.2;
    }

    private sealed class PerplexityChatChoice
    {
        [JsonPropertyName("message")]
        public PerplexityMessage? Message { get; set; }
    }

    private sealed class PerplexityChatResponse
    {
        [JsonPropertyName("choices")]
        public List<PerplexityChatChoice>? Choices { get; set; }

        [JsonPropertyName("citations")]
        public List<string>? Citations { get; set; }
    }

    /// <summary>
    /// Executes a search query using Perplexity AI API.
    /// </summary>
    public async Task<PerplexitySearchResult> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("PerplexitySearchService is not configured. Missing 'PerplexityApiKey' in settings.");
        }

        var requestPayload = new PerplexityChatRequest
        {
            Model = "sonar",
            Messages =
            [
                new PerplexityMessage
                {
                    Role = "system",
                    Content = "You are a concise academic and educational research assistant. Provide clear, accurate, fact-grounded pedagogical explanations and real-world application examples based on live search results."
                },
                new PerplexityMessage
                {
                    Role = "user",
                    Content = query
                }
            ],
            Temperature = 0.2
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.perplexity.ai/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey!.Trim());
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(requestPayload), System.Text.Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Perplexity API call failed ({StatusCode}): {Error}", response.StatusCode, err);
            throw new HttpRequestException($"Perplexity API request failed with status {response.StatusCode}: {err}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<PerplexityChatResponse>(json);

        var answer = parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        var citations = parsed?.Citations ?? [];

        return new PerplexitySearchResult
        {
            Answer = answer,
            Citations = citations
        };
    }
}
