using HumanOS.Agents.Studio;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Pure routing predicates for the module-completion gate (fixed Paso 5,
/// 2026-07-14, threshold relaxed Paso 7 2026-07-14 — see
/// HUMAN-OS-STUDIO.md §14/§16). Separated from
/// <see cref="CapabilityCreationWorkflowFactory"/> so they are directly
/// unit-testable (see <c>[InternalsVisibleTo]</c> in HumanOS.csproj)
/// without needing to run the full Agent Framework workflow.
/// </summary>
internal static class ModuleCompletionGate
{
    /// <summary>
    /// Minimum fraction of FINAL module outcomes (after
    /// <see cref="ModuleCompletionRouterExecutor"/>'s bounded per-module
    /// retries are exhausted) that must be <see cref="ModuleProcessingStatus.Verified"/>
    /// for the capability to be considered ready to assemble (Paso 7,
    /// 2026-07-14). This is a CAPABILITY-LEVEL acceptance threshold only —
    /// it never changes what "Verified" means for any individual module
    /// (still decided per-criterion by CompletedModuleValidator/Métrico,
    /// see HUMAN-OS-STUDIO.md §16).
    /// </summary>
    public const double MinVerifiedRatio = 0.85;

    /// <summary>
    /// True once module generation is done AND at least <see cref="MinVerifiedRatio"/>
    /// of modules reached <see cref="ModuleProcessingStatus.Verified"/> —
    /// the ONLY condition under which Experiencia may assemble the final
    /// package. Vacuously true for a blueprint with zero modules (matches
    /// the pre-Paso-5 "any completion = proceed" behavior for that edge
    /// case).
    /// </summary>
    public static bool MeetsPublishThreshold(ModuleRouterOutput? message)
    {
        if (message?.Completed is null)
        {
            return false;
        }

        var modules = message.Completed.Modules;
        if (modules.Count == 0)
        {
            return true;
        }

        var verifiedCount = modules.Count(m => m.Status == ModuleProcessingStatus.Verified);
        return verifiedCount / (double)modules.Count >= MinVerifiedRatio;
    }

    /// <summary>
    /// True once module generation is done but FEWER than
    /// <see cref="MinVerifiedRatio"/> of modules are <see cref="ModuleProcessingStatus.Verified"/>
    /// — routes to <see cref="ModuleRevisionRequiredExecutor"/> instead of
    /// Experiencia.
    /// </summary>
    public static bool RequiresRevision(ModuleRouterOutput? message) =>
        message?.Completed is not null && !MeetsPublishThreshold(message);
}
