using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /people/{personId}/resume — a Person's résumé/CV for the
/// enterprise console's employee dashboard. Always returns 200 (with
/// empty fields) even if the person hasn't saved a résumé yet — see
/// PersonResumeService.GetByPersonIdAsync.</summary>
public sealed class GetPersonResumeFunction
{
    private readonly PersonResumeService _personResumeService;

    public GetPersonResumeFunction(PersonResumeService personResumeService)
    {
        _personResumeService = personResumeService;
    }

    [Function("GetPersonResume")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/resume")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var resume = await _personResumeService.GetByPersonIdAsync(personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, resume, cancellationToken: cancellationToken);
    }
}
