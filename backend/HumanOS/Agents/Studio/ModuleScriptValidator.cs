namespace HumanOS.Agents.Studio;

/// <summary>
/// Deterministic, code-level validation of the Instructor agent's
/// structured output against the module's APPROVED contract (Paso 2's
/// TargetMetric/RecallRequirement/LearnerProduction/SuccessCriteria) — a
/// safety net that does not rely on the LLM following the prompt
/// correctly. Fixed in Paso 3 (2026-07-14, see <c>HUMAN-OS-STUDIO.md</c>
/// §12). Run immediately inside <see cref="InstructorAgent.WriteScriptAsync"/>,
/// right after the LLM call, BEFORE the script reaches the Métrico agent.
/// </summary>
/// <remarks>
/// Intentionally does NOT verify whether Confidence, Independence,
/// Retention, or any metric other than the approved TargetMetric was
/// actually achieved — that remains the Métrico agent's job in a later
/// pipeline step (untouched in Paso 3, per the user's explicit
/// instruction). This validator only enforces that the Instructor
/// IMPLEMENTED a complete, well-formed, contract-faithful activity.
/// </remarks>
public static class ModuleScriptValidator
{
    /// <summary>
    /// Validates <paramref name="output"/> against the <paramref name="approvedModule"/>
    /// it was written for. Throws <see cref="InvalidOperationException"/>
    /// on the first rule violation found. Does not mutate either argument.
    /// </summary>
    public static void Validate(HumanEvolutionLayer layer, ModuleSkeleton approvedModule, ModuleScript output)
    {
        ArgumentNullException.ThrowIfNull(approvedModule);
        ArgumentNullException.ThrowIfNull(output);

        if (output.TargetMetric != approvedModule.TargetMetric)
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}': the Instructor changed the approved TargetMetric " +
                $"(approved '{approvedModule.TargetMetric}', got '{output.TargetMetric}').");
        }

        if (string.IsNullOrWhiteSpace(output.RecallActivity?.Instructions))
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}' does not contain an explicit Recall activity.");
        }

        if (!output.RecallActivity.OccursBeforeInstruction)
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}': Recall must occur before instruction, examples, " +
                "hints, or AI assistance.");
        }

        if (string.IsNullOrWhiteSpace(output.LearnerTask))
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}' does not contain an observable learner production.");
        }

        if (output.SuccessCriteria is null || output.SuccessCriteria.Count < 2)
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}' requires at least two SuccessCriteria.");
        }

        if (layer == HumanEvolutionLayer.Mastery &&
            output.RecallActivity.SupportLevel != RecallSupportLevel.WithoutCues)
        {
            throw new InvalidOperationException(
                $"Module '{approvedModule.Title}': Mastery requires Recall without cues.");
        }
    }
}
