namespace HumanOS.Contracts.People;

public sealed class UpsertHumanProfileRequest
{
    public string? MissionStatement { get; set; }

    public string? PrimaryGoal { get; set; }

    public string? LearningStyle { get; set; }

    public string? CurrentLifeStage { get; set; }

    public decimal? WeeklyAvailabilityHours { get; set; }

    public decimal? MotivationScore { get; set; }

    public decimal? ConfidenceScore { get; set; }
}
