using HumanOS.Contracts.Gamification;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services;

/// <summary>
/// Master gamification orchestrator that executes the neuro-science reward sequence
/// when a student passes/masters an engram:
/// 1. RewardEngineService: points calculation, variable surprise bonus, level bands, and frozen ledger entry.
/// 2. StreakService: daily activity registration and streak/freeze progression.
/// 3. BadgeService: evaluation and unlocking of any new achievement badges based on the latest points and streak.
/// </summary>
public class GamificationOrchestrator
{
    private readonly RewardEngineService _rewardEngine;
    private readonly StreakService _streakService;
    private readonly BadgeService _badgeService;
    private readonly ILogger<GamificationOrchestrator> _logger;

    public GamificationOrchestrator(
        RewardEngineService rewardEngine,
        StreakService streakService,
        BadgeService badgeService,
        ILogger<GamificationOrchestrator> logger)
    {
        _rewardEngine = rewardEngine ?? throw new ArgumentNullException(nameof(rewardEngine));
        _streakService = streakService ?? throw new ArgumentNullException(nameof(streakService));
        _badgeService = badgeService ?? throw new ArgumentNullException(nameof(badgeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes engram mastery rewards in sequence: points -> daily streak -> badge unlocks.
    /// </summary>
    public async Task<GamificationRewardBundle> ProcessEngramMasteryAsync(
        Guid personId,
        Guid courseId,
        string engramId,
        string sourceType,
        int? engramDifficultyLevel = null,
        double? memoryRecallPct = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engramId);

        _logger.LogInformation(
            "Processing engram mastery gamification for person {PersonId}, course {CourseId}, engram {EngramId}, source {SourceType}.",
            personId, courseId, engramId, sourceType);

        // 1. RewardEngine (points, surprise, level-up, frozen ledger, idempotency check)
        RewardResult reward;
        try
        {
            reward = await _rewardEngine.GrantEngramRewardAsync(
                personId,
                courseId,
                engramId,
                sourceType,
                engramDifficultyLevel,
                memoryRecallPct,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gamification stage 'RewardEngine' failed for person {PersonId}, engram {EngramId}.", personId, engramId);
            throw new InvalidOperationException($"Gamification stage 'RewardEngine' failed: {ex.Message}", ex);
        }

        // 2. StreakService (daily activity tracking, always active even if reward was already earned earlier)
        StreakResult streak;
        try
        {
            streak = await _streakService.RegisterActivityAsync(personId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gamification stage 'StreakService' failed for person {PersonId}.", personId);
            throw new InvalidOperationException($"Gamification stage 'StreakService' failed: {ex.Message}", ex);
        }

        // 3. BadgeService (evaluate badges at the end to evaluate fresh points & streak)
        List<AwardedBadge> newBadges;
        try
        {
            newBadges = await _badgeService.EvaluateAndGrantBadgesAsync(personId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gamification stage 'BadgeService' failed for person {PersonId}.", personId);
            throw new InvalidOperationException($"Gamification stage 'BadgeService' failed: {ex.Message}", ex);
        }

        return new GamificationRewardBundle
        {
            Reward = reward,
            Streak = streak,
            NewBadges = newBadges,
            AlreadyEarned = reward.AlreadyEarned
        };
    }
}
