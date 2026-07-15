namespace HumanOS.Contracts.Responses;

public sealed class PracticeResponse
{
    public Guid CapabilityPracticeId { get; set; }

    public Guid PersonCapabilityId { get; set; }

    public string PracticeType { get; set; } = null!;

    public int AssistanceLevel { get; set; }

    public string? PersonReflection { get; set; }

    public string LanguageCode { get; set; } = null!;

    public DateTime PracticedDate { get; set; }

    public DateTime CreatedDate { get; set; }
}
