using System.Net;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /family/master-data — the Family household record (the
/// consumer/family-household analogue of GetTenant/GetTenantPeople for the
/// enterprise side) plus its active members, read straight from the real
/// HumanOSDev database (dbo.FamilyMaster / dbo.FamilyMember via
/// <see cref="HumanOsDbContext"/>). Returns 404 rather than any fabricated
/// placeholder data when no family record exists yet.</summary>
public sealed class GetFamilyMasterDataFunction
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public GetFamilyMasterDataFunction(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Function("GetFamilyMasterData")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/master-data")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(request, out var requestedFamilyId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidFamilyId",
                "The familyId query parameter must be a valid GUID.",
                cancellationToken);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var familiesQuery = dbContext.FamilyMasters
            .AsNoTracking()
            .Where(f => f.IsActive);

        familiesQuery = requestedFamilyId.HasValue
            ? familiesQuery.Where(f => f.FamilyId == requestedFamilyId.Value)
            : familiesQuery;

        var families = await familiesQuery
            .OrderByDescending(f => f.CreatedDate)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (families.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "FamilyNotFound",
                "No active family record was found.",
                cancellationToken);
        }

        if (families.Count > 1)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Conflict,
                "FamilyIdRequired",
                "More than one active family exists. Supply the familyId query parameter.",
                cancellationToken);
        }

        var family = families[0];

        var members = await dbContext.FamilyMembers
            .AsNoTracking()
            .Where(m => m.FamilyId == family.FamilyId && m.IsActive)
            .OrderBy(m => m.CreatedDate)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new
            {
                family = new
                {
                    family.FamilyId,
                    family.FamilyName,
                    family.FamilySlug,
                    family.HouseholdType,
                    family.AdminFullName,
                    family.AdminEmail,
                    family.AdminPhone,
                    family.CoAdminFullName,
                    family.CoAdminEmail,
                    family.PrimaryStreet,
                    family.PrimaryCity,
                    family.PrimaryState,
                    family.PrimaryZipCode,
                    family.PrimaryCountry,
                    family.PrimaryFullAddress,
                    family.Latitude,
                    family.Longitude,
                    family.SecondaryLocation,
                    family.TimeZone,
                    family.PreferredCurrency,
                    family.PrimaryLanguage,
                    family.IsBilingualHousehold,
                    family.AnnualEducationBudget,
                    DigitalSunsetTime = family.DigitalSunsetTime?.ToString(),
                    family.FamilyMissionStatement,
                    family.SubscriptionTier,
                    family.IsActive,
                    family.CreatedDate,
                    family.UpdatedDate,
                },
                members = members.Select(m => new
                {
                    m.MemberId,
                    m.FullName,
                    m.PreferredName,
                    m.FamilyRole,
                    m.RoleTitleEs,
                    m.RoleTitleEn,
                    m.DateOfBirth,
                    m.Age,
                    m.EducationLevelEs,
                    m.EducationLevelEn,
                    m.Institution,
                    m.PrimaryLocation,
                    m.Email,
                    m.AzureOid,
                    m.AzureLoginEmail,
                    m.AzureLinkedDate,
                    m.PlatformRole,
                    m.AvatarBg,
                    Languages = ParseJsonArray(m.LanguagesJson),
                    TopFocus = ParseJsonArray(m.TopFocusJson),
                    m.WeeklyHoursTarget,
                    m.LearningStreakDays,
                    m.TargetMilestoneEs,
                    m.TargetMilestoneEn,
                    m.IsActive,
                }),
            },
            cancellationToken: cancellationToken);
    }

    private static bool TryGetFamilyId(HttpRequestData request, out Guid? familyId)
    {
        familyId = null;
        var value = System.Web.HttpUtility.ParseQueryString(request.Url.Query)["familyId"];
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Guid.TryParse(value, out var parsed))
        {
            return false;
        }

        familyId = parsed;
        return true;
    }

    private static string[] ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
