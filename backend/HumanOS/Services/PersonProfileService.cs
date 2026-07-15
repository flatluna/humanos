using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class PersonProfileService
{
    private readonly HumanOsDbContext _dbContext;

    public PersonProfileService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PersonProfile?> GetByPersonIdAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PersonProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                profile => profile.PersonId == personId,
                cancellationToken);
    }

    public async Task<PersonProfile> CreateAsync(
        Guid personId,
        string? firstName,
        string? lastName,
        string? displayName,
        string? phoneNumber,
        string preferredLanguage,
        string? countryCode,
        string? timeZone,
        string? profilePhotoUrl,
        DateOnly? dateOfBirth,
        string? occupation,
        string? company,
        string? biography,
        CancellationToken cancellationToken = default)
    {
        var profileExists = await _dbContext.PersonProfiles
            .AnyAsync(
                profile => profile.PersonId == personId,
                cancellationToken);

        if (profileExists)
        {
            throw new InvalidOperationException(
                "A profile already exists for this person.");
        }

        var personExists = await _dbContext.People
            .AnyAsync(
                person => person.PersonId == personId,
                cancellationToken);

        if (!personExists)
        {
            throw new KeyNotFoundException(
                "The requested person was not found.");
        }

        var languageExists = await _dbContext.Languages
            .AnyAsync(
                language =>
                    language.LanguageCode == preferredLanguage &&
                    language.IsActive,
                cancellationToken);

        if (!languageExists)
        {
            throw new ArgumentException(
                "The preferred language is not supported.",
                nameof(preferredLanguage));
        }

        var profile = new PersonProfile
        {
            PersonProfileId = Guid.NewGuid(),
            PersonId = personId,
            FirstName = NormalizeOptional(firstName),
            LastName = NormalizeOptional(lastName),
            DisplayName = NormalizeOptional(displayName),
            PhoneNumber = NormalizeOptional(phoneNumber),
            PreferredLanguage = preferredLanguage.Trim(),
            CountryCode = NormalizeOptional(countryCode),
            TimeZone = NormalizeOptional(timeZone),
            ProfilePhotoUrl = NormalizeOptional(profilePhotoUrl),
            DateOfBirth = dateOfBirth,
            Occupation = NormalizeOptional(occupation),
            Company = NormalizeOptional(company),
            Biography = NormalizeOptional(biography),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _dbContext.PersonProfiles.Add(profile);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return profile;
    }

    public async Task<PersonProfile?> UpdateAsync(
        Guid personId,
        string? firstName,
        string? lastName,
        string? displayName,
        string? phoneNumber,
        string preferredLanguage,
        string? countryCode,
        string? timeZone,
        string? profilePhotoUrl,
        DateOnly? dateOfBirth,
        string? occupation,
        string? company,
        string? biography,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.PersonProfiles
            .SingleOrDefaultAsync(
                item => item.PersonId == personId,
                cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var languageExists = await _dbContext.Languages
            .AnyAsync(
                language =>
                    language.LanguageCode == preferredLanguage &&
                    language.IsActive,
                cancellationToken);

        if (!languageExists)
        {
            throw new ArgumentException(
                "The preferred language is not supported.",
                nameof(preferredLanguage));
        }

        profile.FirstName = NormalizeOptional(firstName);
        profile.LastName = NormalizeOptional(lastName);
        profile.DisplayName = NormalizeOptional(displayName);
        profile.PhoneNumber = NormalizeOptional(phoneNumber);
        profile.PreferredLanguage = preferredLanguage.Trim();
        profile.CountryCode = NormalizeOptional(countryCode);
        profile.TimeZone = NormalizeOptional(timeZone);
        profile.ProfilePhotoUrl = NormalizeOptional(profilePhotoUrl);
        profile.DateOfBirth = dateOfBirth;
        profile.Occupation = NormalizeOptional(occupation);
        profile.Company = NormalizeOptional(company);
        profile.Biography = NormalizeOptional(biography);
        profile.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return profile;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
