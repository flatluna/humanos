using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetPersonProjectsFunction
{
    private readonly ProjectService _projectService;

    public GetPersonProjectsFunction(ProjectService projectService)
    {
        _projectService = projectService;
    }

    [Function("GetPersonProjects")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/projects")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var projects = await _projectService.GetPersonProjectsAsync(personId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, projects, cancellationToken: cancellationToken);
    }
}
