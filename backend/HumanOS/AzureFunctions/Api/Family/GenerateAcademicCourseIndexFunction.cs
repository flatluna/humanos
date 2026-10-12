using System.Net;
using System.Text.Json;
using HumanOS.Agents.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// POST /family/academic/generate-course-index
/// "Step 1" (2026-10-08) of the two-step course generation flow — runs
/// ProfesorExpertAgent.GenerateCourseIndexAsync, returning ONLY the course's
/// title/intro/chapter+lesson titles (fast, lightweight), for the educator
/// to review/approve before the much slower per-chapter content calls (see
/// <see cref="GenerateAcademicCourseChapterFunction"/>). Replaces the
/// single, slow, failure-prone "whole 12+-chapter course in ONE call"
/// endpoint (<see cref="GenerateAcademicCourseFunction"/>, kept only for
/// backward compatibility) that was timing out on large courses regardless
/// of which model served it — the bottleneck was the sheer size of a
/// single response, not any one model being slow.
/// </summary>
public sealed class GenerateAcademicCourseIndexFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ProfesorExpertAgent _agent;

    public GenerateAcademicCourseIndexFunction(ProfesorExpertAgent agent)
    {
        _agent = agent;
    }

    [Function("GenerateAcademicCourseIndex")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/academic/generate-course-index")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        AcademicCourseIndexRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<AcademicCourseIndexRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", $"Malformed JSON payload: {ex.Message}", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingPayload", "A valid course index request payload is required.", cancellationToken);
        }

        try
        {
            var result = await _agent.GenerateCourseIndexAsync(body, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, HttpStatusCode.OK, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "IndexGenerationFailed", ex.Message, cancellationToken);
        }
    }
}
