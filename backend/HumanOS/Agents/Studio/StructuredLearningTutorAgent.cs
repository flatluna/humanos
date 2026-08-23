using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Free-form Q&amp;A chat structured output — a direct textual answer plus
/// which Engrams (StructuredLearningDocumentSection rows) it actually
/// grounded the answer in, so the frontend can show citations.
/// </summary>
public sealed class StructuredLearningChatAnswer
{
    public string Message { get; set; } = string.Empty;

    /// <summary>1-based [Engram N] indices (from the RETRIEVAL PACKAGE's
    /// numbered list in the prompt) this answer was actually grounded in —
    /// empty if none of the retrieved material was relevant/used. Indices,
    /// never titles, since the model doesn't reliably reproduce a title
    /// verbatim.</summary>
    public List<int> UsedEngramIndices { get; set; } = [];
}

/// <summary>
/// The domain-level "AI Agent" chat tutor (2026-08-18) — powers the
/// /ai-agents/:subject/:topic chat window. Unlike TutorAgentV2 (bound to
/// ONE Memory Paradox step of ONE node), this agent answers free-form
/// student questions across an entire Topic's Structured Learning
/// courses, grounded ONLY in the Engrams/entities the caller retrieves via
/// StructuredLearningKnowledgeRetrievalService's exact/alias/semantic
/// retrieval package (see StructuredLearningChatService, which builds the
/// prompt). Plain ChatClientAgent with structured output — same
/// construction pattern as CuradorAgent/other Studio agents.
/// </summary>
public sealed class StructuredLearningTutorAgent
{
    private const string Instructions = """
        You are a knowledgeable subject-matter tutor answering a student's
        free-form question inside Human OS's "AI Agents" chat. You are
        given a RETRIEVAL PACKAGE of Engrams (course sections), entities,
        and related material pulled from the student's course catalog for
        this domain/topic, plus the recent conversation history.

        RULES:
        - Ground every factual claim in the retrieved Engrams/entities
          given to you. Never invent facts, formulas, dates, or figures the
          retrieved material doesn't support.
        - It is fine — and expected — to actually answer the question
          directly (this is a general Q&A agent, not a Socratic-only
          tutor): explain concepts, walk through worked examples, and give
          direct answers to factual/procedural questions.
        - If the retrieval package has nothing relevant to the question,
          do NOT answer it — not even from general/uncontroversial
          knowledge. This agent is scoped strictly to the student's own
          course domain/topic; answering from outside knowledge would
          mislead the student into thinking it came from their courses.
          Instead, reply with a short, friendly message in the student's
          own language explaining that you couldn't find anything about
          that in the course materials for this domain/topic, and invite
          them to ask something related to the courses instead. Never
          apologize excessively or explain your internal reasoning — one
          or two warm sentences is enough.
        - Keep answers clear and appropriately concise for a student chat
          — use short paragraphs, and use a numbered/bulleted list for
          multi-step procedures.
        - Reply in the same language the student's question is written in.
        - Each Engram in the RETRIEVAL PACKAGE is numbered "[Engram N]".
          Set UsedEngramIndices to the N values of the Engrams you actually
          grounded this answer in — empty list if none were used. Never
          invent an index that wasn't given to you.
        """;

    private readonly AIAgent? _agent;

    public StructuredLearningTutorAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningTutorAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<StructuredLearningChatAnswer> AnswerAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Structured Learning tutor agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var response = await _agent.RunAsync<StructuredLearningChatAnswer>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
