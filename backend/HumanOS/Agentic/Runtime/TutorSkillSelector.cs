using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Selects which <see cref="TutorSkill"/> (if any) applies to the current
/// turn, from <see cref="RuntimeStage"/> and the module's
/// <see cref="RuntimePedagogicalContract.TargetMetric"/> (fixed Paso 5,
/// 2026-07-14). This is Runtime-owned selection logic — the Tutor Agent
/// never decides which Skill is active, same authority split as
/// <see cref="TutorTurnContextBuilder.ComputePermissions"/>.
/// </summary>
internal static class TutorSkillSelector
{
    public static TutorSkill? Select(RuntimeStage stage, CapabilityMetric targetMetric) => stage switch
    {
        RuntimeStage.RecallRequired => TutorSkill.Recall,
        RuntimeStage.PredictionRequired => TutorSkill.Prediction,
        RuntimeStage.Reflection => TutorSkill.Reflection,

        // LearnerProduction's applicable skill depends on WHICH metric this
        // module is actually building — mirrors the Level×Metric coordinate
        // already established in Studio (see ArquitectoAgent's TargetMetric).
        RuntimeStage.LearnerProduction => targetMetric switch
        {
            CapabilityMetric.Application => TutorSkill.Application,
            CapabilityMetric.Confidence => TutorSkill.Confidence,
            CapabilityMetric.Independence => TutorSkill.Independence,
            CapabilityMetric.Recall => TutorSkill.Recall,
            _ => null
        },

        // ModuleStarted/Instruction/Assessment/Completed: no Skill applies
        // (Instruction presents content; Assessment judgment is Paso 6).
        _ => null
    };
}
