using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetMotivationsFunction
{
    private readonly MotivationService _motivationService;

    public GetMotivationsFunction(MotivationService motivationService)
    {
        _motivationService = motivationService;
    }

    [Function("GetMotivations")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "motivations")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var language = query["language"] ?? "en";

        var motivations = await _motivationService.GetActiveAsync(language, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, motivations, cancellationToken: cancellationToken);
    }
}
