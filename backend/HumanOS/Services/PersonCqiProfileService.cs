using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Get/upsert for a Person's CQI Workspace profile — see
/// <see cref="PersonCqiProfile"/>. One optional row per Person, replaced
/// whenever a new report is uploaded and extracted.</summary>
public sealed class PersonCqiProfileService
{
    private readonly HumanOsDbContext _dbContext;

    public PersonCqiProfileService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CqiProfileResponse> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.PersonCqiProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

        return profile is null ? new CqiProfileResponse { PersonId = personId } : ToResponse(profile);
    }

    public async Task<CqiProfileResponse> SaveAsync(
        Guid personId,
        CqiProfileData data,
        string? sourceFileName,
        string? extractionModel,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.PersonCqiProfiles
            .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

        var now = DateTime.UtcNow;

        if (profile is null)
        {
            profile = new PersonCqiProfile
            {
                PersonCqiProfileId = Guid.NewGuid(),
                PersonId = personId,
                CreatedDate = now,
            };
            _dbContext.PersonCqiProfiles.Add(profile);
        }

        profile.LeadingPatternType = string.IsNullOrWhiteSpace(data.LeadingPatternType) ? null : data.LeadingPatternType.Trim();
        profile.LeadingPatternName = string.IsNullOrWhiteSpace(data.LeadingPatternName) ? null : data.LeadingPatternName.Trim();
        profile.ProfileJson = JsonSerializer.Serialize(data);
        profile.SourceFileName = sourceFileName;
        profile.ExtractionModel = extractionModel;
        profile.ExtractedDate = now;
        profile.UpdatedDate = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(profile);
    }

    private static CqiProfileResponse ToResponse(PersonCqiProfile profile) => new()
    {
        PersonId = profile.PersonId,
        Profile = JsonSerializer.Deserialize<CqiProfileData>(profile.ProfileJson),
        SourceFileName = profile.SourceFileName,
        ExtractionModel = profile.ExtractionModel,
        ExtractedDate = profile.ExtractedDate,
        UpdatedDate = profile.UpdatedDate,
    };
}
