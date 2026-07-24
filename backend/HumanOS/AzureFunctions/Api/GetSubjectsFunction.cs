using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetSubjectsFunction
{
    private readonly SubjectService _subjectService;

    public GetSubjectsFunction(SubjectService subjectService)
    {
        _subjectService = subjectService;
    }

    [Function("GetSubjects")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "subjects")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var languageCode = request.Query["language"] ?? "en";

        var subjects = await _subjectService.GetAsync(languageCode, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(subjects, cancellationToken);

        return response;
    }
}
