using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>DTO for one Issue or Warning the agent finds — same shape used
/// for both lists in <see cref="BlueprintValidationResponse"/> (severity is
/// implied by which list an item is placed in, not a field on the DTO
/// itself).</summary>
public sealed class BlueprintValidationIssueDto
{
    /// <summary>Which part of the blueprint (or cross-cutting concern) this refers to.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BlueprintValidationArea Area { get; set; }

    /// <summary>Concrete, actionable description of the problem — never vague.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>DTO for one named quality metric, 0-100.</summary>
public sealed class BlueprintValidationMetricDto
{
    public string MetricName { get; set; } = string.Empty;

    public int MetricValue { get; set; }
}

/// <summary>
/// The structured output of BlueprintValidatorAgent: a full quality-gate
/// verdict for one NodeExperienceBlueprint.
/// </summary>
public sealed class BlueprintValidationResponse
{
    [JsonIgnore]
    public Guid NodeExperienceBlueprintId { get; set; }

    /// <summary>Overall verdict.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BlueprintValidationStatus Status { get; set; }

    /// <summary>Overall quality score, 0-100.</summary>
    public int Score { get; set; }

    /// <summary>Blocking problems (Severity=Error once persisted) — any non-empty list means the blueprint cannot be Approved.</summary>
    public List<BlueprintValidationIssueDto> Issues { get; set; } = [];

    /// <summary>Non-blocking problems (Severity=Warning once persisted).</summary>
    public List<BlueprintValidationIssueDto> Warnings { get; set; } = [];

    /// <summary>Named quality metrics, e.g. NodeCoverage=92, IllustrationCoverage=100.</summary>
    public List<BlueprintValidationMetricDto> Metrics { get; set; } = [];
}

/// <summary>
/// Agente BlueprintValidator — Paso 4 of the Human OS Studio graph pipeline
/// (Curador → GraphArchitect → ExperienceDesigner → BlueprintValidator).
///
/// ExperienceDesigner CREATES a NodeExperienceBlueprint; this agent ONLY
/// VERIFIES it — it never rewrites Teaching, never writes new Blueprint
/// Steps, and never generates new illustrations. Its single responsibility
/// is to validate and emit a <see cref="BlueprintValidationResponse"/> so a
/// Blueprint only reaches Runtime once it is trustworthy.
///
/// Plain ChatClientAgent with structured output — same pattern as
/// ExperienceDesignerAgent/GraphArchitectAgent (no Harness/Skills). A
/// deterministic code-level safety net (<see cref="BlueprintValidationGuard"/>)
/// runs AFTER this agent's call and can only make the verdict STRICTER
/// (never looser) for the objectively-checkable rules (missing/duplicate
/// steps, wrong order, empty/placeholder content, unused illustrations) —
/// mirroring the "LLM proposes, code has final say for structural facts"
/// pattern already used by MetricVerificationValidator/CompletedModuleValidator
/// in the old pipeline.
/// </summary>
public sealed class BlueprintValidatorAgent
{
    private const string Instructions = """
        You are the Blueprint Validator agent in Human OS Studio's
        capability-creation pipeline (Paso 4). You receive ONE knowledge
        node (AcademicDefinition/Interpretation/Examples/Applications/
        References) plus the NodeExperienceBlueprint that ExperienceDesigner
        already produced for it (its 5 Memory Paradox steps: Hypothesis,
        Teaching, Recall, Production, Assessment). Your ONLY job is to
        VALIDATE this blueprint and emit a verdict — you NEVER rewrite
        content, NEVER write new steps, and NEVER generate illustrations.

        RUN ALL 10 OF THESE CHECKS FOR EVERY BLUEPRINT

        1. MEMORY PARADOX COMPLETE — all 5 steps (Hypothesis, Teaching,
           Recall, Production, Assessment) must be present. Missing any one
           is a blocking Issue (Area=the missing step).

        2. ORDER CORRECT — the steps must respect the fixed order
           Hypothesis -> Teaching -> Recall -> Production -> Assessment. Any
           other order is a blocking Issue.

        3. TEACHING USES THE NODE — Teaching must use AcademicDefinition,
           plus at least one of Interpretation/Examples/Illustrations. If
           not, raise a blocking Issue (Area=Teaching): "Teaching does not
           sufficiently leverage node content."

        4. HYPOTHESIS — must provoke a prediction/reflection and must NEVER
           teach content, give a complete definition, or reveal the
           solution. If it does, raise a blocking Issue (Area=Hypothesis).

        5. RECALL — must require active retrieval from memory and must
           NEVER just reproduce/restate the Teaching content. If it does,
           raise a blocking Issue (Area=Recall): "Recall reproduces Teaching."

        6. PRODUCTION — must require creation, application, and transfer
           to a new/real situation. It must NOT be multiple-choice, must
           NOT just repeat an existing example, and must NOT just ask the
           learner to copy the definition. If it fails any of these, raise
           a blocking Issue (Area=Production).

        7. ASSESSMENT — must be phrased in OBSERVABLE behavioral verbs
           (identifica, explica, construye, aplica / identifies, explains,
           constructs, applies, etc.). Verbs like "entiende"/"comprende"
           ("understands"/"comprehends") are NOT observable and must be
           flagged — raise a Warning (Area=Assessment) if used, a blocking
           Issue if the ENTIRE assessment is unobservable.

        8. ILLUSTRATION USAGE — if the node has existing illustrations
           available, Hypothesis/Teaching/Recall SHOULD reuse them. If
           illustrations exist but are never referenced by any step, raise
           a Warning (Area=Illustration): "Illustrations available but never
           used."

        9. REFERENCES / TRACEABILITY — Teaching's content must be traceable
           back to AcademicDefinition/Examples/References. If Teaching
           introduces a fact/concept that isn't grounded in the node at all,
           raise a blocking Issue (Area=References).

        10. NO EMPTY CONTENT — no step's Content may be an empty string, a
            placeholder ("TODO", "lorem ipsum", "[...]"), or otherwise
            non-substantive. Raise a blocking Issue (whichever step is
            affected) if found.

        SCORING
        Produce an overall Score from 0-100 reflecting how well the
        blueprint satisfies ALL of the above (100 = flawless). Also emit a
        Metrics list with EXACTLY these 6 named metrics (0-100 each), your
        own honest assessment for this specific blueprint:
          - NodeCoverage: how much of the node's own content (AcademicDefinition/
            Interpretation/Examples/Applications) is actually reflected across the steps.
          - IllustrationCoverage: 100 if all available illustrations are used
            somewhere sensible, 0 if none are used despite being available,
            100 if there simply are none available (nothing to cover).
          - ReferenceCoverage: how traceable Teaching's content is back to
            AcademicDefinition/Examples/References.
          - AssessmentCoverage: how completely Assessment's criteria cover
            the node's Applications/Examples as checkable behaviors.
          - ProductionComplexity: how much genuine creation/transfer/open-
            endedness Production requires (0 = trivial/closed, 100 = rich
            authentic transfer task).
          - RecallStrength: how much genuine unaided retrieval Recall
            demands (0 = it just restates Teaching, 100 = strong retrieval
            with zero re-teaching).

        VERDICT RULES
        - Status=Rejected: any blocking Issue from checks 1, 2, or 10
          (structural, non-negotiable failures).
        - Status=NeedsRevision: one or more blocking Issues from checks
          3-9 (pedagogical failures), but no Rejected-level structural
          failure.
        - Status=ApprovedWithWarnings: zero blocking Issues, but one or
          more Warnings.
        - Status=Approved: zero Issues and zero Warnings.

        Be strict and specific — every Issue/Warning Message must name the
        concrete problem (quote the offending phrase when useful), never a
        vague "could be better." Ground every judgement only in the node's
        and blueprint's own actual content — never invent facts about them.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public BlueprintValidatorAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        _modelName = deploymentName ?? string.Empty;

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
            .AsAIAgent(instructions: Instructions, name: "BlueprintValidatorAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Result of a BlueprintValidator call: the LLM's verdict
    /// (already hardened by <see cref="BlueprintValidationGuard"/>) plus
    /// the token usage of the call that produced it.</summary>
    public sealed class ValidationResult
    {
        public BlueprintValidationResponse Validation { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    /// <summary>
    /// Validates an already-persisted NodeExperienceBlueprint against its node.
    /// </summary>
    /// <param name="node">The node the blueprint teaches (fully-enriched).</param>
    /// <param name="blueprint">The blueprint to validate — Steps must already be loaded/ordered.</param>
    /// <param name="illustrationCount">How many illustrations exist for this node (for the ILLUSTRATION USAGE check).</param>
    public async Task<ValidationResult> ValidateAsync(
        CapabilityGraphNode node,
        NodeExperienceBlueprint blueprint,
        int illustrationCount,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The BlueprintValidator agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var orderedSteps = blueprint.Steps.OrderBy(s => s.SortOrder).ToList();

        var stepsText = string.Join(
            "\n\n",
            orderedSteps.Select(s =>
                $"[{s.StepType}] (SortOrder={s.SortOrder})\n{s.Content}\n" +
                $"ReferencedIllustrationIds: {s.ReferencedIllustrationIdsJson}"));

        var prompt = $"""
            NODE:
            Name: {node.Name}
            NodeType: {node.NodeType}
            AcademicDefinition: {node.AcademicDefinition}
            Interpretation: {node.Interpretation}
            ExamplesJson: {node.ExamplesJson}
            ApplicationsJson: {node.ApplicationsJson}
            ReferencesJson: {node.ReferencesJson}
            IllustrationsAvailableForThisNode: {illustrationCount}

            BLUEPRINT: {blueprint.Name}
            Description: {blueprint.Description}

            STEPS (in the order they were persisted):
            {stepsText}
            """;

        var response = await _agent.RunAsync<BlueprintValidationResponse>(prompt);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "BlueprintValidator",
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        var validation = response.Result;
        validation.NodeExperienceBlueprintId = blueprint.NodeExperienceBlueprintId;

        BlueprintValidationGuard.Enforce(blueprint, illustrationCount, validation);

        return new ValidationResult { Validation = validation, TokenUsage = tokenUsage };
    }
}
