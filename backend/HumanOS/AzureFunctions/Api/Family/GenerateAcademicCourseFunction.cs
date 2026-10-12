using System.Net;
using System.Text.Json;
using HumanOS.Agents.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// POST /family/academic/generate-course
/// Runs the ProfesorExpertAgent (Microsoft.Agents.AI + gpt-6.1-sol)
/// to generate an academic course adhering to the 9-step Engrams.ai architecture.
/// </summary>
public sealed class GenerateAcademicCourseFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ProfesorExpertAgent _agent;

    public GenerateAcademicCourseFunction(ProfesorExpertAgent agent)
    {
        _agent = agent;
    }

    [Function("GenerateAcademicCourse")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/academic/generate-course")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        AcademicCourseGenerationRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<AcademicCourseGenerationRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", $"Malformed JSON payload: {ex.Message}", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingPayload", "A valid course request payload is required.", cancellationToken);
        }

        try
        {
            var result = await _agent.GenerateAcademicCourseAsync(body, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, HttpStatusCode.OK, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "GenerationFailed", ex.Message, cancellationToken);
        }
    }
}
