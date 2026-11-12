using System.Net;
using System.Text.Json;
using HumanOS.AzureFunctions.Api;
using HumanOS.Data;
using HumanOS.Models.Culture;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.EnterpriseEvolution.Culture;

public sealed class CreateCulturePrincipleRequest
{
    public Guid? TenantId { get; set; }
    public Guid? SourceCulturePrincipleId { get; set; }
    public string? PrincipleCode { get; set; }
    public string? NameEn { get; set; }
    public string? NameEs { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionEs { get; set; }
    public Guid? OwnerPersonId { get; set; }
    public int? VersionNumber { get; set; }
    public string? Status { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public Guid? ApprovedByPersonId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? CreatedByPersonId { get; set; }
}

public sealed class UpdateCulturePrincipleRequest
{
    public Guid? SourceCulturePrincipleId { get; set; }
    public string? PrincipleCode { get; set; }
    public string? NameEn { get; set; }
    public string? NameEs { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionEs { get; set; }
    public Guid? OwnerPersonId { get; set; }
    public int? VersionNumber { get; set; }
    public string? Status { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public Guid? ApprovedByPersonId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

/// <summary>
/// CRUD endpoints for dbo.CulturePrinciple, the Culture Library module under Enterprise
/// Evolution. Enforces the same lifecycle invariants as the database check constraints
/// (status values, approved-state requirements, effective-date ordering, self-reference
/// guard) so client callers get clear 400 responses instead of raw SQL errors, while the
/// database constraints remain the source of truth.
/// </summary>
public sealed class CulturePrincipleFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] AllowedStatuses =
    [
        CulturePrincipleStatus.Draft,
        CulturePrincipleStatus.Approved,
        CulturePrincipleStatus.Superseded,
        CulturePrincipleStatus.Retired,
    ];

    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public CulturePrincipleFunctions(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Function("GetCulturePrinciples")]
    public async Task<HttpResponseData> GetCulturePrinciplesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "enterprise-evolution/culture/principles")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var tenantIdRaw = query["tenantId"];

        var principlesQuery = dbContext.CulturePrinciples.AsNoTracking();

        if (string.Equals(tenantIdRaw, "global", StringComparison.OrdinalIgnoreCase))
        {
            principlesQuery = principlesQuery.Where(p => p.TenantId == null);
        }
        else if (Guid.TryParse(tenantIdRaw, out var tenantId))
        {
            principlesQuery = principlesQuery.Where(p => p.TenantId == tenantId);
        }

        var principles = await principlesQuery
            .OrderBy(p => p.PrincipleCode)
            .ThenByDescending(p => p.VersionNumber)
            .Select(p => ToDto(p))
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { principles }, cancellationToken: cancellationToken);
    }

    [Function("GetCulturePrincipleById")]
    public async Task<HttpResponseData> GetCulturePrincipleByIdAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "enterprise-evolution/culture/principles/{principleId:guid}")]
        HttpRequestData request,
        Guid principleId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var principle = await dbContext.CulturePrinciples
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CulturePrincipleId == principleId, cancellationToken);

        if (principle is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PrincipleNotFound", "Culture principle was not found.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, ToDto(principle), cancellationToken: cancellationToken);
    }

    [Function("CreateCulturePrinciple")]
    public async Task<HttpResponseData> CreateCulturePrincipleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "enterprise-evolution/culture/principles")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        CreateCulturePrincipleRequest? body;
        try
        {
            body = await DeserializeBodyAsync<CreateCulturePrincipleRequest>(request, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.PrincipleCode)
            || string.IsNullOrWhiteSpace(body.NameEn) || string.IsNullOrWhiteSpace(body.DescriptionEn))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody",
                "PrincipleCode, NameEn, and DescriptionEn are required.", cancellationToken);
        }

        var versionNumber = body.VersionNumber ?? 1;
        var status = string.IsNullOrWhiteSpace(body.Status) ? CulturePrincipleStatus.Draft : body.Status.Trim();

        var validationError = ValidatePrincipleRules(
            principleId: null,
            sourceCulturePrincipleId: body.SourceCulturePrincipleId,
            versionNumber: versionNumber,
            status: status,
            effectiveFrom: body.EffectiveFrom,
            effectiveTo: body.EffectiveTo,
            ownerPersonId: body.OwnerPersonId,
            approvedByPersonId: body.ApprovedByPersonId,
            approvedAt: body.ApprovedAt);

        if (validationError is not null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", validationError, cancellationToken);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var principle = new CulturePrinciple
        {
            CulturePrincipleId = Guid.NewGuid(),
            TenantId = body.TenantId,
            SourceCulturePrincipleId = body.SourceCulturePrincipleId,
            PrincipleCode = body.PrincipleCode.Trim(),
            NameEn = body.NameEn.Trim(),
            NameEs = string.IsNullOrWhiteSpace(body.NameEs) ? null : body.NameEs.Trim(),
            DescriptionEn = body.DescriptionEn.Trim(),
            DescriptionEs = string.IsNullOrWhiteSpace(body.DescriptionEs) ? null : body.DescriptionEs.Trim(),
            OwnerPersonId = body.OwnerPersonId,
            VersionNumber = versionNumber,
            Status = status,
            EffectiveFrom = body.EffectiveFrom,
            EffectiveTo = body.EffectiveTo,
            ApprovedByPersonId = body.ApprovedByPersonId,
            ApprovedAt = body.ApprovedAt,
            CreatedAt = DateTime.UtcNow,
            CreatedByPersonId = body.CreatedByPersonId,
            UpdatedAt = DateTime.UtcNow,
        };

        dbContext.CulturePrinciples.Add(principle);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "DuplicatePrinciple",
                "A principle with the same PrincipleCode and VersionNumber already exists for this tenant.",
                cancellationToken);
        }

        return await FunctionResponseFactory.CreatedResponseAsync(request, ToDto(principle), cancellationToken);
    }

    [Function("UpdateCulturePrinciple")]
    public async Task<HttpResponseData> UpdateCulturePrincipleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "enterprise-evolution/culture/principles/{principleId:guid}")]
        HttpRequestData request,
        Guid principleId,
        CancellationToken cancellationToken)
    {
        UpdateCulturePrincipleRequest? body;
        try
        {
            body = await DeserializeBodyAsync<UpdateCulturePrincipleRequest>(request, cancellationToken);
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

        var principle = await dbContext.CulturePrinciples
            .FirstOrDefaultAsync(p => p.CulturePrincipleId == principleId, cancellationToken);

        if (principle is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PrincipleNotFound", "Culture principle was not found to update.", cancellationToken);
        }

        var versionNumber = body.VersionNumber ?? principle.VersionNumber;
        var status = string.IsNullOrWhiteSpace(body.Status) ? principle.Status : body.Status.Trim();
        var sourceCulturePrincipleId = body.SourceCulturePrincipleId ?? principle.SourceCulturePrincipleId;
        var effectiveFrom = body.EffectiveFrom ?? principle.EffectiveFrom;
        var effectiveTo = body.EffectiveTo ?? principle.EffectiveTo;
        var ownerPersonId = body.OwnerPersonId ?? principle.OwnerPersonId;
        var approvedByPersonId = body.ApprovedByPersonId ?? principle.ApprovedByPersonId;
        var approvedAt = body.ApprovedAt ?? principle.ApprovedAt;

        var validationError = ValidatePrincipleRules(
            principleId: principleId,
            sourceCulturePrincipleId: sourceCulturePrincipleId,
            versionNumber: versionNumber,
            status: status,
            effectiveFrom: effectiveFrom,
            effectiveTo: effectiveTo,
            ownerPersonId: ownerPersonId,
            approvedByPersonId: approvedByPersonId,
            approvedAt: approvedAt);

        if (validationError is not null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", validationError, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(body.PrincipleCode)) principle.PrincipleCode = body.PrincipleCode.Trim();
        if (!string.IsNullOrWhiteSpace(body.NameEn)) principle.NameEn = body.NameEn.Trim();
        if (body.NameEs != null) principle.NameEs = string.IsNullOrWhiteSpace(body.NameEs) ? null : body.NameEs.Trim();
        if (!string.IsNullOrWhiteSpace(body.DescriptionEn)) principle.DescriptionEn = body.DescriptionEn.Trim();
        if (body.DescriptionEs != null) principle.DescriptionEs = string.IsNullOrWhiteSpace(body.DescriptionEs) ? null : body.DescriptionEs.Trim();
        principle.SourceCulturePrincipleId = sourceCulturePrincipleId;
        principle.OwnerPersonId = ownerPersonId;
        principle.VersionNumber = versionNumber;
        principle.Status = status;
        principle.EffectiveFrom = effectiveFrom;
        principle.EffectiveTo = effectiveTo;
        principle.ApprovedByPersonId = approvedByPersonId;
        principle.ApprovedAt = approvedAt;
        principle.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "DuplicatePrinciple",
                "A principle with the same PrincipleCode and VersionNumber already exists for this tenant.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, ToDto(principle), cancellationToken: cancellationToken);
    }

    [Function("DeleteCulturePrinciple")]
    public async Task<HttpResponseData> DeleteCulturePrincipleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "enterprise-evolution/culture/principles/{principleId:guid}")]
        HttpRequestData request,
        Guid principleId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var principle = await dbContext.CulturePrinciples
            .FirstOrDefaultAsync(p => p.CulturePrincipleId == principleId, cancellationToken);

        if (principle is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PrincipleNotFound", "Culture principle was not found to delete.", cancellationToken);
        }

        // Approved, Superseded, or historically referenced principles must not be
        // physically deleted. Only Draft principles with no tenant adaptations or
        // behaviors attached may be removed.
        if (!string.Equals(principle.Status, CulturePrincipleStatus.Draft, StringComparison.Ordinal))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "PrincipleNotDeletable",
                "Only principles in Draft status may be deleted. Approved, Superseded, or Retired principles must be retained.",
                cancellationToken);
        }

        var isReferenced = await dbContext.CulturePrinciples
            .AsNoTracking()
            .AnyAsync(p => p.SourceCulturePrincipleId == principleId, cancellationToken);

        if (isReferenced)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "PrincipleNotDeletable",
                "This principle is referenced by at least one tenant adaptation and cannot be deleted.",
                cancellationToken);
        }

        dbContext.CulturePrinciples.Remove(principle);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "PrincipleNotDeletable",
                "This principle cannot be deleted because other records still reference it.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new { principle.CulturePrincipleId, deleted = true }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Mirrors the database check constraints so invalid requests fail fast with a clear
    /// 400 response instead of a raw SQL exception.
    /// </summary>
    private static string? ValidatePrincipleRules(
        Guid? principleId,
        Guid? sourceCulturePrincipleId,
        int versionNumber,
        string status,
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        Guid? ownerPersonId,
        Guid? approvedByPersonId,
        DateTime? approvedAt)
    {
        if (versionNumber <= 0)
        {
            return "VersionNumber must be greater than zero.";
        }

        if (!AllowedStatuses.Contains(status))
        {
            return $"Status must be one of: {string.Join(", ", AllowedStatuses)}.";
        }

        if (principleId.HasValue && sourceCulturePrincipleId.HasValue && sourceCulturePrincipleId.Value == principleId.Value)
        {
            return "SourceCulturePrincipleId must not reference the same principle.";
        }

        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo.Value < effectiveFrom.Value)
        {
            return "EffectiveTo must not be earlier than EffectiveFrom.";
        }

        if (string.Equals(status, CulturePrincipleStatus.Approved, StringComparison.Ordinal))
        {
            if (!ownerPersonId.HasValue
                || !approvedByPersonId.HasValue
                || !approvedAt.HasValue
                || !effectiveFrom.HasValue)
            {
                return "Approved principles require OwnerPersonId, ApprovedByPersonId, ApprovedAt, and EffectiveFrom.";
            }
        }

        return null;
    }

    private static object ToDto(CulturePrinciple p) => new
    {
        p.CulturePrincipleId,
        p.TenantId,
        p.SourceCulturePrincipleId,
        p.PrincipleCode,
        p.NameEn,
        p.NameEs,
        p.DescriptionEn,
        p.DescriptionEs,
        p.OwnerPersonId,
        p.VersionNumber,
        p.Status,
        p.EffectiveFrom,
        p.EffectiveTo,
        p.ApprovedByPersonId,
        p.ApprovedAt,
        p.CreatedAt,
        p.CreatedByPersonId,
        p.UpdatedAt,
    };

    private static async Task<T?> DeserializeBodyAsync<T>(HttpRequestData request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text)) return default;
        return JsonSerializer.Deserialize<T>(text, JsonOptions);
    }
}
