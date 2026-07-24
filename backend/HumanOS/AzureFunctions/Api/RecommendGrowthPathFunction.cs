using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Contracts.GrowthPlan;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Growth Plan Step 3 ("Planeemos Juntos tu Desarrollo") — recommends a
/// Program or a set of individual Capabilities from a person's free-text
/// goal, via <see cref="GrowthPathRecommenderAgent"/> (real LLM call,
/// Microsoft Agent Framework). Replaces the earlier pure-frontend
/// keyword-matching mock. Anonymous/stateless — no PersonId route
/// parameter, since nothing is persisted here; the frontend decides what
/// (if anything) to save via the existing gap-capability mechanism once
/// the person accepts a recommendation.
/// </summary>
public sealed class RecommendGrowthPathFunction
{
    private readonly GrowthPathRecommenderAgent _agent;
    private readonly HumanOS.Services.ProgramService _programService;

    public RecommendGrowthPathFunction(GrowthPathRecommenderAgent agent, HumanOS.Services.ProgramService programService)
    {
        _agent = agent;
        _programService = programService;
    }

    [Function("RecommendGrowthPath")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "growth-plan/starting-point/recommend")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "RecommenderAgentNotConfigured",
                "The Growth Path recommender agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        RecommendGrowthPathRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<RecommendGrowthPathRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.GoalPrompt) || body.AllowedSubjects.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "GoalPromptRequired",
                "goalPrompt and at least one allowedSubjects entry are required.",
                cancellationToken);
        }

        var realPrograms = await _programService.GetActiveAsync(cancellationToken);

        var recommendation = await _agent.RecommendAsync(
            new GrowthPathRequestContext
            {
                PersonName = body.PersonName,
                GoalPrompt = body.GoalPrompt,
                AllowedSubjects = body.AllowedSubjects
                    .Select(s => new GrowthPathSubjectOption { Code = s.Code, Name = s.Name })
                    .ToList(),
                StatedGoals = body.StatedGoals,
                CatalogContext = body.CatalogContext ?? string.Empty,
                RealPrograms = realPrograms
                    .Select(p => new RealProgramOption { ProgramId = p.ProgramId, Name = p.Name, Description = p.Description })
                    .ToList(),
            },
            cancellationToken);

        // Defensive re-check: never trust the LLM's SubjectCode blindly —
        // enforce the same "only within Step 1 selected Subjects" hard
        // rule server-side, in case of a hallucinated/out-of-scope code.
        var allowedCodes = body.AllowedSubjects
            .Select(s => s.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isInScope = recommendation.SubjectCode is not null && allowedCodes.Contains(recommendation.SubjectCode);

        // Same defensive re-check for MatchedProgramId — only trust it if
        // it's really one of the real Programs we handed the agent.
        var realProgramIds = realPrograms.Select(p => p.ProgramId).ToHashSet();
        var matchedProgramId = recommendation.MatchedProgramId.HasValue
            && realProgramIds.Contains(recommendation.MatchedProgramId.Value)
                ? recommendation.MatchedProgramId
                : null;

        var response = new RecommendGrowthPathResponse
        {
            HasRecommendation = recommendation.HasRecommendation && isInScope,
            RecommendationType = recommendation.RecommendationType,
            ProgramName = recommendation.ProgramName,
            ProgramDescription = recommendation.ProgramDescription,
            SubjectCode = recommendation.SubjectCode,
            Steps = recommendation.Steps
                .Select(s => new RecommendGrowthPathStepResponse { Name = s.Name, Level = s.Level })
                .ToList(),
            Rationale = recommendation.Rationale,
            MatchedProgramId = matchedProgramId,
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
