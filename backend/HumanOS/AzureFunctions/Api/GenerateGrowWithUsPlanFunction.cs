using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /people/{personId}/grow-with-us-plan — "Grow With Us" → Advance
/// Your Knowledge growth-plan generator (2026-09-27). Triggered from the
/// employee app's "Grow With Us" prompt modal: given a free-text prompt,
/// gathers the person's role skills + FULL job description, résumé, and
/// already-completed courses, searches the live internet for current
/// courses/papers/news via <see cref="WebGroundingService"/> (skipped
/// silently if not configured), then asks <see cref="GrowWithUsPlanAgent"/>
/// to produce the full report.
/// </summary>
public sealed class GenerateGrowWithUsPlanFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HumanOsDbContext _dbContext;
    private readonly JobRoleService _jobRoleService;
    private readonly PersonResumeService _personResumeService;
    private readonly CapabilityEnrollmentService _capabilityEnrollmentService;
    private readonly WebGroundingService _webGrounding;
    private readonly GrowWithUsPlanAgent _agent;
    private readonly GrowWithUsSavedSearchService _savedSearchService;

    public GenerateGrowWithUsPlanFunction(
        HumanOsDbContext dbContext,
        JobRoleService jobRoleService,
        PersonResumeService personResumeService,
        CapabilityEnrollmentService capabilityEnrollmentService,
        WebGroundingService webGrounding,
        GrowWithUsPlanAgent agent,
        GrowWithUsSavedSearchService savedSearchService)
    {
        _dbContext = dbContext;
        _jobRoleService = jobRoleService;
        _personResumeService = personResumeService;
        _capabilityEnrollmentService = capabilityEnrollmentService;
        _webGrounding = webGrounding;
        _agent = agent;
        _savedSearchService = savedSearchService;
    }

    public sealed class Request
    {
        public string Prompt { get; set; } = string.Empty;

        public string? LanguageCode { get; set; }
    }

    [Function("GenerateGrowWithUsPlan")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/grow-with-us-plan")]
        HttpRequestData request,
        Guid personId,
        FunctionContext executionContext,
        CancellationToken cancellationToken)
    {
        var logger = executionContext.GetLogger<GenerateGrowWithUsPlanFunction>();

        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "GrowWithUsAgentNotConfigured",
                "The Grow With Us plan agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        Request? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<Request>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Prompt))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "PromptRequired", "A 'prompt' is required.", cancellationToken);
        }

        var person = await _dbContext.People
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

        if (person is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonNotFound", "This person was not found.", cancellationToken);
        }

        var tenant = person.TenantId.HasValue
            ? await _dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == person.TenantId.Value, cancellationToken)
            : null;

        var profile = await _dbContext.Set<HumanOS.Models.People.PersonProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        var personName = profile?.DisplayName
            ?? (profile is not null ? string.Join(' ', new[] { profile.FirstName, profile.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) : null);
        personName = string.IsNullOrWhiteSpace(personName) ? (person.Email ?? "Learner") : personName;

        var jobRole = person.JobRoleId.HasValue
            ? await _jobRoleService.GetResponseByIdAsync(person.JobRoleId.Value, cancellationToken)
            : null;

        var resume = await _personResumeService.GetByPersonIdAsync(personId, cancellationToken);

        var enrolledCapabilities = await _capabilityEnrollmentService.GetEnrolledCapabilitiesAsync(personId, cancellationToken);
        var completedCourses = enrolledCapabilities
            .Where(c => c.Status == "Completed")
            .Select(c => c.Name)
            .ToList();

        var languageCode = string.IsNullOrWhiteSpace(body.LanguageCode) ? "es" : body.LanguageCode!;

        var jobDescriptionText = jobRole is null
            ? null
            : string.Join("\n\n", new[]
            {
                jobRole.Purpose is { Length: > 0 } purpose ? $"Purpose: {purpose}" : null,
                jobRole.Summary is { Length: > 0 } summary ? $"Summary: {summary}" : null,
                jobRole.Responsibilities.Count > 0 ? "Responsibilities:\n" + string.Join('\n', jobRole.Responsibilities.Select(r => "- " + r)) : null,
                jobRole.RequiredExperience is { Length: > 0 } exp ? $"Required experience: {exp}" : null,
                jobRole.RequiredEducation is { Length: > 0 } edu ? $"Required education: {edu}" : null,
                jobRole.Tools.Count > 0 ? "Tools: " + string.Join(", ", jobRole.Tools) : null,
                jobRole.DailyActivities.Count > 0 ? "Daily activities:\n" + string.Join('\n', jobRole.DailyActivities.Select(a => "- " + a)) : null,
                jobRole.ExpectedOutcomes.Count > 0 ? "Expected outcomes:\n" + string.Join('\n', jobRole.ExpectedOutcomes.Select(o => "- " + o)) : null,
            }.Where(s => s is not null));

        var roleSkills = jobRole is null
            ? []
            : jobRole.TechnicalSkills.Concat(jobRole.SoftSkills).ToList();

        string? webFindings = null;
        if (_webGrounding.IsConfigured)
        {
            try
            {
                var topic = body.Prompt;
                var context = string.Join(" — ", new[] { jobRole?.Title, string.Join(", ", roleSkills.Take(8)) }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var groundingResult = await _webGrounding.SearchAsync(topic, context, cancellationToken);
                webFindings = groundingResult.Text;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "GenerateGrowWithUsPlan: web grounding search failed, continuing without internet findings.");
            }
        }

        var planContext = new GrowWithUsPlanRequestContext
        {
            PersonName = personName,
            JobTitle = jobRole?.Title,
            CompanyName = tenant?.Name,
            CompanyDescription = tenant?.Description,
            CompanyType = tenant?.OrgType,
            JobDescriptionText = jobDescriptionText,
            RoleSkills = roleSkills,
            ResumeSkills = resume.Skills.ToList(),
            ResumeText = resume.RawResumeText,
            CompletedCourses = completedCourses,
            UserPrompt = body.Prompt,
            WebFindings = webFindings,
            LanguageCode = languageCode,
        };

        GrowWithUsPlanProposal proposal;
        try
        {
            proposal = await _agent.GenerateAsync(planContext, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.UnprocessableEntity, "PlanGenerationFailed",
                "The Grow With Us plan agent could not produce a plan: " + ex.Message, cancellationToken);
        }

        try
        {
            var planJson = JsonSerializer.Serialize(proposal);
            var topic = string.IsNullOrWhiteSpace(proposal.Title) ? body.Prompt : proposal.Title;
            await _savedSearchService.SaveAsync(personId, topic, planJson, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort — saving the search for the panel/history must
            // never block returning the freshly generated plan itself.
            logger.LogWarning(ex, "GenerateGrowWithUsPlan: failed to save the search for personId {PersonId}.", personId);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, proposal, cancellationToken: cancellationToken);
    }
}
