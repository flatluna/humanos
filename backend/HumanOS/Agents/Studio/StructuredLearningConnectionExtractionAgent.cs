using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One relationship found between two ALREADY-KNOWN entities in a
/// section's text — see <see cref="StructuredLearningConnectionExtractionAgent"/>.</summary>
public sealed class ExtractedConnectionDto
{
    /// <summary>Must exactly match one of the candidate entities' Id passed in the
    /// prompt (short id, e.g. "e3") — never a new/invented id or an entity name.</summary>
    public string SourceEntityId { get; set; } = string.Empty;

    public string TargetEntityId { get; set; } = string.Empty;

    /// <summary>"Sequence" (A happens/is done before B — ordered checklist/
    /// procedure steps), "Requires" (A cannot happen without B), "PartOf"
    /// (A is a component/member of B), "IsRoleOf" (A is a ROLE that only
    /// makes sense as something a class-of-being B plays — e.g.
    /// "Contribuyente" --IsRoleOf--> "Persona física"/"Persona moral"; a
    /// role is NEVER its own ontological class, always tie it back to WHO
    /// or WHAT plays it), or "Generic" (anything else — use
    /// RelationshipLabel to describe it).</summary>
    public string RelationshipCategory { get; set; } = "Generic";

    /// <summary>Free-text description of the relationship, e.g. "ayuda a",
    /// "requiere", "es miembro de". Always required, even for non-Generic
    /// categories, so the edge is human-readable on its own.</summary>
    public string RelationshipLabel { get; set; } = string.Empty;

    /// <summary>Exact verbatim quote from the section's own text supporting
    /// this relationship — never paraphrased.</summary>
    public string Evidence { get; set; } = string.Empty;

    /// <summary>Epistemic origin (2026-08-15, GraphRAG-style provenance) —
    /// applies to ANY domain (law, contract, math, history, or other), never
    /// only to legal articles: "explicit" (directly stated by this section's
    /// own text), "inferred" (reasonably deduced from this text, not stated
    /// verbatim), "referenced" (this text mentions/points to another
    /// entity/document without actually stating the relationship's content —
    /// e.g. "as defined in Article 5" without Article 5's text being given
    /// here), "attributed" (the text reports this as someone else's claim/
    /// definition, not asserting it directly itself — e.g. "according to the
    /// plaintiff..."), or "hypothetical" (a conditional/not-yet-triggered
    /// relationship — e.g. "if X occurs, then Y applies", true only under a
    /// stated condition, not an unconditional fact). NEVER invent what a
    /// referenced-but-unseen article/section/document actually says.</summary>
    public string EpistemicStatus { get; set; } = "explicit";

    /// <summary>0.0-1.0 confidence this relationship is correctly grounded
    /// in the section's text.</summary>
    public double Confidence { get; set; } = 1.0;
}

public sealed class StructuredLearningConnectionExtractionResponse
{
    public List<ExtractedConnectionDto> Connections { get; set; } = [];

    /// <summary>The single entity (from the candidate list) that best acts as the
    /// conceptual "hub"/root of this section's knowledge graph — the one most other
    /// entities relate to or depend on, chosen BY THE MODEL rather than by degree-count
    /// heuristics on the client. Must exactly match a candidate entity Id. Empty if
    /// no single entity stands out as a clear hub.</summary>
    public string RootEntityId { get; set; } = string.Empty;
}

/// <summary>Extraction result + the token usage that produced it — same
/// shape as <see cref="StructuredLearningEntityExtractionResult"/>.</summary>
public sealed class StructuredLearningConnectionExtractionResult
{
    public StructuredLearningConnectionExtractionResponse Response { get; set; } = new();

    public string ModelName { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int CachedInputTokens { get; set; }
}

/// <summary>
/// Knowledge Graph edges (2026-08-14) — extracts relationships BETWEEN
/// entities <see cref="StructuredLearningEntityExtractionAgent"/> already
/// found in one section's text. Runs strictly AFTER entity extraction for
/// that section, since it's only ever given the section's own already-
/// extracted entity names as the closed candidate list — never allowed to
/// introduce a relationship to/from an entity name outside that list, so
/// every edge is guaranteed to resolve to a real
/// <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningEntity"/>
/// row. See Services/StructuredLearningConnectionExtractionService.cs for
/// how the returned connections are resolved/persisted.
/// </summary>
public class StructuredLearningConnectionExtractionAgent
{
    public static IReadOnlySet<string> RelationshipCategories { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "IsA", "PartOf", "HasComponent", "Causes", "Precedes", "Requires", "Enables",
        "Supports", "ContrastsWith", "SimilarTo", "AppliesTo", "UsedFor", "Produces", "Processes",
        "LocatedIn", "RelatedTo"
    };

    public static string NormalizeRelationshipCategory(string? category) =>
        category is not null && RelationshipCategories.Contains(category.Trim())
            ? category.Trim()
            : "RelatedTo";

    private const string Instructions = """
        You build the RELATIONS half of a GraphRAG-style knowledge graph for one excerpt of text —
        entities (the "nodes") were already extracted separately; your job is to find the real,
        semantically meaningful edges BETWEEN a given CLOSED list of entities (each with a short Id
        and a Name). Think like a GraphRAG extractor: relationships should read like real-world
        semantic predicates (e.g. "is built on", "requires", "precedes", "is part of"), grounded
        strictly in what the text says — never invent an entity outside the given list, never
        invent a relationship the text doesn't support.

        Rules:
        1. SourceEntityId and TargetEntityId MUST be one of the candidate Ids given in the prompt
           (e.g. "e3") — never the entity's Name, never an id not in the list. If a relationship
           involves an entity NOT in the candidate list, skip it.
          2. Only extract relationships the text actually supports. Rhetorical or contrastive
              wording alone is not semantic evidence: phrases such as "more than", "not merely",
              "rather than", or "isn't just" do not imply ContrastsWith, IsA, PartOf, or
              HasComponent unless the text separately states that relation. In particular, never
              create AI --ContrastsWith--> algorithms solely from "AI is more than a set of
              algorithms".
          3. RelationshipCategory must use this small, universal taxonomy:
              "IsA" (A is a kind/type of B), "PartOf" (A belongs to B),
              "HasComponent" (B contains A as a component), "Causes" (A produces B),
              "Precedes" (A occurs before B), "Requires" (A depends on B),
              "Enables" (A makes B possible), "Supports" (A helps sustain or justify B),
              "ContrastsWith" (A is opposed to or differs from B), "SimilarTo" (A resembles B),
              "AppliesTo" (A governs or is applicable to B), "UsedFor" (A is used for B),
               "Produces" (A generates B), "Processes" (A acts on, analyzes, or transforms B),
               "LocatedIn" (A is situated in B), or
              "RelatedTo" as the LAST RESORT when no more precise category is supported.
              Do not use domain-specific categories and never use "Generic".
        4. RelationshipLabel is always required — a short label written in the TARGET LANGUAGE given
           in the prompt ("Target language: ...") — NEVER default to Spanish regardless of what
           language the illustrative examples elsewhere in these instructions happen to use. Prefer
           a SPECIFIC, directed predicate over a vague one — e.g. English "regulates", "imposes an
           obligation on", "grants a right to", "prohibits", "requires", "is part of", "occurred
           in", "caused", "preceded", "uses formula", "applies rate", "is calculated from" (or the
           equivalent phrasing in the target language) — instead of a generic "related to" whenever
           a more precise predicate is supported by the text. These are illustrative examples across
           ANY domain (law, contract, math, history, general) — not a fixed enum, pick or coin
           whatever specific predicate the text actually supports, always in the target language.
          5. Evidence is a contiguous verbatim snippet from the text supporting this edge — never
              invented or paraphrased. Include enough surrounding context to prove the predicate and
              its endpoints, normally up to ~35 words when needed. Do not use an isolated heading or
              fragment such as "New, high value use cases" to support a relation to Innovation.
        6. Never duplicate the same (SourceEntityId, TargetEntityId, RelationshipLabel) triple.
        6a. Never emit BOTH directions of the same real-world relationship between the same pair
            of entities — e.g. "A requires B" (A->B, Requires) and its passive mirror "B is
            required for A" (B->A, RelatedTo/Requires) describe one single fact, not two. Before
            adding an edge, check whether an edge already exists between that same entity pair (in
            either direction) that already expresses this relationship; if so, skip the new one
            instead of adding its inverse. This applies to every RelationshipCategory and every
            domain — not just "Requires".
        7. If no relationships are supported, return an empty list.
        7a. Do not create a relationship whose only support is a section title, heading, or
            educational container name (for example "Foundations of AI"). A heading may be a
            candidate only when the body text also uses it as a real domain concept.
        7b. For bullets, comma-separated lists, bullet lists, coordinated adjectives, and items
            joined by "and" or "or", inspect every important item and emit each explicitly stated
            relation when both endpoints are candidates. Do not collapse a list into one summary
            edge and do not omit the final items. This includes every item in phrases such as
            "productivity and creativity" and "accountable, inclusive, reliable, safe, fair,
            transparent, secure, and respectful of privacy" when those items are extracted.
        7b-FANIN. MANDATORY EDGE COUNT for a coordinated list that shares ONE target (or ONE
            source): if the text states "A, B, C, D and E [verb] X" (a fan-in, e.g. "la visión, el
            modelo operativo, las habilidades, los recursos y la cultura dan forma a la adopción"),
            you MUST emit exactly one edge per listed item into X — 5 items means 5 separate edges
            (A->X, B->X, C->X, D->X, E->X), never fewer. The same applies in reverse for one source
            fanning OUT to several targets. Do NOT treat these parallel edges as redundant with
            each other just because they share the same RelationshipLabel/category and the same
            other endpoint — each one names a different entity and MUST be its own edge. Before
            finishing, re-count: for every sentence that lists N coordinated items against one
            shared entity, confirm your output contains N edges for it, not fewer.
        7c. Keep RelationshipCategory semantically compatible with RelationshipLabel: use
            PartOf only for membership/composition predicates, IsA only for type/category
            predicates, Processes for analyzes/uses/transforms/acts-on predicates, and RelatedTo
            when no precise universal category fits. Never force an incompatible category.
          8. EpistemicStatus: "explicit" when the text directly states the relationship; "inferred"
              when you reasonably deduce it from context without it being stated verbatim;
           "referenced" when the text merely points to another entity/section/document (e.g. "as
           provided in Article 12", "per the attached schedule") WITHOUT that other content being
           given to you here — in that case NEVER invent what the referenced content says, only
           record that a reference exists. If the relation depends on resolving a pronoun or
           contextual pointer such as "it", "them", "this", "these", or "those", use
           EpistemicStatus "inferred" even when the surrounding sentence states the action.
           Confidence must reflect how sure you are of this
           classification and of the relationship itself (near 1.0 for clearly explicit/well-
           evidenced edges, lower for inferred/referenced ones).
        9. RootEntityId: pick the ONE candidate Id that best acts as the conceptual hub/root of
           this section's whole graph — the concept most others relate back to or depend on
           (NOT simply the entity with the most edges). Must exactly match a candidate Id, or be
           empty if none clearly stands out.

        GRAPH QUALITY PRIORITIES (2026-08-15, apply to ANY domain):
        10. NEVER leave a candidate entity isolated (zero edges) if the text relates it to
            anything else — look harder for an implicit connection before giving up. In
            particular, the entity that names/matches this section's own title (e.g. "Artículo
            1o.") must connect to at least the rule(s)/concept(s) it establishes — never leave a
            document/section-type entity dangling with no edges.
        11. Do NOT phrase a CONDITIONED rule as an unconditional fact. If the text says an
            obligation/right/consequence applies only when some condition holds (e.g. "personas
            físicas y morales que... realicen los actos... siguientes"), do NOT emit a direct edge
            like "Persona física --está obligada al pago de--> Impuesto" as if it were absolute.
            Instead, if a dedicated Rule/Norm entity was extracted for this (see entity extraction
            rule 18), connect actor/condition/result THROUGH that rule entity (e.g. "Regla de
            obligación de pago --aplica a--> Persona física", "Regla de obligación de pago
            --requiere--> Actividad gravada", "Regla de obligación de pago --produce-->
            Obligación de pagar IVA") rather than a single flattened edge. If no such rule entity
            exists among the candidates, phrase the RelationshipLabel itself to keep the condition
            visible (e.g. "está obligada al pago si realiza actividad gravada") instead of omitting
            it, and use EpistemicStatus "hypothetical" for it.
        12. Separate ACTOR, ACTION/RULE, OBJECT, and RESULT into distinct edges rather than
            collapsing them into one vague edge — e.g. prefer "Enajenación de bienes --tiene por
            objeto--> Bien" (action -> object) plus "IVA --grava--> Enajenación de bienes" (rule ->
            action) over a single flattened "IVA --grava la enajenación de--> Bien" that skips the
            activity entity entirely.
        13. ROLE ONTOLOGY (applies to ANY domain and ANY class-of-being — Person, Organization,
            Animal, RealEstate/Property, Vehicle, Institution, or any other ontological class the
            text supports; a "role" is never its own class of being, it is always something a
            member of one of THOSE classes PLAYS): whenever a candidate
            entity represents a ROLE (its EntityType ends in "Role", or it's clearly a role like
            "Contribuyente", "Retenedor", "Enajenante", "Vendedor", "Cliente", "Empleado",
            "Mascota registrada" (role of an Animal), "Bien inmueble arrendado" (role of a
            RealEstate/Property), "Vehículo asegurado" (role of a Vehicle) — a real member of any
            class-of-being can hold MANY such roles simultaneously, e.g. a person can be a parent
            AND a taxpayer AND an employee at once, or an animal can be a "mascota" AND a "sujeto
            de exhibición" at once — so labeling something merely as "a Role type" explains nothing
            on its own), you MUST emit an explicit "IsRoleOf" edge from that role entity to the
            underlying class-of-being candidate(s) it applies to (e.g. "Contribuyente --IsRoleOf-->
            Persona física" AND "Contribuyente --IsRoleOf--> Persona moral" if both are candidates
            and the text supports either playing that role; equally "Mascota --IsRoleOf--> Animal"
            or "Arrendatario --IsRoleOf--> Persona física" in other domains).
            RelationshipLabel for these should read like "is a role that can be played by" (or the
            equivalent phrasing in the TARGET LANGUAGE given in the prompt). Skip this only if no
            plausible underlying class-of-being candidate exists in this section's candidate list.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    // gpt-5-mini burns a lot of its output-token budget on its own hidden
    // reasoning before ever emitting the structured JSON — for this agent
    // that was observed consuming the ENTIRE output budget (thousands of
    // reasoning tokens) and leaving zero connections extracted, even though
    // the same section's entity-extraction call succeeded fine. "Medium"
    // reasoning effort (default tier, same fix pattern as TutorAgentV2's
    // "Low") trims that burn while keeping the model on gpt-5-mini and
    // still giving it enough room to reason about relationships. Only
    // reasoning-tier deployments accept the param — "chat" flavors
    // (gpt-5-chat) and non-reasoning models (gpt-4o-mini) reject it with a
    // 400, so it must stay conditional on the deployment name.
    private readonly bool _isReasoningModel;

    // Only set when the Claude Opus path (below) is active — used to cap
    // output tokens explicitly (Claude has no OpenAI-style reasoning_effort
    // param to tune, so only max tokens is configurable here).
    private readonly int? _claudeMaxTokens;

    public StructuredLearningConnectionExtractionAgent(IConfiguration configuration)
    {
        // Experimental (2026-08-15) — this ONE agent can be pointed at a
        // Claude model deployed on Azure AI Foundry instead of gpt-5-mini,
        // to compare edge-extraction quality/cost. Opt-in only via these two
        // keys; every other Structured Learning agent is untouched and
        // keeps using AzureOpenAIEndpoint/AzureOpenAIDeploymentName (gpt-5-mini).
        // Claude here is deployed behind Azure's OpenAI-compatible "v1" API
        // surface (endpoint shape "https://<resource>.openai.azure.com/openai/v1"),
        // NOT the separate Azure AI Model Inference API — so the exact same
        // AzureOpenAIClient path as the gpt-5-mini branch below works, just
        // pointed at a different endpoint/deployment/key.
        var claudeEndpoint = configuration["ClaudeConnectionAgentEndpoint"];
        var claudeDeploymentName = configuration["ClaudeConnectionAgentDeploymentName"];
        if (!string.IsNullOrWhiteSpace(claudeEndpoint) && !string.IsNullOrWhiteSpace(claudeDeploymentName))
        {
            _modelName = claudeDeploymentName;
            // Generalized (2026-08-15) to also cover swapping in other gpt-5.x
            // reasoning models on a different Azure OpenAI resource (not just
            // Claude) via this same experimental slot — only real OpenAI
            // "gpt-5*" (non-chat) deployments accept reasoning_effort.
            _isReasoningModel = claudeDeploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
                && !claudeDeploymentName.Contains("chat", StringComparison.OrdinalIgnoreCase);
            _claudeMaxTokens = int.TryParse(configuration["ClaudeConnectionAgentMaxTokens"], out var maxTokens) ? maxTokens : null;
            var claudeApiKey = configuration["ClaudeConnectionAgentApiKey"];
            AzureOpenAIClient claudeClient = string.IsNullOrWhiteSpace(claudeApiKey)
                ? new AzureOpenAIClient(new Uri(claudeEndpoint), new DefaultAzureCredential())
                : new AzureOpenAIClient(new Uri(claudeEndpoint), new System.ClientModel.ApiKeyCredential(claudeApiKey));
            // Newest "flagship" reasoning deployments (e.g. gpt-5.6-sol) only
            // support the Responses API, not Chat Completions — GetChatClient
            // fails with a 404 "api_not_supported" for those, confirmed
            // 2026-08-15. GetResponsesClient + AsIChatClient bridges it back
            // to the same Microsoft.Extensions.AI.IChatClient shape so the
            // rest of this agent (AsAIAgent, RunAsync<T>) is unchanged.
#pragma warning disable OPENAI001 // Responses API surface is still experimental in this SDK version (2.9.0-beta.1).
            _agent = claudeClient
                .GetResponsesClient()
                .AsIChatClient(claudeDeploymentName)
                .AsAIAgent(instructions: Instructions, name: "StructuredLearningConnectionExtractionAgent");
#pragma warning restore OPENAI001
            return;
        }

        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];
        _modelName = deploymentName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        _isReasoningModel = deploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
            && !deploymentName.Contains("chat", StringComparison.OrdinalIgnoreCase);

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningConnectionExtractionAgent");
    }

    public virtual bool IsConfigured => _agent is not null;

    public virtual async Task<StructuredLearningConnectionExtractionResult> ExtractAsync(
        string sectionTitle, string sectionText, IReadOnlyList<(string Id, string Name)> candidateEntities, CancellationToken cancellationToken) =>
        await ExtractAsync(sectionTitle, sectionText, candidateEntities, existingRelationshipLabels: [], languageCode: "en", cancellationToken);

    /// <summary>Master de Clases de Arista (2026-08-15, Fase 3) overload —
    /// <paramref name="existingRelationshipLabels"/> are the relationship
    /// labels ALREADY catalogued for this document's domain, so the model
    /// reuses them instead of inventing near-duplicate phrasings.
    /// <paramref name="languageCode"/> (e.g. "en"/"es") is the document's own
    /// configured LanguageCode — RelationshipLabel must be written in this
    /// language regardless of the instructions' own illustrative examples
    /// (2026-08-30 fix: labels were silently coming back in Spanish for
    /// English documents because no language was ever passed in here).</summary>
    public virtual async Task<StructuredLearningConnectionExtractionResult> ExtractAsync(
        string sectionTitle, string sectionText, IReadOnlyList<(string Id, string Name)> candidateEntities,
        IReadOnlyList<string> existingRelationshipLabels, string languageCode, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningConnectionExtractionAgent is not configured.");
        }

        var existingLabelsBlock = existingRelationshipLabels.Count > 0
            ? $"\n\nExisting relationship labels (reuse these EXACT labels when they fit):\n{string.Join("\n", existingRelationshipLabels.Select(l => $"- {l}"))}"
            : string.Empty;

        var prompt = $"""
            Target language for RelationshipLabel: {languageCode}

            Section: {sectionTitle}

            Candidate entities (Id | Name — reference ONLY by Id):
            {string.Join("\n", candidateEntities.Select(e => $"- {e.Id} | {e.Name}"))}

            Text:
            {sectionText}{existingLabelsBlock}
            """;

        ChatClientAgentRunOptions? runOptions = null;
        if (_isReasoningModel)
        {
            runOptions = new ChatClientAgentRunOptions(new Microsoft.Extensions.AI.ChatOptions
            {
#pragma warning disable OPENAI001 // ChatCompletionOptions.ReasoningEffortLevel is an experimental SDK member (2.10.0), same opt-in as TutorAgentV2.
                RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    ReasoningEffortLevel = ChatReasoningEffortLevel.Medium
                }
#pragma warning restore OPENAI001
            });
        }
        else if (_claudeMaxTokens is int maxTokens)
        {
            runOptions = new ChatClientAgentRunOptions(new Microsoft.Extensions.AI.ChatOptions
            {
                RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    MaxOutputTokenCount = maxTokens
                }
            });
        }

        StructuredLearningConnectionExtractionResponse result;
        Microsoft.Extensions.AI.UsageDetails? usage = null;

        try
        {
            var response = await _agent.RunAsync<StructuredLearningConnectionExtractionResponse>(prompt, options: runOptions, cancellationToken: cancellationToken);
            usage = response.Usage;
            result = response.Result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // If RunAsync<T> fails due to formatting issues (e.g. ```json markdown backticks),
            // fallback to raw text RunAsync and manually clean the markdown fences before deserializing.
            var stringResponse = await _agent.RunAsync(prompt, options: runOptions, cancellationToken: cancellationToken);
            usage = stringResponse.Usage;
            var text = stringResponse.Text?.Trim() ?? string.Empty;
            if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) text = text[7..];
            else if (text.StartsWith("```", StringComparison.OrdinalIgnoreCase)) text = text[3..];
            if (text.EndsWith("```", StringComparison.OrdinalIgnoreCase)) text = text[..^3];
            text = text.Trim();

            result = JsonSerializer.Deserialize<StructuredLearningConnectionExtractionResponse>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new StructuredLearningConnectionExtractionResponse();
        }

        // If the model produced output tokens but empty connections list, retry with raw string
        if (result.Connections.Count == 0 && (usage?.OutputTokenCount ?? 0) > 100)
        {
            try
            {
                var stringResponse = await _agent.RunAsync(prompt, options: runOptions, cancellationToken: cancellationToken);
                var text = stringResponse.Text?.Trim() ?? string.Empty;
                if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) text = text[7..];
                else if (text.StartsWith("```", StringComparison.OrdinalIgnoreCase)) text = text[3..];
                if (text.EndsWith("```", StringComparison.OrdinalIgnoreCase)) text = text[..^3];
                text = text.Trim();

                var fallbackResult = JsonSerializer.Deserialize<StructuredLearningConnectionExtractionResponse>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (fallbackResult != null && fallbackResult.Connections.Count > 0)
                {
                    result = fallbackResult;
                }
            }
            catch { }
        }

        return new StructuredLearningConnectionExtractionResult
        {
            Response = result,
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };
    }
}
