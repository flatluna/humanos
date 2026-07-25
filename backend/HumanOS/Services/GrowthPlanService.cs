using HumanOS.Contracts.GrowthPlan;
using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class GrowthPlanService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly ProgramService _programService;
    private readonly GraphProgressionEngine _graphProgressionEngine;

    public GrowthPlanService(HumanOsDbContext dbContext, ProgramService programService, GraphProgressionEngine graphProgressionEngine)
    {
        _dbContext = dbContext;
        _programService = programService;
        _graphProgressionEngine = graphProgressionEngine;
    }

    // ── Current Situation ────────────────────────────────────────────

    public async Task<PersonCurrentSituation?> GetCurrentSituationAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PersonCurrentSituations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == personId, cancellationToken);
    }

    public async Task<PersonCurrentSituation> UpsertCurrentSituationAsync(
        Guid personId,
        IEnumerable<string> selectedSubjectCodes,
        Dictionary<string, string> selfAssessedLevelBySubject,
        bool completed,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PersonCurrentSituations
            .FirstOrDefaultAsync(x => x.PersonId == personId, cancellationToken);

        var subjectCodes = string.Join(",", selectedSubjectCodes);
        var levelsJson = System.Text.Json.JsonSerializer.Serialize(selfAssessedLevelBySubject);

        if (existing is null)
        {
            existing = new PersonCurrentSituation
            {
                PersonCurrentSituationId = Guid.NewGuid(),
                PersonId = personId,
                SelectedSubjectCodes = subjectCodes,
                SelfAssessedLevelsJson = levelsJson,
                Completed = completed,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _dbContext.PersonCurrentSituations.Add(existing);
        }
        else
        {
            existing.SelectedSubjectCodes = subjectCodes;
            existing.SelfAssessedLevelsJson = levelsJson;
            existing.Completed = completed;
            existing.UpdatedDate = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    // ── Future Direction ─────────────────────────────────────────────

    public async Task<PersonFutureDirection?> GetFutureDirectionAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PersonFutureDirections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == personId, cancellationToken);
    }

    public async Task<PersonFutureDirection> UpsertFutureDirectionAsync(
        Guid personId,
        IEnumerable<string> selectedGoalIds,
        IEnumerable<string> selectedMotivationCodes,
        bool completed,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PersonFutureDirections
            .FirstOrDefaultAsync(x => x.PersonId == personId, cancellationToken);

        var goalIds = string.Join(",", selectedGoalIds);
        var motivationCodes = string.Join(",", selectedMotivationCodes);

        if (existing is null)
        {
            existing = new PersonFutureDirection
            {
                PersonFutureDirectionId = Guid.NewGuid(),
                PersonId = personId,
                SelectedGoalIds = goalIds,
                SelectedMotivationCodes = motivationCodes,
                Completed = completed,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _dbContext.PersonFutureDirections.Add(existing);
        }
        else
        {
            existing.SelectedGoalIds = goalIds;
            existing.SelectedMotivationCodes = motivationCodes;
            existing.Completed = completed;
            existing.UpdatedDate = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    // ── Starting Point ───────────────────────────────────────────────

    public async Task<PersonStartingPoint?> GetStartingPointAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PersonStartingPoints
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == personId, cancellationToken);
    }

    public async Task<PersonStartingPoint> UpsertStartingPointAsync(
        Guid personId,
        IEnumerable<string> selectedCapabilityIds,
        Dictionary<string, List<string>> gapCapabilitiesBySubject,
        List<HumanOS.Contracts.GrowthPlan.AcceptedRecommendation> acceptedRecommendations,
        bool completed,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PersonStartingPoints
            .FirstOrDefaultAsync(x => x.PersonId == personId, cancellationToken);

        var capabilityIds = string.Join(",", selectedCapabilityIds);
        var gapJson = System.Text.Json.JsonSerializer.Serialize(gapCapabilitiesBySubject);
        var recommendationsJson = System.Text.Json.JsonSerializer.Serialize(acceptedRecommendations);

        if (existing is null)
        {
            existing = new PersonStartingPoint
            {
                PersonStartingPointId = Guid.NewGuid(),
                PersonId = personId,
                SelectedCapabilityIds = capabilityIds,
                GapCapabilitiesBySubjectJson = gapJson,
                AcceptedRecommendationsJson = recommendationsJson,
                Completed = completed,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _dbContext.PersonStartingPoints.Add(existing);
        }
        else
        {
            existing.SelectedCapabilityIds = capabilityIds;
            existing.GapCapabilitiesBySubjectJson = gapJson;
            existing.AcceptedRecommendationsJson = recommendationsJson;
            existing.Completed = completed;
            existing.UpdatedDate = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    // ── Enrolled Programs ("Mis Programas") ──────────────────────────

    /// <summary>
    /// Every Program the person is enrolled in, derived from the Starting
    /// Point step's accepted recommendations that resolved to a real
    /// Program (AcceptedRecommendation.ProgramId != null) — the de facto
    /// enrollment record (2026-07-24 design decision, no separate
    /// enrollment table). For each Capability in each Program, progress is
    /// computed from the Capability Graph's node mastery for this person,
    /// scoped to this Program (LearningSession.ProgramId), so the same
    /// Capability tracks independent progress if it belongs to more than
    /// one Program.
    /// </summary>
    public async Task<IReadOnlyList<EnrolledProgramResponse>> GetEnrolledProgramsAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var startingPoint = await GetStartingPointAsync(personId, cancellationToken);
        if (startingPoint is null || string.IsNullOrWhiteSpace(startingPoint.AcceptedRecommendationsJson))
        {
            return [];
        }

        List<AcceptedRecommendation> recommendations;
        try
        {
            recommendations = System.Text.Json.JsonSerializer.Deserialize<List<AcceptedRecommendation>>(
                startingPoint.AcceptedRecommendationsJson) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }

        var results = new List<EnrolledProgramResponse>();

        foreach (var recommendation in recommendations.Where(r => r.ProgramId is not null))
        {
            var programId = recommendation.ProgramId!.Value;
            var program = await _programService.GetByIdAsync(programId, cancellationToken);
            if (program is null)
            {
                continue;
            }

            var capabilities = new List<EnrolledProgramCapabilityResponse>();
            var allScores = new List<int>();
            foreach (var programCapability in program.Capabilities.OrderBy(c => c.SortOrder))
            {
                var graph = await _graphProgressionEngine.GetFullGraphAsync(
                    _dbContext, personId, programCapability.CapabilityId, cancellationToken);

                var totalNodes = graph?.Nodes.Count ?? 0;
                var masteredNodes = graph?.Nodes.Count(n => n.State == "Mastered") ?? 0;
                var capabilityScores = graph?.Nodes
                    .Where(n => n.FinalScore.HasValue)
                    .Select(n => n.FinalScore!.Value)
                    .ToList() ?? [];
                allScores.AddRange(capabilityScores);

                var hasSession = totalNodes > 0 && await _dbContext.LearningSessions
                    .AsNoTracking()
                    .AnyAsync(s => s.PersonId == personId
                                && s.CapabilityId == programCapability.CapabilityId
                                && s.ProgramId == programId, cancellationToken);

                var status = totalNodes > 0 && masteredNodes == totalNodes
                    ? "Completed"
                    : masteredNodes > 0 || hasSession
                        ? "InProgress"
                        : "NotStarted";

                capabilities.Add(new EnrolledProgramCapabilityResponse
                {
                    CapabilityId = programCapability.CapabilityId,
                    Name = programCapability.CapabilityName,
                    SortOrder = programCapability.SortOrder,
                    IsRequired = programCapability.IsRequired,
                    Status = status,
                    TotalNodes = totalNodes,
                    MasteredNodes = masteredNodes,
                    AverageScore = capabilityScores.Count > 0 ? (int)Math.Round(capabilityScores.Average()) : null
                });
            }

            results.Add(new EnrolledProgramResponse
            {
                ProgramId = program.ProgramId,
                Code = program.Code,
                Name = program.Name,
                Description = program.Description,
                AcceptedDate = recommendation.AcceptedDate,
                AverageScore = allScores.Count > 0 ? (int)Math.Round(allScores.Average()) : null,
                Capabilities = capabilities
            });
        }

        return results.OrderByDescending(r => r.AcceptedDate).ToList();
    }
}
