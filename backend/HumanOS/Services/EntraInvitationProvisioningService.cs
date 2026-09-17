using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace HumanOS.Services;

public sealed class EntraInvitationProvisioningService
{
    private readonly GraphServiceClient _graphClient;
    private readonly EntraInvitationOptions _options;

    public EntraInvitationProvisioningService(IOptions<EntraInvitationOptions> options)
    {
        _options = options.Value;
        _graphClient = CreateGraphClient(_options);
    }

    public bool IsEnabled => _options.Enabled;

    /// <summary>
    /// Creates a local account directly in the tenant (POST /users with an "emailAddress" sign-in
    /// identity), instead of sending a classic B2B guest invitation. Microsoft Entra External ID (CIAM)
    /// tenants reject the /invitations endpoint outright ("Guest invitations not allowed for your
    /// company") since B2B guest collaboration doesn't apply to CIAM \u2014 local accounts are the correct,
    /// supported mechanism there. The employee signs in with their email + this temporary password.
    /// </summary>
    public async Task<EntraInvitationResult> InviteMemberAsync(
        string email,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Entra invitation provisioning is disabled. Set EntraInvitation:Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("A valid email is required to invite a member in Entra.");
        }

        if (string.IsNullOrWhiteSpace(_options.IssuerDomain))
        {
            throw new InvalidOperationException("EntraInvitation:IssuerDomain must be configured (the tenant's own verified domain) to create local accounts.");
        }

        var normalizedEmail = email.Trim();
        var temporaryPassword = GenerateTemporaryPassword();

        var user = new User
        {
            AccountEnabled = true,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim(),
            MailNickname = BuildMailNickname(normalizedEmail),
            Identities =
            [
                new ObjectIdentity
                {
                    SignInType = "emailAddress",
                    Issuer = _options.IssuerDomain,
                    IssuerAssignedId = normalizedEmail,
                },
            ],
            PasswordProfile = new PasswordProfile
            {
                Password = temporaryPassword,
                ForceChangePasswordNextSignIn = true,
            },
            PasswordPolicies = "DisablePasswordExpiration",
        };

        var created = await _graphClient.Users.PostAsync(user, cancellationToken: cancellationToken);

        if (created is null)
        {
            throw new InvalidOperationException("Microsoft Graph did not return a user response.");
        }

        return new EntraInvitationResult
        {
            InvitationId = created.Id,
            InvitedUserId = created.Id,
            TemporaryPassword = temporaryPassword,
        };
    }

    private static string BuildMailNickname(string email)
    {
        var localPart = email.Split('@')[0];
        var sanitized = new string(localPart.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? $"user{Guid.NewGuid():N}"[..20] : sanitized;
    }

    /// <summary>Generates a random password satisfying Entra's default complexity policy
    /// (upper, lower, digit, symbol, 12+ chars).</summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*-_=+";
        var all = upper + lower + digits + symbols;

        var bytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

        var chars = new char[16];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = all[bytes[i] % all.Length];
        }

        // Guarantee at least one of each required character class.
        chars[0] = upper[bytes[0] % upper.Length];
        chars[1] = lower[bytes[1] % lower.Length];
        chars[2] = digits[bytes[2] % digits.Length];
        chars[3] = symbols[bytes[3] % symbols.Length];

        return new string(chars);
    }

    private static GraphServiceClient CreateGraphClient(EntraInvitationOptions options)
    {
        TokenCredential credential;

        if (!string.IsNullOrWhiteSpace(options.TenantId)
            && !string.IsNullOrWhiteSpace(options.ClientId)
            && !string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            credential = new ClientSecretCredential(
                options.TenantId,
                options.ClientId,
                options.ClientSecret);
        }
        else
        {
            credential = new DefaultAzureCredential();
        }

        return new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
    }
}

public sealed class EntraInvitationResult
{
    public string? InvitationId { get; set; }

    public string? InvitedUserId { get; set; }

    public string? InviteRedeemUrl { get; set; }

    /// <summary>One-time temporary password for the newly created local account \u2014 never persisted,
    /// only returned to the caller so an HR admin can share it with the new employee out of band.</summary>
    public string? TemporaryPassword { get; set; }
}
