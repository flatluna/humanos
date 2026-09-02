namespace HumanOS.Contracts.Gamification;

/// <summary>
/// Consolidated bundle of gamification rewards, streak changes, and newly awarded badges
/// triggered upon mastering an engram.
/// </summary>
public class GamificationRewardBundle
{
    public RewardResult? Reward { get; set; }

    public StreakResult? Streak { get; set; }

    public List<AwardedBadge> NewBadges { get; set; } = [];

    public bool AlreadyEarned { get; set; }
}
