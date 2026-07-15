namespace HumanOS.Contracts.Capabilities;

public sealed class PersonCapabilityResponse
{
    public Guid PersonCapabilityId { get; set; }

    public Guid PersonId { get; set; }

    public Guid CapabilityId { get; set; }

    public string CapabilityCode { get; set; } = null!;

    public string CapabilityName { get; set; } = null!;

    public int CurrentLevel { get; set; }

    public int TargetLevel { get; set; }

    public decimal ProgressPercentage { get; set; }

    public decimal MasteryScore { get; set; }

    public int IndependenceLevel { get; set; }

    public decimal? RetentionScore { get; set; }

    public decimal? ConfidenceScore { get; set; }

    public int KnowledgeScore { get; set; }

    public int RecallScore { get; set; }

    public int ApplicationScore { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? StartedDate { get; set; }

    public DateTime? LastActivityDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
