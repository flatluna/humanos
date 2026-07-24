using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class DeleteProgramFunction
{
    private readonly ProgramService _programService;

    public DeleteProgramFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("DeleteProgram")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "programs/{programId:guid}")]
        HttpRequestData request,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var deleted = await _programService.DeleteAsync(programId, cancellationToken);

        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProgramNotFound",
                $"No program found with id {programId}.", cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
