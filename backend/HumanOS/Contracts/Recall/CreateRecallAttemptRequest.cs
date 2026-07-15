namespace HumanOS.Contracts.Recall;

public sealed class CreateRecallAttemptRequest
{
    public Guid PersonCapabilityId { get; set; }

    public string RecallPrompt { get; set; } = null!;

    public string LanguageCode { get; set; } = "en";
}
