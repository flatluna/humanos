using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetCapabilityProgramsFunction
{
    private readonly ProgramService _programService;

    public GetCapabilityProgramsFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("GetCapabilityPrograms")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "capabilities/{capabilityId:guid}/programs")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var memberships = await _programService.GetProgramsForCapabilityAsync(capabilityId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, memberships, cancellationToken: cancellationToken);
    }
}
