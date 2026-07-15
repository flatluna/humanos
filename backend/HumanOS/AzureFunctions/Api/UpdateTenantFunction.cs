using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Tenants;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class UpdateTenantFunction
{
    private readonly TenantService _tenantService;

    public UpdateTenantFunction(TenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [Function("UpdateTenant")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "tenants/{tenantId:guid}")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        UpdateTenantRequest? updateRequest;

        try
        {
            updateRequest =
                await JsonSerializer.DeserializeAsync<UpdateTenantRequest>(
                    request.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    },
                    cancellationToken);
        }
        catch (JsonException)
        {
            return await CreateErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidJson",
                "The request body contains invalid JSON.",
                cancellationToken);
        }

        if (updateRequest is null)
        {
            return await CreateErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "RequestBodyRequired",
                "A request body is required.",
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(updateRequest.Name))
        {
            return await CreateErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "TenantNameRequired",
                "The tenant name is required.",
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(updateRequest.CultureCode))
        {
            return await CreateErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "CultureCodeRequired",
                "The culture code is required.",
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(updateRequest.TimeZone))
        {
            return await CreateErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "TimeZoneRequired",
                "The time zone is required.",
                cancellationToken);
        }

        var tenant = await _tenantService.UpdateAsync(
            tenantId,
            updateRequest.Name,
            updateRequest.Domain,
            updateRequest.Description,
            updateRequest.CultureCode,
            updateRequest.TimeZone,
            updateRequest.IsActive,
            cancellationToken);

        if (tenant is null)
        {
            return await CreateErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "TenantNotFound",
                "The requested tenant was not found.",
                cancellationToken);
        }

        var response = request.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(
            new
            {
                tenant.TenantId,
                tenant.Name,
                tenant.Slug,
                tenant.Domain,
                tenant.Description,
                tenant.CultureCode,
                tenant.TimeZone,
                tenant.IsActive,
                tenant.CreatedDate,
                tenant.UpdatedDate
            },
            cancellationToken);

        return response;
    }

    private static async Task<HttpResponseData>
        CreateErrorResponseAsync(
            HttpRequestData request,
            HttpStatusCode statusCode,
            string error,
            string message,
            CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);

        await response.WriteAsJsonAsync(
            new
            {
                error,
                message
            },
            cancellationToken);

        return response;
    }
}
