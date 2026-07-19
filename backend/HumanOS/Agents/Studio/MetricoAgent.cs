using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// The 3 verification states the module's OWN TargetMetric can end up in
/// (fixed Paso 4, 2026-07-14 — see HUMAN-OS-STUDIO.md §13). Central
/// principle: a metric is not verified because the script APPEARS to
/// support it — it needs observable evidence, an exact location, and met
/// success criteria.
/// </summary>
public enum MetricVerificationStatus
{
    /// <summary>Confirmed: concrete Evidence + exact EvidenceLocation +
    /// every SuccessCriterion satisfied.</summary>
    Verified,

    /// <summary>Not enough evidence to judge either way (e.g. the script
    /// doesn't clearly attempt this metric at all).</summary>
    NotVerified,

    /// <summary>Evidence exists and was checked, but it demonstrably does
    /// NOT meet the bar (e.g. a required SuccessCriterion failed).</summary>
    Failed
}

/// <summary>
/// The 3 states Métrico's INDEPENDENT re-check of the module's Recall
/// moment can find — distinct from the Instructor's own self-reported
/// <see cref="RecallSupportLevel"/> (which has no "Missing" option,
/// since Paso 2's <c>BlueprintValidator</c> already guarantees a
/// RecallRequirement was approved). Métrico audits the ACTUAL script
/// independently, so it can catch the Instructor failing to implement it
/// at all.
/// </summary>
public enum RecallVerificationStatus
{
    /// <summary>No retrieval moment is actually present in the script,
    /// regardless of what the Instructor claimed.</summary>
    Missing,

    WithCues,

    WithoutCues
}

/// <summary>One approved SuccessCriterion, evaluated individually — never
/// a blanket "criteria are present".</summary>
public sealed class SuccessCriterionResult
{
    /// <summary>The exact approved criterion text being evaluated.</summary>
    public string Criterion { get; set; } = string.Empty;

    public bool IsSatisfied { get; set; }

    /// <summary>Concrete, observable justification for IsSatisfied — never blank.</summary>
    public string Evidence { get; set; } = string.Empty;
}

/// <summary>
/// Métrico's independent verification of the module's Recall
/// implementation. Existence of a Recall activity does NOT mean Recall is
/// the module's TargetMetric — those are independent concepts (see
/// HUMAN-OS-STUDIO.md §10.3/§12).
/// </summary>
public sealed class RecallVerification
{
    public RecallVerificationStatus Status { get; set; }

    /// <summary>Observable proof of the retrieval moment actually present
    /// in the script (or of its absence) — never blank.</summary>
    public string Evidence { get; set; } = string.Empty;

    /// <summary>Exactly where in the script this recall moment occurs.</summary>
    public string EvidenceLocation { get; set; } = string.Empty;

    /// <summary>Whether the recall moment occurs BEFORE any explanation,
    /// example, hint, checklist, source, or AI assistance. Must be
    /// <see langword="true"/> whenever <see cref="Status"/> is not
    /// <see cref="RecallVerificationStatus.Missing"/>.</summary>
    public bool OccursBeforeInstruction { get; set; }
}

/// <summary>
/// The Métrico agent's structured-output shape (fixed Paso 4, 2026-07-14).
/// Verifies ONLY the module's own approved TargetMetric, with precise,
/// per-criterion evidence — never a generically-inferred multi-metric list.
/// </summary>
public sealed class MetricVerification
{
    /// <summary>
    /// Echo of the module's identifier, given in the prompt as a plain
    /// string (deliberately NOT typed as <see cref="Guid"/> in structured
    /// output — a Guid-typed LLM-authored property risks the model
    /// producing an invalid GUID literal; a string just needs to match).
    /// </summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Must equal the Arquitecto-approved <see cref="ModuleSkeleton.TargetMetric"/>
    /// exactly — Métrico verifies ONLY this one metric, never a list.</summary>
    public CapabilityMetric TargetMetric { get; set; }

    public MetricVerificationStatus Status { get; set; }

    /// <summary>The concrete, observable learner production that proves
    /// (or fails to prove) the TargetMetric.</summary>
    public string Evidence { get; set; } = string.Empty;

    /// <summary>Exactly where in the script this evidence occurs (e.g.
    /// "Tarea obligatoria — Pasos 1-7").</summary>
    public string EvidenceLocation { get; set; } = string.Empty;

    /// <summary>One result per approved <see cref="ModuleSkeleton.SuccessCriteria"/>
    /// entry, in the same order — never a blanket "criteria are present".</summary>
    public List<SuccessCriterionResult> SuccessCriteriaResults { get; set; } = [];

    public RecallVerification Recall { get; set; } = new();

    /// <summary>Why this evidence does (or does not) demonstrate the
    /// TargetMetric — never a list of other metrics the script "also"
    /// touches (see SINGLE TARGET METRIC in <see cref="MetricoAgent"/>'s
    /// Instructions).</summary>
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// Downstream-compatible result of a Métrico verification, kept so
/// <c>MetricoExecutor.cs</c>/<c>PublishExecutor.cs</c> (and the existing
/// <c>CapabilityModuleMetric</c> DB schema, which has no evidence/
/// location/status columns yet) keep working unchanged.
/// <see cref="Metrics"/>/<see cref="Rationale"/> are derived
/// deterministically in code from <see cref="Verification"/> — never
/// produced directly by the LLM.
/// </summary>
public sealed class ModuleMetricAssignment
{
    /// <summary>Contains the TargetMetric if (and only if)
    /// <see cref="Verification"/>'s Status is Verified — otherwise empty.
    /// Never contains any metric other than the TargetMetric (Paso 4's
    /// SINGLE TARGET METRIC rule: no secondary metric can ever be
    /// reported as verified by this module).</summary>
    public List<CapabilityMetric> Metrics { get; set; } = [];

    public string Rationale { get; set; } = string.Empty;

    /// <summary>The precise, evidence-based verification this assignment
    /// was derived from — kept for observability; not yet persisted to
    /// the DB schema (future step).</summary>
    public MetricVerification Verification { get; set; } = new();
}

/// <summary>
/// <see cref="MetricoAgent.AssignMetricsAsync"/>'s return value: the
/// validated assignment plus the token usage of the call that produced it
/// (secondary/observability concern only, see HUMAN-OS-STUDIO.md §13).
/// </summary>
public sealed class MetricoResult
{
    public ModuleMetricAssignment Assignment { get; set; } = null!;

    public AgentTokenUsage TokenUsage { get; set; } = null!;
}

/// <summary>
/// Agente Métrico — VERIFIES whether a completed module's script actually
/// achieves the TargetMetric the Arquitecto agent already assigned it,
/// with precise, per-criterion evidence, instead of guessing or inferring
/// a multi-metric list (changed 2026-07-13, refined Paso 4 2026-07-14 —
/// see /memories/repo/humanstudio-multiagent-vision.md).
/// </summary>
public sealed class MetricoAgent
{
    private const string Instructions = """
        You are the Métrico agent in Human OS Studio. Your role is
        verification, not guessing: the Arquitecto agent already assigned
        this module a TargetMetric (one of the 4 MVP-active metrics:
        Recall, Application, Confidence, Independence) when it designed
        the capability's blueprint, and the Instructor implemented a
        RecallActivity, a LearnerTask, and SuccessCriteria for it. Your job
        is to check, with PRECISE EVIDENCE, whether all of that was really
        achieved.

        CENTRAL PRINCIPLE (never break this)
        A metric is NOT verified because the script APPEARS to support it.
        A metric may be marked Verified only when you can identify: (1) an
        observable learner production, (2) its exact location in the
        script, (3) the success criteria used to evaluate it, all
        satisfied, and (4) why that evidence demonstrates the TargetMetric.
        Do not mark a metric Verified based on intention, wording, content
        exposure, or a possible side effect.

        YOU ARE REVIEWING A COURSE DESIGN, NOT GRADING A STUDENT (read this
        before anything else — a real misunderstanding found in testing)
        You are reviewing this module during CAPABILITY CREATION — Curador
        writes the corpus, Arquitecto designs the blueprint, Instructor
        writes this script, YOU verify it, then Experiencia assembles the
        final package for publishing. At this stage, NO learner has ever
        taken this module yet — it is still a reusable TEMPLATE, not a
        completed lesson. You will NEVER find a learner's filled-in
        checklist, a pasted answer, a reported measurement value, or any
        other completed submission inside the script — that is IMPOSSIBLE
        at this stage, because the module hasn't been delivered to anyone.
        Do NOT reject a module for "not including a learner-produced
        response" or "no pasted checklist to inspect" — expecting that is
        a mistake; it is asking the design to contain evidence that can
        only exist AFTER a real person takes the course.
        What you ARE verifying is the QUALITY OF THE DESIGN: does the
        script's LearnerTask, AS WRITTEN, make it MANDATORY (not optional,
        not implied, not skippable) for a future real learner to produce
        the exact evidence each approved SuccessCriterion needs, in a way
        that satisfies the Human Evolution Layer x Metric matrix and the
        Memory Paradox thesis (retrieval before assistance, real
        production over passive consumption, no cognitive offloading)?
        Your "Evidence" for each criterion should therefore CITE THE
        MANDATORY INSTRUCTION in the script that WOULD force that proof to
        exist once a learner completes it — e.g. "El LearnerTask exige
        explícitamente que el alumno registre y reporte la temperatura
        usada (paso 3), por lo que un alumno real dejaría esa evidencia al
        completar la tarea" is a valid, sufficient Evidence statement. You
        should mark a criterion NotSatisfied only when the INSTRUCTION
        ITSELF is missing, optional, or too vague to force that specific
        evidence to exist — never because a completed example isn't
        present in the script (there should never be one).
        HOW TO DECIDE: ACCEPT vs REJECT (be a fair reviewer, not an
        adversarial one — this is a quality review, not an impossible bar)
        Your job is to catch REAL gaps, not to invent reasons to reject a
        well-designed module. Use this concrete test for EACH
        SuccessCriterion:

        ACCEPT (IsSatisfied = true) whenever LearnerTask contains ANY of:
        - A direct instruction requiring the learner to DO the exact thing
          the criterion describes (e.g. criterion "ordenó los ítems
          lógicamente" + LearnerTask asks the learner to produce an
          ordered agenda -> accept).
        - A direct instruction requiring the learner to RECORD/REPORT a
          specific value, decision, or observation the criterion needs
          (e.g. criterion about a temperature range + LearnerTask requires
          writing down the temperature used -> accept, even though you
          will never see the actual number, because a real learner
          following the instruction WILL produce it).
        - A self-assessment/reflection step where the learner explicitly
          checks their own work against that criterion (e.g. an
          "autoevaluación" section asking the learner to confirm the
          criterion was met), as long as it follows the real production
          step rather than replacing it.
        You do NOT need a completed example, a filled-in value, or a model
        answer to accept a criterion — the MANDATORY instruction IS the
        evidence at this stage.

        REJECT (IsSatisfied = false) only when:
        - The instruction is completely ABSENT for that criterion (nothing
          in LearnerTask would require the learner to produce/report
          anything related to it), or
        - The instruction is merely a SUGGESTION/TIP ("puedes anotar...",
          "si quieres, registra...") rather than a requirement, or
        - The instruction is too VAGUE to reliably produce that specific
          evidence (e.g. "prepara el café correctamente" with no explicit
          request to record the parameters the criterion needs).
        Do not reject for style, phrasing, brevity, or because you would
        have written the instruction differently — only reject when the
        MECHANISM that would produce the evidence is genuinely missing,
        optional, or too vague to work.

        WHEN IN DOUBT, LEAN TOWARD ACCEPT
        If a reasonable real learner following LearnerTask AS WRITTEN
        would very likely end up producing the required evidence, accept
        the criterion. Only reject when you are confident the instruction,
        as written, would NOT reliably produce it.
        SINGLE TARGET METRIC (never break this)
        Verify ONLY the approved TargetMetric. Do not report any of the
        other 3 active metrics (or Knowledge/Retention/Fluency, which
        aren't active in the current MVP scope) as verified unless one IS
        the approved TargetMetric. Possible side effects must never be
        presented as verified metrics. For example, if the TargetMetric is
        Application, do NOT write "it also develops Confidence and
        Independence" — concentrate your entire evaluation on:
        TargetMetric, Status, Evidence, EvidenceLocation,
        SuccessCriteriaResults, Recall, Explanation. Nothing else is
        verified by this module.

        USE THIS CHECKLIST TO FIND YOUR EVIDENCE
        - Does the learner PREDICT before anything is revealed? (P1)
        - Is it connected to something the learner already knows? (P5)
        - Is productive friction dosed correctly for this Human Evolution
          Layer? (P3)
        - Does the learner PRODUCE, instead of being given the answer?
          (P7 / anti-offloading)
        - Is retrieval practice used (not just re-reading), with/without
          cues appropriate to the level?
        - Is real context/material used rather than a generic example?
          (P4)
        - Is spaced review scheduled, calibrated to the level? (P6)
        - Is the level's scaffolding respected (high in Foundation, zero
          in Creator)?

        EVALUATE EVERY SUCCESS CRITERION INDIVIDUALLY
        You are given the exact approved SuccessCriteria list. Return one
        SuccessCriterionResult per criterion, in the SAME order, each with
        its own Evidence — never a blanket "the criteria are present".
        Example of a properly evaluated criterion (citing the MANDATORY
        instruction that would force the evidence to exist — never a
        completed learner example, which cannot exist at this stage):
        Criterion: "Cada elemento tiene objetivo, tiempo y responsable."
        IsSatisfied: true
        Evidence: "El LearnerTask exige explícitamente que el alumno
        incluya esos tres componentes en su agenda (paso 4), por lo que
        cualquier alumno real que complete la tarea dejará esa evidencia."
        A metric CANNOT be Verified if any SuccessCriterionResult has
        IsSatisfied = false.

        RECALL — VERIFY INDEPENDENTLY (updated 2026-07-16 — see
        REORDERING RATIONALE in RuntimeSessionWorkflowFactory.cs: the
        Instructor now deliberately writes TEACHING CONTENT FIRST, then the
        recall/retrieval moment right after it — teach step by step, then
        ask what the learner retained. This is the CORRECT order; do NOT
        flag it as a violation.)
        Do not just copy the Instructor's self-reported RecallActivity —
        re-check the actual script. Report:
        - Status: Missing (no retrieval moment actually present),
          WithCues, or WithoutCues.
        - Evidence + EvidenceLocation for whatever you find (or for why
          it's missing).
        - OccursBeforeInstruction: despite its name (kept for backward
          compatibility), this field now means "the retrieval attempt
          happens right after the module's real teaching content, and
          BEFORE the LearnerTask/application step or any FURTHER
          scaffolding beyond that teaching content" — NOT "before the
          teaching content itself". Set it to true when the script's
          order is: (1) real teaching content, (2) the recall/retrieval
          moment, (3) LearnerTask. Set it to FALSE only if the retrieval
          moment is missing entirely, or if it appears AFTER the
          LearnerTask/application step, or after additional scaffolding
          (extra examples/hints/checklists/source material/AI assistance)
          given beyond the initial teaching content.
        IMPORTANT: the existence of a Recall activity does NOT mean Recall
        is the TargetMetric — these are independent concepts. Report
        Recall the same way regardless of what the TargetMetric is.

        PER-METRIC RULES FOR WHAT COUNTS AS EVIDENCE (apply the one
        matching this module's TargetMetric — only these 4 are active in
        the current MVP scope)
        - Recall: requires the learner retrieving knowledge WITHOUT
          consulting the answer, source, or AI.
        - Application: requires the learner USING the knowledge to
          produce a result in a real case or context, not just describing
          it.
        - Confidence: requires ALL THREE: a declared confidence BEFORE
          responding, the real performance, AND an explicit comparison
          between the two. Merely practicing something does not
          demonstrate Confidence by itself.
        - Independence: requires an execution with NO steps, worked
          examples, hints, checklist, source consultation, or AI answers.
          Producing something with high scaffolding does NOT demonstrate
          Independence, no matter how good the production is.

        Use this guide only as background context for what each module
        TYPE typically supports — not a substitute for citing real
        evidence and location from the actual script:
        Lectura -> Recall
        Video -> Recall
        Practica -> Application + Independence
        SimuladorIA -> Confidence + Application
        Mentoria -> Independence + Confidence

        Echo the given ModuleId and TargetMetric back EXACTLY as provided.
        Write Explanation as 2-3 sentences stating clearly why the
        evidence does (or does not) demonstrate the TargetMetric —
        never as a list of other metrics the script also happens to touch.
        """;

    private readonly AIAgent? _agent;

    public MetricoAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "MetricoAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<MetricoResult> AssignMetricsAsync(
        HumanEvolutionLayer layer,
        ModuleSkeleton module,
        ModuleScript script,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Métrico agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var moduleIdText = module.ModuleId.ToString();
        var successCriteriaText = string.Join(
            "\n", module.SuccessCriteria.Select((c, i) => $"{i + 1}. {c}"));
        var prompt =
            $"ModuleId (echo back exactly): {moduleIdText}\n" +
            $"Human Evolution Layer: {layer}\n" +
            $"Module: {module.Title} ({module.Type})\n" +
            $"TargetMetric assigned by the Architect (verify ONLY this one, echo back exactly): " +
            $"{module.TargetMetric}\n" +
            $"Approved SuccessCriteria — evaluate EVERY one, in this exact order " +
            $"({module.SuccessCriteria.Count} total):\n{successCriteriaText}\n\n" +
            $"Script:\n{script.Script}";

        var response = await _agent.RunAsync<MetricVerification>(prompt);
        var verification = response.Result;

        MetricVerificationValidator.Validate(module, verification);

        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "Metrico",
            ModuleId = moduleIdText,
            InputTokens = (int)(response.Usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(response.Usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(response.Usage?.CachedInputTokenCount ?? 0)
        };

        return new MetricoResult { Assignment = BuildAssignment(verification), TokenUsage = tokenUsage };
    }

    private static ModuleMetricAssignment BuildAssignment(MetricVerification verification)
    {
        // Only the TargetMetric can ever be persisted as verified — never
        // force-included by default (that pre-Paso-4 safety net directly
        // contradicted the "evidence, not appearance" principle; removed
        // in MetricoExecutor.cs).
        var metrics = new List<CapabilityMetric>();
        if (verification.Status == MetricVerificationStatus.Verified)
        {
            metrics.Add(verification.TargetMetric);
        }

        var criteriaLines = verification.SuccessCriteriaResults.Select(
            r => $"{(r.IsSatisfied ? "✓" : "✗")} {r.Criterion} — {r.Evidence}");

        var rationale = string.Join("\n", new[]
        {
            $"TargetMetric: {verification.TargetMetric} — {verification.Status}.",
            $"Evidence: {verification.Evidence}",
            $"EvidenceLocation: {verification.EvidenceLocation}",
            "SuccessCriteria:",
        }.Concat(criteriaLines).Concat(new[]
        {
            $"Recall: {verification.Recall.Status} " +
                $"({(verification.Recall.OccursBeforeInstruction ? "before instruction" : "NOT before instruction")}) " +
                $"— {verification.Recall.Evidence}",
            $"Explanation: {verification.Explanation}"
        }));

        return new ModuleMetricAssignment
        {
            Metrics = metrics,
            Rationale = rationale,
            Verification = verification
        };
    }
}

