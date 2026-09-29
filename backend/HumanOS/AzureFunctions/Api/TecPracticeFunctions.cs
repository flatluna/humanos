using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Practica y refuerza" endpoints (Tec Contigo, 2026-09-28). See TecPracticeService.
/// </summary>
public sealed class TecPracticeFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly TecPracticeService _service;
    private readonly CapabilityEnrollmentService _enrollmentService;
    private readonly StructuredLearningDocumentEnrollmentService _structuredLearningEnrollmentService;

    public TecPracticeFunctions(
        TecPracticeService service,
        CapabilityEnrollmentService enrollmentService,
        StructuredLearningDocumentEnrollmentService structuredLearningEnrollmentService)
    {
        _service = service;
        _enrollmentService = enrollmentService;
        _structuredLearningEnrollmentService = structuredLearningEnrollmentService;
    }

    /// <summary>GET .../practice — returns the cached session, or an empty result
    /// (no exercises) when nothing has been generated for this course yet.</summary>
    [Function("GetTecPracticeSession")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/capabilities/{capabilityId:guid}/practice")]
        HttpRequestData request,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var existing = await _service.GetExistingAsync(personId, capabilityId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, existing, cancellationToken: cancellationToken);
    }

    /// <summary>POST .../practice/generate — generates a brand-new set of exercises
    /// (overwrites any previous one for this course), one click, on demand.</summary>
    [Function("GenerateTecPracticeSession")]
    public async Task<HttpResponseData> GenerateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/capabilities/{capabilityId:guid}/practice/generate")]
        HttpRequestData request,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        if (!_service.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "TecPracticeAgentNotConfigured",
                "The practice agent is not yet configured (missing Azure OpenAI settings).", cancellationToken);
        }

        var enrolled = await _enrollmentService.GetEnrolledCapabilitiesAsync(personId, cancellationToken);
        var course = enrolled.FirstOrDefault(c => c.CapabilityId == capabilityId);
        if (course is not null)
        {
            var capabilityMasteredTopics = new List<string> { $"{course.MasteredNodes}/{course.TotalNodes} conceptos dominados" };
            var capabilityResult = await _service.GenerateAsync(
                personId, capabilityId, course.Name, course.Description, capabilityMasteredTopics, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, capabilityResult, cancellationToken: cancellationToken);
        }

        // Not a standalone Capability — check real completed Structured Learning documents
        // (e.g. Sofia's "Inteligencia Emocional y Mindfulness", 6/6 engrams) as a second real
        // data source, since a completed course can live in either model.
        var enrolledDocuments = await _structuredLearningEnrollmentService.GetEnrolledDocumentsAsync(personId, cancellationToken);
        var document = enrolledDocuments.FirstOrDefault(d => d.StructuredLearningDocumentId == capabilityId);
        if (document is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "CapabilityNotFound", "This course was not found for this person.", cancellationToken);
        }

        var documentMasteredTopics = new List<string> { $"{document.CompletedSectionCount}/{document.SectionCount} engramas completados" };
        var documentResult = await _service.GenerateAsync(
            personId, capabilityId, document.DocumentName, null, documentMasteredTopics, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, documentResult, cancellationToken: cancellationToken);
    }

    public sealed class SubmitAnswerRequest
    {
        public string StudentAnswer { get; set; } = string.Empty;
    }

    /// <summary>POST .../practice/{practiceSessionId}/exercises/{exerciseIndex}/answer —
    /// grades the answer and awards points.</summary>
    [Function("SubmitTecPracticeAnswer")]
    public async Task<HttpResponseData> SubmitAnswerAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/practice/{practiceSessionId:guid}/exercises/{exerciseIndex:int}/answer")]
        HttpRequestData request,
        Guid personId,
        Guid practiceSessionId,
        int exerciseIndex,
        CancellationToken cancellationToken)
    {
        SubmitAnswerRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitAnswerRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(body?.StudentAnswer))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingAnswer", "studentAnswer is required.", cancellationToken);
        }

        try
        {
            var result = await _service.SubmitAnswerAsync(personId, practiceSessionId, exerciseIndex, body.StudentAnswer, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PracticeSessionOrExerciseNotFound", ex.Message, cancellationToken);
        }
    }
}
