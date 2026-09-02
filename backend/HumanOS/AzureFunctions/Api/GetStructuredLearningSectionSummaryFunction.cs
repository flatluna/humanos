using System.Net;
using System.Web;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Detailed DTO for a Structured Learning section summary (engram) modal in My Brain,
/// enriched with Memory (forgetting-curve), Reward (points/multipliers), and Impact (role impact) blocks.
/// </summary>
public sealed class StructuredLearningSectionSummaryDto
{
    public Guid StructuredLearningDocumentSectionId { get; set; }
    public Guid StructuredLearningDocumentId { get; set; }
    public string? DocumentName { get; set; }
    public string? SectionTitle { get; set; }
    public string? ChapterTitle { get; set; }
    public string? Capability { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? FirstCompletedDate { get; set; }
    public DateTime? LastCompletedDate { get; set; }
    public int? FinalScore { get; set; }

    public NodeMemorySummaryDto? Memory { get; set; }
    public NodeRewardSummaryDto? Reward { get; set; }
    public NodeImpactSummaryDto? Impact { get; set; }
}

/// <summary>
/// Read-only endpoint that returns full summary details for a Structured Learning section (engram),
/// including the student's mastery score, review history, memory retention metrics, reward ledger breakdown,
/// and job-role impact score.
/// </summary>
public sealed class GetStructuredLearningSectionSummaryFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly PersonService _personService;
    private readonly ILogger<GetStructuredLearningSectionSummaryFunction> _logger;

    public GetStructuredLearningSectionSummaryFunction(
        HumanOsDbContext dbContext,
        PersonService personService,
        ILogger<GetStructuredLearningSectionSummaryFunction> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _personService = personService ?? throw new ArgumentNullException(nameof(personService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetStructuredLearningSectionSummary")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/summary")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var personId = await ResolveAuthenticatedPersonIdAsync(request, cancellationToken);
        if (personId is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "Unauthorized", "Authenticated person identity is required.", cancellationToken);
        }

        try
        {
            var section = await _dbContext.StructuredLearningDocumentSections
                .AsNoTracking()
                .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
                .Select(s => new
                {
                    s.StructuredLearningDocumentSectionId,
                    s.StructuredLearningDocumentId,
                    s.SubchapterTitle,
                    s.ChapterTitle,
                    s.Capability
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (section is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "SectionNotFound", $"Section '{sectionId}' was not found.", cancellationToken);
            }

            var docName = await _dbContext.StructuredLearningDocuments
                .AsNoTracking()
                .Where(d => d.StructuredLearningDocumentId == section.StructuredLearningDocumentId)
                .Select(d => d.DocumentName)
                .FirstOrDefaultAsync(cancellationToken);

            // 1. Assessment rounds for this section & person
            var rounds = await _dbContext.StructuredLearningAssessmentRounds
                .AsNoTracking()
                .Where(r => r.StructuredLearningDocumentSectionId == sectionId && r.PersonId == personId.Value)
                .OrderByDescending(r => r.CompletedDate ?? r.CreatedDate)
                .ToListAsync(cancellationToken);

            var passedRounds = rounds.Where(r => r.Status == StructuredLearningAssessmentRoundStatus.Passed).ToList();
            var attemptCount = rounds.Count;
            var passedCount = passedRounds.Count;

            var finalScore = passedRounds.FirstOrDefault()?.FinalScore ?? rounds.FirstOrDefault()?.FinalScore;
            DateTime? firstCompletedDate = passedRounds.Count > 0
                ? passedRounds.Min(r => r.CompletedDate ?? r.CreatedDate)
                : rounds.Min(r => (DateTime?)(r.CompletedDate ?? r.CreatedDate));

            DateTime? lastCompletedDate = passedRounds.Count > 0
                ? passedRounds.Max(r => r.CompletedDate ?? r.CreatedDate)
                : rounds.Max(r => (DateTime?)(r.CompletedDate ?? r.CreatedDate));

            // 2. REWARD BLOCK
            var engramIdStr = sectionId.ToString();
            var rewardEntry = await _dbContext.RewardLedgerEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.PersonId == personId.Value &&
                         r.CourseId == section.StructuredLearningDocumentId &&
                         r.EngramId == engramIdStr,
                    cancellationToken);

            NodeRewardSummaryDto? reward = null;
            if (rewardEntry != null)
            {
                reward = new NodeRewardSummaryDto
                {
                    PointsAwarded = rewardEntry.PointsAwarded,
                    WasSurprise = rewardEntry.WasSurprise,
                    SurpriseType = rewardEntry.SurpriseType ?? "NONE",
                    PointsBase = rewardEntry.PointsBase,
                    DifficultyMultiplier = rewardEntry.DifficultyMultiplier,
                    MemoryEffortMultiplier = rewardEntry.MemoryEffortMultiplier,
                    SurpriseMultiplier = rewardEntry.SurpriseMultiplier,
                    EarnedAt = rewardEntry.EarnedAt
                };

                if (!lastCompletedDate.HasValue || rewardEntry.EarnedAt > lastCompletedDate.Value)
                {
                    lastCompletedDate = rewardEntry.EarnedAt;
                }
            }

            // 3. IMPACT BLOCK
            var impactEntry = await _dbContext.EngramImpactScores
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    i => i.EmployeeId == personId.Value &&
                         i.CourseId == section.StructuredLearningDocumentId &&
                         i.EngramId == engramIdStr,
                    cancellationToken);

            NodeImpactSummaryDto? impact = null;
            if (impactEntry != null)
            {
                impact = new NodeImpactSummaryDto
                {
                    Score = impactEntry.Score,
                    Tier = impactEntry.Tier ?? string.Empty,
                    PrimarySignal = impactEntry.PrimarySignal,
                    Reason = impactEntry.Reason
                };
            }

            // 4. MEMORY BLOCK
            NodeMemorySummaryDto? memory = null;
            if (lastCompletedDate.HasValue)
            {
                int reviewCount = Math.Max(1, passedCount > 0 ? passedCount : (rewardEntry != null ? 1 : 0));
                int recallPercent = MemoryScienceEngine.CalculateRecallPercent(lastCompletedDate.Value, reviewCount);
                string memoryState = MemoryScienceEngine.DetermineMemoryState(recallPercent);
                bool isAuto = MemoryScienceEngine.IsAutomatic(reviewCount);
                int reviewsUntilAuto = Math.Max(0, 5 - reviewCount);
                DateTime? nextReviewDate = MemoryScienceEngine.CalculateNextReviewDate(lastCompletedDate.Value, reviewCount);

                memory = new NodeMemorySummaryDto
                {
                    RecallPercent = recallPercent,
                    MemoryState = memoryState,
                    ReviewCount = reviewCount,
                    LastReviewDate = lastCompletedDate,
                    IsAutomatic = isAuto,
                    ReviewsUntilAutomatic = reviewsUntilAuto,
                    NextReviewDate = nextReviewDate
                };
            }

            var dto = new StructuredLearningSectionSummaryDto
            {
                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                StructuredLearningDocumentId = section.StructuredLearningDocumentId,
                DocumentName = docName,
                SectionTitle = section.SubchapterTitle,
                ChapterTitle = section.ChapterTitle,
                Capability = section.Capability,
                AttemptCount = attemptCount,
                FirstCompletedDate = firstCompletedDate,
                LastCompletedDate = lastCompletedDate,
                FinalScore = finalScore,
                Memory = memory,
                Reward = reward,
                Impact = impact
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, dto, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting StructuredLearningSection summary for Section {SectionId}, Person {PersonId}", sectionId, personId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "StructuredLearningSectionSummaryError", ex.Message, cancellationToken);
        }
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
