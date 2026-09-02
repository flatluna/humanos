using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>DELETE /certifications/{certificationId} — hard-deletes a catalog Certification.</summary>
public sealed class DeleteCertificationFunction
{
    private readonly CertificationService _certificationService;

    public DeleteCertificationFunction(CertificationService certificationService)
    {
        _certificationService = certificationService;
    }

    [Function("DeleteCertification")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "certifications/{certificationId:guid}")]
        HttpRequestData request,
        Guid certificationId,
        CancellationToken cancellationToken)
    {
        var deleted = await _certificationService.DeleteAsync(certificationId, cancellationToken);

        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "CertificationNotFound", "The requested certification was not found.", cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
