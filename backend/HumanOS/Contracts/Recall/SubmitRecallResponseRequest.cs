namespace HumanOS.Contracts.Recall;

public sealed class SubmitRecallResponseRequest
{
    public string PersonResponse { get; set; } = null!;

    public decimal? ConfidenceScore { get; set; }

    public int AssistanceLevel { get; set; }
}
