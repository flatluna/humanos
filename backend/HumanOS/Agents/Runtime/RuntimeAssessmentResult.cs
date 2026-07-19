using HumanOS.Agents.Studio;

namespace HumanOS.Agents.Runtime;

/// <summary>
/// One approved SuccessCriterion, evaluated individually by the Tutor
/// Agent during <see cref="RuntimeStage.Assessment"/> (fixed Paso 6,
/// 2026-07-14) — never a blanket "criteria are present", same principle
/// as Studio's <c>SuccessCriterionResult</c>.
/// </summary>
public sealed class SuccessCriterionAssessment
{
    /// <summary>The exact approved criterion text being evaluated.</summary>
    public string Criterion { get; set; } = string.Empty;

    public bool IsSatisfied { get; set; }

    /// <summary>Concrete, observable justification for IsSatisfied — never blank.</summary>
    public string Evidence { get; set; } = string.Empty;
}

/// <summary>
/// The Tutor Agent's structured-output Assessment verdict (fixed Paso 6,
/// 2026-07-14, see /memories/repo/human-os-runtime-design.md) — reuses
/// Studio's proven "LLM proposes, code decides" shape
/// (<c>MetricVerification</c>/<c>MetricVerificationValidator</c>) applied
/// to REAL learner evidence produced during a live Runtime session,
/// instead of a content-generation-time script.
/// </summary>
/// <remarks>
/// Reuses <see cref="MetricVerificationStatus"/> (Verified/NotVerified/
/// Failed) directly rather than defining a parallel Runtime-specific
/// enum — the 3-state judgment means the same thing here as it did in
/// Studio's Métrico: evidence + met criteria, not appearance.
/// </remarks>
public sealed class RuntimeAssessmentResult
{
    /// <summary>Must equal <see cref="RuntimePedagogicalContract.TargetMetric"/>
    /// exactly — the Tutor verifies ONLY this one metric, never a list
    /// (same SINGLE TARGET METRIC rule as Studio).</summary>
    public CapabilityMetric TargetMetric { get; set; }

    public MetricVerificationStatus Status { get; set; }

    /// <summary>One result per approved <see cref="RuntimePedagogicalContract.SuccessCriteria"/>
    /// entry, in the same order — never a blanket "criteria are present".</summary>
    public List<SuccessCriterionAssessment> SuccessCriteriaResults { get; set; } = [];

    /// <summary>Why the cited evidence does (or does not) demonstrate the
    /// TargetMetric — required whenever <see cref="Status"/> is Verified.</summary>
    public string Explanation { get; set; } = string.Empty;
}
