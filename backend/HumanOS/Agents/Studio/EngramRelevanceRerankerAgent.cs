using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One candidate engram/section offered to the reranker — a
/// truncated excerpt only (never the whole section) to keep the call
/// cheap even with several candidates.</summary>
public sealed class EngramRerankCandidate
{
    /// <summary>Position in the caller's candidate list — echoed back so
    /// the caller can map the answer back without relying on title text.</summary>
    public int Index { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;
}

public sealed class EngramRerankRequest
{
    public string Question { get; set; } = string.Empty;

    public List<EngramRerankCandidate> Candidates { get; set; } = [];

    /// <summary>Max number of candidates the agent may keep.</summary>
    public int MaxResults { get; set; } = 3;
}

public sealed class EngramRerankCandidateScore
{
    public int Index { get; set; }

    /// <summary>Estimated probability/confidence (0 to 100) that this
    /// candidate section directly and substantially answers the question.</summary>
    public int ConfidencePercent { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public sealed class EngramRerankResponse
{
    /// <summary>Detailed scores per evaluated candidate.</summary>
    public List<EngramRerankCandidateScore> ScoredCandidates { get; set; } = [];

    /// <summary>Indices (matching <see cref="EngramRerankCandidate.Index"/>)
    /// of the candidates that actually answer the question with high confidence
    /// (ConfidencePercent >= 80%), ordered from highest confidence to lowest.
    /// May be empty if none of them meet the threshold.</summary>
    public List<int> RelevantIndices { get; set; } = [];
}

/// <summary>
/// Opt-in LLM reranking step (2026-09-04 / 2026-09-05) for the AI Agents chat's
/// knowledge retrieval: evaluates candidate course sections against the user's
/// question with a strict confidence threshold (>= 80% relevance). This separates
/// direct answers from sections that merely mention keywords or tangential topics.
/// </summary>
public sealed class EngramRelevanceRerankerAgent
{
    private const string Instructions = """
        You evaluate a list of candidate course sections to determine which
        ones DIRECTLY AND SUBSTANTIALLY ANSWER a student's question,
        applying a strict 80%+ confidence threshold.

        You receive the student's Question and a list of Candidates, each
        with an Index, a Title, and a short Excerpt of that section's text.

        For each candidate:
        - Score ConfidencePercent from 0 to 100:
          * 80–100%: The excerpt directly defines, explains, solves, or gives
            essential instructions for what was asked. If the student asks for a specific
            article, section, law, rule, or concept (e.g. "Artículo 1", "Art. 5", "Artículo 1.-", "Concepto X"),
            any candidate whose Title or Excerpt directly contains, defines, or quotes that
            specific article or concept MUST be scored 90–100%.
            Be flexible with colloquial, shorthand, and legal naming: students frequently say
            "ley de lavado de dinero", "ley antilavado", "la ley", "reglamento", or "norma" interchangeably.
            If the candidate excerpt contains the corresponding article from the course's regulatory/legal
            material (such as "Artículo 1.- El presente ordenamiento tiene por objeto..."), score it 90–100%.
          * 50–79%: The excerpt is topically related or provides context, but
            does NOT directly answer the specific question.
          * 0–49%: The excerpt only mentions keywords in passing, is about a
            different subtopic, or is a summary/conclusion that doesn't explain the core concept.

        In RelevantIndices:
        - Include ONLY candidates whose ConfidencePercent is >= 80%.
        - Order them from highest confidence to lowest (up to MaxResults).
        - If NO candidates reach 80% confidence, return an empty RelevantIndices list.
          Do NOT include weak, partial, or tangentially related candidates just to fill results.
        - Only return Index values that were actually present in the input.
        """;

    private readonly AIAgent? _agent;

    public EngramRelevanceRerankerAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Economy tier: this is a short classification/selection task, not
        // open-ended generation — same tier choice as OutlineTitleRefinementAgent.
        var deploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "EngramRelevanceRerankerAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<EngramRerankResponse> RerankAsync(
        EngramRerankRequest request, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "EngramRelevanceRerankerAgent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName'/'AzureOpenAIEconomyDeploymentName' application settings.");
        }

        var prompt = $"Question: {request.Question}\nMaxResults: {request.MaxResults}\n\n" +
            System.Text.Json.JsonSerializer.Serialize(request.Candidates);

        var response = await _agent.RunAsync<EngramRerankResponse>(prompt, cancellationToken: cancellationToken);

        // Defensive filtering only — never trust indices the model may
        // have hallucinated outside the candidates it was actually given.
        var validIndices = request.Candidates.Select(c => c.Index).ToHashSet();
        response.Result.RelevantIndices = response.Result.RelevantIndices
            .Where(validIndices.Contains)
            .Take(request.MaxResults)
            .ToList();

        return response.Result;
    }
}
