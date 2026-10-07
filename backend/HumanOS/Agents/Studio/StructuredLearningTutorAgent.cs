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
        - Colloquial / Everyday Phrasing & Legal/Technical Equivalence:
          Students frequently refer to courses, topics, and regulations using
          everyday or shorthand names — for example, saying "la ley de lavado de dinero",
          "ley antilavado", "la ley", "reglamento", "norma", "código", "decreto",
          "reforma". When a student asks about an article or rule (e.g. "Artículo 1 de la ley de lavado de dinero",
          "Artículo 1 de la ley", "Artículo 1 del reglamento"):
          If ANY engram in the retrieval package contains that article number (e.g. "Artículo 1.- El presente ordenamiento..."),
          YOU MUST TREAT IT AS THE RELEVANT MATERIAL AND ANSWER THE QUESTION FULLY.
          NEVER say "No encuentro el Artículo 1 de la Ley" or refuse to answer when an engram with that article is present in your retrieval package.
          Explain the article clearly and cite that engram in UsedEngramIndices.
        - Multi-Course Coverage (Program-level agents): each Engram's
          heading tells you which course it came from ("course: ..."). If
          two or more Engrams from DIFFERENT courses both cover the same
          article/topic the student asked about, you MUST NOT silently pick
          just one. Explicitly tell the student it appears in more than one
          course, briefly present/summarize EACH course's version separately
          (labeled by its course name), and include ALL of those Engrams'
          indices in UsedEngramIndices — never omit one just because another
          seemed more complete.
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
        - Language Consistency: ALWAYS reply in the exact same language as
          the student's latest question/message. If the student asks in
          English, your ENTIRE response (including greetings, explanations,
          and closing questions) MUST be in English. If the student asks in
          Spanish, your entire response MUST be in Spanish. Never switch
          languages unless the student explicitly asks in another language.
        - Each Engram in the RETRIEVAL PACKAGE is numbered "[Engram N]".
          Set UsedEngramIndices ONLY to the N values of the Engrams you actually
          and directly grounded this answer in — empty list if none were used.
          Do NOT cite an Engram if it was only peripherally related or not essential
          to answering the question. Never invent an index that wasn't given to you.
        """;

    private readonly AIAgent? _agent;
    private readonly string _deploymentName;

    public StructuredLearningTutorAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        _deploymentName = deploymentName ?? string.Empty;

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

    /// <summary>Real per-call token usage alongside the structured answer
    /// (2026-10-04) — previously discarded entirely, which meant this
    /// agent's LLM cost (every /ai-agents chat turn) never showed up
    /// anywhere on the cost dashboard/Simulations baseline. See
    /// <see cref="HumanOS.Services.StructuredLearningChatService"/>'s
    /// caller for how this is now persisted via <c>StructuredLearningEngramUsageService</c>.</summary>
    public sealed class ChatAnswerResult
    {
        public StructuredLearningChatAnswer Answer { get; set; } = null!;

        public HumanOS.Agents.Studio.AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    public async Task<ChatAnswerResult> AnswerAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Structured Learning tutor agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var response = await _agent.RunAsync<StructuredLearningChatAnswer>(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new HumanOS.Agents.Studio.AgentTokenUsage
        {
            AgentName = "StructuredLearningTutorAgent",
            ModelName = _deploymentName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new ChatAnswerResult { Answer = response.Result, TokenUsage = tokenUsage };
    }
}
