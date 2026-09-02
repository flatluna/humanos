using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HumanOS.Services;

public static class HumanOsStudioSsoToken
{
    public const string Issuer = "humanos";
    public const string Audience = "capability-studio";

    public static string Issue(IConfiguration configuration, string oid, string email, Guid tenantId, string tenantName, Guid personId, string orgRole, string returnUrl)
    {
        var claims = new[]
        {
            new Claim("oid", oid),
            new Claim("email", email),
            new Claim("tenantId", tenantId.ToString()),
            new Claim("tenantName", tenantName),
            new Claim("memberId", personId.ToString()),
            new Claim("memberType", orgRole),
            new Claim("sourceApplication", "HumanOS"),
            new Claim("returnUrl", returnUrl),
            new Claim("studioView", "True"),
            new Claim("studioCreate", "True"),
            new Claim("studioEdit", "True"),
            new Claim("studioDelete", "False"),
        };
        var token = new JwtSecurityToken(Issuer, Audience, claims, expires: DateTime.UtcNow.AddHours(12), signingCredentials: SigningCredentials(configuration));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static ClaimsPrincipal? Validate(IConfiguration configuration, string token)
    {
        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = Audience,
                IssuerSigningKey = GetKey(configuration),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            }, out _);
        }
        catch { return null; }
    }

    public static string? TryGetBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader)) return null;
        const string prefix = "Bearer ";
        return authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[prefix.Length..].Trim()
            : null;
    }

    private static SigningCredentials SigningCredentials(IConfiguration configuration) =>
        new(GetKey(configuration), SecurityAlgorithms.HmacSha256);

    private static SymmetricSecurityKey GetKey(IConfiguration configuration)
    {
        var secret = configuration["HumanOSStudioLaunchJwtSecret"];
        if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("HumanOSStudioLaunchJwtSecret is not configured.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }
}