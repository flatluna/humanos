using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Embeddings;
using SharpToken;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Wraps the Azure OpenAI embeddings deployment (text-embedding-ada-002,
/// 1536 dimensions) used to index Human OS Studio content into
/// CapabilityKnowledgeChunk's native Azure SQL VECTOR column. Configured
/// via the same flat 'AzureOpenAIEndpoint'/'AzureOpenAIApiKey' settings as
/// the other Studio agents, plus 'AzureOpenAIEmbeddingDeploymentName'.
/// </summary>
public class CapabilityEmbeddingService
{
    private readonly EmbeddingClient? _client;

    /// <summary>text-embedding-ada-002's hard context limit (2026-11-04) —
    /// a whole chapter/section's raw text (this service's callers mostly
    /// embed section-level text for retrieval, not small snippets) can
    /// easily exceed this for an image-heavy or especially long chapter,
    /// which the Azure OpenAI API rejects outright with HTTP 400 instead
    /// of truncating for you. Leaves a small safety margin below the
    /// model's real 8191-token cap.</summary>
    private const int MaxEmbeddingInputTokens = 8000;

    private static readonly GptEncoding TokenEncoding = GptEncoding.GetEncoding("cl100k_base");

    public CapabilityEmbeddingService(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIEmbeddingDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _client = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _client = client.GetEmbeddingClient(deploymentName);
    }

    public virtual bool IsConfigured => _client is not null;

    /// <summary>Generates an embedding for <paramref name="text"/>, silently
    /// truncating it to <see cref="MaxEmbeddingInputTokens"/> tokens first
    /// (keeping the BEGINNING of the text, where a section's own title/
    /// opening content lives) if it's too long for the embedding model's
    /// context window — every caller embeds text purely for similarity
    /// search/retrieval, never for fidelity-critical storage (the full,
    /// untruncated text is always stored separately alongside the
    /// embedding), so losing the tail of an unusually long chapter is a
    /// safe trade-off against an outright HTTP 400 failure.</summary>
    public virtual async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            throw new InvalidOperationException(
                "The embedding service is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIEmbeddingDeploymentName' application settings.");
        }

        var tokens = TokenEncoding.Encode(text);
        if (tokens.Count > MaxEmbeddingInputTokens)
        {
            text = TokenEncoding.Decode(tokens.Take(MaxEmbeddingInputTokens).ToList());
        }

        var response = await _client.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return response.Value.ToFloats().ToArray();
    }
}
