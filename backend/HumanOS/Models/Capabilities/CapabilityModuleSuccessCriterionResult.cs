namespace HumanOS.Models.Capabilities;

/// <summary>
/// One approved SuccessCriterion's individual verification result (Paso 6,
/// 2026-07-14) — never a blanket "criteria are present" (see
/// HUMAN-OS-STUDIO.md §13, §15).
/// </summary>
public sealed class CapabilityModuleSuccessCriterionResult
{
    public Guid CapabilityModuleSuccessCriterionResultId { get; set; }

    public Guid CapabilityModuleVerificationId { get; set; }

    public int SortOrder { get; set; }

    public string Criterion { get; set; } = null!;

    public bool IsSatisfied { get; set; }

    public string Evidence { get; set; } = null!;

    public CapabilityModuleVerification CapabilityModuleVerification { get; set; } = null!;
}
