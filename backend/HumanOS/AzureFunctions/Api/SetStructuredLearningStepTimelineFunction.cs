using System.Net;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class SetStructuredLearningStepTimelineRequest
{
    /// <summary>JSON array of ordered timeline items, e.g.
    /// [{"Type":"Text","ParagraphIndex":0},{"Type":"Video","IllustrationId":"..."}].
    /// Pass null/empty to clear the custom order (revert to all-at-once view).</summary>
    public string? TimelineJson { get; set; }
}

/// <summary>
/// Saves a Teaching step's author-defined narration/video presentation
/// order (2026-08-22) — see <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningSectionStep.TimelineJson"/>.
/// Deliberately a direct DB write (no AI/orchestrator involved), same
/// "just save what the reviewer arranged" shape as
/// <see cref="SetStructuredLearningStepContentFunction"/>.
/// </summary>
public sealed class SetStructuredLearningStepTimelineFunction
{
    private readonly HumanOsDbContext _dbContext;

    public SetStructuredLearningStepTimelineFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("SetStructuredLearningStepTimeline")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "studio/structured-learning/steps/{stepId:guid}/timeline")]
        HttpRequestData request,
        Guid stepId,
        CancellationToken cancellationToken)
    {
        SetStructuredLearningStepTimelineRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SetStructuredLearningStepTimelineRequest>(
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

        step.TimelineJson = string.IsNullOrWhiteSpace(body?.TimelineJson) ? null : body.TimelineJson;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { step.StructuredLearningSectionStepId, step.TimelineJson }, cancellationToken: cancellationToken);
    }
}
