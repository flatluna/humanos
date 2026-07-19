using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Deterministic, code-level validation of the Tutor Agent's
/// <see cref="RuntimeAssessmentResult"/> — the Runtime-side safety net
/// mirroring Studio's proven <c>MetricVerificationValidator</c> (fixed
/// Paso 6, 2026-07-14, see /memories/repo/human-os-runtime-design.md).
/// Run immediately after <see cref="TutorAgent.AssessAsync"/>'s LLM call,
/// BEFORE the result is trusted for progression.
/// </summary>
/// <remarks>
/// Same central principle as Studio's Métrico: a metric is not verified
/// because the evidence APPEARS to support it — it needs every
/// SuccessCriterion evaluated individually, with concrete evidence, and
/// internal consistency between <see cref="RuntimeAssessmentResult.Status"/>
/// and the per-criterion results. This validator cannot check whether the
/// cited evidence is actually TRUE (that still requires trusting the
/// LLM's reading of the learner's evidence) — it only guarantees the
/// report is complete, well-formed, and internally consistent.
/// </remarks>
internal static class RuntimeAssessmentValidator
{
    public static void Validate(RuntimePedagogicalContract contract, RuntimeAssessmentResult result)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(result);

        if (result.TargetMetric != contract.TargetMetric)
        {
            throw new InvalidOperationException(
                $"Assessment changed the approved TargetMetric (approved '{contract.TargetMetric}', " +
                $"got '{result.TargetMetric}').");
        }

        if (result.SuccessCriteriaResults.Count != contract.SuccessCriteria.Count)
        {
            throw new InvalidOperationException(
                $"Every approved SuccessCriterion must be evaluated (expected " +
                $"{contract.SuccessCriteria.Count}, got {result.SuccessCriteriaResults.Count}).");
        }

        if (result.SuccessCriteriaResults.Any(r => string.IsNullOrWhiteSpace(r.Evidence)))
        {
            throw new InvalidOperationException(
                "Every SuccessCriterion result requires concrete, non-blank evidence.");
        }

        if (result.Status == MetricVerificationStatus.Verified)
        {
            if (string.IsNullOrWhiteSpace(result.Explanation))
            {
                throw new InvalidOperationException("A Verified assessment requires an explanation.");
            }

            if (result.SuccessCriteriaResults.Any(r => !r.IsSatisfied))
            {
                throw new InvalidOperationException(
                    "Assessment cannot be Verified while any SuccessCriterion is not satisfied.");
            }
        }
    }
}
