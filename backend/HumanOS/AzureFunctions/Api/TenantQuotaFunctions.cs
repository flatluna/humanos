using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/quotas — every known platform quota
/// (<see cref="QuotaCatalog"/>) merged with this tenant's own overrides.
/// Powers the admin app's "Quotas" tab. See <see cref="TenantQuotaService"/>.</summary>
public sealed class GetTenantQuotasFunction
{
    private readonly TenantQuotaService _quotaService;

    public GetTenantQuotasFunction(TenantQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    [Function("GetTenantQuotas")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/quotas")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var quotas = await _quotaService.GetTenantQuotasAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, quotas, cancellationToken: cancellationToken);
    }
}

/// <summary>PUT /tenants/{tenantId}/quotas/{quotaKey} — sets (or, with a null
/// body value, clears back to the platform default) this tenant's override
/// for one quota. Admin-only in intent (same anonymous-trigger convention as
/// every other admin-app endpoint in this codebase — the admin app itself is
/// not yet behind its own auth gate).</summary>
public sealed class SetTenantQuotaFunction
{
    private readonly TenantQuotaService _quotaService;

    public SetTenantQuotaFunction(TenantQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    public sealed class SetQuotaRequest
    {
        public decimal? LimitValue { get; set; }
    }

    [Function("SetTenantQuota")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tenants/{tenantId:guid}/quotas/{quotaKey}")]
        HttpRequestData request,
        Guid tenantId,
        string quotaKey,
        CancellationToken cancellationToken)
    {
        SetQuotaRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SetQuotaRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        try
        {
            var updated = await _quotaService.SetQuotaAsync(tenantId, quotaKey, body?.LimitValue, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, updated, cancellationToken: cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "UnknownQuotaKey", ex.Message, cancellationToken);
        }
    }
}
