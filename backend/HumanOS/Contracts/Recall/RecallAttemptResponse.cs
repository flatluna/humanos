namespace HumanOS.Contracts.Recall;

public sealed class RecallAttemptResponse
{
    public Guid RecallAttemptId { get; set; }

    public Guid PersonCapabilityId { get; set; }

    public string RecallPrompt { get; set; } = null!;

    public string? PersonResponse { get; set; }

    public decimal? RecallScore { get; set; }

    public decimal? ConfidenceScore { get; set; }

    public int AssistanceLevel { get; set; }

    public string LanguageCode { get; set; } = null!;

    public DateTime AttemptedDate { get; set; }

    public DateTime CreatedDate { get; set; }
}
