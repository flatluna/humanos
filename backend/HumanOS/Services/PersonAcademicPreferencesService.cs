using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class PersonAcademicPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HumanOsDbContext _dbContext;

    public PersonAcademicPreferencesService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PersonAcademicPreferences?> GetAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PersonAcademicPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.PersonId == personId, cancellationToken);
    }

    public async Task<PersonAcademicPreferences> UpsertAsync(
        Guid personId,
        UpsertPersonAcademicPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var personExists = await _dbContext.People.AnyAsync(item => item.PersonId == personId, cancellationToken);
        if (!personExists)
        {
            throw new KeyNotFoundException("The requested person was not found.");
        }

        var profile = await _dbContext.PersonAcademicPreferences
            .SingleOrDefaultAsync(item => item.PersonId == personId, cancellationToken);

        if (profile is null)
        {
            profile = new PersonAcademicPreferences
            {
                PersonAcademicPreferencesId = Guid.NewGuid(),
                PersonId = personId,
                CreatedDate = DateTime.UtcNow,
            };
            _dbContext.PersonAcademicPreferences.Add(profile);
        }

        profile.AcademicInterestsJson = Serialize(request.AcademicInterests);
        profile.LearningPreferencesJson = Serialize(request.LearningPreferences);
        profile.AcademicMotivationsJson = Serialize(request.AcademicMotivations);
        profile.GrowthAreasJson = Serialize(request.GrowthAreas);
        profile.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private static string Serialize(IEnumerable<string>? values) =>
        JsonSerializer.Serialize((values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(), JsonOptions);
}
