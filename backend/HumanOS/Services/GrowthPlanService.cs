using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class GrowthPlanService
{
    private readonly HumanOsDbContext _dbContext;

    public GrowthPlanService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
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
}
