using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class EditStructuredLearningStepRequest
{
    public string Instruction { get; set; } = string.Empty;
}

/// <summary>
/// Structured Learning's "Edit mode" review feature (2026-08-10) — applies
/// a reviewer's free-text instruction to ONE step's Content via
/// StructuredLearningStepEditorAgent. Mirrors EditNodeBlueprintStepFunction
/// (Graph's equivalent). Only valid for Hypothesis/Teaching/Production —
/// see StructuredLearningReviewService.EditStepContentAsync's guard.
/// </summary>
public sealed class EditStructuredLearningStepFunction
{
    private readonly StructuredLearningReviewService _service;
    private readonly HumanOS.Agents.Studio.StructuredLearningStepEditorAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public EditStructuredLearningStepFunction(
        StructuredLearningReviewService service, HumanOS.Agents.Studio.StructuredLearningStepEditorAgent agent, HumanOsDbContext dbContext)
    {
        _service = service;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("EditStructuredLearningStep")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/steps/{stepType}/edit")]
        HttpRequestData request,
        Guid sectionId,
        string stepType,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StructuredLearningStepEditorAgentNotConfigured",
                "StructuredLearningStepEditorAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        if (!Enum.TryParse<StructuredLearningStepType>(stepType, ignoreCase: true, out var parsedStepType))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidStepType",
                "Route parameter stepType must be one of: Hypothesis, Teaching, DeepDive, Recall, Production, Assessment.", cancellationToken);
        }

        EditStructuredLearningStepRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<EditStructuredLearningStepRequest>(
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
            var result = await _service.EditStepContentAsync(_dbContext, sectionId, parsedStepType, body.Instruction, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new StructuredLearningStepDetailDto
            {
                StructuredLearningSectionStepId = result.StructuredLearningSectionStepId,
                StepType = result.StepType.ToString(),
                Content = result.Content,
                StepDataJson = result.StepDataJson,
                IsHumanApproved = result.IsHumanApproved,
                HumanApprovedAtUtc = result.HumanApprovedAtUtc
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidStepEdit", ex.Message, cancellationToken);
        }
    }
}
