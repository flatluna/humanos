using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class PausePersonProjectFunction
{
    private readonly ProjectService _projectService;

    public PausePersonProjectFunction(ProjectService projectService)
    {
        _projectService = projectService;
    }

    [Function("PausePersonProject")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/projects/{personProjectId:guid}/pause")]
        HttpRequestData request,
        Guid personId,
        Guid personProjectId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        try
        {
            var result = await _projectService.PauseAsync(personProjectId, cancellationToken);

            if (result is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "PersonProjectNotFound", "The requested person project was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "InvalidStatusTransition", ex.Message, cancellationToken);
        }
    }
}
