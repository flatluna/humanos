using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /people/{personId}/human-state/today — today's Human State
/// check-in (or the "not checked in yet" shape) for the /today dashboard.</summary>
public sealed class GetHumanStateTodayFunction
{
    private readonly HumanStateService _humanStateService;

    public GetHumanStateTodayFunction(HumanStateService humanStateService)
    {
        _humanStateService = humanStateService;
    }

    [Function("GetHumanStateToday")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/human-state/today")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var result = await _humanStateService.GetTodayAsync(personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
