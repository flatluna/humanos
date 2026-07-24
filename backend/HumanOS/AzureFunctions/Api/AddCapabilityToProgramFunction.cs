using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Connects an existing Capability to an existing Program, appending it to
/// the END of that Program's sequence (auto SortOrder). This is the
/// "bottom-up" direction of the Program↔Capability relationship — Programs
/// are created top-down first (empty), then Capabilities are attached to
/// them afterward, either at Capability-creation time (see
/// StartPdfCapabilityGraphFunction/StartCapabilityGraphFromDescriptionFunction's
/// optional ProgramId) or later from the Capability's own detail page.
/// </summary>
public sealed class AddCapabilityToProgramFunction
{
    private readonly ProgramService _programService;

    public AddCapabilityToProgramFunction(ProgramService programService)
    {
        _programService = programService;
    }

    [Function("AddCapabilityToProgram")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "capabilities/{capabilityId:guid}/programs/{programId:guid}")]
        HttpRequestData request,
        Guid capabilityId,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var attached = await _programService.AttachCapabilityAsync(programId, capabilityId, cancellationToken);

        if (!attached)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProgramNotFound",
                $"No program found with id {programId}.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new { programId, capabilityId }, cancellationToken: cancellationToken);
    }
}
