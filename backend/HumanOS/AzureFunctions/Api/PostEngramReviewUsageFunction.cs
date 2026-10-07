using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Learning;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class EngramReviewUsageRequest
{
    public Guid? PersonId { get; set; }

    public Guid? CapabilityGraphNodeId { get; set; }

    public int DurationSeconds { get; set; }
}

/// <summary>
/// Logs REAL "Reactivar Engrama" voice-session duration (2026-10-04) —
/// called on every session teardown, regardless of whether the student
/// finished the whole lesson, only reviewed one Deep Dive card, or closed
/// early. Deliberately separate from <see cref="EngramReviewCompleteFunction"/>
/// (the "did they genuinely finish" memory signal) — real cost must be
/// tracked for actual consumption, not gated behind full completion. See
/// <see cref="EngramReviewSessionUsage"/>.
/// </summary>
public sealed class PostEngramReviewUsageFunction
{
    private readonly HumanOsDbContext _dbContext;

    public PostEngramReviewUsageFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("PostEngramReviewUsage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/tutor/engram-review-usage")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        EngramReviewUsageRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<EngramReviewUsageRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.DurationSeconds <= 0)
        {
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { Success = true }, cancellationToken: cancellationToken);
        }

        _dbContext.EngramReviewSessionUsages.Add(new EngramReviewSessionUsage
        {
            PersonId = body.PersonId,
            CapabilityGraphNodeId = body.CapabilityGraphNodeId,
            DurationSeconds = body.DurationSeconds,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { Success = true }, cancellationToken: cancellationToken);
    }
}
