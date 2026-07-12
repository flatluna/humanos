using HumanOS.Models.Capabilities;
using HumanOS.Models.Localization;

namespace HumanOS.Models.Recall;

public sealed class RecallAttempt
{
    public Guid RecallAttemptId { get; set; }

    public Guid PersonCapabilityId { get; set; }

    public string RecallPrompt { get; set; } = null!;

    public string? PersonResponse { get; set; }

    public decimal? RecallScore { get; set; }

    public decimal? ConfidenceScore { get; set; }

    public int AssistanceLevel { get; set; }

    public string LanguageCode { get; set; } = "en";

    public DateTime AttemptedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public PersonCapability PersonCapability { get; set; } = null!;

    public Language Language { get; set; } = null!;
}
