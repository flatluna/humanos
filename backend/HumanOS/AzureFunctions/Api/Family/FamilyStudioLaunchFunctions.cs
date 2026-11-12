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

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// Cross-app "Studio launch" for the Family app (see
/// docs/STUDIO_LAUNCH_INTEGRATION.md in capabilitystudio). Lets an
/// AdminEngrams family member open HumanOS Studio already authenticated —
/// Studio no longer has its own MSAL login, it only trusts a signed launch
/// JWT. Mirrors HumanOsStudioLaunchFunctions (used by the corporate HumanOS
/// app), but resolves identity against family.FamilyMembers instead of
/// People, and maps FamilyId/FamilyName/PlatformRole into the same JWT
/// claims Studio already understands (same issuer/audience/secret as
/// HumanOsStudioSsoToken — GetStudioContextFunction validates both apps'
/// tokens identically).
/// </summary>
public sealed class FamilyStudioLaunchFunctions
{
    private static readonly ConcurrentDictionary<string, LaunchRecord> Launches = new();
    private readonly HumanOsDbContext _db;
    private readonly IConfiguration _configuration;

    public FamilyStudioLaunchFunctions(HumanOsDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    /// <summary>
    /// GET /api/family/studio-launch?oid=&amp;email=&amp;returnTo=
    /// Validates the signed-in family member has the AdminEngrams platform
    /// role, mints a one-time (60s TTL) launch code, and 302-redirects the
    /// browser to Studio's /launch page to exchange it for a JWT.
    /// </summary>
    [Function("CreateFamilyStudioLaunch")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/studio-launch")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var oid = query["oid"]?.Trim();
        var email = query["email"]?.Trim();
        var returnTo = query["returnTo"]?.Trim();

        if (string.IsNullOrWhiteSpace(oid))
        {
            return await ErrorAsync(request, HttpStatusCode.BadRequest, "MissingIdentity", "Azure identity (oid) is required.", cancellationToken);
        }

        var member = await _db.FamilyMembers.AsNoTracking()
            .Include(m => m.Family)
            .Where(m => m.IsActive && m.AzureOid == oid)
            .FirstOrDefaultAsync(cancellationToken);

        if (member?.Family is null)
        {
            return await ErrorAsync(request, HttpStatusCode.Forbidden, "MemberNotAuthorized", "No active family member account was found for this identity.", cancellationToken);
        }

        if (!string.Equals(member.PlatformRole, "AdminEngrams", StringComparison.OrdinalIgnoreCase))
        {
            return await ErrorAsync(request, HttpStatusCode.Forbidden, "StudioAccessDenied", "Only AdminEngrams members can open HumanOS Studio.", cancellationToken);
        }

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Launches[Hash(code)] = new LaunchRecord(
            oid,
            member.AzureLoginEmail ?? member.Email ?? email ?? string.Empty,
            member.FamilyId,
            member.MemberId,
            member.PlatformRole,
            member.Family.FamilyName,
            DateTimeOffset.UtcNow.AddSeconds(60));

        var location = $"http://localhost:3010/launch?code={code}&exchangeUrl={Uri.EscapeDataString("http://localhost:7071/api/family/studio-launch/exchange")}";
        if (!string.IsNullOrWhiteSpace(returnTo))
        {
            location += $"&returnTo={Uri.EscapeDataString(returnTo)}";
        }

        var response = request.CreateResponse(HttpStatusCode.Found);
        response.Headers.Add("Location", location);
        return response;
    }

    /// <summary>
    /// POST /api/family/studio-launch/exchange { code }
    /// Consumes the one-time code (never reusable) and issues the signed
    /// launch JWT Studio stores for the rest of its session.
    /// </summary>
    [Function("ExchangeFamilyStudioLaunch")]
    public async Task<HttpResponseData> ExchangeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/studio-launch/exchange")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        LaunchInput? input;
        try
        {
            input = await JsonSerializer.DeserializeAsync<LaunchInput>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await ErrorAsync(request, HttpStatusCode.BadRequest, "InvalidBody", "Request body must be valid JSON.", cancellationToken);
        }

        if (input?.Code is null || !Launches.TryRemove(Hash(input.Code), out var record) || record.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return await ErrorAsync(request, HttpStatusCode.Unauthorized, "InvalidLaunchCode", "The launch code is invalid or expired.", cancellationToken);
        }

        var token = HumanOsStudioSsoToken.Issue(
            _configuration, record.Oid, record.Email, record.FamilyId, record.FamilyName, record.MemberId, record.PlatformRole, "http://localhost:5174", sourceAppType: "Family");

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { token }, cancellationToken);
        return response;
    }

    private sealed record LaunchInput(string? Code);
    private sealed record LaunchRecord(string Oid, string Email, Guid FamilyId, Guid MemberId, string PlatformRole, string FamilyName, DateTimeOffset ExpiresAt);

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value.Trim())));

    private static async Task<HttpResponseData> ErrorAsync(HttpRequestData request, HttpStatusCode status, string code, string message, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(status);
        await response.WriteAsJsonAsync(new { code, error = message }, cancellationToken);
        return response;
    }
}
