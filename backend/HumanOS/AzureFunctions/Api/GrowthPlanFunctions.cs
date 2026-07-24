using System.Net;
using System.Text.Json;
using HumanOS.Contracts.GrowthPlan;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GrowthPlanCurrentSituationFunction
{
    private readonly GrowthPlanService _growthPlanService;

    public GrowthPlanCurrentSituationFunction(GrowthPlanService growthPlanService)
    {
        _growthPlanService = growthPlanService;
    }

    [Function("GetCurrentSituation")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/growth-plan/current-situation")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var situation = await _growthPlanService.GetCurrentSituationAsync(personId, cancellationToken);

        if (situation is null)
        {
            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                new GetCurrentSituationResponse(),
                cancellationToken: cancellationToken);
        }

        var subjectCodes = string.IsNullOrEmpty(situation.SelectedSubjectCodes)
            ? []
            : situation.SelectedSubjectCodes.Split(',');

        var levelsDict = string.IsNullOrEmpty(situation.SelfAssessedLevelsJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(situation.SelfAssessedLevelsJson) ?? [];

        var response = new GetCurrentSituationResponse
        {
            SelectedSubjectCodes = subjectCodes,
            SelfAssessedLevelBySubject = levelsDict,
            Completed = situation.Completed
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }

    [Function("UpsertCurrentSituation")]
    public async Task<HttpResponseData> UpsertAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/growth-plan/current-situation")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        try
        {
            var req = await JsonSerializer.DeserializeAsync<UpsertCurrentSituationRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (req is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "RequestBodyRequired", "Request body is required.", cancellationToken);
            }

            var saved = await _growthPlanService.UpsertCurrentSituationAsync(
                personId,
                req.SelectedSubjectCodes,
                req.SelfAssessedLevelBySubject,
                req.Completed,
                cancellationToken);

            var response = new GetCurrentSituationResponse
            {
                SelectedSubjectCodes = req.SelectedSubjectCodes,
                SelfAssessedLevelBySubject = req.SelfAssessedLevelBySubject,
                Completed = req.Completed
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "UnexpectedError", ex.Message, cancellationToken);
        }
    }
}

public sealed class GrowthPlanFutureDirectionFunction
{
    private readonly GrowthPlanService _growthPlanService;

    public GrowthPlanFutureDirectionFunction(GrowthPlanService growthPlanService)
    {
        _growthPlanService = growthPlanService;
    }

    [Function("GetFutureDirection")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/growth-plan/future-direction")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var direction = await _growthPlanService.GetFutureDirectionAsync(personId, cancellationToken);

        if (direction is null)
        {
            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                new GetFutureDirectionResponse(),
                cancellationToken: cancellationToken);
        }

        var goalIds = string.IsNullOrEmpty(direction.SelectedGoalIds) ? [] : direction.SelectedGoalIds.Split(',');
        var motivations = string.IsNullOrEmpty(direction.SelectedMotivationCodes) ? [] : direction.SelectedMotivationCodes.Split(',');

        var response = new GetFutureDirectionResponse
        {
            SelectedGoalIds = goalIds,
            SelectedMotivationCodes = motivations,
            Completed = direction.Completed
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }

    [Function("UpsertFutureDirection")]
    public async Task<HttpResponseData> UpsertAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/growth-plan/future-direction")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        try
        {
            var req = await JsonSerializer.DeserializeAsync<UpsertFutureDirectionRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (req is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "RequestBodyRequired", "Request body is required.", cancellationToken);
            }

            await _growthPlanService.UpsertFutureDirectionAsync(
                personId,
                req.SelectedGoalIds,
                req.SelectedMotivationCodes,
                req.Completed,
                cancellationToken);

            var response = new GetFutureDirectionResponse
            {
                SelectedGoalIds = req.SelectedGoalIds,
                SelectedMotivationCodes = req.SelectedMotivationCodes,
                Completed = req.Completed
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "UnexpectedError", ex.Message, cancellationToken);
        }
    }
}

public sealed class GrowthPlanStartingPointFunction
{
    private readonly GrowthPlanService _growthPlanService;

    public GrowthPlanStartingPointFunction(GrowthPlanService growthPlanService)
    {
        _growthPlanService = growthPlanService;
    }

    [Function("GetStartingPoint")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/growth-plan/starting-point")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var point = await _growthPlanService.GetStartingPointAsync(personId, cancellationToken);

        if (point is null)
        {
            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                new GetStartingPointResponse(),
                cancellationToken: cancellationToken);
        }

        var capIds = string.IsNullOrEmpty(point.SelectedCapabilityIds)
            ? []
            : point.SelectedCapabilityIds.Split(',');

        var gaps = string.IsNullOrEmpty(point.GapCapabilitiesBySubjectJson)
            ? new Dictionary<string, List<string>>()
            : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(point.GapCapabilitiesBySubjectJson) ?? [];

        var acceptedRecommendations = string.IsNullOrEmpty(point.AcceptedRecommendationsJson)
            ? []
            : JsonSerializer.Deserialize<List<AcceptedRecommendation>>(point.AcceptedRecommendationsJson) ?? [];

        var response = new GetStartingPointResponse
        {
            SelectedCapabilityIds = capIds,
            GapCapabilitiesBySubject = gaps,
            AcceptedRecommendations = acceptedRecommendations,
            Completed = point.Completed
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }

    [Function("UpsertStartingPoint")]
    public async Task<HttpResponseData> UpsertAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/growth-plan/starting-point")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        try
        {
            var req = await JsonSerializer.DeserializeAsync<UpsertStartingPointRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (req is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "RequestBodyRequired", "Request body is required.", cancellationToken);
            }

            await _growthPlanService.UpsertStartingPointAsync(
                personId,
                req.SelectedCapabilityIds,
                req.GapCapabilitiesBySubject,
                req.AcceptedRecommendations,
                req.Completed,
                cancellationToken);

            var response = new GetStartingPointResponse
            {
                SelectedCapabilityIds = req.SelectedCapabilityIds,
                GapCapabilitiesBySubject = req.GapCapabilitiesBySubject,
                AcceptedRecommendations = req.AcceptedRecommendations,
                Completed = req.Completed
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "UnexpectedError", ex.Message, cancellationToken);
        }
    }
}
