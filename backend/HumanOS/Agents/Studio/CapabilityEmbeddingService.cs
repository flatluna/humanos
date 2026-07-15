using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Embeddings;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Wraps the Azure OpenAI embeddings deployment (text-embedding-ada-002,
/// 1536 dimensions) used to index Human OS Studio content into
/// CapabilityKnowledgeChunk's native Azure SQL VECTOR column. Configured
/// via the same flat 'AzureOpenAIEndpoint'/'AzureOpenAIApiKey' settings as
/// the other Studio agents, plus 'AzureOpenAIEmbeddingDeploymentName'.
/// </summary>
public sealed class CapabilityEmbeddingService
{
    private readonly EmbeddingClient? _client;

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

    public bool IsConfigured => _client is not null;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            throw new InvalidOperationException(
                "The embedding service is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIEmbeddingDeploymentName' application settings.");
        }

        var response = await _client.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return response.Value.ToFloats().ToArray();
    }
}
