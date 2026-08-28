using System.Net;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class SetStructuredLearningVideoWizardStateRequest
{
    /// <summary>JSON snapshot of the video wizard's inputs and generated
    /// prompt sequence. Pass null/empty to clear it.</summary>
    public string? VideoWizardStateJson { get; set; }
}

/// <summary>
/// Saves a Teaching step's video wizard inputs (Objective/Instructions/
/// Environment/Starting image description) and generated prompt sequence
/// (2026-08-26) — see
/// <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningSectionStep.VideoWizardStateJson"/>.
/// Direct DB write, same shape as <see cref="SetStructuredLearningStepTimelineFunction"/>.
/// </summary>
public sealed class SetStructuredLearningVideoWizardStateFunction
{
    private readonly HumanOsDbContext _dbContext;

    public SetStructuredLearningVideoWizardStateFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("SetStructuredLearningVideoWizardState")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "studio/structured-learning/steps/{stepId:guid}/video-wizard-state")]
        HttpRequestData request,
        Guid stepId,
        CancellationToken cancellationToken)
    {
        SetStructuredLearningVideoWizardStateRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SetStructuredLearningVideoWizardStateRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        var step = await _dbContext.StructuredLearningSectionSteps
            .FirstOrDefaultAsync(s => s.StructuredLearningSectionStepId == stepId, cancellationToken);

        if (step is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StepNotFound", $"No step found with id {stepId}.", cancellationToken);
        }

        step.VideoWizardStateJson = string.IsNullOrWhiteSpace(body?.VideoWizardStateJson) ? null : body.VideoWizardStateJson;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new { step.StructuredLearningSectionStepId, step.VideoWizardStateJson }, cancellationToken: cancellationToken);
    }
}
