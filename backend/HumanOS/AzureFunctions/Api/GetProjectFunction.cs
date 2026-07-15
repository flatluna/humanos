using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetProjectFunction
{
    private readonly ProjectService _projectService;

    public GetProjectFunction(ProjectService projectService)
    {
        _projectService = projectService;
    }

    [Function("GetProject")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "projects/{projectId:guid}")]
        HttpRequestData request,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var language = query["language"] ?? "en";

        var project = await _projectService.GetByIdAsync(projectId, language, cancellationToken);

        if (project is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "ProjectNotFound",
                "The requested project was not found.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, project, cancellationToken: cancellationToken);
    }
}
