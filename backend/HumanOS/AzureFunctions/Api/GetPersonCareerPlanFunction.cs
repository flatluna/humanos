using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /people/{personId}/career-plan — the latest saved AI
/// career development plan for a Person, for the enterprise console's
/// "Rutas de Carrera" tab. Always returns 200 (HasPlan=false if none has
/// been generated yet) — see PersonCareerPlanService.GetByPersonIdAsync.</summary>
public sealed class GetPersonCareerPlanFunction
{
    private readonly PersonCareerPlanService _personCareerPlanService;

    public GetPersonCareerPlanFunction(PersonCareerPlanService personCareerPlanService)
    {
        _personCareerPlanService = personCareerPlanService;
    }

    [Function("GetPersonCareerPlan")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/career-plan")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var plan = await _personCareerPlanService.GetByPersonIdAsync(personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, plan, cancellationToken: cancellationToken);
    }
}
