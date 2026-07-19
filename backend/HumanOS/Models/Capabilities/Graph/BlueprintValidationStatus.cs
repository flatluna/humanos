namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// Outcome of a <c>BlueprintValidatorAgent</c> run (Paso 4) over one
/// <see cref="NodeExperienceBlueprint"/>. Two "approved" states and two
/// "not approved" states — a Blueprint only reaches Runtime with confidence
/// once it is <see cref="Approved"/> or <see cref="ApprovedWithWarnings"/>.
/// </summary>
public enum BlueprintValidationStatus
{
    /// <summary>No issues, no warnings — fully compliant with the Memory Paradox.</summary>
    Approved,

    /// <summary>No blocking issues, but one or more non-blocking warnings were raised.</summary>
    ApprovedWithWarnings,

    /// <summary>At least one blocking issue found — ExperienceDesigner must regenerate/fix the blueprint.</summary>
    NeedsRevision,

    /// <summary>Severe structural violation (e.g. missing step, empty content) — never usable as-is.</summary>
    Rejected
}
