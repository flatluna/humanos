using System.Net;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class SetStructuredLearningVideoPublishedRequest
{
    public bool IsPublished { get; set; }
}

/// <summary>Toggles whether one video is visible to students in real-mode
/// (2026-08-26) — edit mode still always shows it; only the learner-facing
/// runtime (see GetStructuredLearningSectionVideosFunction's personId
/// check) filters by this flag. Lets a reviewer keep an unused clip
/// around without deleting it.</summary>
public sealed class SetStructuredLearningVideoPublishedFunction
{
    private readonly HumanOsDbContext _dbContext;

    public SetStructuredLearningVideoPublishedFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("SetStructuredLearningVideoPublished")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "studio/structured-learning/videos/{illustrationId:guid}/published")]
        HttpRequestData request,
        Guid illustrationId,
        CancellationToken cancellationToken)
    {
        SetStructuredLearningVideoPublishedRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SetStructuredLearningVideoPublishedRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingBody", "isPublished is required.", cancellationToken);
        }

        var illustration = await _dbContext.StructuredLearningIllustrations
            .FirstOrDefaultAsync(i => i.StructuredLearningIllustrationId == illustrationId && i.MediaType == "Video", cancellationToken);

        if (illustration is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "VideoNotFound", "Video not found.", cancellationToken);
        }

        illustration.IsPublished = body.IsPublished;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { illustration.StructuredLearningIllustrationId, illustration.IsPublished }, cancellationToken: cancellationToken);
    }
}
