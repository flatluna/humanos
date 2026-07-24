using System.Net;
using HumanOS.Agents;
using HumanOS.Contracts.GrowthPlan;
using HumanOS.Models.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Growth Plan Step 3 Auto-Recommendation: Given Steps 1 (Current Situation)
/// and Step 2 (Future Direction) already saved in the database, automatically
/// generates a recommended learning program without requiring the user to enter
/// a free-text goal. Reads PersonCurrentSituation and PersonFutureDirection,
/// then calls GrowthPathRecommenderAgent to generate the recommendation.
/// </summary>
public sealed class GrowthPlanAutoRecommendFunction
{
    private readonly GrowthPlanService _growthPlanService;
    private readonly GrowthPathRecommenderAgent _agent;
    private readonly ILogger<GrowthPlanAutoRecommendFunction> _logger;

    public GrowthPlanAutoRecommendFunction(
        GrowthPlanService growthPlanService,
        GrowthPathRecommenderAgent agent,
        ILogger<GrowthPlanAutoRecommendFunction> logger)
    {
        _growthPlanService = growthPlanService;
        _agent = agent;
        _logger = logger;
    }

    [Function("GrowthPlanAutoRecommend")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/growth-plan/starting-point/auto-recommend")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_agent.IsConfigured)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.ServiceUnavailable,
                    "RecommenderAgentNotConfigured",
                    "The Growth Path recommender agent is not yet configured.",
                    cancellationToken);
            }

            // Load Step 1: Current Situation
            var currentSituation = await _growthPlanService.GetCurrentSituationAsync(personId, cancellationToken);
            if (currentSituation is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "CurrentSituationNotFound",
                    "Complete Step 1 (Current Situation) first.",
                    cancellationToken);
            }

            // Load Step 2: Future Direction
            var futureDirection = await _growthPlanService.GetFutureDirectionAsync(personId, cancellationToken);
            if (futureDirection is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "FutureDirectionNotFound",
                    "Complete Step 2 (Future Direction) first.",
                    cancellationToken);
            }

            // Extract data for recommendation request
            var subjectCodes = string.IsNullOrEmpty(currentSituation.SelectedSubjectCodes)
                ? Array.Empty<string>()
                : currentSituation.SelectedSubjectCodes.Split(',');

            var futureGoalIds = string.IsNullOrEmpty(futureDirection.SelectedGoalIds)
                ? Array.Empty<string>()
                : futureDirection.SelectedGoalIds.Split(',');

            var motivationCodes = string.IsNullOrEmpty(futureDirection.SelectedMotivationCodes)
                ? Array.Empty<string>()
                : futureDirection.SelectedMotivationCodes.Split(',');

            if (subjectCodes.Length == 0)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "NoSubjectsSelected",
                    "No subjects selected in Step 1.",
                    cancellationToken);
            }

            // Build recommendation request (using goals + motivations as the "goal prompt")
            var goalPrompt = BuildGoalPrompt(futureGoalIds, motivationCodes);
            
            // For now, we'll use a simple subject mapping. In production, this should come
            // from a database lookup. Here we use the codes as names as fallback.
            var allowedSubjects = subjectCodes
                .Select(code => new GrowthPathSubjectOption { Code = code.Trim(), Name = code.Trim() })
                .ToList();

            var statedGoals = new List<string>(futureGoalIds);
            statedGoals.AddRange(motivationCodes);

            // Call the recommender agent
            var recommendation = await _agent.RecommendAsync(
                new GrowthPathRequestContext
                {
                    PersonName = personId.ToString(), // Use personId as name
                    GoalPrompt = goalPrompt,
                    AllowedSubjects = allowedSubjects,
                    StatedGoals = statedGoals,
                    CatalogContext = string.Empty,
                },
                cancellationToken);

            // Build response
            var response = new RecommendGrowthPathResponse
            {
                HasRecommendation = recommendation.HasRecommendation,
                RecommendationType = recommendation.RecommendationType,
                ProgramName = recommendation.ProgramName,
                ProgramDescription = recommendation.ProgramDescription,
                SubjectCode = recommendation.SubjectCode,
                Steps = recommendation.Steps?
                    .Select(s => new RecommendGrowthPathStepResponse
                    {
                        Name = s.Name,
                        Level = s.Level,
                    })
                    .ToList() ?? new List<RecommendGrowthPathStepResponse>(),
                Rationale = recommendation.Rationale,
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GrowthPlanAutoRecommend");
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "InternalError",
                "An error occurred while generating the recommendation.",
                cancellationToken);
        }
    }

    private static string BuildGoalPrompt(string[] goalIds, string[] motivationCodes)
    {
        // Build a natural language goal prompt from the selected goals and motivations
        var items = new List<string>();
        items.AddRange(goalIds);
        items.AddRange(motivationCodes);

        if (items.Count == 0)
        {
            return "User wants to develop their skills.";
        }

        return $"User is interested in: {string.Join(", ", items)}";
    }
}
