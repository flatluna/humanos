using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.AzureFunctions.Api;

public sealed class HumanOsStudioLaunchFunctions
{
    private static readonly ConcurrentDictionary<string, LaunchRecord> Launches = new();
    private readonly HumanOsDbContext _db;
    private readonly IConfiguration _configuration;

    public HumanOsStudioLaunchFunctions(HumanOsDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [Function("CreateHumanOsStudioLaunch")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "humanos/studio-launch")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var oid = query["oid"]?.Trim();
        var tid = query["tid"]?.Trim();
        var email = query["email"]?.Trim();
        var returnTo = query["returnTo"]?.Trim();
        if (string.IsNullOrWhiteSpace(oid) || string.IsNullOrWhiteSpace(tid)) return await ErrorAsync(request, HttpStatusCode.BadRequest, "MissingIdentity", "Azure identity is required.", cancellationToken);

        var person = await _db.People.AsNoTracking().SingleOrDefaultAsync(
            p => p.IsActive && p.TenantId != null &&
                 ((p.AzureOid == oid && p.AzureTid == tid) || (!string.IsNullOrEmpty(email) && p.Email == email)),
            cancellationToken);
        if (person?.TenantId is not Guid tenantId) return await ErrorAsync(request, HttpStatusCode.Forbidden, "EmployeeNotAuthorized", "No active HumanOS employee account was found.", cancellationToken);
        var tenantName = await _db.Tenants.AsNoTracking().Where(t => t.TenantId == tenantId).Select(t => t.Name).SingleOrDefaultAsync(cancellationToken) ?? "HumanOS organization";
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Launches[Hash(code)] = new LaunchRecord(oid, person.Email ?? email ?? string.Empty, tenantId, person.PersonId, person.OrgRole.ToString(), tenantName, DateTimeOffset.UtcNow.AddSeconds(60));
        
        var location = $"http://localhost:3010/launch?code={code}&exchangeUrl={Uri.EscapeDataString("http://localhost:7071/api/humanos/studio-launch/exchange")}";
        if (!string.IsNullOrWhiteSpace(returnTo))
        {
            location += $"&returnTo={Uri.EscapeDataString(returnTo)}";
        }

        var response = request.CreateResponse(HttpStatusCode.Found);
        response.Headers.Add("Location", location);
        return response;
    }

    [Function("ExchangeHumanOsStudioLaunch")]
    public async Task<HttpResponseData> ExchangeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "humanos/studio-launch/exchange")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var input = await JsonSerializer.DeserializeAsync<LaunchInput>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);
        if (input?.Code is null || !Launches.TryRemove(Hash(input.Code), out var record) || record.ExpiresAt <= DateTimeOffset.UtcNow) return await ErrorAsync(request, HttpStatusCode.Unauthorized, "InvalidLaunchCode", "The launch code is invalid or expired.", cancellationToken);
        var token = HumanOsStudioSsoToken.Issue(_configuration, record.Oid, record.Email, record.TenantId, record.TenantName, record.PersonId, record.OrgRole, "http://localhost:5179");
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { token }, cancellationToken);
        return response;
    }

    private sealed record LaunchInput(string? Code);
    private sealed record LaunchRecord(string Oid, string Email, Guid TenantId, Guid PersonId, string OrgRole, string TenantName, DateTimeOffset ExpiresAt);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value.Trim())));
    private static async Task<HttpResponseData> ErrorAsync(HttpRequestData request, HttpStatusCode status, string code, string message, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(status);
        await response.WriteAsJsonAsync(new { code, error = message }, cancellationToken);
        return response;
    }
}