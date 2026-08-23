using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetStudioContextFunction
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GetStudioContextFunction> _logger;

    public GetStudioContextFunction(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GetStudioContextFunction> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [Function("GetStudioContext")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/studio-context")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var ngoBaseUrl = _configuration["NgoBackendBaseUrl"] ?? "http://localhost:7072/api";
        using var outgoing = new HttpRequestMessage(HttpMethod.Get, $"{ngoBaseUrl.TrimEnd('/')}/me/studio-context");

        CopyHeader(request, outgoing, "Authorization");
        CopyHeader(request, outgoing, "X-Azure-OID");
        CopyHeader(request, outgoing, "X-Azure-TID");
        CopyHeader(request, outgoing, "X-Azure-Email");

        try
        {
            using var response = await _httpClientFactory.CreateClient().SendAsync(outgoing, cancellationToken);
            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var result = request.CreateResponse(response.StatusCode);
            if (response.Content.Headers.ContentType is not null)
            {
                result.Headers.Add("Content-Type", response.Content.Headers.ContentType.ToString());
            }
            CopyResponseHeader(response, result, "X-Studio-Tenant-Id");
            CopyResponseHeader(response, result, "X-Studio-Tenant-Name");
            CopyResponseHeader(response, result, "X-Studio-Member-Id");
            CopyResponseHeader(response, result, "X-Studio-Member-Type");
            result.Headers.Add(
                "Access-Control-Expose-Headers",
                "X-Studio-Tenant-Id, X-Studio-Tenant-Name, X-Studio-Member-Id, X-Studio-Member-Type");

            if (body.Length > 0)
            {
                await result.Body.WriteAsync(body, cancellationToken);
            }
            else if (response.Headers.TryGetValues("X-Studio-Tenant-Id", out var tenantValues) &&
                     Guid.TryParse(tenantValues.FirstOrDefault(), out var tenantId))
            {
                var memberId = response.Headers.TryGetValues("X-Studio-Member-Id", out var memberValues)
                    ? memberValues.FirstOrDefault()
                    : null;
                var memberType = response.Headers.TryGetValues("X-Studio-Member-Type", out var typeValues)
                    ? typeValues.FirstOrDefault()
                    : null;
                var tenantName = response.Headers.TryGetValues("X-Studio-Tenant-Name", out var nameValues)
                    ? nameValues.FirstOrDefault()
                    : null;
                var payload = new
                {
                    sourceApplication = "ngo",
                    tenantId,
                    tenantName,
                    memberId,
                    memberType,
                    permissions = new { studioView = true, studioCreate = true, studioEdit = true, studioDelete = false },
                };
                var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
                result.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await result.Body.WriteAsync(payloadBytes, cancellationToken);
                await result.Body.FlushAsync(cancellationToken);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not proxy Studio context from NGO backend");
            var error = request.CreateResponse(HttpStatusCode.BadGateway);
            await error.WriteAsJsonAsync(new { code = "NgoContextUnavailable", error = "The NGO context service is unavailable." }, cancellationToken);
            return error;
        }
    }

    private static void CopyHeader(HttpRequestData request, HttpRequestMessage outgoing, string name)
    {
        if (request.Headers.TryGetValues(name, out var values) && !string.IsNullOrWhiteSpace(values.FirstOrDefault()))
        {
            outgoing.Headers.TryAddWithoutValidation(name, values.First());
        }
    }

    private static void CopyResponseHeader(HttpResponseMessage source, HttpResponseData target, string name)
    {
        if (source.Headers.TryGetValues(name, out var values) && !string.IsNullOrWhiteSpace(values.FirstOrDefault()))
        {
            target.Headers.Add(name, values.First());
        }
    }
}