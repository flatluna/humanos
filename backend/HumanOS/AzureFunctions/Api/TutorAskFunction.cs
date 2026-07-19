using System.Net;
using System.Text.Json;
using HumanOS.Agentic.Runtime;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// TutorAgent V2 (see /memories/repo/agent-framework-native-architecture-mandate.md
/// and TutorService.cs) — on-demand Tutor turn for the Teaching, Production,
/// and AssessmentFeedback modes. Persists this exchange itself as a
/// LearningEvidence row (student's question + Tutor's reply, ungraded) —
/// see TutorService.AskAsync.
///
/// Recall is NOT handled here — <see cref="TutorSubmitRecallAttemptFunction"/>
/// is the only entry point for Recall turns, since a Recall turn always
/// scores an attempt the student already made and must persist + apply the
/// attempt-cap/mastery gate in the same call.
/// </summary>
public sealed class TutorAskFunction
{
    private readonly TutorService _tutorService;
    private readonly HumanOsDbContext _dbContext;

    public TutorAskFunction(TutorService tutorService, HumanOsDbContext dbContext)
    {
        _tutorService = tutorService;
        _dbContext = dbContext;
    }

    [Function("TutorAsk")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/tutor/ask")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        TutorAskRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<TutorAskRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.LearningSessionStepId == Guid.Empty || string.IsNullOrWhiteSpace(body.StudentMessage))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "LearningSessionStepId and StudentMessage are both required.", cancellationToken);
        }

        if (!Enum.TryParse<TutorInteractionMode>(body.Mode, ignoreCase: true, out var mode))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidMode",
                "Mode must be one of: Teaching, Production, AssessmentFeedback.", cancellationToken);
        }

        if (mode == TutorInteractionMode.Recall)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "UseRecallEndpoint",
                "Recall attempts must be submitted via instructor-runtime/tutor/recall-attempts, not this endpoint.", cancellationToken);
        }

        if (mode == TutorInteractionMode.AssessmentFeedback && string.IsNullOrWhiteSpace(body.RawAssessmentFeedback))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "RawAssessmentFeedback is required when Mode is AssessmentFeedback.", cancellationToken);
        }

        try
        {
            var result = await _tutorService.AskAsync(
                _dbContext, body.LearningSessionStepId, mode, body.StudentMessage, body.RawAssessmentFeedback, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, TutorApiMappers.ToDto(result), cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StepNotFound", ex.Message, cancellationToken);
        }
    }
}
