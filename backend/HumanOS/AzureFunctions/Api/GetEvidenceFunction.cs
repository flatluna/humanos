using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetEvidenceFunction
{
    private readonly EvidenceService _evidenceService;

    public GetEvidenceFunction(EvidenceService evidenceService)
    {
        _evidenceService = evidenceService;
    }

    [Function("GetEvidence")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "evidence/{evidenceId:guid}")]
        HttpRequestData request,
        Guid evidenceId,
        CancellationToken cancellationToken)
    {
        var evidence = await _evidenceService.GetByIdAsync(evidenceId, cancellationToken);

        if (evidence is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "EvidenceNotFound",
                "The requested evidence was not found.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, evidence, cancellationToken: cancellationToken);
    }
}
