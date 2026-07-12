namespace HumanOS.Models.People;

public sealed class HumanProfile
{
    public Guid HumanProfileId { get; set; }

    public Guid PersonId { get; set; }

    public string? MissionStatement { get; set; }

    public string? PrimaryGoal { get; set; }

    public string? LearningStyle { get; set; }

    public string? CurrentLifeStage { get; set; }

    public decimal? WeeklyAvailabilityHours { get; set; }

    public decimal? MotivationScore { get; set; }

    public decimal? ConfidenceScore { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public Person Person { get; set; } = null!;
}
