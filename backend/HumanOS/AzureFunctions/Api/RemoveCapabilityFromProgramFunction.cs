using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class RemoveCapabilityFromProgramFunction
{
    private readonly ProgramService _programService;

    public RemoveCapabilityFromProgramFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("RemoveCapabilityFromProgram")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "capabilities/{capabilityId:guid}/programs/{programId:guid}")]
        HttpRequestData request,
        Guid capabilityId,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var detached = await _programService.DetachCapabilityAsync(programId, capabilityId, cancellationToken);

        if (!detached)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "MembershipNotFound",
                $"Capability {capabilityId} is not linked to program {programId}.", cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
