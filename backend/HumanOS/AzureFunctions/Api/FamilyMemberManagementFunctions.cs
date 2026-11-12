using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class LinkFamilyMemberAccountRequest
{
    public string? AzureOid { get; set; }
    public string? AzureLoginEmail { get; set; }
}

public sealed class UpdateFamilyMemberRequest
{
    public string? FullName { get; set; }
    public string? PreferredName { get; set; }
    public string? FamilyRole { get; set; }
    public string? RoleTitleEs { get; set; }
    public string? RoleTitleEn { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public string? PrimaryLocation { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhoneCountryCode { get; set; }
    public string? AvatarBg { get; set; }
    public string[]? Languages { get; set; }
    public string[]? TopFocus { get; set; }
    public decimal? WeeklyHoursTarget { get; set; }
    public string? TargetMilestoneEs { get; set; }
    public string? TargetMilestoneEn { get; set; }
}

public sealed class CreateFamilyMemberRequest
{
    public Guid? FamilyId { get; set; }
    public string? FullName { get; set; }
    public string? PreferredName { get; set; }
    public string? FamilyRole { get; set; }
    public string? RoleTitleEs { get; set; }
    public string? RoleTitleEn { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public string? PrimaryLocation { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhoneCountryCode { get; set; }
    public string? AvatarBg { get; set; }
    public string[]? Languages { get; set; }
    public string[]? TopFocus { get; set; }
    public decimal? WeeklyHoursTarget { get; set; }
    public string? TargetMilestoneEs { get; set; }
    public string? TargetMilestoneEn { get; set; }
}

public sealed class SaveMemberEducationRequest
{
    public string? EducationLevel { get; set; }
    public string? SchoolName { get; set; }
    public string? GradeOrDegree { get; set; }
    public string? FieldOfStudy { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public bool? IsCurrent { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? Gpa { get; set; }
    public string? Degree { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public sealed class FamilyMemberManagementFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public FamilyMemberManagementFunctions(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Function("GetFamilyMemberById")]
    public async Task<HttpResponseData> GetMemberByIdAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/members/{memberId:guid}")]
        HttpRequestData request,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var member = await dbContext.FamilyMembers
            .AsNoTracking()
            .Include(m => m.Family)
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.IsActive, cancellationToken);

        if (member is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "MemberNotFound", "Member was not found.", cancellationToken);
        }

        var educations = await dbContext.FamilyMemberEducations
            .AsNoTracking()
            .Where(e => e.MemberId == memberId && e.IsActive)
            .OrderByDescending(e => e.IsCurrent)
            .ThenByDescending(e => e.EndYear ?? e.StartYear ?? 0)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new
            {
                member = new
                {
                    member.MemberId,
                    member.FamilyId,
                    member.FullName,
                    member.PreferredName,
                    member.FamilyRole,
                    member.RoleTitleEs,
                    member.RoleTitleEn,
                    member.DateOfBirth,
                    member.Age,
                    member.EducationLevelEs,
                    member.EducationLevelEn,
                    member.Institution,
                    PrimaryLocation = member.PrimaryLocation ?? member.Family?.PrimaryFullAddress,
                    PrimaryStreet = member.Family?.PrimaryStreet,
                    PrimaryCity = member.Family?.PrimaryCity,
                    PrimaryState = member.Family?.PrimaryState,
                    PrimaryZipCode = member.Family?.PrimaryZipCode,
                    PrimaryCountry = member.Family?.PrimaryCountry ?? "USA",
                    PrimaryFullAddress = member.PrimaryLocation ?? member.Family?.PrimaryFullAddress,
                    Latitude = member.Family?.Latitude,
                    Longitude = member.Family?.Longitude,
                    member.Email,
                    member.PhoneNumber,
                    member.PhoneCountryCode,
                    member.AzureOid,
                    member.AzureLoginEmail,
                    member.AzureLinkedDate,
                    member.PlatformRole,
                    member.AvatarBg,
                    Languages = ParseJsonArray(member.LanguagesJson),
                    TopFocus = ParseJsonArray(member.TopFocusJson),
                    member.WeeklyHoursTarget,
                    member.LearningStreakDays,
                    member.TargetMilestoneEs,
                    member.TargetMilestoneEn,
                    member.IsActive,
                },
                educations = educations.Select(e => new
                {
                    e.EducationId,
                    e.TenantId,
                    e.FamilyId,
                    e.MemberId,
                    e.EducationLevel,
                    e.SchoolName,
                    e.GradeOrDegree,
                    e.FieldOfStudy,
                    e.StartYear,
                    e.EndYear,
                    e.IsCurrent,
                    e.City,
                    e.State,
                    e.Country,
                    e.Gpa,
                    e.Degree,
                    e.Status,
                    e.Notes,
                    e.IsActive,
                    e.CreatedDate,
                    e.UpdatedDate,
                }),
            },
            cancellationToken: cancellationToken);
    }

    [Function("CreateFamilyMember")]
    public async Task<HttpResponseData> CreateMemberAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/members")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        CreateFamilyMemberRequest? body;
        try
        {
            body = await DeserializeBodyAsync<CreateFamilyMemberRequest>(request, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.FullName))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", "FullName is required.", cancellationToken);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var familyId = body.FamilyId;
        if (!familyId.HasValue)
        {
            var defaultFamily = await dbContext.FamilyMasters
                .AsNoTracking()
                .Where(f => f.IsActive)
                .OrderByDescending(f => f.CreatedDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (defaultFamily is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "FamilyNotFound", "No active family record was found.", cancellationToken);
            }

            familyId = defaultFamily.FamilyId;
        }

        var newMember = new FamilyMember
        {
            MemberId = Guid.NewGuid(),
            FamilyId = familyId.Value,
            FullName = body.FullName.Trim(),
            PreferredName = string.IsNullOrWhiteSpace(body.PreferredName) ? body.FullName.Trim() : body.PreferredName.Trim(),
            FamilyRole = string.IsNullOrWhiteSpace(body.FamilyRole) ? "son" : body.FamilyRole.Trim(),
            RoleTitleEs = string.IsNullOrWhiteSpace(body.RoleTitleEs) ? "Integrante" : body.RoleTitleEs.Trim(),
            RoleTitleEn = string.IsNullOrWhiteSpace(body.RoleTitleEn) ? "Member" : body.RoleTitleEn.Trim(),
            DateOfBirth = body.DateOfBirth,
            Age = body.Age,
            PrimaryLocation = body.PrimaryLocation?.Trim(),
            Email = body.Email?.Trim(),
            PhoneNumber = body.PhoneNumber?.Trim(),
            PhoneCountryCode = body.PhoneCountryCode?.Trim(),
            AvatarBg = string.IsNullOrWhiteSpace(body.AvatarBg) ? "from-emerald-600 to-teal-700" : body.AvatarBg.Trim(),
            WeeklyHoursTarget = body.WeeklyHoursTarget ?? 8.0m,
            LearningStreakDays = 0,
            TargetMilestoneEs = body.TargetMilestoneEs?.Trim(),
            TargetMilestoneEn = body.TargetMilestoneEn?.Trim(),
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
            LanguagesJson = body.Languages is not null
                ? JsonSerializer.Serialize(body.Languages.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()))
                : null,
            TopFocusJson = body.TopFocus is not null
                ? JsonSerializer.Serialize(body.TopFocus.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()))
                : null,
        };

        dbContext.FamilyMembers.Add(newMember);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.CreatedResponseAsync(
            request,
            new
            {
                newMember.MemberId,
                newMember.FamilyId,
                newMember.FullName,
                newMember.PreferredName,
                newMember.FamilyRole,
                newMember.RoleTitleEs,
                newMember.RoleTitleEn,
                newMember.DateOfBirth,
                newMember.Age,
                newMember.EducationLevelEs,
                newMember.EducationLevelEn,
                newMember.Institution,
                newMember.PrimaryLocation,
                newMember.Email,
                newMember.PhoneNumber,
                newMember.PhoneCountryCode,
                newMember.AzureOid,
                newMember.AzureLoginEmail,
                newMember.AzureLinkedDate,
                newMember.PlatformRole,
                newMember.AvatarBg,
                Languages = ParseJsonArray(newMember.LanguagesJson),
                TopFocus = ParseJsonArray(newMember.TopFocusJson),
                newMember.WeeklyHoursTarget,
                newMember.LearningStreakDays,
                newMember.TargetMilestoneEs,
                newMember.TargetMilestoneEn,
                newMember.IsActive,
            },
            cancellationToken: cancellationToken);
    }

    [Function("UpdateFamilyMember")]
    public async Task<HttpResponseData> UpdateMemberAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "family/members/{memberId:guid}")]
        HttpRequestData request,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        UpdateFamilyMemberRequest? body;
        try
        {
            body = await DeserializeBodyAsync<UpdateFamilyMemberRequest>(request, cancellationToken);
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

        var member = await dbContext.FamilyMembers
            .Include(m => m.Family)
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.IsActive, cancellationToken);

        if (member is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "MemberNotFound", "Member was not found to update.", cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(body.FullName)) member.FullName = body.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(body.PreferredName)) member.PreferredName = body.PreferredName.Trim();
        if (!string.IsNullOrWhiteSpace(body.FamilyRole)) member.FamilyRole = body.FamilyRole.Trim();
        if (!string.IsNullOrWhiteSpace(body.RoleTitleEs)) member.RoleTitleEs = body.RoleTitleEs.Trim();
        if (!string.IsNullOrWhiteSpace(body.RoleTitleEn)) member.RoleTitleEn = body.RoleTitleEn.Trim();
        if (body.DateOfBirth.HasValue) member.DateOfBirth = body.DateOfBirth.Value;
        if (body.Age.HasValue) member.Age = body.Age.Value;
        if (body.PrimaryLocation != null) member.PrimaryLocation = body.PrimaryLocation.Trim();
        if (body.Email != null) member.Email = body.Email.Trim();
        if (body.PhoneNumber != null) member.PhoneNumber = body.PhoneNumber.Trim();
        if (body.PhoneCountryCode != null) member.PhoneCountryCode = body.PhoneCountryCode.Trim();
        if (!string.IsNullOrWhiteSpace(body.AvatarBg)) member.AvatarBg = body.AvatarBg.Trim();
        if (body.WeeklyHoursTarget.HasValue) member.WeeklyHoursTarget = body.WeeklyHoursTarget.Value;
        if (body.TargetMilestoneEs != null) member.TargetMilestoneEs = body.TargetMilestoneEs.Trim();
        if (body.TargetMilestoneEn != null) member.TargetMilestoneEn = body.TargetMilestoneEn.Trim();

        if (body.Languages is not null)
        {
            member.LanguagesJson = JsonSerializer.Serialize(body.Languages.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()));
        }
        if (body.TopFocus is not null)
        {
            member.TopFocusJson = JsonSerializer.Serialize(body.TopFocus.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()));
        }

        member.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new
            {
                member.MemberId,
                member.FamilyId,
                member.FullName,
                member.PreferredName,
                member.FamilyRole,
                member.RoleTitleEs,
                member.RoleTitleEn,
                member.DateOfBirth,
                member.Age,
                member.EducationLevelEs,
                member.EducationLevelEn,
                member.Institution,
                PrimaryLocation = member.PrimaryLocation ?? member.Family?.PrimaryFullAddress,
                PrimaryStreet = member.Family?.PrimaryStreet,
                PrimaryCity = member.Family?.PrimaryCity,
                PrimaryState = member.Family?.PrimaryState,
                PrimaryZipCode = member.Family?.PrimaryZipCode,
                PrimaryCountry = member.Family?.PrimaryCountry ?? "USA",
                PrimaryFullAddress = member.PrimaryLocation ?? member.Family?.PrimaryFullAddress,
                Latitude = member.Family?.Latitude,
                Longitude = member.Family?.Longitude,
                member.Email,
                member.PhoneNumber,
                member.PhoneCountryCode,
                member.AzureOid,
                member.AzureLoginEmail,
                member.AzureLinkedDate,
                member.PlatformRole,
                member.AvatarBg,
                Languages = ParseJsonArray(member.LanguagesJson),
                TopFocus = ParseJsonArray(member.TopFocusJson),
                member.WeeklyHoursTarget,
                member.LearningStreakDays,
                member.TargetMilestoneEs,
                member.TargetMilestoneEn,
                member.IsActive,
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Soft-deletes a family member (sets IsActive = false). Used e.g. to
    /// remove a member record created by mistake; data is kept for audit but
    /// excluded from all "active member" queries/projections.
    /// </summary>
    [Function("DeleteFamilyMember")]
    public async Task<HttpResponseData> DeleteMemberAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "family/members/{memberId:guid}")]
        HttpRequestData request,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var member = await dbContext.FamilyMembers
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.IsActive, cancellationToken);

        if (member is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "MemberNotFound", "Member was not found to delete.", cancellationToken);
        }

        member.IsActive = false;
        member.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new { member.MemberId, member.IsActive },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Links the real MSAL (Entra CIAM) account used by a family member to
    /// their FamilyMember record: stores the Azure object id (AzureOid) AND
    /// the actual email/UPN the account was created with (AzureLoginEmail).
    /// AzureLoginEmail can differ from the member's household/contact Email
    /// — e.g. demo accounts created once and reused across several business
    /// use cases. Per family login policy, only members 12+ may link an
    /// account; younger members are managed from the family admin's login.
    /// </summary>
    [Function("LinkFamilyMemberAccount")]
    public async Task<HttpResponseData> LinkMemberAccountAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/members/{memberId:guid}/link-account")]
        HttpRequestData request,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        LinkFamilyMemberAccountRequest? body;
        try
        {
            body = await DeserializeBodyAsync<LinkFamilyMemberAccountRequest>(request, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.AzureOid) || string.IsNullOrWhiteSpace(body.AzureLoginEmail))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", "AzureOid and AzureLoginEmail are required.", cancellationToken);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var member = await dbContext.FamilyMembers
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.IsActive, cancellationToken);

        if (member is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "MemberNotFound", "Member was not found.", cancellationToken);
        }

        const int minAgeForOwnAccount = 12;
        if (!member.Age.HasValue || member.Age.Value < minAgeForOwnAccount)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "MemberTooYoung",
                $"Members under {minAgeForOwnAccount} years old cannot be linked to their own MSAL account.", cancellationToken);
        }

        member.AzureOid = body.AzureOid.Trim();
        member.AzureLoginEmail = body.AzureLoginEmail.Trim();
        member.AzureLinkedDate = DateTime.UtcNow;
        member.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new
            {
                member.MemberId,
                member.AzureOid,
                member.AzureLoginEmail,
                member.AzureLinkedDate,
                member.PlatformRole,
            },
            cancellationToken: cancellationToken);
    }

    [Function("GetMemberEducations")]
    public async Task<HttpResponseData> GetMemberEducationsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/members/{memberId:guid}/education")]
        HttpRequestData request,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var educations = await dbContext.FamilyMemberEducations
            .AsNoTracking()
            .Where(e => e.MemberId == memberId && e.IsActive)
            .OrderByDescending(e => e.IsCurrent)
            .ThenByDescending(e => e.EndYear ?? e.StartYear ?? 0)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            educations.Select(e => new
            {
                e.EducationId,
                e.TenantId,
                e.FamilyId,
                e.MemberId,
                e.EducationLevel,
                e.SchoolName,
                e.GradeOrDegree,
                e.FieldOfStudy,
                e.StartYear,
                e.EndYear,
                e.IsCurrent,
                e.City,
                e.State,
                e.Country,
                e.Gpa,
                e.Degree,
                e.Status,
                e.Notes,
                e.IsActive,
                e.CreatedDate,
                e.UpdatedDate,
            }),
            cancellationToken: cancellationToken);
    }

    [Function("CreateMemberEducation")]
    public async Task<HttpResponseData> CreateMemberEducationAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/members/{memberId:guid}/education")]
        HttpRequestData request,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        SaveMemberEducationRequest? body;
        try
        {
            body = await DeserializeBodyAsync<SaveMemberEducationRequest>(request, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.EducationLevel) || string.IsNullOrWhiteSpace(body.SchoolName))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidFields", "EducationLevel and SchoolName are required.", cancellationToken);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var member = await dbContext.FamilyMembers
            .Include(m => m.Family)
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.IsActive, cancellationToken);

        if (member is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "MemberNotFound", "Member was not found.", cancellationToken);
        }

        var tenantId = member.Family?.TenantId ?? Guid.Empty;

        var isCurrent = body.IsCurrent ?? true;
        if (isCurrent)
        {
            // If marked as current, mark other existing educations as not current
            var existingCurrent = await dbContext.FamilyMemberEducations
                .Where(e => e.MemberId == memberId && e.IsCurrent && e.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var ex in existingCurrent)
            {
                ex.IsCurrent = false;
                ex.UpdatedDate = DateTime.UtcNow;
            }
        }

        var newEducation = new FamilyMemberEducation
        {
            EducationId = Guid.NewGuid(),
            TenantId = tenantId,
            FamilyId = member.FamilyId,
            MemberId = memberId,
            EducationLevel = body.EducationLevel.Trim(),
            SchoolName = body.SchoolName.Trim(),
            GradeOrDegree = string.IsNullOrWhiteSpace(body.GradeOrDegree) ? null : body.GradeOrDegree.Trim(),
            FieldOfStudy = string.IsNullOrWhiteSpace(body.FieldOfStudy) ? null : body.FieldOfStudy.Trim(),
            StartYear = body.StartYear,
            EndYear = body.EndYear,
            IsCurrent = isCurrent,
            City = string.IsNullOrWhiteSpace(body.City) ? null : body.City.Trim(),
            State = string.IsNullOrWhiteSpace(body.State) ? null : body.State.Trim(),
            Country = string.IsNullOrWhiteSpace(body.Country) ? null : body.Country.Trim(),
            Gpa = string.IsNullOrWhiteSpace(body.Gpa) ? null : body.Gpa.Trim(),
            Degree = string.IsNullOrWhiteSpace(body.Degree) ? null : body.Degree.Trim(),
            Status = string.IsNullOrWhiteSpace(body.Status) ? null : body.Status.Trim(),
            Notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };

        dbContext.FamilyMemberEducations.Add(newEducation);

        // Also update member's denormalized primary institution/education level
        if (isCurrent)
        {
            member.EducationLevelEs = newEducation.EducationLevel;
            member.EducationLevelEn = newEducation.EducationLevel;
            member.Institution = newEducation.SchoolName;
            member.UpdatedDate = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new
            {
                newEducation.EducationId,
                newEducation.TenantId,
                newEducation.FamilyId,
                newEducation.MemberId,
                newEducation.EducationLevel,
                newEducation.SchoolName,
                newEducation.GradeOrDegree,
                newEducation.FieldOfStudy,
                newEducation.StartYear,
                newEducation.EndYear,
                newEducation.IsCurrent,
                newEducation.City,
                newEducation.State,
                newEducation.Country,
                newEducation.Gpa,
                newEducation.Degree,
                newEducation.Status,
                newEducation.Notes,
                newEducation.IsActive,
                newEducation.CreatedDate,
                newEducation.UpdatedDate,
            },
            HttpStatusCode.Created,
            cancellationToken);
    }

    [Function("UpdateMemberEducation")]
    public async Task<HttpResponseData> UpdateMemberEducationAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "family/members/{memberId:guid}/education/{educationId:guid}")]
        HttpRequestData request,
        Guid memberId,
        Guid educationId,
        CancellationToken cancellationToken)
    {
        SaveMemberEducationRequest? body;
        try
        {
            body = await DeserializeBodyAsync<SaveMemberEducationRequest>(request, cancellationToken);
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

        var education = await dbContext.FamilyMemberEducations
            .FirstOrDefaultAsync(e => e.EducationId == educationId && e.MemberId == memberId && e.IsActive, cancellationToken);

        if (education is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "EducationNotFound", "Education record was not found.", cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(body.EducationLevel)) education.EducationLevel = body.EducationLevel.Trim();
        if (!string.IsNullOrWhiteSpace(body.SchoolName)) education.SchoolName = body.SchoolName.Trim();
        if (body.GradeOrDegree != null) education.GradeOrDegree = string.IsNullOrWhiteSpace(body.GradeOrDegree) ? null : body.GradeOrDegree.Trim();
        if (body.FieldOfStudy != null) education.FieldOfStudy = string.IsNullOrWhiteSpace(body.FieldOfStudy) ? null : body.FieldOfStudy.Trim();
        if (body.StartYear.HasValue) education.StartYear = body.StartYear.Value;
        if (body.EndYear.HasValue) education.EndYear = body.EndYear.Value;
        if (body.City != null) education.City = string.IsNullOrWhiteSpace(body.City) ? null : body.City.Trim();
        if (body.State != null) education.State = string.IsNullOrWhiteSpace(body.State) ? null : body.State.Trim();
        if (body.Country != null) education.Country = string.IsNullOrWhiteSpace(body.Country) ? null : body.Country.Trim();
        if (body.Gpa != null) education.Gpa = string.IsNullOrWhiteSpace(body.Gpa) ? null : body.Gpa.Trim();
        if (body.Degree != null) education.Degree = string.IsNullOrWhiteSpace(body.Degree) ? null : body.Degree.Trim();
        if (body.Status != null) education.Status = string.IsNullOrWhiteSpace(body.Status) ? null : body.Status.Trim();
        if (body.Notes != null) education.Notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim();

        if (body.IsCurrent.HasValue)
        {
            education.IsCurrent = body.IsCurrent.Value;
            if (education.IsCurrent)
            {
                var others = await dbContext.FamilyMemberEducations
                    .Where(e => e.MemberId == memberId && e.EducationId != educationId && e.IsCurrent && e.IsActive)
                    .ToListAsync(cancellationToken);
                foreach (var other in others)
                {
                    other.IsCurrent = false;
                    other.UpdatedDate = DateTime.UtcNow;
                }

                var member = await dbContext.FamilyMembers.FirstOrDefaultAsync(m => m.MemberId == memberId, cancellationToken);
                if (member != null)
                {
                    member.EducationLevelEs = education.EducationLevel;
                    member.EducationLevelEn = education.EducationLevel;
                    member.Institution = education.SchoolName;
                    member.UpdatedDate = DateTime.UtcNow;
                }
            }
        }

        education.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new
            {
                education.EducationId,
                education.TenantId,
                education.FamilyId,
                education.MemberId,
                education.EducationLevel,
                education.SchoolName,
                education.GradeOrDegree,
                education.FieldOfStudy,
                education.StartYear,
                education.EndYear,
                education.IsCurrent,
                education.City,
                education.State,
                education.Country,
                education.Gpa,
                education.Degree,
                education.Status,
                education.Notes,
                education.IsActive,
                education.CreatedDate,
                education.UpdatedDate,
            },
            cancellationToken: cancellationToken);
    }

    [Function("DeleteMemberEducation")]
    public async Task<HttpResponseData> DeleteMemberEducationAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "family/members/{memberId:guid}/education/{educationId:guid}")]
        HttpRequestData request,
        Guid memberId,
        Guid educationId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var education = await dbContext.FamilyMemberEducations
            .FirstOrDefaultAsync(e => e.EducationId == educationId && e.MemberId == memberId && e.IsActive, cancellationToken);

        if (education is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "EducationNotFound", "Education record was not found.", cancellationToken);
        }

        education.IsActive = false;
        education.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new { message = "Education record deleted successfully.", educationId },
            cancellationToken: cancellationToken);
    }

    private static async Task<T?> DeserializeBodyAsync<T>(HttpRequestData request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text)) return default;
        return JsonSerializer.Deserialize<T>(text, JsonOptions);
    }

    private static string[] ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
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
