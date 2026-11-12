using System.Net;
using System.Text.Json;
using HumanOS.AzureFunctions.Api;
using HumanOS.Data;
using HumanOS.Models.Culture;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.EnterpriseEvolution.Culture;

public sealed class CreateCulturePrincipleBehaviorRequest
{
    public string? BehaviorCode { get; set; }
    public string? BehaviorType { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionEs { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }
    public Guid? CreatedByPersonId { get; set; }
}

public sealed class UpdateCulturePrincipleBehaviorRequest
{
    public string? BehaviorCode { get; set; }
    public string? BehaviorType { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionEs { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// CRUD endpoints for dbo.CulturePrincipleBehavior, the observable-behavior catalog that
/// hangs off a Culture Library principle. Behaviors are always addressed in the context of
/// their parent principle (Route starts with the principleId), matching the FK relationship
/// enforced by the database.
/// </summary>
public sealed class CulturePrincipleBehaviorFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] AllowedBehaviorTypes =
    [
        CulturePrincipleBehaviorType.ExpectedBehavior,
        CulturePrincipleBehaviorType.BehaviorToAvoid,
        CulturePrincipleBehaviorType.LeadershipExpectation,
        CulturePrincipleBehaviorType.EmployeeExpectation,
        CulturePrincipleBehaviorType.AITransformationImplication,
    ];

    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public CulturePrincipleBehaviorFunctions(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Function("GetCulturePrincipleBehaviors")]
    public async Task<HttpResponseData> GetCulturePrincipleBehaviorsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "enterprise-evolution/culture/principles/{principleId:guid}/behaviors")]
        HttpRequestData request,
        Guid principleId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var principleExists = await dbContext.CulturePrinciples
            .AsNoTracking()
            .AnyAsync(p => p.CulturePrincipleId == principleId, cancellationToken);

        if (!principleExists)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PrincipleNotFound", "Culture principle was not found.", cancellationToken);
        }

        var behaviors = await dbContext.CulturePrincipleBehaviors
            .AsNoTracking()
            .Where(b => b.CulturePrincipleId == principleId)
            .OrderBy(b => b.BehaviorType)
            .ThenBy(b => b.SortOrder)
            .Select(b => ToDto(b))
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { behaviors }, cancellationToken: cancellationToken);
    }

    [Function("GetCulturePrincipleBehaviorById")]
    public async Task<HttpResponseData> GetCulturePrincipleBehaviorByIdAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "enterprise-evolution/culture/principles/{principleId:guid}/behaviors/{behaviorId:guid}")]
        HttpRequestData request,
        Guid principleId,
        Guid behaviorId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var behavior = await dbContext.CulturePrincipleBehaviors
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.CulturePrincipleBehaviorId == behaviorId && b.CulturePrincipleId == principleId, cancellationToken);

        if (behavior is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "BehaviorNotFound", "Culture principle behavior was not found.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, ToDto(behavior), cancellationToken: cancellationToken);
    }

    [Function("CreateCulturePrincipleBehavior")]
    public async Task<HttpResponseData> CreateCulturePrincipleBehaviorAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "enterprise-evolution/culture/principles/{principleId:guid}/behaviors")]
        HttpRequestData request,
        Guid principleId,
        CancellationToken cancellationToken)
    {
        CreateCulturePrincipleBehaviorRequest? body;
        try
        {
            body = await DeserializeBodyAsync<CreateCulturePrincipleBehaviorRequest>(request, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.BehaviorCode)
            || string.IsNullOrWhiteSpace(body.BehaviorType) || string.IsNullOrWhiteSpace(body.DescriptionEn))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody",
                "BehaviorCode, BehaviorType, and DescriptionEn are required.", cancellationToken);
        }

        var behaviorType = body.BehaviorType.Trim();
        var sortOrder = body.SortOrder ?? 0;

        var validationError = ValidateBehaviorRules(behaviorType, sortOrder);
        if (validationError is not null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", validationError, cancellationToken);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var principleExists = await dbContext.CulturePrinciples
            .AsNoTracking()
            .AnyAsync(p => p.CulturePrincipleId == principleId, cancellationToken);

        if (!principleExists)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PrincipleNotFound", "Culture principle was not found.", cancellationToken);
        }

        var behavior = new CulturePrincipleBehavior
        {
            CulturePrincipleBehaviorId = Guid.NewGuid(),
            CulturePrincipleId = principleId,
            BehaviorCode = body.BehaviorCode.Trim(),
            BehaviorType = behaviorType,
            DescriptionEn = body.DescriptionEn.Trim(),
            DescriptionEs = string.IsNullOrWhiteSpace(body.DescriptionEs) ? null : body.DescriptionEs.Trim(),
            SortOrder = sortOrder,
            IsActive = body.IsActive ?? true,
            CreatedAt = DateTime.UtcNow,
            CreatedByPersonId = body.CreatedByPersonId,
            UpdatedAt = DateTime.UtcNow,
        };

        dbContext.CulturePrincipleBehaviors.Add(behavior);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "DuplicateBehavior",
                "A behavior with the same BehaviorCode already exists for this principle.", cancellationToken);
        }

        return await FunctionResponseFactory.CreatedResponseAsync(request, ToDto(behavior), cancellationToken);
    }

    [Function("UpdateCulturePrincipleBehavior")]
    public async Task<HttpResponseData> UpdateCulturePrincipleBehaviorAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "enterprise-evolution/culture/principles/{principleId:guid}/behaviors/{behaviorId:guid}")]
        HttpRequestData request,
        Guid principleId,
        Guid behaviorId,
        CancellationToken cancellationToken)
    {
        UpdateCulturePrincipleBehaviorRequest? body;
        try
        {
            body = await DeserializeBodyAsync<UpdateCulturePrincipleBehaviorRequest>(request, cancellationToken);
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

        var behavior = await dbContext.CulturePrincipleBehaviors
            .FirstOrDefaultAsync(
                b => b.CulturePrincipleBehaviorId == behaviorId && b.CulturePrincipleId == principleId, cancellationToken);

        if (behavior is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "BehaviorNotFound", "Culture principle behavior was not found to update.", cancellationToken);
        }

        var behaviorType = string.IsNullOrWhiteSpace(body.BehaviorType) ? behavior.BehaviorType : body.BehaviorType.Trim();
        var sortOrder = body.SortOrder ?? behavior.SortOrder;

        var validationError = ValidateBehaviorRules(behaviorType, sortOrder);
        if (validationError is not null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", validationError, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(body.BehaviorCode)) behavior.BehaviorCode = body.BehaviorCode.Trim();
        behavior.BehaviorType = behaviorType;
        if (!string.IsNullOrWhiteSpace(body.DescriptionEn)) behavior.DescriptionEn = body.DescriptionEn.Trim();
        if (body.DescriptionEs != null) behavior.DescriptionEs = string.IsNullOrWhiteSpace(body.DescriptionEs) ? null : body.DescriptionEs.Trim();
        behavior.SortOrder = sortOrder;
        if (body.IsActive.HasValue) behavior.IsActive = body.IsActive.Value;
        behavior.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "DuplicateBehavior",
                "A behavior with the same BehaviorCode already exists for this principle.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, ToDto(behavior), cancellationToken: cancellationToken);
    }

    [Function("DeleteCulturePrincipleBehavior")]
    public async Task<HttpResponseData> DeleteCulturePrincipleBehaviorAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "enterprise-evolution/culture/principles/{principleId:guid}/behaviors/{behaviorId:guid}")]
        HttpRequestData request,
        Guid principleId,
        Guid behaviorId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var behavior = await dbContext.CulturePrincipleBehaviors
            .FirstOrDefaultAsync(
                b => b.CulturePrincipleBehaviorId == behaviorId && b.CulturePrincipleId == principleId, cancellationToken);

        if (behavior is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "BehaviorNotFound", "Culture principle behavior was not found to delete.", cancellationToken);
        }

        dbContext.CulturePrincipleBehaviors.Remove(behavior);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new { behavior.CulturePrincipleBehaviorId, deleted = true }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Mirrors the database check constraints so invalid requests fail fast with a clear
    /// 400 response instead of a raw SQL exception.
    /// </summary>
    private static string? ValidateBehaviorRules(string behaviorType, int sortOrder)
    {
        if (!AllowedBehaviorTypes.Contains(behaviorType))
        {
            return $"BehaviorType must be one of: {string.Join(", ", AllowedBehaviorTypes)}.";
        }

        if (sortOrder < 0)
        {
            return "SortOrder must not be negative.";
        }

        return null;
    }

    private static object ToDto(CulturePrincipleBehavior b) => new
    {
        b.CulturePrincipleBehaviorId,
        b.CulturePrincipleId,
        b.BehaviorCode,
        b.BehaviorType,
        b.DescriptionEn,
        b.DescriptionEs,
        b.SortOrder,
        b.IsActive,
        b.CreatedAt,
        b.CreatedByPersonId,
        b.UpdatedAt,
    };

    private static async Task<T?> DeserializeBodyAsync<T>(HttpRequestData request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text)) return default;
        return JsonSerializer.Deserialize<T>(text, JsonOptions);
    }
}
