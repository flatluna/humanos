using System.Net;
using System.Web;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Minimal read-only DTO exposing essential learner profile summary for UI personalization banners.
/// </summary>
public sealed class LearnerProfileSummaryDto
{
    public Guid EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public string RoleTitle { get; set; } = string.Empty;

    public string RolePurpose { get; set; } = string.Empty;

    public int? YearsOfExperience { get; set; }

    public string ExperienceLevel { get; set; } = string.Empty; // "Senior" | "Mid-Level" | "Junior"

    public List<string> PrimaryTools { get; set; } = [];

    public List<string> TopSkills { get; set; } = [];

    public List<string> ActiveObjectiveTitles { get; set; } = [];
}

/// <summary>
/// Minimal read-only endpoint returning learner personalization summary.
/// </summary>
public sealed class GetLearnerProfileFunction
{
    private readonly EmployeeScoringProfileService _profileService;
    private readonly PersonService _personService;
    private readonly HumanOsDbContext _dbContext;
    private readonly ILogger<GetLearnerProfileFunction> _logger;

    public GetLearnerProfileFunction(
        EmployeeScoringProfileService profileService,
        PersonService personService,
        HumanOsDbContext dbContext,
        ILogger<GetLearnerProfileFunction> logger)
    {
        _profileService = profileService;
        _personService = personService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/learner-profile
    /// Retrieves a lightweight summary of the learner's profile (name, role, tools, experience level)
    /// for UI personalization banners.
    /// </summary>
    [Function("GetLearnerProfile")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "learner-profile")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var employeeId = await ResolveAuthenticatedEmployeeIdAsync(request, cancellationToken);
        if (employeeId is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "Unauthorized", "Authenticated employee identity is required.", cancellationToken);
        }

        try
        {
            var profile = await _profileService.BuildProfileAsync(employeeId.Value, cancellationToken);

            var tools = profile.Tools
                .Concat(profile.TechnicalSkills.Take(3))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            var expYears = profile.YearsOfExperience;
            string expLevel = "Mid-Level";
            if (expYears.HasValue)
            {
                expLevel = expYears.Value >= 5 ? "Senior" : expYears.Value >= 2 ? "Mid-Level" : "Junior";
            }
            else if (!string.IsNullOrWhiteSpace(profile.RoleTitle))
            {
                var lower = profile.RoleTitle.ToLowerInvariant();
                if (lower.Contains("senior") || lower.Contains("lead") || lower.Contains("principal") || lower.Contains("architect"))
                {
                    expLevel = "Senior";
                }
                else if (lower.Contains("junior") || lower.Contains("trainee") || lower.Contains("intern"))
                {
                    expLevel = "Junior";
                }
            }

            var dto = new LearnerProfileSummaryDto
            {
                EmployeeId = profile.EmployeeId,
                EmployeeName = profile.EmployeeName,
                RoleTitle = profile.RoleTitle,
                RolePurpose = profile.RolePurpose,
                YearsOfExperience = expYears,
                ExperienceLevel = expLevel,
                PrimaryTools = tools,
                TopSkills = profile.TechnicalSkills.Take(5).ToList(),
                ActiveObjectiveTitles = profile.ActiveObjectives.Select(o => o.Title).Take(2).ToList()
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, dto, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving learner profile summary for {EmployeeId}", employeeId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "LearnerProfileError", ex.Message, cancellationToken);
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