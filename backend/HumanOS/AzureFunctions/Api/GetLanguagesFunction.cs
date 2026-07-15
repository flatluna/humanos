using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetLanguagesFunction
{
    private readonly LanguageService _languageService;

    public GetLanguagesFunction(LanguageService languageService)
    {
        _languageService = languageService;
    }

    [Function("GetLanguages")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "languages")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var languages = await _languageService.GetActiveAsync(cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(
            languages.Select(lang => new
            {
                lang.LanguageCode,
                lang.EnglishName,
                lang.NativeName,
                lang.IsActive
            }),
            cancellationToken);

        return response;
    }
}
