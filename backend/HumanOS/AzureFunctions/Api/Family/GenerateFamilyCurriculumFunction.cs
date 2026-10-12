using System.Net;
using System.Text.Json;
using HumanOS.Agents.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

public sealed class GenerateCurriculumRequest
{
    public string? StageName { get; set; }

    public string? AgeRange { get; set; }

    public string? AdditionalFocus { get; set; }
}

/// <summary>
/// POST /family/stages/{stageKey}/generate-skills
/// Runs the state-of-the-art FamilyCurriculumAgent (Microsoft.Agents.AI + gpt-5-mini)
/// to formulate global reference capabilities for a human development stage.
/// </summary>
public sealed class GenerateFamilyCurriculumFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly FamilyCurriculumAgent _curriculumAgent;

    public GenerateFamilyCurriculumFunction(FamilyCurriculumAgent curriculumAgent)
    {
        _curriculumAgent = curriculumAgent;
    }

    [Function("GenerateFamilyCurriculum")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/stages/{stageKey}/generate-skills")]
        HttpRequestData request,
        string stageKey,
        CancellationToken cancellationToken)
    {
        GenerateCurriculumRequest? body = null;
        try
        {
            if (request.Body.Length > 0)
            {
                body = await JsonSerializer.DeserializeAsync<GenerateCurriculumRequest>(request.Body, JsonOptions, cancellationToken);
            }
        }
        catch (JsonException)
        {
            // Optional body, ignore if empty
        }

        var stageName = body?.StageName ?? stageKey;
        var ageRange = body?.AgeRange ?? "K-12";
        var focus = body?.AdditionalFocus;

        try
        {
            var result = await _curriculumAgent.GenerateCurriculumAsync(stageKey, stageName, ageRange, focus, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, HttpStatusCode.OK, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "GenerationFailed", ex.Message, cancellationToken);
        }
    }
}
