namespace HumanOS.Contracts.RoleExperience;

/// <summary>The provisional, agent-proposed extraction result. Nothing
/// in this response has been confirmed by the employee yet — see
/// ConfirmJobDescriptionFunction for that step.</summary>
public sealed class JobDescriptionExtractionResponse
{
    public Guid JobDescriptionId { get; set; }

    /// <summary>Pending | Extracted | Failed | Confirmed.</summary>
    public string ExtractionStatus { get; set; } = null!;

    public string JobTitle { get; set; } = null!;

    public string? RolePurpose { get; set; }

    public string? RoleSummary { get; set; }

    public List<string> PrimaryResponsibilities { get; set; } = [];

    public List<string> ExpectedOutcomes { get; set; } = [];

    public string? RequiredExperience { get; set; }

    public List<string> ToolsMentioned { get; set; } = [];

    public string? SuggestedProfessionalLevel { get; set; }

    public string ExtractionModel { get; set; } = null!;

    public DateTime ExtractedDate { get; set; }
}
