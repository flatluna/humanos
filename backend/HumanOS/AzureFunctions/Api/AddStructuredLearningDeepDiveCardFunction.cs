using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class AddStructuredLearningDeepDiveCardRequest
{
    public string Instruction { get; set; } = string.Empty;
}

/// <summary>Structured Learning's "Edit mode" — appends a brand-new Deep
/// Dive card via StructuredLearningExampleCardEditorAgent. Mirrors
/// AddDeepDiveCardFunction (Graph's equivalent).</summary>
public sealed class AddStructuredLearningDeepDiveCardFunction
{
    private readonly StructuredLearningReviewService _service;
    private readonly HumanOS.Agents.Studio.StructuredLearningExampleCardEditorAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public AddStructuredLearningDeepDiveCardFunction(
        StructuredLearningReviewService service, HumanOS.Agents.Studio.StructuredLearningExampleCardEditorAgent agent, HumanOsDbContext dbContext)
    {
        _service = service;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("AddStructuredLearningDeepDiveCard")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/deepdive/cards/add")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StructuredLearningExampleCardEditorAgentNotConfigured",
                "StructuredLearningExampleCardEditorAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        AddStructuredLearningDeepDiveCardRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<AddStructuredLearningDeepDiveCardRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Instruction))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Instruction is required.", cancellationToken);
        }

        try
        {
            var result = await _service.AddExampleCardAsync(_dbContext, sectionId, body.Instruction, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new StructuredLearningStepDetailDto
            {
                StructuredLearningSectionStepId = result.StructuredLearningSectionStepId,
                StepType = result.StepType.ToString(),
                Content = result.Content,
                StepDataJson = result.StepDataJson
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidCardAdd", ex.Message, cancellationToken);
        }
    }
}
