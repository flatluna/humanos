using System.Net;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class UploadTenantLogoFunction
{
    private readonly TenantLogoStorageService _logoStorage;

    public UploadTenantLogoFunction(TenantLogoStorageService logoStorage) => _logoStorage = logoStorage;

    [Function("UploadTenantLogo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId:guid}/logo")] HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!_logoStorage.IsConfigured)
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured", "Data Lake storage is not configured.", cancellationToken);

        var contentType = request.Headers.TryGetValues("Content-Type", out var values) ? values.First() : "image/png";
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest, "InvalidLogoType", "The tenant logo must be an image.", cancellationToken);

        await _logoStorage.UploadLogoAsync(tenantId, request.Body, contentType, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, new { tenantId, logoUrl = $"/tenants/{tenantId}/logo" }, cancellationToken: cancellationToken);
    }
}
