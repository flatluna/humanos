namespace HumanOS.Contracts.RoleExperience;

/// <summary>The result of confirming a previously-extracted Job
/// Description — this is what makes it usable as context for a future
/// Development Plan, and is what sets PersonProfile.CurrentJobDescriptionId.</summary>
public sealed class ConfirmJobDescriptionResponse
{
    public Guid JobDescriptionId { get; set; }

    public Guid PersonId { get; set; }

    public string ExtractionStatus { get; set; } = null!;

    public DateTime ConfirmedDate { get; set; }
}
