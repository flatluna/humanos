using HumanOS.Contracts.Goals;
using HumanOS.Data;
using HumanOS.Models.Goals;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class GoalService
{
    private readonly HumanOsDbContext _dbContext;

    public GoalService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<GoalResponse>> GetActiveAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var goals = await _dbContext.Goals
            .AsNoTracking()
            .Include(g => g.Translations)
            .Include(g => g.GoalCapabilities)
            .ThenInclude(gc => gc.Capability)
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return goals.Select(g => MapToResponse(g, normalizedLanguageCode)).ToList();
    }

    public async Task<GoalResponse?> GetByIdAsync(
        Guid goalId,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var goal = await _dbContext.Goals
            .AsNoTracking()
            .Include(g => g.Translations)
            .Include(g => g.GoalCapabilities)
            .ThenInclude(gc => gc.Capability)
            .SingleOrDefaultAsync(
                g => g.GoalId == goalId,
                cancellationToken);

        if (goal is null)
        {
            return null;
        }

        return MapToResponse(goal, normalizedLanguageCode);
    }

    public async Task<IReadOnlyList<PersonGoalResponse>> GetPersonGoalsAsync(
        Guid personId,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var personGoals = await _dbContext.PersonGoals
            .AsNoTracking()
            .Include(pg => pg.Goal)
            .ThenInclude(g => g.Translations)
            .Where(pg => pg.PersonId == personId)
            .OrderByDescending(pg => pg.StartedDate)
            .ToListAsync(cancellationToken);

        return personGoals.Select(pg => MapPersonGoalToResponse(pg, normalizedLanguageCode)).ToList();
    }

    public async Task<PersonGoalResponse?> AdoptAsync(
        Guid personId,
        Guid goalId,
        DateOnly? targetDate = null,
        CancellationToken cancellationToken = default)
    {
        var personGoalExists = await _dbContext.PersonGoals
            .AnyAsync(
                pg => pg.PersonId == personId && pg.GoalId == goalId && pg.Status != "Abandoned",
                cancellationToken);

        if (personGoalExists)
        {
            throw new InvalidOperationException(
                "This person has already adopted this goal.");
        }

        var goalExists = await _dbContext.Goals
            .AnyAsync(
                g => g.GoalId == goalId && g.IsActive,
                cancellationToken);

        if (!goalExists)
        {
            throw new KeyNotFoundException(
                "The requested goal was not found or is not active.");
        }

        var personExists = await _dbContext.People
            .AnyAsync(
                p => p.PersonId == personId && p.IsActive,
                cancellationToken);

        if (!personExists)
        {
            throw new KeyNotFoundException(
                "The requested person was not found or is not active.");
        }

        var personGoal = new PersonGoal
        {
            PersonGoalId = Guid.NewGuid(),
            PersonId = personId,
            GoalId = goalId,
            Status = "Active",
            ProgressPercentage = 0,
            TargetDate = targetDate.HasValue ? targetDate.Value.ToDateTime(TimeOnly.MinValue) : null,
            StartedDate = DateTime.UtcNow,
            CompletedDate = null,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _dbContext.PersonGoals.Add(personGoal);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var saved = await _dbContext.PersonGoals
            .AsNoTracking()
            .Include(pg => pg.Goal)
            .ThenInclude(g => g.Translations)
            .SingleAsync(pg => pg.PersonGoalId == personGoal.PersonGoalId, cancellationToken);

        return MapPersonGoalToResponse(saved, "en");
    }

    public async Task<PersonGoalResponse?> PauseAsync(
        Guid personGoalId,
        CancellationToken cancellationToken = default)
    {
        var personGoal = await _dbContext.PersonGoals
            .Include(pg => pg.Goal)
            .ThenInclude(g => g.Translations)
            .SingleOrDefaultAsync(
                pg => pg.PersonGoalId == personGoalId,
                cancellationToken);

        if (personGoal is null)
        {
            return null;
        }

        if (personGoal.Status != "Active")
        {
            throw new InvalidOperationException(
                $"Cannot pause a goal with status '{personGoal.Status}'.");
        }

        personGoal.Status = "Paused";
        personGoal.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPersonGoalToResponse(personGoal, "en");
    }

    public async Task<PersonGoalResponse?> ResumeAsync(
        Guid personGoalId,
        CancellationToken cancellationToken = default)
    {
        var personGoal = await _dbContext.PersonGoals
            .Include(pg => pg.Goal)
            .ThenInclude(g => g.Translations)
            .SingleOrDefaultAsync(
                pg => pg.PersonGoalId == personGoalId,
                cancellationToken);

        if (personGoal is null)
        {
            return null;
        }

        if (personGoal.Status != "Paused")
        {
            throw new InvalidOperationException(
                $"Cannot resume a goal with status '{personGoal.Status}'.");
        }

        personGoal.Status = "Active";
        personGoal.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPersonGoalToResponse(personGoal, "en");
    }

    public async Task<PersonGoalResponse?> AbandonAsync(
        Guid personGoalId,
        CancellationToken cancellationToken = default)
    {
        var personGoal = await _dbContext.PersonGoals
            .Include(pg => pg.Goal)
            .ThenInclude(g => g.Translations)
            .SingleOrDefaultAsync(
                pg => pg.PersonGoalId == personGoalId,
                cancellationToken);

        if (personGoal is null)
        {
            return null;
        }

        if (personGoal.Status == "Completed" || personGoal.Status == "Abandoned")
        {
            throw new InvalidOperationException(
                $"Cannot abandon a goal with status '{personGoal.Status}'.");
        }

        personGoal.Status = "Abandoned";
        personGoal.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPersonGoalToResponse(personGoal, "en");
    }

    public async Task<PersonGoalResponse?> CompleteAsync(
        Guid personGoalId,
        CancellationToken cancellationToken = default)
    {
        var personGoal = await _dbContext.PersonGoals
            .Include(pg => pg.Goal)
            .ThenInclude(g => g.Translations)
            .SingleOrDefaultAsync(
                pg => pg.PersonGoalId == personGoalId,
                cancellationToken);

        if (personGoal is null)
        {
            return null;
        }

        if (personGoal.Status == "Completed" || personGoal.Status == "Abandoned")
        {
            throw new InvalidOperationException(
                $"Cannot complete a goal with status '{personGoal.Status}'.");
        }

        personGoal.Status = "Completed";
        personGoal.ProgressPercentage = 100;
        personGoal.CompletedDate = DateTime.UtcNow;
        personGoal.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPersonGoalToResponse(personGoal, "en");
    }

    private static GoalResponse MapToResponse(Goal goal, string languageCode)
    {
        return new GoalResponse
        {
            GoalId = goal.GoalId,
            Name = goal.Translations?.FirstOrDefault(t => t.LanguageCode == languageCode)?.Name
                ?? goal.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? goal.Name,
            Description = goal.Translations?.FirstOrDefault(t => t.LanguageCode == languageCode)?.Description
                ?? goal.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? goal.Description,
            Category = goal.Category,
            IsActive = goal.IsActive,
            RequiredCapabilities = goal.GoalCapabilities
                .Select(gc => new CapabilityRefResponse
                {
                    Code = gc.Capability.Code,
                    Name = gc.Capability.Name
                })
                .ToList()
        };
    }

    private static PersonGoalResponse MapPersonGoalToResponse(PersonGoal personGoal, string languageCode)
    {
        return new PersonGoalResponse
        {
            PersonGoalId = personGoal.PersonGoalId,
            PersonId = personGoal.PersonId,
            GoalId = personGoal.GoalId,
            GoalName = personGoal.Goal.Translations?.FirstOrDefault(t => t.LanguageCode == languageCode)?.Name
                ?? personGoal.Goal.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? personGoal.Goal.Name,
            Category = personGoal.Goal.Category,
            Status = personGoal.Status,
            ProgressPercentage = Math.Min(100, Math.Max(0, personGoal.ProgressPercentage)),
            TargetDate = personGoal.TargetDate.HasValue ? DateOnly.FromDateTime(personGoal.TargetDate.Value) : null,
            StartedDate = personGoal.StartedDate,
            CompletedDate = personGoal.CompletedDate,
            CreatedDate = personGoal.CreatedDate,
            UpdatedDate = personGoal.UpdatedDate
        };
    }
}
