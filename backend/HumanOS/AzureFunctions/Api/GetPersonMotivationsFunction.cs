using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetPersonMotivationsFunction
{
    private readonly MotivationService _motivationService;

    public GetPersonMotivationsFunction(MotivationService motivationService)
    {
        _motivationService = motivationService;
    }

    [Function("GetPersonMotivations")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/motivations")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var language = query["language"] ?? "en";

        var motivations = await _motivationService.GetPersonMotivationsAsync(personId, language, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, motivations, cancellationToken: cancellationToken);
    }
}
