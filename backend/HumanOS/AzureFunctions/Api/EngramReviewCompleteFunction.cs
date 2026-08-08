using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Learning;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Logs one completed full "Reactivar Engrama" voice-review session (product
/// decision 2026-08-07). Called by the frontend ONLY when the Agent itself
/// invokes the <c>complete_review</c> Realtime tool during an
/// EngramReviewSession conversation (see EngramReviewSessionFunction),
/// signaling it walked the student through the ENTIRE lesson from beginning
/// to end with genuine engagement — never called for a partial/interrupted
/// session. Simply inserts a row; the Brain page's forgetting-curve
/// recency/stability signals (GraphProgressionEngine) read it back
/// alongside graded LearningSessionNode completions.
/// </summary>
public sealed class EngramReviewCompleteFunction
{
    private readonly HumanOsDbContext _dbContext;

    public EngramReviewCompleteFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("EngramReviewComplete")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/tutor/engram-review-complete")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        EngramReviewCompleteRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<EngramReviewCompleteRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.PersonId == Guid.Empty || body.CapabilityGraphNodeId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "PersonId and CapabilityGraphNodeId are both required.", cancellationToken);
        }

        _dbContext.EngramReviewCompletions.Add(new EngramReviewCompletion
        {
            PersonId = body.PersonId,
            CapabilityGraphNodeId = body.CapabilityGraphNodeId
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { Success = true }, cancellationToken: cancellationToken);
    }
}
