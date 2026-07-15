namespace HumanOS.Contracts.Evidence;

public sealed class EvidenceResponse
{
    public Guid EvidenceId { get; set; }

    public Guid PersonId { get; set; }

    public Guid CapabilityId { get; set; }

    public Guid? PersonProjectId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string EvidenceType { get; set; } = null!;

    public string? EvidenceUrl { get; set; }

    public string ValidationStatus { get; set; } = null!;

    public int AssistanceLevel { get; set; }

    public string? ValidationFeedback { get; set; }

    public DateTime? ValidatedDate { get; set; }

    public DateTime SubmittedDate { get; set; }

    public DateTime CreatedDate { get; set; }
}
