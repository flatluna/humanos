using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// TutorAgent V2 (see /memories/repo/agent-framework-native-architecture-mandate.md
/// and TutorService.cs) — the ONLY entry point for Recall turns. Scores one
/// Recall attempt the student already made, persists it as LearningEvidence
/// (TutorPromptShown becomes that row's TutorPrompt, the freshly-computed
/// score becomes its TutorScore), and — per RecallLoopGate's deterministic
/// rule — advances the step to Production if the student either mastered
/// it or exhausted their attempts. The frontend never has to orchestrate
/// that sequencing itself; this single call does it all.
/// </summary>
public sealed class TutorSubmitRecallAttemptFunction
{
    private readonly TutorService _tutorService;
    private readonly HumanOsDbContext _dbContext;
    private readonly ILogger<TutorSubmitRecallAttemptFunction> _logger;

    public TutorSubmitRecallAttemptFunction(TutorService tutorService, HumanOsDbContext dbContext, ILogger<TutorSubmitRecallAttemptFunction> logger)
    {
        _tutorService = tutorService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [Function("TutorSubmitRecallAttempt")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/tutor/recall-attempts")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        SubmitRecallAttemptRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitRecallAttemptRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.LearningSessionStepId == Guid.Empty || string.IsNullOrWhiteSpace(body.StudentResponse))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "LearningSessionStepId and StudentResponse are both required.", cancellationToken);
        }

        try
        {
            var outcome = await _tutorService.SubmitRecallAttemptAsync(
                _dbContext, body.LearningSessionStepId, body.StudentResponse, body.TutorPromptShown, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, TutorApiMappers.ToDto(outcome), cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "TutorSubmitRecallAttempt: InvalidOperationException for LearningSessionStepId={LearningSessionStepId}: {Message}", body.LearningSessionStepId, ex.Message);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRecallStep", ex.Message, cancellationToken);
        }
    }
}
