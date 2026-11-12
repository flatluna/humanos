using System.Net;
using HumanOS.Contracts.Family;
using HumanOS.Services.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// POST /family/capability-recommendations/generate
/// Runs the SubjectCapabilityRecommenderAgent for a Country + EducationSystemType + Grade + Subject
/// combination and saves the results as pending recommendations (GenerationStatus = 'PendingEngramGeneration').
/// This agent does NOT generate Engrams — it only produces the template later handed off to HumanOS Studio.
/// </summary>
public sealed class GenerateSubjectCapabilityRecommendationsFunction
{
    private readonly FamilyCapabilityService _capabilityService;
    private readonly HumanOS.Agents.Family.SubjectCapabilityRecommenderAgent _agent;

    public GenerateSubjectCapabilityRecommendationsFunction(
        FamilyCapabilityService capabilityService,
        HumanOS.Agents.Family.SubjectCapabilityRecommenderAgent agent)
    {
        _capabilityService = capabilityService;
        _agent = agent;
    }

    [Function("GenerateSubjectCapabilityRecommendations")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/capability-recommendations/generate")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        GenerateSubjectCapabilityRecommendationsRequest? body;
        try
        {
            body = await System.Text.Json.JsonSerializer.DeserializeAsync<GenerateSubjectCapabilityRecommendationsRequest>(
                request.Body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", "Request body must be valid JSON.", cancellationToken);
        }

        if (body is null
            || string.IsNullOrWhiteSpace(body.CountryCode)
            || string.IsNullOrWhiteSpace(body.EducationSystemTypeCode)
            || string.IsNullOrWhiteSpace(body.StageKey)
            || string.IsNullOrWhiteSpace(body.SubjectCode))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "CountryCode, EducationSystemTypeCode, StageKey and SubjectCode are all required.", cancellationToken);
        }

        try
        {
            var result = await _capabilityService.GenerateAndSaveRecommendationsAsync(_agent, body, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, HttpStatusCode.OK, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "GenerationFailed", ex.Message, cancellationToken);
        }
    }
}

/// <summary>
/// GET /family/capability-recommendations?countryCode=&educationSystemTypeCode=&stageKey=&subjectCode=
/// Lists saved AI capability recommendations, optionally filtered.
/// </summary>
public sealed class ListSubjectCapabilityRecommendationsFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public ListSubjectCapabilityRecommendationsFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("ListSubjectCapabilityRecommendations")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/capability-recommendations")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var list = await _capabilityService.ListRecommendationsAsync(
            query["countryCode"], query["educationSystemTypeCode"], query["stageKey"], query["subjectCode"], cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, list, HttpStatusCode.OK, cancellationToken);
    }
}

/// <summary>
/// PUT /family/capability-recommendations/{recommendationId}
/// Edits the text fields (name/description/why it matters/micro-skills) of an existing,
/// still-pending recommendation. Returns 404 if it doesn't exist.
/// </summary>
public sealed class UpdateSubjectCapabilityRecommendationFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public UpdateSubjectCapabilityRecommendationFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("UpdateSubjectCapabilityRecommendation")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "family/capability-recommendations/{recommendationId:guid}")]
        HttpRequestData request,
        Guid recommendationId,
        CancellationToken cancellationToken)
    {
        UpdateCapabilityRecommendationRequest? body;
        try
        {
            body = await System.Text.Json.JsonSerializer.DeserializeAsync<UpdateCapabilityRecommendationRequest>(
                request.Body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", "Request body must be valid JSON.", cancellationToken);
        }

        if (body is null
            || string.IsNullOrWhiteSpace(body.NameEs)
            || string.IsNullOrWhiteSpace(body.NameEn)
            || string.IsNullOrWhiteSpace(body.DescriptionEs)
            || string.IsNullOrWhiteSpace(body.DescriptionEn))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "NameEs, NameEn, DescriptionEs and DescriptionEn are all required.", cancellationToken);
        }

        var updated = await _capabilityService.UpdateRecommendationAsync(recommendationId, body, cancellationToken);
        if (updated is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NotFound", $"Recommendation '{recommendationId}' was not found.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, updated, HttpStatusCode.OK, cancellationToken);
    }
}

/// <summary>
/// DELETE /family/capability-recommendations/{recommendationId}
/// Permanently deletes a pending AI capability recommendation. Returns 404 if it doesn't exist.
/// </summary>
public sealed class DeleteSubjectCapabilityRecommendationFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public DeleteSubjectCapabilityRecommendationFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("DeleteSubjectCapabilityRecommendation")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "family/capability-recommendations/{recommendationId:guid}")]
        HttpRequestData request,
        Guid recommendationId,
        CancellationToken cancellationToken)
    {
        var deleted = await _capabilityService.DeleteRecommendationAsync(recommendationId, cancellationToken);
        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NotFound", $"Recommendation '{recommendationId}' was not found.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { recommendationId, deleted = true }, HttpStatusCode.OK, cancellationToken);
    }
}

public sealed class MarkRecommendationSentToStudioRequest
{
    public string? StudioRunId { get; set; }
}

/// <summary>
/// POST /family/capability-recommendations/{recommendationId}/mark-sent-to-studio
/// Records that the admin opened Studio's NewCapabilityPage for this recommendation
/// (and optionally the resulting RunId), moving it from 'PendingEngramGeneration' to 'SentToStudio'.
/// </summary>
public sealed class MarkRecommendationSentToStudioFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public MarkRecommendationSentToStudioFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("MarkRecommendationSentToStudio")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/capability-recommendations/{recommendationId}/mark-sent-to-studio")]
        HttpRequestData request,
        Guid recommendationId,
        CancellationToken cancellationToken)
    {
        MarkRecommendationSentToStudioRequest? body = null;
        try
        {
            if (request.Body.Length > 0)
            {
                body = await System.Text.Json.JsonSerializer.DeserializeAsync<MarkRecommendationSentToStudioRequest>(
                    request.Body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Optional body
        }

        await _capabilityService.MarkRecommendationSentToStudioAsync(recommendationId, body?.StudioRunId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, new { success = true }, HttpStatusCode.OK, cancellationToken);
    }
}

/// <summary>
/// POST /family/capability-recommendations/{recommendationId}/sync-studio-status
/// Checks whether Studio has already created the Structured Learning Document for this
/// recommendation (matched by Code) and, if so, flips GenerationStatus to 'Generated' and
/// stores the StudioDocumentId — called by the UI right after "Send to Studio" and/or on a
/// "Check status" action, so the card can switch its button to "View course".
/// </summary>
public sealed class SyncRecommendationStudioStatusFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public SyncRecommendationStudioStatusFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("SyncRecommendationStudioStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/capability-recommendations/{recommendationId:guid}/sync-studio-status")]
        HttpRequestData request,
        Guid recommendationId,
        CancellationToken cancellationToken)
    {
        var updated = await _capabilityService.SyncStudioStatusAsync(recommendationId, cancellationToken);
        if (updated is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "RecommendationNotFound", "Recommendation not found.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, updated, HttpStatusCode.OK, cancellationToken);
    }
}

