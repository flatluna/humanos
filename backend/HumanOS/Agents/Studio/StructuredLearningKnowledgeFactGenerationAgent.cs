using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Step 3 of the Structured Learning pipeline: proposes Knowledge Facts from
/// one Engram's source text plus its already-validated Knowledge Graph.
/// This agent never persists facts or changes the graph.
/// </summary>
public class StructuredLearningKnowledgeFactGenerationAgent
{
    private static readonly IReadOnlySet<string> FactTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Definition", "Structure", "Relationship", "Principle", "Capability", "Process", "Function", "Outcome", "Application"
    };

    private static readonly IReadOnlySet<string> ImportanceValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "Core", "Supporting"
    };

    private static readonly IReadOnlySet<string> DerivationTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Atomic", "Composite"
    };

    private static readonly IReadOnlySet<string> EpistemicStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "explicit", "inferred"
    };

    private const string Instructions = """
        You generate proposed Knowledge Facts for one educational Engram.
        Return ONLY the requested StructuredLearningKnowledgeFactGenerationResponse object.
        Never return Markdown, commentary, or additional properties.

        SOURCE OF TRUTH:
        - SourceText determines which ideas are essential, useful, and worth teaching.
        - Entities provide the only valid concepts and IDs.
        - Connections provide the only valid relationship IDs and graph paths.
        Use both sources together. Do not generate facts mechanically for every connection and
        do not generate facts from SourceText alone when no graph support exists.
        Never add outside knowledge or invent IDs.

        FACT SELECTION:
        Generate 7-15 concise facts when the Engram is sufficiently dense, fewer when the text is
        short. Select the most important non-redundant ideas. Group closely related clauses and
        coordinated list items into one useful fact only when they can share a single continuous
        GraphPath (see GRAPH PATH below); do not emit a separate fact for every component of the
        same explanation when they do share one. Use Core for knowledge required to understand the
        Engram's main objective; use Supporting for examples, secondary use cases, products,
        organizations, and useful extensions. Ignore titles, module names, activity names, XP,
        duration, isolated headings, promotional copy, and educational containers.

        DEDUPLICATION (2026-08-17): never emit two facts for the same pair of entities where one
        is just the other's relationship stated in the reverse direction (e.g. "A requires B" and
        "B is required for A" are the SAME fact — emit it only once, in whichever direction reads
        more naturally as the primary relationship). Also never emit a general/umbrella fact (e.g.
        "X addresses the requirements for Y") alongside separate facts that already cover each of
        its parts individually (e.g. one fact for "X addresses the data needed for Y" and another
        for "X addresses the infrastructure needed for Y") — keep only the more specific per-part
        facts and drop the umbrella one, unless the umbrella fact has GraphPath/Evidence support
        that the per-part facts do not.

        FAN-IN / FAN-OUT COVERAGE (do not silently drop parallel edges): when several distinct
        connections share the same target entity (several elements each shaping/enabling one same
        outcome) or the same source entity (one element shaping/enabling several distinct
        outcomes), those connections are NOT redundant with each other and are NOT a continuous
        path — each one names a different concept, so each one MUST produce its own Atomic
        KnowledgeFact. Never treat "these connections look similar" or "same relationship label"
        as a reason to keep only some of them; if the SourceText and Connections support five
        parallel edges into/out of the same entity, generate five Atomic facts (or one fact per
        edge, each naming its own other entity), not two. Only omit a connection entirely when it
        is a true exact duplicate of another connection (same source, target, and label) or when it
        has no SourceText support at all (then use MissingGraphFacts instead).

        FACT TYPES:
        FactType must be one of Definition, Structure, Relationship, Principle, Capability,
        Process, Function, Outcome, Application. Use Principle for a general rule, guideline,
        or condition that should guide decisions; Outcome only for a result or effect produced by
        an action; Function only for what a thing or method does or is used to do. Do not label a
        general business objective as Function or Principle unless the text supports that reading.
        Importance must be Core or Supporting.
        DerivationType is Atomic for exactly one connection, Composite for two or three
        semantically continuous connections. EpistemicStatus is explicit or inferred.
        Write every Statement in the requested Language, preserving canonical names where useful.

        LANGUAGE RULE (STRICT):
        When Language is 'es' (Spanish), EVERY single Statement in KnowledgeFacts and MissingGraphFacts
        MUST be written in clear, natural SPANISH (e.g. "El Artículo 1 establece...", "Las facultades de la UIF...", "El SAT aplica las sanciones...").
        Under NO circumstances write statements in English when Language is 'es'.
        Preserve canonical entity names naturally within the Spanish syntax.

        GRAPH PATH:
        Every KnowledgeFact must have Evidence from SourceText and graph support.
        Atomic facts have exactly one GraphPath step with StepOrder 1.
        Composite facts have two or three consecutive steps starting at 1, but create a Composite
        fact only when the supplied connections form a continuous path. Never force or invent a
        Composite fact when no two- or three-connection path exists; use an Atomic fact or
        MissingGraphFacts instead. A composite path is continuous: the TargetEntityId of each
        connection equals the SourceEntityId of the next.
        GraphPath contains only ConnectionId and StepOrder. SourceConnectionIds must contain the
        same connection IDs in the same order, without duplicates. SourceEntityIds must contain
        only valid entity IDs used by the path, without duplicates.
        Use only ConnectionId and EntityId values present in the input.

        EVIDENCE AND MISSING GRAPH FACTS:
        Evidence must be one or more literal contiguous fragments from SourceText with enough
        context to support the complete Statement; never invent or paraphrase evidence and never
        use only a title or fragment without a predicate. For Composite facts, evidence must
        support the combined path. If SourceText contains an important claim that cannot be
        supported by the supplied connections, do not put it in KnowledgeFacts. Put it in
        MissingGraphFacts with Statement, Reason, EpistemicStatus, and Confidence instead.

        The response must preserve EngramId, EngramName, and Language exactly as received.
        Confidence values must be between 0 and 1. Do not generate questions, Recall, Apply it,
        Assessment, learner responses, mastery, pedagogy instructions, or persistence metadata.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;
    private readonly bool _isReasoningModel;
    private readonly int? _maxOutputTokens;

    public StructuredLearningKnowledgeFactGenerationAgent(IConfiguration configuration)
    {
        var endpoint = configuration["ClaudeConnectionAgentEndpoint"];
        var deploymentName = configuration["ClaudeConnectionAgentDeploymentName"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            _modelName = string.Empty;
            return;
        }

        _modelName = deploymentName;
        _isReasoningModel = deploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
            && !deploymentName.Contains("chat", StringComparison.OrdinalIgnoreCase);
        _maxOutputTokens = int.TryParse(configuration["ClaudeConnectionAgentMaxTokens"], out var maxTokens)
            ? maxTokens
            : null;
        var apiKey = configuration["ClaudeConnectionAgentApiKey"];
        var client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

#pragma warning disable OPENAI001
        _agent = client
            .GetResponsesClient()
            .AsIChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningKnowledgeFactGenerationAgent");
#pragma warning restore OPENAI001
    }

    public virtual bool IsConfigured => _agent is not null;

    public virtual async Task<StructuredLearningKnowledgeFactGenerationResponse> GenerateAsync(
        StructuredLearningKnowledgeFactGenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (response, _) = await RunModelAsync(request, cancellationToken);
        ValidateResponse(request, response);
        return response;
    }

    /// <summary>Unvalidated variant used by callers (e.g. the persistence
    /// service) that need to drop individually invalid facts instead of
    /// failing the whole batch when the model puts one bad fact alongside
    /// otherwise-valid ones.</summary>
    public virtual async Task<StructuredLearningKnowledgeFactGenerationResponse> GenerateRawAsync(
        StructuredLearningKnowledgeFactGenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (response, _) = await GenerateRawWithUsageAsync(request, cancellationToken);
        return response;
    }

    /// <summary>Same call as <see cref="GenerateRawAsync"/>, also returning the token usage
    /// that produced it — used to persist a StructuredLearningGenerationUsage row per call.</summary>
    public virtual async Task<(StructuredLearningKnowledgeFactGenerationResponse Response, AgentTokenUsage Usage)> GenerateRawWithUsageAsync(
        StructuredLearningKnowledgeFactGenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await RunModelAsync(request, cancellationToken);
    }

    protected virtual async Task<(StructuredLearningKnowledgeFactGenerationResponse Response, AgentTokenUsage Usage)> RunModelAsync(
        StructuredLearningKnowledgeFactGenerationRequest request,
        CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningKnowledgeFactGenerationAgent is not configured.");
        }

        var langReminder = string.Equals(request.Language, "es", StringComparison.OrdinalIgnoreCase)
            ? "MANDATORY LANGUAGE: Language is 'es' (Spanish). Every single Statement in KnowledgeFacts and MissingGraphFacts MUST be written in fluent SPANISH. Do NOT write in English.\n\n"
            : string.Empty;
        var prompt = langReminder + JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        ChatClientAgentRunOptions? runOptions = null;
        if (_isReasoningModel)
        {
            runOptions = new ChatClientAgentRunOptions(new ChatOptions
            {
#pragma warning disable OPENAI001
                RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    ReasoningEffortLevel = ChatReasoningEffortLevel.Medium
                }
#pragma warning restore OPENAI001
            });
        }
        else if (_maxOutputTokens is int maxOutputTokens)
        {
            runOptions = new ChatClientAgentRunOptions(new ChatOptions
            {
                RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    MaxOutputTokenCount = maxOutputTokens
                }
            });
        }

        var response = await _agent.RunAsync<StructuredLearningKnowledgeFactGenerationResponse>(
            prompt, options: runOptions, cancellationToken: cancellationToken);
        var usage = response.Usage;
        return (response.Result, new AgentTokenUsage
        {
            AgentName = "StructuredLearningKnowledgeFactGenerationAgent",
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        });
    }

    public static void ValidateResponse(
        StructuredLearningKnowledgeFactGenerationRequest request,
        StructuredLearningKnowledgeFactGenerationResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        if (response.EngramId != request.EngramId || response.EngramName != request.EngramName || response.Language != request.Language)
        {
            throw new InvalidOperationException("Knowledge Fact response identity does not match the request.");
        }

        var entityIds = request.Entities.Select(entity => entity.EntityId).ToHashSet();
        var connections = request.Connections.ToDictionary(connection => connection.ConnectionId);
        var statements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fact in response.KnowledgeFacts)
        {
            if (string.IsNullOrWhiteSpace(fact.Statement) || !statements.Add(Normalize(fact.Statement)))
            {
                throw new InvalidOperationException("Knowledge Facts must have unique, non-empty Statements.");
            }
            if (!FactTypes.Contains(fact.FactType) || !ImportanceValues.Contains(fact.Importance) ||
                !DerivationTypes.Contains(fact.DerivationType) || !EpistemicStatuses.Contains(fact.EpistemicStatus) ||
                fact.Confidence is < 0 or > 1 || fact.Evidence.Count == 0 || fact.Evidence.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException($"Knowledge Fact '{fact.Statement}' has invalid confidence or evidence.");
            }
            fact.Evidence = fact.Evidence
                .Select(NormalizeEvidence)
                .Where(evidence => evidence.Length > 0)
                .ToList();
            if (fact.Evidence.Count == 0)
            {
                throw new InvalidOperationException($"Knowledge Fact '{fact.Statement}' has no usable evidence.");
            }
            if (fact.SourceEntityIds.Any(id => !entityIds.Contains(id)) || fact.SourceEntityIds.Distinct().Count() != fact.SourceEntityIds.Count)
            {
                throw new InvalidOperationException($"Knowledge Fact '{fact.Statement}' contains invalid or duplicate EntityIds.");
            }
            if (fact.GraphPath.Count is < 1 or > 3 || fact.GraphPath.Select(step => step.StepOrder).SequenceEqual(Enumerable.Range(1, fact.GraphPath.Count)) is false)
            {
                throw new InvalidOperationException($"Knowledge Fact '{fact.Statement}' has a non-consecutive GraphPath.");
            }
            if (fact.DerivationType == "Atomic" && fact.GraphPath.Count != 1 ||
                fact.DerivationType == "Composite" && fact.GraphPath.Count is < 2 or > 3 ||
                fact.DerivationType is not "Atomic" and not "Composite")
            {
                throw new InvalidOperationException($"Knowledge Fact '{fact.Statement}' has an invalid DerivationType or path length.");
            }
            var pathConnectionIds = fact.GraphPath.Select(step => step.ConnectionId).ToList();
            if (pathConnectionIds.Distinct().Count() != pathConnectionIds.Count || !pathConnectionIds.SequenceEqual(fact.SourceConnectionIds))
            {
                throw new InvalidOperationException($"Knowledge Fact '{fact.Statement}' has inconsistent SourceConnectionIds.");
            }
            if (pathConnectionIds.Any(id => !connections.ContainsKey(id)))
            {
                throw new InvalidOperationException($"Knowledge Fact '{fact.Statement}' contains an unknown ConnectionId.");
            }
            for (var index = 1; index < pathConnectionIds.Count; index++)
            {
                var previous = connections[pathConnectionIds[index - 1]];
                var current = connections[pathConnectionIds[index]];
                if (previous.TargetEntityId != current.SourceEntityId)
                {
                    throw new InvalidOperationException($"Knowledge Fact '{fact.Statement}' has a discontinuous GraphPath.");
                }
            }
        }

        foreach (var missing in response.MissingGraphFacts)
        {
            if (string.IsNullOrWhiteSpace(missing.Statement) || missing.Confidence is < 0 or > 1)
            {
                throw new InvalidOperationException("MissingGraphFacts must have non-empty Statements and confidence between 0 and 1.");
            }
        }
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeEvidence(string value) =>
        string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
