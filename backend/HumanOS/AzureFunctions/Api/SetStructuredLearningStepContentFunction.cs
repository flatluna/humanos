using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class SetStructuredLearningStepContentRequest
{
    public string Content { get; set; } = string.Empty;

    /// <summary>Must be true — the reviewer's explicit confirmation that
    /// they read this content and take responsibility for it (the AI is
    /// not perfect and can alter or invent content). Enforced server-side,
    /// not just as a disabled button in the UI.</summary>
    public bool Confirmed { get; set; }
}

/// <summary>
/// Structured Learning's "Edit mode" manual review tool (2026-08-11) — lets
/// a reviewer directly overwrite ONE step's Content with their own
/// hand-edited text, no AI involved at all. Deliberately separate from
/// <see cref="EditStructuredLearningStepFunction"/> (which rewrites the
/// step via an AI instruction): this is the "just let me delete the
/// sentence I don't like" escape hatch, requiring an explicit sign-off
/// since the AI is not perfect and can alter or invent content — see
/// <see cref="StructuredLearningReviewService.SetStepContentManuallyAsync"/>.
/// </summary>
public sealed class SetStructuredLearningStepContentFunction
{
    private readonly StructuredLearningReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public SetStructuredLearningStepContentFunction(StructuredLearningReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("SetStructuredLearningStepContent")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/steps/{stepType}/content")]
        HttpRequestData request,
        Guid sectionId,
        string stepType,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<StructuredLearningStepType>(stepType, ignoreCase: true, out var parsedStepType))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidStepType",
                "Route parameter stepType must be one of: Hypothesis, Teaching, DeepDive, Recall, Production, Assessment.", cancellationToken);
        }

        SetStructuredLearningStepContentRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SetStructuredLearningStepContentRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Content))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Content is required.", cancellationToken);
        }

        if (!body.Confirmed)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "ConfirmationRequired",
                "You must confirm you have read this content and take responsibility for it before saving.", cancellationToken);
        }

        try
        {
            var result = await _service.SetStepContentManuallyAsync(_dbContext, sectionId, parsedStepType, body.Content, cancellationToken);
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
