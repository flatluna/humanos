using System.Net;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class UpdateFamilyMasterDataRequest
{
    public Guid? FamilyId { get; set; }
    public string? FamilyName { get; set; }
    public string? HouseholdType { get; set; }
    public string? AdminFullName { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminPhone { get; set; }
    public string? CoAdminFullName { get; set; }
    public string? CoAdminEmail { get; set; }
    public string? PrimaryStreet { get; set; }
    public string? PrimaryCity { get; set; }
    public string? PrimaryState { get; set; }
    public string? PrimaryZipCode { get; set; }
    public string? PrimaryCountry { get; set; }
    public string? PrimaryFullAddress { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? SecondaryLocation { get; set; }
    public string? TimeZone { get; set; }
    public string? PreferredCurrency { get; set; }
    public string? PrimaryLanguage { get; set; }
    public bool? IsBilingualHousehold { get; set; }
    public decimal? AnnualEducationBudget { get; set; }
    public string? DigitalSunsetTime { get; set; }
    public string? FamilyMissionStatement { get; set; }
    public string? SubscriptionTier { get; set; }
}

/// <summary>PUT / POST /family/master-data — updates the Family household record in dbo.FamilyMaster.</summary>
public sealed class UpdateFamilyMasterDataFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public UpdateFamilyMasterDataFunction(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Function("UpdateFamilyMasterData")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "post", Route = "family/master-data")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        UpdateFamilyMasterDataRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<UpdateFamilyMasterDataRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", "A request body is required.", cancellationToken);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var familiesQuery = dbContext.FamilyMasters.Where(f => f.IsActive);
        if (body.FamilyId.HasValue && body.FamilyId.Value != Guid.Empty)
        {
            familiesQuery = familiesQuery.Where(f => f.FamilyId == body.FamilyId.Value);
        }

        var family = await familiesQuery.OrderByDescending(f => f.CreatedDate).FirstOrDefaultAsync(cancellationToken);
        if (family is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "FamilyNotFound", "No active family record was found to update.", cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(body.FamilyName)) family.FamilyName = body.FamilyName.Trim();
        if (!string.IsNullOrWhiteSpace(body.HouseholdType)) family.HouseholdType = body.HouseholdType.Trim();
        if (!string.IsNullOrWhiteSpace(body.AdminFullName)) family.AdminFullName = body.AdminFullName.Trim();
        if (!string.IsNullOrWhiteSpace(body.AdminEmail)) family.AdminEmail = body.AdminEmail.Trim();
        if (body.AdminPhone is not null) family.AdminPhone = string.IsNullOrWhiteSpace(body.AdminPhone) ? null : body.AdminPhone.Trim();
        if (body.CoAdminFullName is not null) family.CoAdminFullName = string.IsNullOrWhiteSpace(body.CoAdminFullName) ? null : body.CoAdminFullName.Trim();
        if (body.CoAdminEmail is not null) family.CoAdminEmail = string.IsNullOrWhiteSpace(body.CoAdminEmail) ? null : body.CoAdminEmail.Trim();

        // Location fields
        if (body.PrimaryCountry is not null) family.PrimaryCountry = string.IsNullOrWhiteSpace(body.PrimaryCountry) ? "USA" : body.PrimaryCountry.Trim();
        if (body.PrimaryState is not null) family.PrimaryState = string.IsNullOrWhiteSpace(body.PrimaryState) ? null : body.PrimaryState.Trim();
        if (body.PrimaryCity is not null) family.PrimaryCity = string.IsNullOrWhiteSpace(body.PrimaryCity) ? null : body.PrimaryCity.Trim();
        if (body.PrimaryStreet is not null) family.PrimaryStreet = string.IsNullOrWhiteSpace(body.PrimaryStreet) ? null : body.PrimaryStreet.Trim();
        if (body.PrimaryZipCode is not null) family.PrimaryZipCode = string.IsNullOrWhiteSpace(body.PrimaryZipCode) ? null : body.PrimaryZipCode.Trim();
        if (body.PrimaryFullAddress is not null) family.PrimaryFullAddress = string.IsNullOrWhiteSpace(body.PrimaryFullAddress) ? null : body.PrimaryFullAddress.Trim();
        if (body.Latitude.HasValue) family.Latitude = body.Latitude.Value;
        if (body.Longitude.HasValue) family.Longitude = body.Longitude.Value;
        if (body.SecondaryLocation is not null) family.SecondaryLocation = string.IsNullOrWhiteSpace(body.SecondaryLocation) ? null : body.SecondaryLocation.Trim();

        if (!string.IsNullOrWhiteSpace(body.TimeZone)) family.TimeZone = body.TimeZone.Trim();
        if (!string.IsNullOrWhiteSpace(body.PreferredCurrency)) family.PreferredCurrency = body.PreferredCurrency.Trim();
        if (!string.IsNullOrWhiteSpace(body.PrimaryLanguage)) family.PrimaryLanguage = body.PrimaryLanguage.Trim();
        if (body.IsBilingualHousehold.HasValue) family.IsBilingualHousehold = body.IsBilingualHousehold.Value;
        if (body.AnnualEducationBudget.HasValue) family.AnnualEducationBudget = body.AnnualEducationBudget.Value;
        if (!string.IsNullOrWhiteSpace(body.DigitalSunsetTime) && TimeSpan.TryParse(body.DigitalSunsetTime, out var sunset))
        {
            family.DigitalSunsetTime = sunset;
        }
        if (body.FamilyMissionStatement is not null) family.FamilyMissionStatement = string.IsNullOrWhiteSpace(body.FamilyMissionStatement) ? null : body.FamilyMissionStatement.Trim();
        if (!string.IsNullOrWhiteSpace(body.SubscriptionTier)) family.SubscriptionTier = body.SubscriptionTier.Trim();

        family.UpdatedDate = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

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

    private static string[] ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
