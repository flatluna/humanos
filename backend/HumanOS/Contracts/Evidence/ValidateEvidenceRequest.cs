namespace HumanOS.Contracts.Evidence;

public sealed class ValidateEvidenceRequest
{
    public string ValidationStatus { get; set; } = null!;

    public string? ValidationFeedback { get; set; }
}
