using System.Net;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetTenantLogoFunction
{
    private readonly TenantLogoStorageService _logoStorage;

    public GetTenantLogoFunction(TenantLogoStorageService logoStorage) => _logoStorage = logoStorage;

    [Function("GetTenantLogo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/logo")] HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!_logoStorage.IsConfigured)
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured", "Data Lake storage is not configured.", cancellationToken);

        try
        {
            var result = await _logoStorage.DownloadLogoAsync(tenantId, cancellationToken);
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", result.Details.ContentType ?? "image/png");
            response.Headers.Add("Cache-Control", "public, max-age=300");
            await result.Content.CopyToAsync(response.Body, cancellationToken);
            return response;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.NotFound, "LogoNotFound", "No tenant logo has been uploaded.", cancellationToken);
        }
    }
}
