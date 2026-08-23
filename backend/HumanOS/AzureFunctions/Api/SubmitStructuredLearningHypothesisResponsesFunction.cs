using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class SubmitStructuredLearningHypothesisResponsesRequest
{
    public List<HypothesisAnswerDto> Answers { get; set; } = [];
}

public sealed class HypothesisAnswerDto
{
    public int QuestionIndex { get; set; }
    public int SelectedOptionIndex { get; set; }
}

public sealed class HypothesisResponseDto
{
    public int QuestionIndex { get; set; }
    public int SelectedOptionIndex { get; set; }
    public bool? IsCorrect { get; set; }
}

/// <summary>
/// Real per-student persistence for the Hypothesis pre-check questions
/// ("Antes de empezar" / "Before we start") of a Structured Learning
/// section (2026-08-13) — the ONLY entry point for saving these answers.
/// Replaces the OLD design where the frontend only wrote to localStorage.
/// See <see cref="StructuredLearningHypothesisService"/>.
/// </summary>
public sealed class SubmitStructuredLearningHypothesisResponsesFunction
{
    private readonly StructuredLearningHypothesisService _service;
    private readonly HumanOsDbContext _dbContext;

    public SubmitStructuredLearningHypothesisResponsesFunction(StructuredLearningHypothesisService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("SubmitStructuredLearningHypothesisResponses")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/hypothesis/responses")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        SubmitStructuredLearningHypothesisResponsesRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitStructuredLearningHypothesisResponsesRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.Answers.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "'answers' is required and must contain at least one item.", cancellationToken);
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        Guid? personId = Guid.TryParse(query["personId"], out var parsedPersonId) ? parsedPersonId : null;

        try
        {
            var results = await _service.SubmitAsync(
                _dbContext,
                sectionId,
                personId,
                body.Answers.Select(a => new StructuredLearningHypothesisService.HypothesisAnswerInput
                {
                    QuestionIndex = a.QuestionIndex,
                    SelectedOptionIndex = a.SelectedOptionIndex
                }).ToList(),
                cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                results.Select(r => new HypothesisResponseDto
                {
                    QuestionIndex = r.QuestionIndex,
                    SelectedOptionIndex = r.SelectedOptionIndex,
                    IsCorrect = r.IsCorrect
                }).ToList(),
                cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidHypothesisResponses", ex.Message, cancellationToken);
        }
    }
}
