namespace HumanOS.Models.Capabilities.Graph;

/// <summary>Severity of one <see cref="BlueprintValidationIssue"/>.</summary>
public enum BlueprintValidationIssueSeverity
{
    /// <summary>Non-blocking — the blueprint may still be Approved/ApprovedWithWarnings.</summary>
    Warning,

    /// <summary>Blocking — the blueprint cannot be Approved while this exists.</summary>
    Error
}
