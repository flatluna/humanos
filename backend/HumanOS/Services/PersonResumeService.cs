using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Get/upsert for a Person's résumé/CV — see
/// <see cref="PersonResume"/>'s own doc comment for why this is a
/// separate record from PersonProfile/JobDescriptionRecord. One optional
/// row per Person, created lazily on first save.</summary>
public sealed class PersonResumeService
{
    private readonly HumanOsDbContext _dbContext;

    public PersonResumeService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Never returns null — a person who hasn't saved a résumé
    /// yet gets an all-empty response (PersonId only) so the
    /// EmployeeDetailPage dashboard can always render the form.</summary>
    public async Task<PersonResumeResponse> GetByPersonIdAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var resume = await _dbContext.PersonResumes
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.PersonId == personId, cancellationToken);

        return resume is null ? EmptyResponse(personId) : ToResponse(resume);
    }

    public async Task<PersonResumeResponse> UpsertAsync(
        Guid personId,
        SavePersonResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        var resume = await _dbContext.PersonResumes
            .SingleOrDefaultAsync(r => r.PersonId == personId, cancellationToken);

        var now = DateTime.UtcNow;

        if (resume is null)
        {
            resume = new PersonResume
            {
                PersonResumeId = Guid.NewGuid(),
                PersonId = personId,
                CreatedDate = now,
            };
            _dbContext.PersonResumes.Add(resume);
        }

        resume.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        resume.LinkedInUrl = string.IsNullOrWhiteSpace(request.LinkedInUrl) ? null : request.LinkedInUrl.Trim();
        resume.ProfessionalObjective = string.IsNullOrWhiteSpace(request.ProfessionalObjective) ? null : request.ProfessionalObjective.Trim();
        resume.ProfessionalSummary = string.IsNullOrWhiteSpace(request.ProfessionalSummary) ? null : request.ProfessionalSummary.Trim();
        resume.SkillsJson = JsonSerializer.Serialize(request.Skills ?? []);
        resume.WorkExperienceJson = JsonSerializer.Serialize(request.WorkExperience ?? []);
        resume.EducationJson = JsonSerializer.Serialize(request.Education ?? []);
        resume.CertificationsJson = JsonSerializer.Serialize(request.Certifications ?? []);
        resume.LanguagesJson = JsonSerializer.Serialize(request.Languages ?? []);
        resume.RawResumeText = string.IsNullOrWhiteSpace(request.RawResumeText) ? null : request.RawResumeText.Trim();
        resume.UpdatedDate = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(resume);
    }

    private static PersonResumeResponse EmptyResponse(Guid personId) => new() { PersonId = personId };

    private static PersonResumeResponse ToResponse(PersonResume resume) => new()
    {
        PersonId = resume.PersonId,
        Address = resume.Address,
        LinkedInUrl = resume.LinkedInUrl,
        ProfessionalObjective = resume.ProfessionalObjective,
        ProfessionalSummary = resume.ProfessionalSummary,
        Skills = JsonSerializer.Deserialize<List<string>>(resume.SkillsJson) ?? [],
        WorkExperience = JsonSerializer.Deserialize<List<string>>(resume.WorkExperienceJson) ?? [],
        Education = JsonSerializer.Deserialize<List<string>>(resume.EducationJson) ?? [],
        Certifications = JsonSerializer.Deserialize<List<string>>(resume.CertificationsJson) ?? [],
        Languages = JsonSerializer.Deserialize<List<string>>(resume.LanguagesJson) ?? [],
        RawResumeText = resume.RawResumeText,
        UpdatedDate = resume.UpdatedDate,
    };
}
