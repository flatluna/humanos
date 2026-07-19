using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using HumanOS.Models.Capabilities;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Pure mapping from Studio's published schema to the Runtime's fixed
/// pedagogical contract (fixed 2026-07-14, closes the persistence gap
/// documented in /memories/repo/human-os-runtime-design.md — this is the
/// "capa intermedia" between a published <see cref="CapabilityModule"/>
/// and <see cref="RuntimePedagogicalContract"/>).
/// </summary>
/// <remarks>
/// Deliberately a pure function over an already-loaded
/// <see cref="CapabilityModule"/> (with its <c>Verifications</c> and each
/// verification's <c>SuccessCriteriaResults</c> navigation collections
/// populated by the caller, e.g. via <c>Include</c>) — this class does NOT
/// itself query the database. Fetching/caching strategy belongs to
/// whatever Runtime component consumes this (Paso 4+), not to the pure
/// projection logic itself.
/// </remarks>
internal static class RuntimePedagogicalContractProjector
{
    /// <summary>
    /// Projects the Studio-approved contract for one module.
    /// <see cref="RuntimePedagogicalContract.TargetMetric"/> and
    /// <see cref="RuntimePedagogicalContract.SuccessCriteria"/> are read
    /// from the MOST RECENT <see cref="CapabilityModuleVerification"/> row
    /// (append-only — a module can have several verification attempts over
    /// time) — never from <see cref="CapabilityModule.Metrics"/>, which is
    /// only populated when a module is actually Verified and would be
    /// empty for a RequiresRevision module, losing the TargetMetric
    /// entirely.
    /// </summary>
    public static RuntimePedagogicalContract Project(CapabilityModule module)
    {
        var latestVerification = module.Verifications
            .OrderByDescending(v => v.CreatedDate)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"CapabilityModule '{module.CapabilityModuleId}' has no CapabilityModuleVerification " +
                "rows — cannot determine its TargetMetric/SuccessCriteria. A published module must " +
                "have gone through Studio's Métrico verification at least once.");

        return new RuntimePedagogicalContract
        {
            CapabilityModuleId = module.CapabilityModuleId,
            CapabilityId = module.CapabilityLevel.CapabilityId,
            TargetMetric = latestVerification.TargetMetric,
            RecallRequirement = module.RecallRequirement,
            LearnerProduction = module.LearnerProduction,
            LearnerTask = module.LearnerTask,
            ModuleTitle = module.Title,
            ModuleDescription = module.Description,
            ModuleScript = module.Script,
            ReflectionPrompt = module.ReflectionPrompt,
            CapabilityTitle = module.CapabilityLevel.Capability.Name,
            CapabilityCode = module.CapabilityLevel.Capability.Code,
            SuccessCriteria =
            [
                .. latestVerification.SuccessCriteriaResults
                    .OrderBy(r => r.SortOrder)
                    .Select(r => r.Criterion)
            ],
            Chapters =
            [
                .. module.Chapters
                    .OrderBy(c => c.SortOrder)
                    .Select(c => new RuntimeModuleChapter
                    {
                        Title = c.Title,
                        TeachingContent = c.TeachingContent,
                        IsPrimaryWeight = c.IsPrimaryWeight,
                        RecallPrompt = c.RecallPrompt,
                        IsCumulativeRecall = c.IsCumulativeRecall,
                        PredictionPrompt = c.PredictionPrompt,
                        MiniPracticePrompt = c.MiniPracticePrompt
                    })
            ]
        };
    }
}
