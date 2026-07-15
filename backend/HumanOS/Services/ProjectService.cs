using HumanOS.Contracts.Responses;
using HumanOS.Data;
using HumanOS.Models.Projects;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class ProjectService
{
    private readonly HumanOsDbContext _dbContext;

    public ProjectService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PersonProjectResponse>> GetActiveAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var projects = await _dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Capability)
            .Include(p => p.Translations)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return projects.Select(p => MapToResponse(p, normalizedLanguageCode)).ToList();
    }

    public async Task<PersonProjectResponse?> GetByIdAsync(
        Guid projectId,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var project = await _dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Capability)
            .Include(p => p.Translations)
            .SingleOrDefaultAsync(
                p => p.ProjectId == projectId,
                cancellationToken);

        if (project is null)
        {
            return null;
        }

        return MapToResponse(project, normalizedLanguageCode);
    }

    public async Task<IReadOnlyList<PersonProjectResponse>> GetPersonProjectsAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var personProjects = await _dbContext.PersonProjects
            .AsNoTracking()
            .Include(pp => pp.Project)
            .ThenInclude(p => p.Capability)
            .Include(pp => pp.Project)
            .ThenInclude(p => p.Translations)
            .Where(pp => pp.PersonId == personId)
            .OrderByDescending(pp => pp.StartedDate)
            .ToListAsync(cancellationToken);

        return personProjects.Select(pp => MapPersonProjectToResponse(pp, "en")).ToList();
    }

    public async Task<PersonProjectResponse?> StartAsync(
        Guid personId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var personProjectExists = await _dbContext.PersonProjects
            .AnyAsync(
                pp => pp.PersonId == personId && pp.ProjectId == projectId && pp.Status != "Abandoned",
                cancellationToken);

        if (personProjectExists)
        {
            throw new InvalidOperationException(
                "This person has already started this project.");
        }

        var projectExists = await _dbContext.Projects
            .AnyAsync(
                p => p.ProjectId == projectId && p.IsActive,
                cancellationToken);

        if (!projectExists)
        {
            throw new KeyNotFoundException(
                "The requested project was not found or is not active.");
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

        var personProject = new PersonProject
        {
            PersonProjectId = Guid.NewGuid(),
            PersonId = personId,
            ProjectId = projectId,
            Status = "InProgress",
            ProgressPercentage = 0,
            StartedDate = DateTime.UtcNow,
            CompletedDate = null,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _dbContext.PersonProjects.Add(personProject);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var saved = await _dbContext.PersonProjects
            .AsNoTracking()
            .Include(pp => pp.Project)
            .ThenInclude(p => p.Capability)
            .Include(pp => pp.Project)
            .ThenInclude(p => p.Translations)
            .SingleAsync(pp => pp.PersonProjectId == personProject.PersonProjectId, cancellationToken);

        return MapPersonProjectToResponse(saved, "en");
    }

    public async Task<PersonProjectResponse?> UpdateProgressAsync(
        Guid personProjectId,
        decimal progressPercentage,
        CancellationToken cancellationToken = default)
    {
        ValidateProgress(progressPercentage);

        var personProject = await _dbContext.PersonProjects
            .Include(pp => pp.Project)
            .ThenInclude(p => p.Capability)
            .Include(pp => pp.Project)
            .ThenInclude(p => p.Translations)
            .SingleOrDefaultAsync(
                pp => pp.PersonProjectId == personProjectId,
                cancellationToken);

        if (personProject is null)
        {
            return null;
        }

        if (personProject.Status == "Completed" || personProject.Status == "Abandoned")
        {
            throw new InvalidOperationException(
                $"Cannot update progress for a project with status '{personProject.Status}'.");
        }

        personProject.ProgressPercentage = Math.Min(100, Math.Max(0, progressPercentage));
        personProject.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPersonProjectToResponse(personProject, "en");
    }

    public async Task<PersonProjectResponse?> PauseAsync(
        Guid personProjectId,
        CancellationToken cancellationToken = default)
    {
        var personProject = await _dbContext.PersonProjects
            .Include(pp => pp.Project)
            .ThenInclude(p => p.Capability)
            .Include(pp => pp.Project)
            .ThenInclude(p => p.Translations)
            .SingleOrDefaultAsync(
                pp => pp.PersonProjectId == personProjectId,
                cancellationToken);

        if (personProject is null)
        {
            return null;
        }

        if (personProject.Status != "InProgress" && personProject.Status != "NotStarted")
        {
            throw new InvalidOperationException(
                $"Cannot pause a project with status '{personProject.Status}'.");
        }

        personProject.Status = "Paused";
        personProject.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPersonProjectToResponse(personProject, "en");
    }

    public async Task<PersonProjectResponse?> CompleteAsync(
        Guid personProjectId,
        CancellationToken cancellationToken = default)
    {
        var personProject = await _dbContext.PersonProjects
            .Include(pp => pp.Project)
            .ThenInclude(p => p.Capability)
            .Include(pp => pp.Project)
            .ThenInclude(p => p.Translations)
            .SingleOrDefaultAsync(
                pp => pp.PersonProjectId == personProjectId,
                cancellationToken);

        if (personProject is null)
        {
            return null;
        }

        if (personProject.Status == "Completed" || personProject.Status == "Abandoned")
        {
            throw new InvalidOperationException(
                $"Cannot complete a project with status '{personProject.Status}'.");
        }

        personProject.Status = "Completed";
        personProject.ProgressPercentage = 100;
        personProject.CompletedDate = DateTime.UtcNow;
        personProject.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPersonProjectToResponse(personProject, "en");
    }

    private static void ValidateProgress(decimal progressPercentage)
    {
        if (progressPercentage < 0 || progressPercentage > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progressPercentage),
                "Progress percentage must be between 0 and 100.");
        }
    }

    private static PersonProjectResponse MapToResponse(Project project, string languageCode)
    {
        return new PersonProjectResponse
        {
            ProjectId = project.ProjectId,
            CapabilityId = project.CapabilityId,
            CapabilityCode = project.Capability.Code,
            Name = project.Translations?.FirstOrDefault(t => t.LanguageCode == languageCode)?.Name
                ?? project.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? project.Name,
            Description = project.Translations?.FirstOrDefault(t => t.LanguageCode == languageCode)?.Description
                ?? project.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? project.Description,
            DifficultyLevel = project.DifficultyLevel,
            EstimatedHours = project.EstimatedHours,
            IsActive = project.IsActive
        };
    }

    private static PersonProjectResponse MapPersonProjectToResponse(PersonProject pp, string languageCode)
    {
        return new PersonProjectResponse
        {
            ProjectId = pp.ProjectId,
            CapabilityId = pp.Project.CapabilityId,
            CapabilityCode = pp.Project.Capability.Code,
            Name = pp.Project.Translations?.FirstOrDefault(t => t.LanguageCode == languageCode)?.Name
                ?? pp.Project.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? pp.Project.Name,
            Description = pp.Project.Translations?.FirstOrDefault(t => t.LanguageCode == languageCode)?.Description
                ?? pp.Project.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? pp.Project.Description,
            DifficultyLevel = pp.Project.DifficultyLevel,
            EstimatedHours = pp.Project.EstimatedHours,
            IsActive = pp.Project.IsActive,
            PersonProjectId = pp.PersonProjectId,
            PersonId = pp.PersonId,
            Status = pp.Status,
            ProgressPercentage = Math.Min(100, Math.Max(0, pp.ProgressPercentage)),
            StartedDate = pp.StartedDate,
            CompletedDate = pp.CompletedDate,
            CreatedDate = pp.CreatedDate,
            UpdatedDate = pp.UpdatedDate
        };
    }
}
