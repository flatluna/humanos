namespace HumanOS.Agents.Studio;

/// <summary>
/// Final, cross-agent validation gate for a single module (fixed Paso 5,
/// 2026-07-14 — see <c>HUMAN-OS-STUDIO.md</c> §14). Run inside
/// <c>MetricoExecutor.cs</c> right after Métrico returns, this decides
/// whether the module is genuinely done (<see cref="ModuleProcessingStatus.Verified"/>)
/// or merely needs pedagogical revision
/// (<see cref="ModuleProcessingStatus.RequiresRevision"/>) — the module
/// having "run through" the Instructor and Métrico is never, by itself,
/// enough to count as complete.
/// </summary>
/// <remarks>
/// Two different kinds of "not done" are deliberately distinguished:
/// <list type="bullet">
/// <item><description>STRUCTURAL contract violations (TargetMetric
/// changed between agents, Recall not before instruction, Mastery with
/// cues, a SuccessCriterion count mismatch) — these should never happen
/// if Paso 3's <see cref="ModuleScriptValidator"/> and Paso 4's
/// <see cref="MetricVerificationValidator"/> both already ran correctly;
/// a violation here means the contract broke somewhere, a genuine bug —
/// thrown as <see cref="InvalidOperationException"/>, caught by the
/// caller and mapped to <see cref="ModuleProcessingStatus.Failed"/>.</description></item>
/// <item><description>LEGITIMATE pedagogical outcomes (Métrico's own
/// Status is NotVerified/Failed) — not a bug, just "not ready yet";
/// returned as <see cref="ModuleProcessingStatus.RequiresRevision"/>,
/// never thrown.</description></item>
/// </list>
/// </remarks>
public static class CompletedModuleValidator
{
    /// <summary>
    /// Returns the module's final <see cref="ModuleProcessingStatus"/> —
    /// always <see cref="ModuleProcessingStatus.Verified"/> or
    /// <see cref="ModuleProcessingStatus.RequiresRevision"/> when it
    /// returns normally. Throws <see cref="InvalidOperationException"/>
    /// for a structural contract violation (the caller should catch this
    /// and map it to <see cref="ModuleProcessingStatus.Failed"/>).
    /// </summary>
    public static ModuleProcessingStatus Validate(
        ModuleSkeleton blueprintModule,
        ModuleScript script,
        MetricVerification verification,
        HumanEvolutionLayer level)
    {
        ArgumentNullException.ThrowIfNull(blueprintModule);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(verification);

        if (script.TargetMetric != blueprintModule.TargetMetric)
        {
            throw new InvalidOperationException(
                $"Module '{blueprintModule.Title}': the Instructor changed the approved TargetMetric.");
        }

        if (verification.TargetMetric != blueprintModule.TargetMetric)
        {
            throw new InvalidOperationException(
                $"Module '{blueprintModule.Title}': Métrico changed the approved TargetMetric.");
        }

        if (!script.RecallActivity.OccursBeforeInstruction)
        {
            throw new InvalidOperationException(
                $"Module '{blueprintModule.Title}': Recall does not occur before instruction.");
        }

        if (level == HumanEvolutionLayer.Mastery &&
            script.RecallActivity.SupportLevel != RecallSupportLevel.WithoutCues)
        {
            throw new InvalidOperationException(
                $"Module '{blueprintModule.Title}': Mastery requires Recall without cues.");
        }

        if (verification.SuccessCriteriaResults.Count != blueprintModule.SuccessCriteria.Count)
        {
            throw new InvalidOperationException(
                $"Module '{blueprintModule.Title}': not all approved SuccessCriteria were evaluated.");
        }

        // From here on, anything short of Verified is a legitimate
        // pedagogical outcome — NotVerified/Failed status, or (should it
        // ever slip through) an unsatisfied criterion — never an
        // exception.
        if (verification.Status != MetricVerificationStatus.Verified)
        {
            return ModuleProcessingStatus.RequiresRevision;
        }

        if (verification.SuccessCriteriaResults.Any(result => !result.IsSatisfied))
        {
            return ModuleProcessingStatus.RequiresRevision;
        }

        return ModuleProcessingStatus.Verified;
    }
}
