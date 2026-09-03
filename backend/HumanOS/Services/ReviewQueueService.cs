using HumanOS.Contracts.Gamification;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services;

/// <summary>
/// Neuroscience-based review queue engine with phase-based filtering,
/// priority scoring (Impact * Memory Decay), and inter-course interleaving.
/// </summary>
public sealed class ReviewQueueService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly BadgeService _badgeService;
    private readonly ILogger<ReviewQueueService> _logger;

    public ReviewQueueService(
        HumanOsDbContext dbContext,
        BadgeService badgeService,
        ILogger<ReviewQueueService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _badgeService = badgeService ?? throw new ArgumentNullException(nameof(badgeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Computes the personalized spaced review queue for a student:
    /// 1. Detects Phase: Phase 1 (&lt;80% ready courses completed) vs Phase 2 (&gt;=80% ready courses completed).
    /// 2. Fetches all mastered engrams with their real-time recall% and AI impact scores.
    /// 3. Filters candidates based on phase thresholds (Phase 1: Critical &lt; 60%; Phase 2: Critical &lt; 75% + Important &lt; 65%).
    /// 4. Calculates priority: PriorityScore = ImpactScore * (100 - RecallPercent).
    /// 5. Applies interleaving (round-robin across distinct courses) to optimize cognitive discrimination.
    /// </summary>
    public async Task<ReviewQueueResponseDto> GetReviewQueueAsync(
        Guid personId,
        CancellationToken ct = default)
    {
        // 1. Detect Learning Phase
        var completedCourses = await _badgeService.GetCompletedCoursesCountAsync(personId, ct);
        var breakdown = await _badgeService.GetAssignedEngramsBreakdownAsync(personId, ct);
        int coursesReady = breakdown.TotalCourses;

        double readyCoursesCompletionPct = coursesReady > 0
            ? (double)completedCourses / coursesReady * 100.0
            : 0.0;

        bool isPhase1 = readyCoursesCompletionPct < 80.0;
        string phase = isPhase1 ? "Phase1" : "Phase2";
        string phaseName = isPhase1 ? "Learning" : "Maintenance";

        // 2. Fetch all mastered engrams (Structured + Graph)
        var masteredEngrams = new Dictionary<string, MasteredEngramCandidate>(StringComparer.OrdinalIgnoreCase);

        // 2a. Structured Learning Sections
        var passedStructuredRounds = await _dbContext.StructuredLearningAssessmentRounds
            .AsNoTracking()
            .Where(r => r.PersonId == personId && r.Status == Models.Capabilities.Studio.StructuredLearningAssessmentRoundStatus.Passed)
            .Join(
                _dbContext.StructuredLearningDocumentSections.AsNoTracking(),
                r => r.StructuredLearningDocumentSectionId,
                s => s.StructuredLearningDocumentSectionId,
                (r, s) => new
                {
                    SectionId = s.StructuredLearningDocumentSectionId,
                    CourseId = s.StructuredLearningDocumentId,
                    s.SubchapterTitle,
                    s.ChapterTitle,
                    s.Capability,
                    r.CompletedDate
                }
            )
            .ToListAsync(ct);

        var structuredDocs = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .ToDictionaryAsync(d => d.StructuredLearningDocumentId, d => d.DocumentName, ct);

        foreach (var group in passedStructuredRounds.GroupBy(x => x.SectionId))
        {
            var first = group.First();
            var sectionIdStr = first.SectionId.ToString();
            int reviewCount = group.Count();
            DateTime? lastCompleted = group.Max(x => x.CompletedDate);

            string title = !string.IsNullOrWhiteSpace(first.SubchapterTitle) ? first.SubchapterTitle
                         : (!string.IsNullOrWhiteSpace(first.Capability) ? first.Capability
                         : (!string.IsNullOrWhiteSpace(first.ChapterTitle) ? first.ChapterTitle : "Section"));

            structuredDocs.TryGetValue(first.CourseId, out var courseName);

            masteredEngrams[sectionIdStr] = new MasteredEngramCandidate
            {
                EngramId = sectionIdStr,
                CourseId = first.CourseId,
                CourseName = courseName ?? "Course",
                Title = title,
                ReviewCount = reviewCount,
                LastCompletedDate = lastCompleted,
                IsStructuredLearning = true
            };
        }

        // 2b. Graph Learning Nodes
        var passedGraphNodes = await _dbContext.LearningSessionNodes
            .AsNoTracking()
            .Include(n => n.LearningSession)
            .Include(n => n.AssessmentResults)
            .Where(n => n.LearningSession!.PersonId == personId
                     && (n.Status == Models.Learning.LearningSessionNodeStatus.Completed || n.AssessmentResults.Any(a => a.Passed)))
            .ToListAsync(ct);

        if (passedGraphNodes.Count > 0)
        {
            var graphNodeIds = passedGraphNodes.Select(n => n.CapabilityGraphNodeId).Distinct().ToList();
            var graphNodeDetails = await _dbContext.CapabilityGraphNodes
                .AsNoTracking()
                .Include(n => n.CapabilityGraph)
                .Where(n => graphNodeIds.Contains(n.CapabilityGraphNodeId))
                .ToDictionaryAsync(n => n.CapabilityGraphNodeId, ct);

            var capabilityIds = graphNodeDetails.Values.Select(v => v.CapabilityGraph?.CapabilityId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var capabilityNames = await _dbContext.Capabilities
                .AsNoTracking()
                .Where(c => capabilityIds.Contains(c.CapabilityId))
                .ToDictionaryAsync(c => c.CapabilityId, c => c.Name, ct);

            foreach (var group in passedGraphNodes.GroupBy(x => x.CapabilityGraphNodeId))
            {
                var nodeIdStr = group.Key.ToString();
                int reviewCount = group.Count();
                DateTime? lastCompleted = group.Max(n => n.CompletedDate ?? n.CreatedDate);

                if (graphNodeDetails.TryGetValue(group.Key, out var nodeDetail))
                {
                    var courseId = nodeDetail.CapabilityGraph?.CapabilityId ?? Guid.Empty;
                    capabilityNames.TryGetValue(courseId, out var courseName);

                    masteredEngrams[nodeIdStr] = new MasteredEngramCandidate
                    {
                        EngramId = nodeIdStr,
                        CourseId = courseId,
                        CourseName = courseName ?? "Capability",
                        Title = nodeDetail.Name ?? "Node",
                        ReviewCount = reviewCount,
                        LastCompletedDate = lastCompleted,
                        IsStructuredLearning = false
                    };
                }
            }
        }

        if (masteredEngrams.Count == 0)
        {
            return new ReviewQueueResponseDto
            {
                Phase = phase,
                PhaseName = phaseName,
                ReadyCoursesCompletionPct = readyCoursesCompletionPct,
                TotalPending = 0,
                Items = []
            };
        }

        // 3. Load EngramImpactScores for this student
        var impactScores = await _dbContext.EngramImpactScores
            .AsNoTracking()
            .Where(s => s.EmployeeId == personId)
            .ToListAsync(ct);

        var impactLookup = impactScores
            .GroupBy(s => $"{s.CourseId:N}:{s.EngramId.Trim().ToLowerInvariant()}")
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // 4. Calculate Recall%, Tier, and Filter candidates per Phase
        var candidateList = new List<ReviewQueueItemDto>();

        foreach (var engram in masteredEngrams.Values)
        {
            int recallPercent = MemoryScienceEngine.CalculateRecallPercent(engram.LastCompletedDate, engram.ReviewCount);

            string key = $"{engram.CourseId:N}:{engram.EngramId.Trim().ToLowerInvariant()}";
            string tier = "Foundational";
            int impactScoreVal = 40;

            if (impactLookup.TryGetValue(key, out var impactRecord))
            {
                tier = !string.IsNullOrWhiteSpace(impactRecord.Tier) ? impactRecord.Tier.Trim() : "Foundational";
                impactScoreVal = impactRecord.Score;
            }

            // Phase Filtering:
            // Phase 1 (Learning): ONLY Critical tier with RecallPercent < 60%
            // Phase 2 (Maintenance): Critical with RecallPercent < 75% OR Important with RecallPercent < 65%
            // Foundational is always omitted from priority review queues
            bool qualifies;
            if (isPhase1)
            {
                qualifies = string.Equals(tier, "Critical", StringComparison.OrdinalIgnoreCase)
                            && recallPercent < 60;
            }
            else
            {
                bool isCriticalFading = string.Equals(tier, "Critical", StringComparison.OrdinalIgnoreCase)
                                       && recallPercent < 75;
                bool isImportantFading = string.Equals(tier, "Important", StringComparison.OrdinalIgnoreCase)
                                        && recallPercent < 65;
                qualifies = isCriticalFading || isImportantFading;
            }

            if (!qualifies)
            {
                continue;
            }

            double priorityScore = impactScoreVal * (100.0 - recallPercent);

            candidateList.Add(new ReviewQueueItemDto
            {
                EngramId = engram.EngramId,
                CourseId = engram.CourseId,
                CourseName = engram.CourseName,
                Title = engram.Title,
                RecallPercent = recallPercent,
                Tier = tier,
                ImpactScore = impactScoreVal,
                PriorityScore = priorityScore,
                ReviewCount = engram.ReviewCount,
                LastCompletedDate = engram.LastCompletedDate,
                IsStructuredLearning = engram.IsStructuredLearning
            });
        }

        if (candidateList.Count == 0)
        {
            return new ReviewQueueResponseDto
            {
                Phase = phase,
                PhaseName = phaseName,
                ReadyCoursesCompletionPct = readyCoursesCompletionPct,
                TotalPending = 0,
                Items = []
            };
        }

        // 5. Interleaving across courses:
        // Group by CourseId, order items within each course by PriorityScore desc,
        // then alternate round-robin across course groups.
        var courseGroups = candidateList
            .GroupBy(x => x.CourseId)
            .Select(g => g.OrderByDescending(x => x.PriorityScore).ToList())
            .OrderByDescending(list => list[0].PriorityScore)
            .ToList();

        var interleavedList = new List<ReviewQueueItemDto>(candidateList.Count);
        var queues = courseGroups.Select(list => new Queue<ReviewQueueItemDto>(list)).ToList();

        bool hasRemaining = true;
        while (hasRemaining)
        {
            hasRemaining = false;
            foreach (var q in queues)
            {
                if (q.Count > 0)
                {
                    interleavedList.Add(q.Dequeue());
                    hasRemaining = true;
                }
            }
        }

        return new ReviewQueueResponseDto
        {
            Phase = phase,
            PhaseName = phaseName,
            ReadyCoursesCompletionPct = readyCoursesCompletionPct,
            TotalPending = interleavedList.Count,
            Items = interleavedList
        };
    }

    private sealed class MasteredEngramCandidate
    {
        public string EngramId { get; set; } = string.Empty;
        public Guid CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int ReviewCount { get; set; }
        public DateTime? LastCompletedDate { get; set; }
        public bool IsStructuredLearning { get; set; }
    }
}
