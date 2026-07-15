using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetPersonEvidenceFunction
{
    private readonly EvidenceService _evidenceService;

    public GetPersonEvidenceFunction(EvidenceService evidenceService)
    {
        _evidenceService = evidenceService;
    }

    [Function("GetPersonEvidence")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/evidence")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var evidence = await _evidenceService.GetByPersonAsync(personId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, evidence, cancellationToken: cancellationToken);
    }
}
