using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetProgramByIdFunction
{
    private readonly ProgramService _programService;

    public GetProgramByIdFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("GetProgramById")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programs/{programId:guid}")]
        HttpRequestData request,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var program = await _programService.GetByIdAsync(programId, cancellationToken);

        if (program is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProgramNotFound",
                $"No program found with id {programId}.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, program, cancellationToken: cancellationToken);
    }
}
