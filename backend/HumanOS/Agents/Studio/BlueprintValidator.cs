namespace HumanOS.Agents.Studio;

/// <summary>
/// Deterministic, code-level validation of the Arquitecto agent's
/// structured output — a safety net that does not rely on the LLM
/// following the prompt correctly. Fixed in Paso 2 (2026-07-14, see
/// <c>HUMAN-OS-STUDIO.md</c> §11). Runs immediately after the LLM call in
/// <see cref="ArquitectoAgent.DesignAsync"/>, BEFORE the blueprint reaches
/// GATE 1 — a human reviewer should never see a blueprint that already
/// violates these rules.
/// </summary>
/// <remarks>
/// Intentionally does NOT verify whether a <c>RecallRequirement</c> was
/// actually implemented in a module's script, nor whether a
/// <c>LearnerProduction</c>/<c>SuccessCriteria</c> set was satisfied —
/// that is the Instructor/Métrico agents' job in a later pipeline step.
/// This validator only enforces that the Architect DECLARED a complete,
/// well-formed contract for every module.
/// </remarks>
public static class BlueprintValidator
{
    /// <summary>
    /// The only <see cref="HumanEvolutionLayer"/> values the Architect may
    /// use in the current MVP scope (see ACTIVE LEVELS in
    /// <see cref="ArquitectoAgent"/>'s Instructions).
    /// </summary>
    public static readonly IReadOnlySet<HumanEvolutionLayer> ActiveLevels =
        new HashSet<HumanEvolutionLayer>
        {
            HumanEvolutionLayer.Foundation,
            HumanEvolutionLayer.Exploration,
            HumanEvolutionLayer.Mastery
        };

    /// <summary>
    /// The only <see cref="CapabilityMetric"/> values the Architect may
    /// assign as a module's TargetMetric in the current MVP scope (fixed
    /// 2026-07-14, see <c>HUMAN-OS-STUDIO.md</c> §16 — the "Memory
    /// Paradox" minimal subset: Recall, Application, Confidence,
    /// Independence). Knowledge, Retention, and Fluency remain in the
    /// <see cref="CapabilityMetric"/> enum but are not active yet.
    /// </summary>
    public static readonly IReadOnlySet<CapabilityMetric> ActiveMetrics =
        new HashSet<CapabilityMetric>
        {
            CapabilityMetric.Recall,
            CapabilityMetric.Application,
            CapabilityMetric.Confidence,
            CapabilityMetric.Independence
        };

    /// <summary>
    /// Corrected 2026-07-14 per the "Recall's two roles" model: Recall as
    /// a LEARNING MECHANISM is transversal (every module's
    /// RecallRequirement, enforced in <see cref="ValidateModule"/>) — but
    /// Recall as a VERIFIABLE METRIC (TargetMetric=Recall) is reserved for
    /// exactly ONE capstone module per capability, placed in the LAST
    /// active level, evaluating recall of the whole capability rather
    /// than a single narrow topic. This replaces the earlier "all 4
    /// metrics mandatory at every level" rule, which inflated blueprints
    /// to 12-15 modules for no real benefit.
    /// </summary>
    private static void ValidateExactlyOneRecallCapstoneAtTheEnd(CapabilityBlueprint blueprint)
    {
        var recallModuleCount = blueprint.Levels
            .SelectMany(l => l.Modules)
            .Count(m => m.TargetMetric == CapabilityMetric.Recall);

        if (recallModuleCount == 0)
        {
            throw new InvalidOperationException(
                "The blueprint must contain exactly one capstone module with TargetMetric=Recall, " +
                "placed in the final level, evaluating recall of the whole capability — none was found.");
        }

        if (recallModuleCount > 1)
        {
            throw new InvalidOperationException(
                $"The blueprint must contain exactly one module with TargetMetric=Recall (found " +
                $"{recallModuleCount}) — Recall as a metric is reserved for a single final capstone, " +
                "not a module per level.");
        }

        var lastLevel = blueprint.Levels[^1];
        if (!lastLevel.Modules.Any(m => m.TargetMetric == CapabilityMetric.Recall))
        {
            throw new InvalidOperationException(
                $"The single Recall-target module must be in the final level ('{lastLevel.Layer}') " +
                "as a capstone recall check for the whole capability — it was found in an earlier level.");
        }
    }

    private const int MinSuccessCriteria = 2;
    private const int MaxSuccessCriteria = 5;

    /// <summary>
    /// Validates every level and module in <paramref name="blueprint"/>.
    /// Throws <see cref="InvalidOperationException"/> on the first rule
    /// violation found. Does not mutate the blueprint.
    /// </summary>
    public static void Validate(CapabilityBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);

        foreach (var level in blueprint.Levels)
        {
            if (!ActiveLevels.Contains(level.Layer))
            {
                throw new InvalidOperationException(
                    $"Level '{level.Layer}' is not active in the current MVP scope " +
                    "(only Foundation, Exploration, and Mastery are allowed).");
            }

            foreach (var module in level.Modules)
            {
                ValidateModule(module);
            }
        }

        ValidateExactlyOneRecallCapstoneAtTheEnd(blueprint);
    }

    private static void ValidateModule(ModuleSkeleton module)
    {
        if (!ActiveMetrics.Contains(module.TargetMetric))
        {
            throw new InvalidOperationException(
                $"Module '{module.Title}': TargetMetric '{module.TargetMetric}' is not active " +
                "in the current MVP scope (only Recall, Application, Confidence, and " +
                "Independence are allowed).");
        }

        if (string.IsNullOrWhiteSpace(module.RecallRequirement))
        {
            throw new InvalidOperationException(
                $"Module '{module.Title}' has no RecallRequirement.");
        }

        if (string.IsNullOrWhiteSpace(module.LearnerProduction))
        {
            throw new InvalidOperationException(
                $"Module '{module.Title}' has no LearnerProduction.");
        }

        if (module.SuccessCriteria is null ||
            module.SuccessCriteria.Count < MinSuccessCriteria ||
            module.SuccessCriteria.Count > MaxSuccessCriteria)
        {
            throw new InvalidOperationException(
                $"Module '{module.Title}' must have between {MinSuccessCriteria} and " +
                $"{MaxSuccessCriteria} SuccessCriteria.");
        }

        if (module.SuccessCriteria.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"Module '{module.Title}' contains an empty SuccessCriterion.");
        }
    }
}
