using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// HTTP endpoint for generating a personalized, spoken professor lecture for an Engram (section)
/// using the Microsoft Agent Framework.
/// </summary>
public sealed class StructuredLearningProfessorLectureFunction
{
    private readonly StructuredLearningProfessorLectureService _lectureService;
    private readonly StructuredLearningProfessorLectureAgent _agent;
    private readonly PersonService _personService;
    private readonly ILogger<StructuredLearningProfessorLectureFunction> _logger;

    public StructuredLearningProfessorLectureFunction(
        StructuredLearningProfessorLectureService lectureService,
        StructuredLearningProfessorLectureAgent agent,
        PersonService personService,
        ILogger<StructuredLearningProfessorLectureFunction> logger)
    {
        _lectureService = lectureService;
        _agent = agent;
        _personService = personService;
        _logger = logger;
    }

    [Function("GenerateStructuredLearningProfessorLecture")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/professor-lecture")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "ProfessorLectureAgentNotConfigured",
                "The Structured Learning Professor Lecture Agent is not configured. Verify Azure OpenAI credentials.",
                cancellationToken);
        }

        GenerateProfessorLectureApiRequest? body = null;
        if (request.Body.Length > 0)
        {
            try
            {
                body = await JsonSerializer.DeserializeAsync<GenerateProfessorLectureApiRequest>(
                    request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse professor lecture request JSON");
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "InvalidJson",
                    "The request body contains invalid JSON.",
                    cancellationToken);
            }
        }

        var effectivePersonId = body?.PersonId ?? await ResolveAuthenticatedEmployeeIdAsync(request, cancellationToken);
        var targetLanguage = body?.TargetLanguage;

        try
        {
            var lecture = await _lectureService.GenerateLectureForSectionAsync(
                sectionId,
                effectivePersonId,
                targetLanguage,
                cancellationToken);

            var response = new GenerateProfessorLectureApiResponse
            {
                SectionId = sectionId,
                Lecture = lecture
            };

            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                response,
                cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "SectionNotFound",
                ex.Message,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate professor lecture for section {SectionId}", sectionId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "ProfessorLectureGenerationFailed",
                ex.Message,
                cancellationToken);
        }
    }

    private async Task<Guid?> ResolveAuthenticatedEmployeeIdAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.TryGetValues("X-Azure-OID", out var oidValues) && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
            request.Headers.TryGetValues("X-Azure-TID", out var tidValues) && !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            var person = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
            if (person is not null)
            {
                return person.PersonId;
            }
        }

        if (request.Headers.TryGetValues("X-Employee-Id", out var empValues) &&
            Guid.TryParse(empValues.FirstOrDefault(), out var headerEmpId))
        {
            return headerEmpId;
        }

        return null;
    }
}
