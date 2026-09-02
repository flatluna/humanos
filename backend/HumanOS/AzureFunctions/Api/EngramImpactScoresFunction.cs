using System.Net;
using System.Web;
using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Exposes endpoints for retrieving and refreshing personalized Engram impact scores
/// for the authenticated employee within a specific course/document.
/// </summary>
public sealed class EngramImpactScoresFunction
{
    private readonly EngramImpactOrchestrator _orchestrator;
    private readonly PersonService _personService;
    private readonly HumanOsDbContext _dbContext;
    private readonly ILogger<EngramImpactScoresFunction> _logger;

    public EngramImpactScoresFunction(
        EngramImpactOrchestrator orchestrator,
        PersonService personService,
        HumanOsDbContext dbContext,
        ILogger<EngramImpactScoresFunction> logger)
    {
        _orchestrator = orchestrator;
        _personService = personService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/engram-impact-scores/{courseId}
    /// Retrieves personalized impact scores for the authenticated employee in the given course.
    /// Hits database cache if scores already exist.
    /// </summary>
    [Function("GetEngramImpactScores")]
    public async Task<HttpResponseData> GetScoresAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "engram-impact-scores/{courseId:guid}")]
        HttpRequestData request,
        Guid courseId,
        CancellationToken cancellationToken)
    {
        if (courseId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidCourseId", "A valid courseId is required.", cancellationToken);
        }

        var employeeId = await ResolveAuthenticatedEmployeeIdAsync(request, cancellationToken);
        if (employeeId is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "Unauthorized", "Authenticated employee identity is required.", cancellationToken);
        }

        try
        {
            var scores = await _orchestrator.GetOrGenerateScoresAsync(
                employeeId.Value,
                courseId,
                forceRefresh: false,
                cancellationToken: cancellationToken);

            var dtos = scores.Select(s => new EngramImpactScoreDto
            {
                EngramId = s.EngramId,
                Score = s.Score,
                Tier = s.Tier ?? string.Empty,
                PrimarySignal = s.PrimarySignal ?? string.Empty,
                Reason = s.Reason ?? string.Empty,
                GeneratedAt = s.GeneratedAt
            }).ToList();

            return await FunctionResponseFactory.SuccessResponseAsync(request, dtos, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting impact scores for employee {EmployeeId} and course {CourseId}", employeeId, courseId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "EngramImpactScoringError", ex.Message, cancellationToken);
        }
    }

    /// <summary>
    /// POST /api/engram-impact-scores/{courseId}/refresh
    /// Forces re-scoring and regeneration of impact scores with the AI agent for the authenticated employee.
    /// </summary>
    [Function("RefreshEngramImpactScores")]
    public async Task<HttpResponseData> RefreshScoresAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "engram-impact-scores/{courseId:guid}/refresh")]
        HttpRequestData request,
        Guid courseId,
        CancellationToken cancellationToken)
    {
        if (courseId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidCourseId", "A valid courseId is required.", cancellationToken);
        }

        var employeeId = await ResolveAuthenticatedEmployeeIdAsync(request, cancellationToken);
        if (employeeId is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "Unauthorized", "Authenticated employee identity is required.", cancellationToken);
        }

        try
        {
            var scores = await _orchestrator.GetOrGenerateScoresAsync(
                employeeId.Value,
                courseId,
                forceRefresh: true,
                cancellationToken: cancellationToken);

            var dtos = scores.Select(s => new EngramImpactScoreDto
            {
                EngramId = s.EngramId,
                Score = s.Score,
                Tier = s.Tier ?? string.Empty,
                PrimarySignal = s.PrimarySignal ?? string.Empty,
                Reason = s.Reason ?? string.Empty,
                GeneratedAt = s.GeneratedAt
            }).ToList();

            return await FunctionResponseFactory.SuccessResponseAsync(request, dtos, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing impact scores for employee {EmployeeId} and course {CourseId}", employeeId, courseId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "EngramImpactScoringError", ex.Message, cancellationToken);
        }
    }

    private async Task<Guid?> ResolveAuthenticatedEmployeeIdAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        // 1. Identity headers (MSAL / Enterprise auth)
        if (request.Headers.TryGetValues("X-Azure-OID", out var oidValues) && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
            request.Headers.TryGetValues("X-Azure-TID", out var tidValues) && !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            var person = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
            if (person is not null)
            {
                return person.PersonId;
            }
        }

        // 2. Direct header
        if (request.Headers.TryGetValues("X-Person-Id", out var personValues) &&
            Guid.TryParse(personValues.FirstOrDefault(), out var personId))
        {
            return personId;
        }

        // 3. Query string parameter (?personId=... or ?employeeId=...)
        var query = HttpUtility.ParseQueryString(request.Url.Query);
        var queryPersonId = query["personId"] ?? query["employeeId"];
        if (!string.IsNullOrWhiteSpace(queryPersonId) && Guid.TryParse(queryPersonId, out var parsedQueryId))
        {
            return parsedQueryId;
        }

        // 4. Fallback for single-tenant / local development context
        var defaultPersonId = await _dbContext.People
            .Where(p => p.IsActive)
            .OrderBy(p => p.CreatedDate)
            .Select(p => (Guid?)p.PersonId)
            .FirstOrDefaultAsync(cancellationToken);

        return defaultPersonId;
    }
}
