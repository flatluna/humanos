namespace HumanOS.Contracts.Practice;

public sealed class RecordPracticeRequest
{
    public Guid PersonCapabilityId { get; set; }

    public string PracticeType { get; set; } = null!;

    public int AssistanceLevel { get; set; }

    public string? PersonReflection { get; set; }

    public string LanguageCode { get; set; } = "en";

    public DateTime? PracticedDate { get; set; }
}
