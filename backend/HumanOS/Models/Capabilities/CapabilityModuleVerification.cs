using HumanOS.Agents.Studio;

namespace HumanOS.Models.Capabilities;

/// <summary>
/// Full, evidence-based verification of a module's TargetMetric (Paso 6,
/// 2026-07-14) — persists what previously only lived in memory via
/// <c>ModuleMetricAssignment.Verification</c> (see HUMAN-OS-STUDIO.md §13,
/// §15). Append-only by design (no unique constraint on
/// <see cref="CapabilityModuleId"/>): a future retry/regeneration of a
/// module should add a NEW row here rather than overwrite the previous
/// attempt, matching the "history, not overwrite" pattern already used
/// for <c>AgentTokenUsage</c>.
/// </summary>
public sealed class CapabilityModuleVerification
{
    public Guid CapabilityModuleVerificationId { get; set; }

    public Guid CapabilityModuleId { get; set; }

    public CapabilityMetric TargetMetric { get; set; }

    public MetricVerificationStatus Status { get; set; }

    public string Evidence { get; set; } = null!;

    public string EvidenceLocation { get; set; } = null!;

    public string Explanation { get; set; } = null!;

    public RecallVerificationStatus RecallStatus { get; set; }

    public string RecallEvidence { get; set; } = null!;

    public string RecallEvidenceLocation { get; set; } = null!;

    public bool RecallOccursBeforeInstruction { get; set; }

    public DateTime CreatedDate { get; set; }

    public CapabilityModule CapabilityModule { get; set; } = null!;

    public ICollection<CapabilityModuleSuccessCriterionResult> SuccessCriteriaResults { get; set; } = [];
}
