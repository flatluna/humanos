using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetProjectsFunction
{
    private readonly ProjectService _projectService;

    public GetProjectsFunction(ProjectService projectService)
    {
        _projectService = projectService;
    }

    [Function("GetProjects")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "projects")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var language = query["language"] ?? "en";

        var projects = await _projectService.GetActiveAsync(language, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, projects, cancellationToken: cancellationToken);
    }
}
