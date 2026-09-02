using System.Text.Json;
using HumanOS.Contracts.Gamification;
using HumanOS.Data;
using HumanOS.Models.Gamification;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Service responsible for evaluating badge achievement rules, awarding badges to students,
/// and computing per-badge progress metrics for gamification UI.
/// Evaluates Effort, Progress, Skills, Quality, Challenge, Habit, and Luck rules.
/// Ensures strict idempotency so badges can only ever be earned once per person.
/// </summary>
public class BadgeService
{
    private readonly HumanOsDbContext _dbContext;

    public BadgeService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Helper record storing progress counts for a specific course (Graph or Structured).
    /// </summary>
    private sealed record CourseProgressSummary(Guid CourseId, int TotalEngrams, int MasteredEngrams, bool IsStructured = false)
    {
        public double CompletionPct => TotalEngrams > 0 ? (double)MasteredEngrams / TotalEngrams * 100.0 : 0.0;
        public bool IsFullyCompleted => TotalEngrams > 0 && MasteredEngrams >= TotalEngrams;
    }

    /// <summary>
    /// Detailed breakdown of assigned engrams and courses for an employee.
    /// </summary>
    public sealed record AssignedEngramsBreakdown(
        int TotalAssignedEngrams,
        int TotalCourses,
        int GraphCoursesCount,
        int GraphEngramsCount,
        int StructuredCoursesCount,
        int StructuredEngramsCount);

    /// <summary>
    /// Computes the total assigned engrams across all courses in the student's career plan and enrollments.
    /// </summary>
    public async Task<int> GetTotalAssignedEngramsAsync(
        Guid personId,
        CancellationToken ct = default)
    {
        var context = new BadgeEvaluationContext(_dbContext, personId, profile: null);
        return await context.GetTotalAssignedEngramsAsync(ct);
    }

    /// <summary>
    /// Computes the detailed breakdown of assigned courses and engrams (Graph vs Structured).
    /// </summary>
    public async Task<AssignedEngramsBreakdown> GetAssignedEngramsBreakdownAsync(
        Guid personId,
        CancellationToken ct = default)
    {
        var context = new BadgeEvaluationContext(_dbContext, personId, profile: null);
        return await context.GetAssignedEngramsBreakdownAsync(ct);
    }

    /// <summary>
    /// Computes the total number of assigned courses in an employee's Career Plan (all roadmap skills regardless of generation status)
    /// plus any explicit course enrollments, deduplicated.
    /// </summary>
    public async Task<int> GetTotalAssignedCoursesAsync(
        Guid personId,
        CancellationToken ct = default)
    {
        var context = new BadgeEvaluationContext(_dbContext, personId, profile: null);
        return await context.GetTotalAssignedCoursesAsync(ct);
    }

    /// <summary>
    /// Computes how many assigned courses are 100% completed (all engrams in the course mastered).
    /// </summary>
    public async Task<int> GetCompletedCoursesCountAsync(
        Guid personId,
        CancellationToken ct = default)
    {
        var context = new BadgeEvaluationContext(_dbContext, personId, profile: null);
        return await context.GetCompletedCoursesCountAsync(ct);
    }

    /// <summary>
    /// Computes per-badge progress details for all active catalog badges for the specified student without granting anything.
    /// Pure read-only operation.
    /// Order: earned first (by EarnedAt desc), then unearned by ProgressPercent desc (closest-to-complete first).
    /// </summary>
    public async Task<List<BadgeProgress>> GetBadgeProgressAsync(
        Guid personId,
        CancellationToken ct = default)
    {
        // 1. Load PlayerProfile (read-only)
        var profile = await _dbContext.PlayerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == personId, ct);

        // 2. Load badges already earned by this student (read-only)
        var earnedBadges = await _dbContext.PlayerBadges
            .AsNoTracking()
            .Where(b => b.PersonId == personId)
            .ToDictionaryAsync(b => b.BadgeCode, b => b.EarnedAt, StringComparer.OrdinalIgnoreCase, ct);

        // 3. Load all active catalog badges
        var activeBadges = await _dbContext.BadgeCatalogs
            .AsNoTracking()
            .Where(b => b.IsActive)
            .ToListAsync(ct);

        if (activeBadges.Count == 0)
        {
            return [];
        }

        var context = new BadgeEvaluationContext(_dbContext, personId, profile);
        var progressList = new List<BadgeProgress>(activeBadges.Count);

        foreach (var badge in activeBadges)
        {
            bool isEarned = earnedBadges.TryGetValue(badge.Code, out var earnedAt);
            int currentValue = await context.GetCurrentValueAsync(badge.RuleType, ct);
            int targetValue = await context.GetEffectiveThresholdAsync(badge, ct);
            string description = await context.GetEffectiveDescriptionAsync(badge, ct);

            int progressPercent;
            if (isEarned)
            {
                progressPercent = 100;
            }
            else if (targetValue <= 0)
            {
                progressPercent = currentValue >= targetValue ? 100 : 0;
            }
            else
            {
                progressPercent = Math.Clamp((int)Math.Round((double)currentValue / targetValue * 100.0), 0, 100);
            }

            progressList.Add(new BadgeProgress
            {
                Code = badge.Code,
                Name = badge.Name ?? badge.Code,
                Description = description,
                IconEmoji = badge.IconEmoji ?? "🏆",
                Category = badge.Category,
                RuleType = badge.RuleType,
                TargetValue = targetValue,
                CurrentValue = currentValue,
                ProgressPercent = progressPercent,
                IsEarned = isEarned,
                EarnedAt = isEarned ? earnedAt : null
            });
        }

        return progressList
            .OrderByDescending(b => b.IsEarned)
            .ThenByDescending(b => b.EarnedAt)
            .ThenByDescending(b => b.ProgressPercent)
            .ThenBy(b => b.TargetValue)
            .ThenBy(b => b.Code)
            .ToList();
    }

    /// <summary>
    /// Evaluates all active badge rules against the student's learning achievements,
    /// persists any newly earned badges to PlayerBadge, and returns the awarded badges.
    /// </summary>
    public async Task<List<AwardedBadge>> EvaluateAndGrantBadgesAsync(
        Guid personId,
        CancellationToken ct = default)
    {
        // 1. Load PlayerProfile (if none exists, nothing to evaluate)
        var profile = await _dbContext.PlayerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == personId, ct);

        if (profile == null)
        {
            return [];
        }

        // 2. Load badges already earned by this student
        var earnedBadgeCodes = (await _dbContext.PlayerBadges
            .Where(b => b.PersonId == personId)
            .Select(b => b.BadgeCode)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 3. Load all active catalog badges
        var activeBadges = await _dbContext.BadgeCatalogs
            .Where(b => b.IsActive)
            .ToListAsync(ct);

        var candidateBadges = activeBadges
            .Where(b => !earnedBadgeCodes.Contains(b.Code))
            .ToList();

        if (candidateBadges.Count == 0)
        {
            return [];
        }

        var context = new BadgeEvaluationContext(_dbContext, personId, profile);
        var awardedBadges = new List<AwardedBadge>();
        var now = DateTime.UtcNow;

        foreach (var badge in candidateBadges)
        {
            int currentValue = await context.GetCurrentValueAsync(badge.RuleType, ct);
            int targetValue = await context.GetEffectiveThresholdAsync(badge, ct);
            bool isFulfilled = currentValue >= targetValue;

            if (isFulfilled)
            {
                var playerBadge = new PlayerBadge
                {
                    Id = Guid.NewGuid(),
                    PersonId = personId,
                    BadgeCode = badge.Code,
                    EarnedAt = now
                };

                _dbContext.PlayerBadges.Add(playerBadge);
                earnedBadgeCodes.Add(badge.Code);

                string description = await context.GetEffectiveDescriptionAsync(badge, ct);

                awardedBadges.Add(new AwardedBadge
                {
                    BadgeCode = badge.Code,
                    Name = badge.Name ?? badge.Code,
                    IconEmoji = badge.IconEmoji ?? "🏆",
                    Description = description,
                    EarnedAt = now
                });
            }
        }

        if (awardedBadges.Count > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
        }

        return awardedBadges;
    }

    /// <summary>
    /// Encapsulates lazy-loaded aggregates and computation rules across badges.
    /// Shared by both EvaluateAndGrantBadgesAsync and GetBadgeProgressAsync to guarantee
    /// identical evaluation results without code duplication.
    /// </summary>
    private sealed class BadgeEvaluationContext
    {
        private readonly HumanOsDbContext _dbContext;
        private readonly Guid _personId;
        private readonly PlayerProfile? _profile;

        private int? _criticalMasteredCount;
        private int? _jackpotCount;
        private int? _memoryRescuedCount;
        private int? _perfectScoresCount;
        private int? _firstTryMasteriesCount;
        private int? _maxFlawlessStreak;
        private int? _automaticEngramsCount;
        private int? _totalAssignedEngrams;
        private int? _totalAssignedCourses;
        private List<CourseProgressSummary>? _allCourseProgress;
        private int? _fullyCompletedCoursesCount;
        private double? _maxCourseProgressPct;

        public BadgeEvaluationContext(HumanOsDbContext dbContext, Guid personId, PlayerProfile? profile)
        {
            _dbContext = dbContext;
            _personId = personId;
            _profile = profile;
        }

        /// <summary>
        /// Dynamic Effort Milestone percentages:
        /// Apprentice: 5%, Practitioner: 15%, Expert: 30%, Master: 50%, Scholar: 75%, Legend: 100%.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (double Percent, string Name)> DynamicEffortBadges =
            new Dictionary<string, (double, string)>(StringComparer.OrdinalIgnoreCase)
            {
                ["effort_10"]  = (0.05, "5%"),
                ["effort_25"]  = (0.15, "15%"),
                ["effort_50"]  = (0.30, "30%"),
                ["effort_100"] = (0.50, "50%"),
                ["effort_200"] = (0.75, "75%"),
                ["effort_300"] = (1.00, "100%"),
            };

        /// <summary>
        /// Dynamic Skills Milestone percentages (Career Courses scale):
        /// First Skill: 5%, Getting Started: 15%, Building Momentum: 30%, Halfway Expert: 50%, Almost There: 75%, Skill Master: 100%.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (double Percent, string Name)> DynamicSkillsBadges =
            new Dictionary<string, (double, string)>(StringComparer.OrdinalIgnoreCase)
            {
                ["skills_1"]   = (0.05, "5%"),
                ["skills_3"]   = (0.15, "15%"),
                ["skills_6"]   = (0.30, "30%"),
                ["skills_10"]  = (0.50, "50%"),
                ["skills_15"]  = (0.75, "75%"),
                ["skills_all"] = (1.00, "100%"),
            };

        public async Task<int> GetEffectiveThresholdAsync(BadgeCatalog badge, CancellationToken ct)
        {
            if (DynamicEffortBadges.TryGetValue(badge.Code, out var effortConfig))
            {
                if (!_totalAssignedEngrams.HasValue)
                {
                    _totalAssignedEngrams = await GetTotalAssignedEngramsAsync(ct);
                }

                int totalAvailable = _totalAssignedEngrams.Value;
                if (totalAvailable <= 0)
                {
                    return badge.RuleThreshold;
                }

                return Math.Max(1, (int)Math.Ceiling(totalAvailable * effortConfig.Percent));
            }

            if (DynamicSkillsBadges.TryGetValue(badge.Code, out var skillConfig))
            {
                if (!_totalAssignedCourses.HasValue)
                {
                    _totalAssignedCourses = await GetTotalAssignedCoursesAsync(ct);
                }

                int totalCourses = _totalAssignedCourses.Value;
                if (totalCourses <= 0)
                {
                    return badge.RuleThreshold;
                }

                return Math.Max(1, (int)Math.Ceiling(totalCourses * skillConfig.Percent));
            }

            return badge.RuleThreshold;
        }

        public async Task<string> GetEffectiveDescriptionAsync(BadgeCatalog badge, CancellationToken ct)
        {
            if (DynamicEffortBadges.TryGetValue(badge.Code, out var effortConfig))
            {
                if (!_totalAssignedEngrams.HasValue)
                {
                    _totalAssignedEngrams = await GetTotalAssignedEngramsAsync(ct);
                }

                int totalAvailable = _totalAssignedEngrams.Value;
                if (totalAvailable > 0)
                {
                    int target = Math.Max(1, (int)Math.Ceiling(totalAvailable * effortConfig.Percent));
                    int pctLabel = (int)Math.Round(effortConfig.Percent * 100);
                    return $"Master {pctLabel}% of your available engrams ({target} of {totalAvailable}).";
                }
            }

            if (DynamicSkillsBadges.TryGetValue(badge.Code, out var skillConfig))
            {
                if (!_totalAssignedCourses.HasValue)
                {
                    _totalAssignedCourses = await GetTotalAssignedCoursesAsync(ct);
                }

                int totalCourses = _totalAssignedCourses.Value;
                if (totalCourses > 0)
                {
                    int target = Math.Max(1, (int)Math.Ceiling(totalCourses * skillConfig.Percent));
                    int pctLabel = (int)Math.Round(skillConfig.Percent * 100);
                    return $"Complete {pctLabel}% of your career courses ({target} of {totalCourses}).";
                }
            }

            return badge.Description ?? string.Empty;
        }

        public async Task<int> GetCurrentValueAsync(string? ruleType, CancellationToken ct)
        {
            var normalizedRule = ruleType?.Trim().ToLowerInvariant();

            switch (normalizedRule)
            {
                // ------------------------------------------------------------
                // CATEGORY A: EFFORT MILESTONES
                // ------------------------------------------------------------
                case "engrams_mastered":
                    return _profile?.TotalEngramsMastered ?? 0;

                // ------------------------------------------------------------
                // CATEGORY B & C: COURSE PROGRESS & SKILLS COMPLETED
                // ------------------------------------------------------------
                case "course_progress_pct":
                    await EnsureCourseProgressLoadedAsync(ct);
                    return (int)Math.Round(_maxCourseProgressPct ?? 0.0);

                case "course_completed":
                case "skills_completed":
                case "all_skills_completed":
                    await EnsureCourseProgressLoadedAsync(ct);
                    return _fullyCompletedCoursesCount ?? 0;

                // ------------------------------------------------------------
                // CATEGORY D: MASTERY / QUALITY
                // ------------------------------------------------------------
                case "perfect_scores":
                    if (!_perfectScoresCount.HasValue)
                    {
                        _perfectScoresCount = await ComputePerfectScoresCountAsync(_personId, ct);
                    }
                    return _perfectScoresCount.Value;

                case "first_try_masteries":
                    if (!_firstTryMasteriesCount.HasValue)
                    {
                        _firstTryMasteriesCount = await ComputeFirstTryMasteriesCountAsync(_personId, ct);
                    }
                    return _firstTryMasteriesCount.Value;

                case "flawless_streak":
                    if (!_maxFlawlessStreak.HasValue)
                    {
                        _maxFlawlessStreak = await ComputeFlawlessStreakAsync(_personId, ct);
                    }
                    return _maxFlawlessStreak.Value;

                // ------------------------------------------------------------
                // CATEGORY E: CHALLENGE
                // ------------------------------------------------------------
                case "critical_mastered":
                    if (!_criticalMasteredCount.HasValue)
                    {
                        _criticalMasteredCount = await ComputeCriticalMasteredCountAsync(_personId, ct);
                    }
                    return _criticalMasteredCount.Value;

                // ------------------------------------------------------------
                // CATEGORY F: HABIT / CONSISTENCY
                // ------------------------------------------------------------
                case "streak_days":
                    return _profile?.CurrentStreakDays ?? 0;

                case "memory_rescued":
                    if (!_memoryRescuedCount.HasValue)
                    {
                        _memoryRescuedCount = await ComputeMemoryRescuedCountAsync(_personId, ct);
                    }
                    return _memoryRescuedCount.Value;

                case "automatic_engrams":
                    if (!_automaticEngramsCount.HasValue)
                    {
                        _automaticEngramsCount = await ComputeAutomaticEngramsCountAsync(_personId, ct);
                    }
                    return _automaticEngramsCount.Value;

                // ------------------------------------------------------------
                // CATEGORY G: LUCK / SURPRISE
                // ------------------------------------------------------------
                case "jackpot_hit":
                    if (!_jackpotCount.HasValue)
                    {
                        _jackpotCount = await ComputeJackpotCountAsync(_personId, ct);
                    }
                    return _jackpotCount.Value;

                default:
                    return 0;
            }
        }

        private async Task EnsureCourseProgressLoadedAsync(CancellationToken ct)
        {
            if (_allCourseProgress == null)
            {
                _allCourseProgress = await LoadAllCourseProgressAsync(_personId, ct);
                _maxCourseProgressPct = _allCourseProgress.Count > 0
                    ? _allCourseProgress.Max(c => c.CompletionPct)
                    : 0.0;
                _fullyCompletedCoursesCount = _allCourseProgress.Count(c => c.IsFullyCompleted);
            }
        }

        private async Task<List<CourseProgressSummary>> LoadAllCourseProgressAsync(Guid personId, CancellationToken ct)
        {
            var summaries = new List<CourseProgressSummary>();

            // 1. Graph Courses: count nodes in CapabilityGraphNode and distinct mastered nodes in LearningSessionNodes
            var graphCapabilities = await _dbContext.CapabilityGraphs
                .AsNoTracking()
                .Where(g => g.Capability != null && g.Capability.IsActive)
                .Select(g => new
                {
                    g.CapabilityId,
                    TotalNodes = g.Nodes.Count
                })
                .ToListAsync(ct);

            var masteredGraphNodes = await _dbContext.LearningSessionNodes
                .AsNoTracking()
                .Where(n => n.LearningSession!.PersonId == personId
                         && (n.Status == Models.Learning.LearningSessionNodeStatus.Completed || n.AssessmentResults.Any(a => a.Passed)))
                .Select(n => new { CourseId = n.LearningSession!.CapabilityId, n.CapabilityGraphNodeId })
                .Distinct()
                .ToListAsync(ct);

            var masteredGraphByCourse = masteredGraphNodes
                .GroupBy(x => x.CourseId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var cap in graphCapabilities)
            {
                if (cap.TotalNodes == 0) continue;
                masteredGraphByCourse.TryGetValue(cap.CapabilityId, out var mastered);
                summaries.Add(new CourseProgressSummary(cap.CapabilityId, cap.TotalNodes, mastered, IsStructured: false));
            }

            // 2. Structured Learning Courses: count sections in StructuredLearningDocumentSection and distinct passed rounds
            var structuredDocs = await _dbContext.StructuredLearningDocuments
                .AsNoTracking()
                .Select(d => new
                {
                    d.StructuredLearningDocumentId,
                    TotalSections = d.Sections.Count
                })
                .ToListAsync(ct);

            var masteredStructuredSections = await _dbContext.StructuredLearningAssessmentRounds
                .AsNoTracking()
                .Where(r => r.PersonId == personId && r.Status == Models.Capabilities.Studio.StructuredLearningAssessmentRoundStatus.Passed)
                .Join(
                    _dbContext.StructuredLearningDocumentSections.AsNoTracking(),
                    r => r.StructuredLearningDocumentSectionId,
                    s => s.StructuredLearningDocumentSectionId,
                    (r, s) => new { CourseId = s.StructuredLearningDocumentId, s.StructuredLearningDocumentSectionId }
                )
                .Distinct()
                .ToListAsync(ct);

            // Also incorporate RewardLedgerEntries for structured learning to capture any offline/fallback-awarded engrams
            var ledgerStructuredMastered = await _dbContext.RewardLedgerEntries
                .AsNoTracking()
                .Where(r => r.PersonId == personId && (r.SourceType == "Structured" || r.SourceType == null))
                .Select(r => new { CourseId = r.CourseId, SectionId = r.EngramId })
                .Distinct()
                .ToListAsync(ct);

            var structuredLookup = masteredStructuredSections
                .Select(x => new { x.CourseId, SectionId = x.StructuredLearningDocumentSectionId.ToString() })
                .Concat(ledgerStructuredMastered)
                .Distinct()
                .GroupBy(x => x.CourseId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var doc in structuredDocs)
            {
                if (doc.TotalSections == 0) continue;
                structuredLookup.TryGetValue(doc.StructuredLearningDocumentId, out var mastered);
                summaries.Add(new CourseProgressSummary(doc.StructuredLearningDocumentId, doc.TotalSections, mastered, IsStructured: true));
            }

            return summaries;
        }

        public async Task<int> GetTotalAssignedEngramsAsync(CancellationToken ct)
        {
            var breakdown = await GetAssignedEngramsBreakdownAsync(ct);
            return breakdown.TotalAssignedEngrams;
        }

        public async Task<int> GetTotalAssignedCoursesAsync(CancellationToken ct)
        {
            var distinctCourseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Primary Live Source: JobRoleSkills from the employee's CURRENT JobRole
            var person = await _dbContext.People
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonId == _personId, ct);

            if (person?.JobRoleId != null && person.JobRoleId != Guid.Empty)
            {
                var roleSkills = await _dbContext.JobRoleSkills
                    .AsNoTracking()
                    .Where(jrs => jrs.JobRoleId == person.JobRoleId.Value)
                    .Select(jrs => new { jrs.SkillId, jrs.Skill.Name, jrs.Skill.Category })
                    .ToListAsync(ct);

                foreach (var s in roleSkills)
                {
                    if (!string.IsNullOrWhiteSpace(s.Name))
                    {
                        distinctCourseKeys.Add($"role_skill:{s.Name.Trim().ToLowerInvariant()}");
                    }
                }
            }

            // Fallback to PersonCareerPlan if employee has no JobRole assigned
            if (distinctCourseKeys.Count == 0)
            {
                var plan = await _dbContext.PersonCareerPlans
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PersonId == _personId, ct);

                if (plan != null)
                {
                    void CollectStepKeys(string? json, string prefix)
                    {
                        if (string.IsNullOrWhiteSpace(json)) return;
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var el in doc.RootElement.EnumerateArray())
                                {
                                    if (el.TryGetProperty("MatchedCapabilityId", out var capProp) &&
                                        capProp.ValueKind == JsonValueKind.String &&
                                        Guid.TryParse(capProp.GetString(), out var capId))
                                    {
                                        distinctCourseKeys.Add($"guid:{capId}");
                                        continue;
                                    }
                                    if (el.TryGetProperty("matchedCapabilityId", out var capPropCamel) &&
                                        capPropCamel.ValueKind == JsonValueKind.String &&
                                        Guid.TryParse(capPropCamel.GetString(), out var capIdCamel))
                                    {
                                        distinctCourseKeys.Add($"guid:{capIdCamel}");
                                        continue;
                                    }

                                    var skill = el.TryGetProperty("Skill", out var sProp) ? sProp.GetString()
                                              : el.TryGetProperty("skill", out var sPropCamel) ? sPropCamel.GetString()
                                              : null;
                                    var title = el.TryGetProperty("Title", out var tProp) ? tProp.GetString()
                                              : el.TryGetProperty("title", out var tPropCamel) ? tPropCamel.GetString()
                                              : null;

                                    var identifier = !string.IsNullOrWhiteSpace(skill) ? skill : title;
                                    if (!string.IsNullOrWhiteSpace(identifier))
                                    {
                                        distinctCourseKeys.Add($"skill:{identifier.Trim().ToLowerInvariant()}");
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Ignore JSON parse anomalies
                        }
                    }

                    CollectStepKeys(plan.TechnicalRoadmapJson, "tech");
                    CollectStepKeys(plan.SoftSkillsRoadmapJson, "soft");
                    CollectStepKeys(plan.DepartmentProcessesRoadmapJson, "dept");
                    CollectStepKeys(plan.HrProcessesRoadmapJson, "hr");
                }
            }

            // 2. Explicit Enrollments
            var enrolledCapIds = await _dbContext.CapabilityEnrollments
                .AsNoTracking()
                .Where(e => e.PersonId == _personId)
                .Select(e => e.CapabilityId)
                .ToListAsync(ct);

            var enrolledDocIds = await _dbContext.StructuredLearningDocumentEnrollments
                .AsNoTracking()
                .Where(e => e.PersonId == _personId)
                .Select(e => e.StructuredLearningDocumentId)
                .ToListAsync(ct);

            foreach (var id in enrolledCapIds.Concat(enrolledDocIds))
            {
                distinctCourseKeys.Add($"guid:{id}");
            }

            return distinctCourseKeys.Count;
        }

        public async Task<int> GetCompletedCoursesCountAsync(CancellationToken ct)
        {
            await EnsureCourseProgressLoadedAsync(ct);
            return _fullyCompletedCoursesCount ?? 0;
        }

        public async Task<AssignedEngramsBreakdown> GetAssignedEngramsBreakdownAsync(CancellationToken ct)
        {
            await EnsureCourseProgressLoadedAsync(ct);
            var assignedCourseIds = await GetAssignedCourseIdsAsync(_personId, ct);
            if (assignedCourseIds.Count == 0)
            {
                return new AssignedEngramsBreakdown(0, 0, 0, 0, 0, 0);
            }

            var progressMap = _allCourseProgress!.ToDictionary(p => p.CourseId);
            int graphCourses = 0;
            int graphEngrams = 0;
            int structuredCourses = 0;
            int structuredEngrams = 0;

            foreach (var courseId in assignedCourseIds)
            {
                if (progressMap.TryGetValue(courseId, out var progress))
                {
                    if (progress.IsStructured)
                    {
                        structuredCourses++;
                        structuredEngrams += progress.TotalEngrams;
                    }
                    else
                    {
                        graphCourses++;
                        graphEngrams += progress.TotalEngrams;
                    }
                }
            }

            return new AssignedEngramsBreakdown(
                graphEngrams + structuredEngrams,
                graphCourses + structuredCourses,
                graphCourses,
                graphEngrams,
                structuredCourses,
                structuredEngrams);
        }

        private async Task<HashSet<Guid>> GetAssignedCourseIdsAsync(Guid personId, CancellationToken ct)
        {
            var assignedCourseIds = new HashSet<Guid>();

            // 1. Primary Live Source: Match JobRoleSkills from employee's CURRENT JobRole against catalog courses
            var person = await _dbContext.People
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonId == personId, ct);

            if (person?.JobRoleId != null && person.JobRoleId != Guid.Empty)
            {
                var roleSkills = await _dbContext.JobRoleSkills
                    .AsNoTracking()
                    .Where(jrs => jrs.JobRoleId == person.JobRoleId.Value)
                    .Select(jrs => new { jrs.Skill.Name, jrs.Skill.Category })
                    .ToListAsync(ct);

                if (roleSkills.Count > 0)
                {
                    static string Normalize(string? s) => string.IsNullOrWhiteSpace(s)
                        ? string.Empty
                        : System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", " ");

                    var structuredDocuments = await _dbContext.StructuredLearningDocuments
                        .AsNoTracking()
                        .Select(d => new { d.StructuredLearningDocumentId, d.DocumentName, d.Category, d.TargetSkill })
                        .ToListAsync(ct);

                    var activeCapabilities = await _dbContext.Capabilities
                        .AsNoTracking()
                        .Where(c => c.IsActive)
                        .Select(c => new { c.CapabilityId, c.Name, c.Category, c.TargetSkill })
                        .ToListAsync(ct);

                    foreach (var s in roleSkills)
                    {
                        var sNorm = Normalize(s.Name);

                        // Match Structured Learning Documents
                        var docMatch = structuredDocuments.FirstOrDefault(d =>
                            (d.TargetSkill != null && string.Equals(Normalize(d.TargetSkill), sNorm, StringComparison.OrdinalIgnoreCase)) ||
                            string.Equals(Normalize(d.DocumentName), sNorm, StringComparison.OrdinalIgnoreCase));

                        if (docMatch != null)
                        {
                            assignedCourseIds.Add(docMatch.StructuredLearningDocumentId);
                        }

                        // Match Capability Graph
                        var capMatch = activeCapabilities.FirstOrDefault(c =>
                            (c.TargetSkill != null && string.Equals(Normalize(c.TargetSkill), sNorm, StringComparison.OrdinalIgnoreCase)) ||
                            string.Equals(Normalize(c.Name), sNorm, StringComparison.OrdinalIgnoreCase));

                        if (capMatch != null)
                        {
                            assignedCourseIds.Add(capMatch.CapabilityId);
                        }
                    }
                }
            }

            // Fallback Source: PersonCareerPlan roadmap steps if no JobRole was assigned or for historical plans
            var plan = await _dbContext.PersonCareerPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonId == personId, ct);

            if (plan != null)
            {
                void CollectMatches(string? json)
                {
                    if (string.IsNullOrWhiteSpace(json)) return;
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in doc.RootElement.EnumerateArray())
                            {
                                if (el.TryGetProperty("MatchedCapabilityId", out var capProp) &&
                                    capProp.ValueKind == JsonValueKind.String &&
                                    Guid.TryParse(capProp.GetString(), out var capId))
                                {
                                    assignedCourseIds.Add(capId);
                                }
                                if (el.TryGetProperty("matchedCapabilityId", out var capPropCamel) &&
                                    capPropCamel.ValueKind == JsonValueKind.String &&
                                    Guid.TryParse(capPropCamel.GetString(), out var capIdCamel))
                                {
                                    assignedCourseIds.Add(capIdCamel);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore JSON parse anomalies
                    }
                }

                CollectMatches(plan.TechnicalRoadmapJson);
                CollectMatches(plan.SoftSkillsRoadmapJson);
                CollectMatches(plan.DepartmentProcessesRoadmapJson);
                CollectMatches(plan.HrProcessesRoadmapJson);
            }

            // 2. Explicit CapabilityEnrollments or StructuredLearningDocumentEnrollments
            var enrolledCapIds = await _dbContext.CapabilityEnrollments
                .AsNoTracking()
                .Where(e => e.PersonId == personId)
                .Select(e => e.CapabilityId)
                .ToListAsync(ct);

            var enrolledDocIds = await _dbContext.StructuredLearningDocumentEnrollments
                .AsNoTracking()
                .Where(e => e.PersonId == personId)
                .Select(e => e.StructuredLearningDocumentId)
                .ToListAsync(ct);

            foreach (var id in enrolledCapIds.Concat(enrolledDocIds))
            {
                assignedCourseIds.Add(id);
            }

            // 3. Activity-based Courses: Courses where the student has real progress/activity
            // 3a. Structured learning assessment rounds
            var activeStructuredCourseIds = await _dbContext.StructuredLearningAssessmentRounds
                .AsNoTracking()
                .Where(r => r.PersonId == personId)
                .Join(
                    _dbContext.StructuredLearningDocumentSections.AsNoTracking(),
                    r => r.StructuredLearningDocumentSectionId,
                    s => s.StructuredLearningDocumentSectionId,
                    (r, s) => s.StructuredLearningDocumentId
                )
                .Distinct()
                .ToListAsync(ct);

            foreach (var id in activeStructuredCourseIds)
            {
                assignedCourseIds.Add(id);
            }

            // 3b. RewardLedgerEntries (covers both Graph and Structured courses with awarded rewards)
            var activeRewardCourseIds = await _dbContext.RewardLedgerEntries
                .AsNoTracking()
                .Where(r => r.PersonId == personId)
                .Select(r => r.CourseId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var id in activeRewardCourseIds)
            {
                if (id != Guid.Empty)
                {
                    assignedCourseIds.Add(id);
                }
            }

            // 3c. LearningSessionNodes (Graph learning sessions)
            var activeGraphCourseIds = await _dbContext.LearningSessionNodes
                .AsNoTracking()
                .Where(n => n.LearningSession!.PersonId == personId)
                .Select(n => n.LearningSession!.CapabilityId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var id in activeGraphCourseIds)
            {
                assignedCourseIds.Add(id);
            }

            return assignedCourseIds;
        }

        private async Task<int> ComputePerfectScoresCountAsync(Guid personId, CancellationToken ct)
        {
            var graphPerfect = await _dbContext.LearningSessionNodes
                .AsNoTracking()
                .Where(n => n.LearningSession!.PersonId == personId
                         && n.AssessmentResults.Any(a => a.Passed && a.Score >= 100))
                .Select(n => n.CapabilityGraphNodeId.ToString())
                .Distinct()
                .ToListAsync(ct);

            var structuredPerfect = await _dbContext.StructuredLearningAssessmentRounds
                .AsNoTracking()
                .Where(r => r.PersonId == personId
                         && r.Status == Models.Capabilities.Studio.StructuredLearningAssessmentRoundStatus.Passed
                         && (r.FinalScore ?? 0) >= 100)
                .Select(r => r.StructuredLearningDocumentSectionId.ToString())
                .Distinct()
                .ToListAsync(ct);

            return graphPerfect.Concat(structuredPerfect).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        private async Task<int> ComputeFirstTryMasteriesCountAsync(Guid personId, CancellationToken ct)
        {
            var graphFirstTry = await _dbContext.AssessmentRounds
                .AsNoTracking()
                .Where(r => r.LearningSessionNode!.LearningSession!.PersonId == personId
                         && r.RoundNumber == 1
                         && r.Status == Models.Learning.AssessmentRoundStatus.Passed)
                .Select(r => r.LearningSessionNode!.CapabilityGraphNodeId.ToString())
                .Distinct()
                .ToListAsync(ct);

            var structuredFirstTry = await _dbContext.StructuredLearningAssessmentRounds
                .AsNoTracking()
                .Where(r => r.PersonId == personId
                         && r.RoundNumber == 1
                         && r.Status == Models.Capabilities.Studio.StructuredLearningAssessmentRoundStatus.Passed)
                .Select(r => r.StructuredLearningDocumentSectionId.ToString())
                .Distinct()
                .ToListAsync(ct);

            return graphFirstTry.Concat(structuredFirstTry).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        private async Task<int> ComputeFlawlessStreakAsync(Guid personId, CancellationToken ct)
        {
            var graphRounds = await _dbContext.AssessmentRounds
                .AsNoTracking()
                .Where(r => r.LearningSessionNode!.LearningSession!.PersonId == personId
                         && r.Status != Models.Learning.AssessmentRoundStatus.InProgress)
                .Select(r => new
                {
                    Date = r.CompletedDate ?? DateTime.UtcNow,
                    IsPassed = r.Status == Models.Learning.AssessmentRoundStatus.Passed
                })
                .ToListAsync(ct);

            var structuredRounds = await _dbContext.StructuredLearningAssessmentRounds
                .AsNoTracking()
                .Where(r => r.PersonId == personId
                         && r.Status != Models.Capabilities.Studio.StructuredLearningAssessmentRoundStatus.InProgress)
                .Select(r => new
                {
                    Date = r.CompletedDate ?? r.CreatedDate,
                    IsPassed = r.Status == Models.Capabilities.Studio.StructuredLearningAssessmentRoundStatus.Passed
                })
                .ToListAsync(ct);

            var chronologicalRounds = graphRounds
                .Concat(structuredRounds)
                .OrderBy(r => r.Date)
                .ToList();

            int maxStreak = 0;
            int currentStreak = 0;

            foreach (var r in chronologicalRounds)
            {
                if (r.IsPassed)
                {
                    currentStreak++;
                    if (currentStreak > maxStreak)
                    {
                        maxStreak = currentStreak;
                    }
                }
                else
                {
                    currentStreak = 0;
                }
            }

            return maxStreak;
        }

        private async Task<int> ComputeCriticalMasteredCountAsync(Guid personId, CancellationToken ct)
        {
            return await (
                from r in _dbContext.RewardLedgerEntries
                join s in _dbContext.EngramImpactScores
                    on new { EmployeeId = r.PersonId, r.CourseId, r.EngramId }
                    equals new { s.EmployeeId, s.CourseId, s.EngramId }
                where r.PersonId == personId && s.Tier == "Critical"
                select r.Id
            ).CountAsync(ct);
        }

        private async Task<int> ComputeMemoryRescuedCountAsync(Guid personId, CancellationToken ct)
        {
            return await _dbContext.RewardLedgerEntries
                .Where(r => r.PersonId == personId && r.MemoryEffortMultiplier >= 1.8m)
                .CountAsync(ct);
        }

        private async Task<int> ComputeJackpotCountAsync(Guid personId, CancellationToken ct)
        {
            return await _dbContext.RewardLedgerEntries
                .Where(r => r.PersonId == personId && r.SurpriseType == "JACKPOT")
                .CountAsync(ct);
        }

        private async Task<int> ComputeAutomaticEngramsCountAsync(Guid personId, CancellationToken ct)
        {
            // A. GRAPH LEARNING: Count distinct nodes where reviewCount >= 5 (MemoryScienceEngine.IsAutomatic)
            var graphCompletedNodes = await _dbContext.LearningSessionNodes
                .AsNoTracking()
                .Include(n => n.AssessmentResults)
                .Where(n => n.LearningSession!.PersonId == personId
                         && n.Status == Models.Learning.LearningSessionNodeStatus.Completed)
                .ToListAsync(ct);

            var graphPassedByNode = graphCompletedNodes
                .GroupBy(n => n.CapabilityGraphNodeId)
                .ToDictionary(g => g.Key, g => g.Count(n => n.AssessmentResults.Any(a => a.Passed)));

            var voiceReviews = await _dbContext.EngramReviewCompletions
                .AsNoTracking()
                .Where(r => r.PersonId == personId)
                .GroupBy(r => r.CapabilityGraphNodeId)
                .Select(g => new { NodeId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            foreach (var vr in voiceReviews)
            {
                graphPassedByNode[vr.NodeId] = graphPassedByNode.GetValueOrDefault(vr.NodeId) + vr.Count;
            }

            int automaticGraphCount = graphPassedByNode.Values.Count(MemoryScienceEngine.IsAutomatic);

            // B. STRUCTURED LEARNING: Count distinct sections where passedRounds >= 5 (MemoryScienceEngine.IsAutomatic)
            var structuredRounds = await _dbContext.StructuredLearningAssessmentRounds
                .AsNoTracking()
                .Where(r => r.PersonId == personId
                         && r.Status == Models.Capabilities.Studio.StructuredLearningAssessmentRoundStatus.Passed)
                .GroupBy(r => r.StructuredLearningDocumentSectionId)
                .Select(g => g.Count())
                .ToListAsync(ct);

            int automaticStructuredCount = structuredRounds.Count(MemoryScienceEngine.IsAutomatic);

            return automaticGraphCount + automaticStructuredCount;
        }
    }
}
