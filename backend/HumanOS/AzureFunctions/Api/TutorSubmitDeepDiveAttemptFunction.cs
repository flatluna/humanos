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
/// and TutorService.cs) — the ONLY entry point for Deep Dive turns. Scores
/// one attempt on the CURRENT example card, persists it as LearningEvidence,
/// and — per DeepDiveLoopGate's deterministic rule — either keeps the loop
/// going on the same or next card, or advances the step to Recall once
/// EVERY card has been mastered in order. The frontend never has to
/// orchestrate that sequencing itself; this single call does it all.
/// </summary>
public sealed class TutorSubmitDeepDiveAttemptFunction
{
    private readonly TutorService _tutorService;
    private readonly HumanOsDbContext _dbContext;
    private readonly ILogger<TutorSubmitDeepDiveAttemptFunction> _logger;

    public TutorSubmitDeepDiveAttemptFunction(TutorService tutorService, HumanOsDbContext dbContext, ILogger<TutorSubmitDeepDiveAttemptFunction> logger)
    {
        _tutorService = tutorService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [Function("TutorSubmitDeepDiveAttempt")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/tutor/deep-dive-attempts")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        SubmitDeepDiveAttemptRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitDeepDiveAttemptRequest>(
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
            var outcome = await _tutorService.SubmitDeepDiveAttemptAsync(
                _dbContext, body.LearningSessionStepId, body.StudentResponse, body.TutorPromptShown, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, TutorApiMappers.ToDto(outcome), cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "TutorSubmitDeepDiveAttempt: InvalidOperationException for LearningSessionStepId={LearningSessionStepId}: {Message}", body.LearningSessionStepId, ex.Message);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidDeepDiveStep", ex.Message, cancellationToken);
        }
    }
}
