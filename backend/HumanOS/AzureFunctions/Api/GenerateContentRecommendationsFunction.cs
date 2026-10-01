using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /people/{personId}/content-recommendations — "Recomienda contenido relevante"
/// (2026-09-29). Gathers the student's career/objective, declared interests + learning
/// preferences ("Intereses"), and completed courses, optionally searches the live internet
/// for the latest advances/trends in their field via
/// <see cref="PerplexitySearchService"/> (skipped silently if not configured), then asks
/// <see cref="ContentRecommendationAgent"/> to produce the report. Optionally emails the
/// student a saveable copy in the same call (skill: send email).
/// </summary>
public sealed class GenerateContentRecommendationsFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HumanOsDbContext _dbContext;
    private readonly PersonResumeService _personResumeService;
    private readonly PersonAcademicPreferencesService _academicPreferencesService;
    private readonly CapabilityEnrollmentService _capabilityEnrollmentService;
    private readonly PerplexitySearchService _perplexityService;
    private readonly ContentRecommendationAgent _agent;
    private readonly ContentRecommendationService _savedSearchService;
    private readonly TecMileniumNotificationEmailService _emailService;

    public GenerateContentRecommendationsFunction(
        HumanOsDbContext dbContext,
        PersonResumeService personResumeService,
        PersonAcademicPreferencesService academicPreferencesService,
        CapabilityEnrollmentService capabilityEnrollmentService,
        PerplexitySearchService perplexityService,
        ContentRecommendationAgent agent,
        ContentRecommendationService savedSearchService,
        TecMileniumNotificationEmailService emailService)
    {
        _dbContext = dbContext;
        _personResumeService = personResumeService;
        _academicPreferencesService = academicPreferencesService;
        _capabilityEnrollmentService = capabilityEnrollmentService;
        _perplexityService = perplexityService;
        _agent = agent;
        _savedSearchService = savedSearchService;
        _emailService = emailService;
    }

    public sealed class Request
    {
        public string? Prompt { get; set; }

        public string? LanguageCode { get; set; }

        /// <summary>When true, also emails the student a saveable copy of the report.</summary>
        public bool SendEmail { get; set; }
    }

    [Function("GenerateContentRecommendations")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/content-recommendations")]
        HttpRequestData request,
        Guid personId,
        FunctionContext executionContext,
        CancellationToken cancellationToken)
    {
        var logger = executionContext.GetLogger<GenerateContentRecommendationsFunction>();

        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "ContentRecommendationAgentNotConfigured",
                "The content recommendation agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        Request body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<Request>(request.Body, JsonOptions, cancellationToken) ?? new Request();
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        var person = await _dbContext.People
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

        if (person is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonNotFound", "This person was not found.", cancellationToken);
        }

        var profile = await _dbContext.Set<HumanOS.Models.People.PersonProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        var personName = profile?.DisplayName
            ?? (profile is not null ? string.Join(' ', new[] { profile.FirstName, profile.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) : null);
        personName = string.IsNullOrWhiteSpace(personName) ? (person.Email ?? "Learner") : personName;

        var resume = await _personResumeService.GetByPersonIdAsync(personId, cancellationToken);
        var academicPreferences = await _academicPreferencesService.GetAsync(personId, cancellationToken);

        List<string> ReadJsonArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        var academicInterests = ReadJsonArray(academicPreferences?.AcademicInterestsJson);
        var learningPreferences = ReadJsonArray(academicPreferences?.LearningPreferencesJson);

        var enrolledCapabilities = await _capabilityEnrollmentService.GetEnrolledCapabilitiesAsync(personId, cancellationToken);
        var completedCourses = enrolledCapabilities
            .Where(c => c.Status == "Completed")
            .Select(c => c.Name)
            .ToList();

        var languageCode = string.IsNullOrWhiteSpace(body.LanguageCode) ? "es" : body.LanguageCode!;

        string? webFindings = null;
        if (_perplexityService.IsConfigured)
        {
            try
            {
                var topicHint = string.IsNullOrWhiteSpace(body.Prompt)
                    ? string.Join(", ", academicInterests.Take(5))
                    : body.Prompt;
                var query =
                    $"What are the latest advances, trends, tools, and papers (2025-2026) relevant to a student " +
                    $"interested in: {topicHint}? Include real, current URLs when possible.";
                var result = await _perplexityService.SearchAsync(query, cancellationToken);
                webFindings = result.Citations.Count > 0
                    ? result.Answer + "\n\nCitations:\n" + string.Join('\n', result.Citations)
                    : result.Answer;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "GenerateContentRecommendations: Perplexity search failed, continuing without internet findings.");
            }
        }

        var context = new ContentRecommendationRequestContext
        {
            PersonName = personName,
            CareerOrProgram = resume.StudentAcademicProfile?.Degree,
            ProfessionalObjective = resume.ProfessionalObjective,
            AcademicInterests = academicInterests,
            LearningPreferences = learningPreferences,
            CompletedCourses = completedCourses,
            PerformanceSummary = resume.StudentAcademicProfile?.CumulativeAverage is { } average
                ? $"Cumulative average: {average}/{resume.StudentAcademicProfile?.AverageScale ?? "10"}"
                : null,
            UserPrompt = body.Prompt,
            WebFindings = webFindings,
            LanguageCode = languageCode,
        };

        ContentRecommendationProposal proposal;
        try
        {
            proposal = await _agent.GenerateAsync(context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.UnprocessableEntity, "RecommendationGenerationFailed",
                "The content recommendation agent could not produce a report: " + ex.Message, cancellationToken);
        }

        Guid? savedSearchId = null;
        try
        {
            var resultJson = JsonSerializer.Serialize(proposal);
            var topic = string.IsNullOrWhiteSpace(proposal.Title) ? (body.Prompt ?? "Recomendaciones") : proposal.Title;
            var saved = await _savedSearchService.SaveAsync(personId, topic, resultJson, cancellationToken);
            savedSearchId = saved.ContentRecommendationSavedSearchId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "GenerateContentRecommendations: failed to save the search for personId {PersonId}.", personId);
        }

        if (body.SendEmail && !string.IsNullOrWhiteSpace(person.Email))
        {
            try
            {
                await _emailService.SendContentRecommendationsAsync(
                    person.Email,
                    personName,
                    proposal.Title,
                    proposal.Summary,
                    proposal.TrendsSummary,
                    proposal.RecommendedItems
                        .Select(i => new TecMileniumNotificationEmailService.RecommendedItem
                        {
                            Title = i.Title,
                            Type = i.Type,
                            Reason = i.Reason,
                            Url = i.Url,
                        })
                        .ToList(),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "GenerateContentRecommendations: failed to email the recommendations for personId {PersonId}.", personId);
            }
        }

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new { proposal.Title, proposal.Summary, proposal.RecommendedItems, proposal.TrendsSummary, savedSearchId },
            cancellationToken: cancellationToken);
    }
}
