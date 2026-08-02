using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /people/{personId}/career-plan/accept — marks the
/// person's latest saved AI career plan as accepted by HR (confirmed as
/// the employee's official training plan). Does not change the plan's
/// content, only its acceptance state — see
/// PersonCareerPlanService.AcceptAsync.</summary>
public sealed class AcceptCareerPlanFunction
{
    private readonly PersonCareerPlanService _personCareerPlanService;
    private readonly ILogger<AcceptCareerPlanFunction> _logger;

    public AcceptCareerPlanFunction(
        PersonCareerPlanService personCareerPlanService,
        ILogger<AcceptCareerPlanFunction> logger)
    {
        _personCareerPlanService = personCareerPlanService;
        _logger = logger;
    }

    [Function("AcceptCareerPlan")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/career-plan/accept")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _personCareerPlanService.AcceptAsync(personId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, plan, cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NotFound", ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting career plan for person {PersonId}", personId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "InternalError",
                "An error occurred while accepting the career development plan.",
                cancellationToken);
        }
    }
}
