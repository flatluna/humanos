using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetProgramsFunction
{
    private readonly ProgramService _programService;

    public GetProgramsFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("GetPrograms")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programs")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var programs = await _programService.GetActiveAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, programs, cancellationToken: cancellationToken);
    }
}
