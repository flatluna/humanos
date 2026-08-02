using System.Net;
using HumanOS.Agents;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /people/{personId}/career-plan/generate — runs
/// CareerDevelopmentPlanAgent against the person's current résumé skills
/// and their assigned JobRole's required skills, and persists the result
/// as their latest career development plan.</summary>
public sealed class GenerateCareerPlanFunction
{
    private readonly PersonCareerPlanService _personCareerPlanService;
    private readonly CareerDevelopmentPlanAgent _agent;
    private readonly ILogger<GenerateCareerPlanFunction> _logger;

    public GenerateCareerPlanFunction(
        PersonCareerPlanService personCareerPlanService,
        CareerDevelopmentPlanAgent agent,
        ILogger<GenerateCareerPlanFunction> logger)
    {
        _personCareerPlanService = personCareerPlanService;
        _agent = agent;
        _logger = logger;
    }

    [Function("GenerateCareerPlan")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/career-plan/generate")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "CareerPlanAgentNotConfigured",
                "The Career Development Plan agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        try
        {
            var plan = await _personCareerPlanService.GenerateAsync(personId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, plan, cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NotFound", ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidState", ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating career plan for person {PersonId}", personId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "InternalError",
                "An error occurred while generating the career development plan.",
                cancellationToken);
        }
    }
}
