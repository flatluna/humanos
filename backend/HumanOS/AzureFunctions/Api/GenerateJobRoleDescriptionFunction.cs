using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /tenants/{tenantId}/job-roles/generate-description — asks
/// <see cref="JobRoleDescriptionAgent"/> (Microsoft Agent Framework) to draft or improve
/// a Job Role's full description, grounded in the real company/department/title context
/// and this organization's own existing job roles (via the agent's
/// search_similar_job_roles tool). Returns a pure PROPOSAL — the RoleDetailPage editor
/// shows it as a before/after diff for the HRAdmin to accept/reject field by field;
/// nothing here is ever saved directly to the JobRole record.
/// </summary>
public sealed class GenerateJobRoleDescriptionFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HumanOsDbContext _dbContext;
    private readonly JobRoleDescriptionAgent _agent;

    public GenerateJobRoleDescriptionFunction(HumanOsDbContext dbContext, JobRoleDescriptionAgent agent)
    {
        _dbContext = dbContext;
        _agent = agent;
    }

    [Function("GenerateJobRoleDescription")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId:guid}/job-roles/generate-description")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "GenerationAgentNotConfigured",
                "The Job Role Description agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        GenerateJobRoleDescriptionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<GenerateJobRoleDescriptionRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Title))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "TitleRequired", "The job role title is required.", cancellationToken);
        }

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

        if (tenant is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "TenantNotFound", "No organization was found with that id.", cancellationToken);
        }

        string departmentName = "General / Multi-Department";
        if (body.DepartmentId.HasValue && body.DepartmentId.Value != Guid.Empty)
        {
            var dept = await _dbContext.Departments
                .AsNoTracking()
                .SingleOrDefaultAsync(d => d.DepartmentId == body.DepartmentId.Value && d.TenantId == tenantId, cancellationToken);
            if (dept != null)
            {
                departmentName = dept.Name;
            }
        }

        var hasAnyExistingContent =
            !string.IsNullOrWhiteSpace(body.Purpose) ||
            !string.IsNullOrWhiteSpace(body.Summary) ||
            (body.Responsibilities?.Count ?? 0) > 0 ||
            (body.TechnicalSkills?.Count ?? 0) > 0 ||
            (body.SoftSkills?.Count ?? 0) > 0 ||
            (body.DailyActivities?.Count ?? 0) > 0;

        var context = new JobRoleDescriptionGenerationContext
        {
            LanguageCode = string.Equals(body.LanguageCode, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es",
            TenantId = tenantId,
            Mode = hasAnyExistingContent ? JobRoleDescriptionMode.ImproveExisting : JobRoleDescriptionMode.CreateFromScratch,
            ExcludeJobRoleId = body.JobRoleId,
            CompanyName = tenant.Name,
            CompanyDescription = tenant.Description,
            DepartmentName = departmentName,
            Title = body.Title.Trim(),
            LevelLabel = body.LevelLabel,
            Purpose = body.Purpose,
            Summary = body.Summary,
            Responsibilities = body.Responsibilities ?? [],
            RequiredExperience = body.RequiredExperience,
            RequiredEducation = body.RequiredEducation,
            RequiredCertifications = body.RequiredCertifications ?? [],
            RequiredLanguages = body.RequiredLanguages ?? [],
            TechnicalSkills = body.TechnicalSkills ?? [],
            SoftSkills = body.SoftSkills ?? [],
            Tools = body.Tools ?? [],
            DailyActivities = body.DailyActivities ?? [],
            PerformanceIndicators = body.PerformanceIndicators ?? [],
            ExpectedOutcomes = body.ExpectedOutcomes ?? [],
            WorkSchedule = body.WorkSchedule,
            WorkModality = body.WorkModality,
            RequiresTravel = body.RequiresTravel,
            WorkLocation = body.WorkLocation,
            AvailabilityNotes = body.AvailabilityNotes,
            EmploymentType = body.EmploymentType,
            SalaryRangeMin = body.SalaryRangeMin,
            SalaryRangeMax = body.SalaryRangeMax,
            SalaryCurrency = body.SalaryCurrency,
            Benefits = body.Benefits ?? [],
        };

        JobRoleDescriptionGenerationResult result;
        try
        {
            result = await _agent.GenerateAsync(context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.UnprocessableEntity,
                "GenerationFailed",
                "The Job Role Description agent could not generate a proposal: " + ex.Message,
                cancellationToken);
        }

        var proposal = result.Proposal;
        var response = new GenerateJobRoleDescriptionResponse
        {
            LevelLabel = proposal.LevelLabel,
            Purpose = proposal.Purpose,
            Summary = proposal.Summary,
            Responsibilities = proposal.Responsibilities,
            RequiredExperience = proposal.RequiredExperience,
            RequiredEducation = proposal.RequiredEducation,
            RequiredCertifications = proposal.RequiredCertifications,
            RequiredLanguages = proposal.RequiredLanguages,
            TechnicalSkills = proposal.TechnicalSkills,
            SoftSkills = proposal.SoftSkills,
            Tools = proposal.Tools,
            DailyActivities = proposal.DailyActivities,
            PerformanceIndicators = proposal.PerformanceIndicators,
            ExpectedOutcomes = proposal.ExpectedOutcomes,
            WorkSchedule = proposal.WorkSchedule,
            WorkModality = proposal.WorkModality,
            RequiresTravel = proposal.RequiresTravel,
            WorkLocation = proposal.WorkLocation,
            AvailabilityNotes = proposal.AvailabilityNotes,
            EmploymentType = proposal.EmploymentType,
            SalaryRangeMin = proposal.SalaryRangeMin,
            SalaryRangeMax = proposal.SalaryRangeMax,
            SalaryCurrency = proposal.SalaryCurrency,
            Benefits = proposal.Benefits,
            ModelName = result.TokenUsage.ModelName,
            InputTokens = result.TokenUsage.InputTokens,
            OutputTokens = result.TokenUsage.OutputTokens,
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
