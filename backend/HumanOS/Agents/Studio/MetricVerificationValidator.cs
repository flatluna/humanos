namespace HumanOS.Agents.Studio;

/// <summary>
/// Deterministic, code-level validation of the Métrico agent's structured
/// output — a safety net that does not rely on the LLM following the
/// prompt correctly. Fixed in Paso 4 (2026-07-14, see
/// <c>HUMAN-OS-STUDIO.md</c> §13). Run immediately inside
/// <see cref="MetricoAgent.AssignMetricsAsync"/>, right after the LLM
/// call, BEFORE the verification is converted into a persisted
/// <see cref="ModuleMetricAssignment"/>.
/// </summary>
/// <remarks>
/// Central principle enforced here: a metric is not verified because the
/// script APPEARS to support it — it needs observable evidence, an exact
/// location, and met success criteria. This validator cannot check
/// whether the cited evidence is actually TRUE (that still requires
/// trusting the LLM's reading of the script), but it DOES guarantee the
/// report is internally consistent, complete, and well-formed.
/// </remarks>
public static class MetricVerificationValidator
{
    /// <summary>
    /// Validates <paramref name="verification"/> against the
    /// <paramref name="approvedModule"/> it was written for. Throws
    /// <see cref="InvalidOperationException"/> on the first rule
    /// violation found. Does not mutate either argument.
    /// </summary>
    public static void Validate(ModuleSkeleton approvedModule, MetricVerification verification)
    {
        ArgumentNullException.ThrowIfNull(approvedModule);
        ArgumentNullException.ThrowIfNull(verification);

        if (verification.ModuleId != approvedModule.ModuleId.ToString())
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}': Métrico returned an unexpected ModuleId " +
                $"(expected '{approvedModule.ModuleId}', got '{verification.ModuleId}').");
        }

        if (verification.TargetMetric != approvedModule.TargetMetric)
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}': Métrico changed the approved TargetMetric " +
                $"(approved '{approvedModule.TargetMetric}', got '{verification.TargetMetric}').");
        }

        if (string.IsNullOrWhiteSpace(verification.EvidenceLocation))
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}': metric verification requires an evidence location.");
        }

        if (verification.SuccessCriteriaResults.Count != approvedModule.SuccessCriteria.Count)
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}': every SuccessCriterion must be evaluated " +
                $"(expected {approvedModule.SuccessCriteria.Count}, got {verification.SuccessCriteriaResults.Count}).");
        }

        if (verification.SuccessCriteriaResults.Any(r => string.IsNullOrWhiteSpace(r.Evidence)))
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}': every SuccessCriterionResult requires its own Evidence " +
                "— it is not enough to state that the criteria are present.");
        }

        if (verification.Status == MetricVerificationStatus.Verified)
        {
            if (string.IsNullOrWhiteSpace(verification.Evidence) ||
                string.IsNullOrWhiteSpace(verification.Explanation))
            {
                throw new InvalidOperationException(
                    $"Module '{approvedModule.Title}': a Verified TargetMetric requires both Evidence and " +
                    "an Explanation — a metric is not verified based on intention, wording, or a possible " +
                    "side effect.");
            }

            if (verification.SuccessCriteriaResults.Any(r => !r.IsSatisfied))
            {
                throw new InvalidOperationException(
                    $"Module '{approvedModule.Title}': a metric cannot be Verified when a required " +
                    "criterion failed.");
            }
        }

        // Recall is verified independently of the TargetMetric — its
        // existence never implies Recall IS the TargetMetric (see
        // HUMAN-OS-STUDIO.md §10.3). Still, if a recall moment was found
        // at all, it must occur before instruction, regardless of which
        // metric is being targeted.
        if (verification.Recall.Status != RecallVerificationStatus.Missing &&
            !verification.Recall.OccursBeforeInstruction)
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}': Recall must occur before instruction, examples, hints, " +
                "or AI assistance.");
        }

        if (string.IsNullOrWhiteSpace(verification.Recall.Evidence))
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}': Recall verification requires observable Evidence, " +
                "including when Status is Missing.");
        }
    }
}
