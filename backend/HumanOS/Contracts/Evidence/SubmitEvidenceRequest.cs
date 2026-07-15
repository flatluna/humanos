namespace HumanOS.Contracts.Evidence;

public sealed class SubmitEvidenceRequest
{
    public Guid CapabilityId { get; set; }

    public Guid? PersonProjectId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string EvidenceType { get; set; } = null!;

    public string? EvidenceUrl { get; set; }

    public int AssistanceLevel { get; set; }
}
