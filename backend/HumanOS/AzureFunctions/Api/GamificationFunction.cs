using System.Net;
using System.Web;
using HumanOS.Contracts.Gamification;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Read-only API exposing student gamification status: player profile (points, levels, streaks),
/// badge collection (earned and locked), and recent reward ledger history.
/// </summary>
public sealed class GamificationFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly PersonService _personService;
    private readonly BadgeService _badgeService;
    private readonly ILogger<GamificationFunction> _logger;

    public GamificationFunction(
        HumanOsDbContext dbContext,
        PersonService personService,
        BadgeService badgeService,
        ILogger<GamificationFunction> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _personService = personService ?? throw new ArgumentNullException(nameof(personService));
        _badgeService = badgeService ?? throw new ArgumentNullException(nameof(badgeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// GET api/gamification/profile
    /// Retrieves the player summary for the authenticated student.
    /// If no profile exists yet, returns default level 1 "Neurona" values.
    /// </summary>
    [Function("GetGamificationProfile")]
    public async Task<HttpResponseData> GetProfileAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "gamification/profile")]
        HttpRequestData request,
        CancellationToken ct)
    {
        var personId = await ResolveAuthenticatedPersonIdAsync(request, ct);
        if (personId is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "Unauthorized", "Authenticated person identity is required.", ct);
        }

        try
        {
            var profile = await _dbContext.PlayerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PersonId == personId.Value, ct);

            int totalPoints = profile?.TotalPoints ?? 0;
            int currentLevel = profile?.CurrentLevel ?? 1;
            string currentLevelName = profile?.CurrentLevelName ?? "Neurona";
            int currentStreakDays = profile?.CurrentStreakDays ?? 0;
            int longestStreakDays = profile?.LongestStreakDays ?? 0;
            int streakFreezesAvailable = profile?.StreakFreezesAvailable ?? 1;
            int totalEngramsMastered = profile?.TotalEngramsMastered ?? 0;

            var (pointsToNextLevel, nextLevelName) = CalculateNextLevelProgress(totalPoints);

            var totalAssignedCourses = await _badgeService.GetTotalAssignedCoursesAsync(personId.Value, ct);
            var completedCourses = await _badgeService.GetCompletedCoursesCountAsync(personId.Value, ct);
            var engramBreakdown = await _badgeService.GetAssignedEngramsBreakdownAsync(personId.Value, ct);

            int coursesReady = engramBreakdown.TotalCourses;
            int coursesPending = Math.Max(0, totalAssignedCourses - coursesReady);

            var dto = new PlayerStatusDto
            {
                TotalPoints = totalPoints,
                CurrentLevel = currentLevel,
                CurrentLevelName = currentLevelName,
                CurrentStreakDays = currentStreakDays,
                LongestStreakDays = longestStreakDays,
                StreakFreezesAvailable = streakFreezesAvailable,
                TotalEngramsMastered = totalEngramsMastered,
                PointsToNextLevel = pointsToNextLevel,
                NextLevelName = nextLevelName,
                TotalAssignedEngrams = engramBreakdown.TotalAssignedEngrams,
                TotalAssignedCourses = totalAssignedCourses,
                CompletedCourses = completedCourses,
                CoursesReady = coursesReady,
                CoursesPendingGeneration = coursesPending
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, dto, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting gamification profile for person {PersonId}", personId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "GamificationProfileError", ex.Message, ct);
        }
    }

    /// <summary>
    /// GET api/gamification/badges
    /// Retrieves all active catalog badges with status indicating whether the authenticated student has unlocked them.
    /// Ordered earned first (by EarnedAt desc), then unearned (by RuleThreshold asc).
    /// </summary>
    [Function("GetGamificationBadges")]
    public async Task<HttpResponseData> GetBadgesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "gamification/badges")]
        HttpRequestData request,
        CancellationToken ct)
    {
        var personId = await ResolveAuthenticatedPersonIdAsync(request, ct);
        if (personId is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "Unauthorized", "Authenticated person identity is required.", ct);
        }

        try
        {
            var activeBadges = await _dbContext.BadgeCatalogs
                .AsNoTracking()
                .Where(b => b.IsActive)
                .ToListAsync(ct);

            var earnedBadges = await _dbContext.PlayerBadges
                .AsNoTracking()
                .Where(b => b.PersonId == personId.Value)
                .ToDictionaryAsync(b => b.BadgeCode, b => b.EarnedAt, StringComparer.OrdinalIgnoreCase, ct);

            var dtos = activeBadges.Select(b =>
            {
                bool isEarned = earnedBadges.TryGetValue(b.Code, out var earnedAt);
                return new BadgeStatusDto
                {
                    Code = b.Code,
                    Category = b.Category,
                    Name = b.Name ?? b.Code,
                    Description = b.Description ?? string.Empty,
                    IconEmoji = b.IconEmoji ?? "🏆",
                    IsEarned = isEarned,
                    EarnedAt = isEarned ? earnedAt : null,
                    RuleType = b.RuleType,
                    RuleThreshold = b.RuleThreshold
                };
            })
            .OrderByDescending(b => b.IsEarned)
            .ThenByDescending(b => b.EarnedAt)
            .ThenBy(b => b.RuleThreshold)
            .ToList();

            return await FunctionResponseFactory.SuccessResponseAsync(request, dtos, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting gamification badges for person {PersonId}", personId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "GamificationBadgesError", ex.Message, ct);
        }
    }

    /// <summary>
    /// GET api/gamification/badges/progress
    /// Retrieves all active catalog badges with per-badge progress (current value, target threshold, % progress, earned status)
    /// for the authenticated student.
    /// Ordered: earned first (by EarnedAt desc), then unearned by ProgressPercent desc (closest-to-complete first).
    /// </summary>
    [Function("GetGamificationBadgesProgress")]
    public async Task<HttpResponseData> GetBadgesProgressAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "gamification/badges/progress")]
        HttpRequestData request,
        CancellationToken ct)
    {
        var personId = await ResolveAuthenticatedPersonIdAsync(request, ct);
        if (personId is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "Unauthorized", "Authenticated person identity is required.", ct);
        }

        try
        {
            var progress = await _badgeService.GetBadgeProgressAsync(personId.Value, ct);
            return await FunctionResponseFactory.SuccessResponseAsync(request, progress, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting gamification badge progress for person {PersonId}", personId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "GamificationBadgeProgressError", ex.Message, ct);
        }
    }

    /// <summary>
    /// GET api/gamification/rewards/recent?limit=20
    /// Retrieves the recent reward history ledger entries for the authenticated student.
    /// Limit is between 1 and 100 (default 20).
    /// </summary>
    [Function("GetGamificationRecentRewards")]
    public async Task<HttpResponseData> GetRecentRewardsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "gamification/rewards/recent")]
        HttpRequestData request,
        CancellationToken ct)
    {
        var personId = await ResolveAuthenticatedPersonIdAsync(request, ct);
        if (personId is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "Unauthorized", "Authenticated person identity is required.", ct);
        }

        try
        {
            var query = HttpUtility.ParseQueryString(request.Url.Query);
            int limit = 20;
            if (int.TryParse(query["limit"], out var parsedLimit) && parsedLimit > 0)
            {
                limit = Math.Clamp(parsedLimit, 1, 100);
            }

            var entries = await _dbContext.RewardLedgerEntries
                .AsNoTracking()
                .Where(r => r.PersonId == personId.Value)
                .OrderByDescending(r => r.EarnedAt)
                .Take(limit)
                .Select(r => new RewardHistoryDto
                {
                    EngramId = r.EngramId,
                    PointsAwarded = r.PointsAwarded,
                    WasSurprise = r.WasSurprise,
                    SurpriseType = r.SurpriseType,
                    Reason = r.Reason,
                    EarnedAt = r.EarnedAt
                })
                .ToListAsync(ct);

            return await FunctionResponseFactory.SuccessResponseAsync(request, entries, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent rewards for person {PersonId}", personId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "GamificationRecentRewardsError", ex.Message, ct);
        }
    }

    /// <summary>
    /// Calculates how many points are needed to reach the next level threshold and the name of that next level.
    /// Bands:
    /// Level 1: 0 - 499     -> Next is 500 ("Sinapsis")
    /// Level 2: 500 - 1499  -> Next is 1500 ("Circuito")
    /// Level 3: 1500 - 3499 -> Next is 3500 ("Red Neuronal")
    /// Level 4: 3500 - 6999 -> Next is 7000 ("Mente Maestra")
    /// Level 5: 7000+       -> Max level reached (0 points to next, null next name)
    /// </summary>
    public static (int PointsToNextLevel, string? NextLevelName) CalculateNextLevelProgress(int totalPoints)
    {
        if (totalPoints < 500)
        {
            return (500 - totalPoints, "Sinapsis");
        }

        if (totalPoints < 1500)
        {
            return (1500 - totalPoints, "Circuito");
        }

        if (totalPoints < 3500)
        {
            return (3500 - totalPoints, "Red Neuronal");
        }

        if (totalPoints < 7000)
        {
            return (7000 - totalPoints, "Mente Maestra");
        }

        return (0, null);
    }

    private async Task<Guid?> ResolveAuthenticatedPersonIdAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        // 1. Identity headers (MSAL / Enterprise auth)
        if (request.Headers.TryGetValues("X-Azure-OID", out var oidValues) && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
            request.Headers.TryGetValues("X-Azure-TID", out var tidValues) && !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            var person = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
            if (person is not null)
            {
                return person.PersonId;
            }
        }

        // 2. Direct header
        if (request.Headers.TryGetValues("X-Person-Id", out var personValues) &&
            Guid.TryParse(personValues.FirstOrDefault(), out var parsedHeaderId))
        {
            return parsedHeaderId;
        }

        // 3. Query string parameter (?personId=... or ?employeeId=...)
        var query = HttpUtility.ParseQueryString(request.Url.Query);
        var queryPersonId = query["personId"] ?? query["employeeId"];
        if (!string.IsNullOrWhiteSpace(queryPersonId) && Guid.TryParse(queryPersonId, out var parsedQueryId))
        {
            return parsedQueryId;
        }

        // 4. Fallback for single-tenant / local development context
        var defaultPersonId = await _dbContext.People
            .Where(p => p.IsActive)
            .OrderBy(p => p.CreatedDate)
            .Select(p => (Guid?)p.PersonId)
            .FirstOrDefaultAsync(cancellationToken);

        return defaultPersonId;
    }
}
