namespace HumanOS.Services;

public sealed class EntraInvitationOptions
{
    public const string SectionName = "EntraInvitation";

    public bool Enabled { get; set; }

    public string? TenantId { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string InviteRedirectUrl { get; set; } = "http://localhost:5180";

    public bool SendInvitationMessage { get; set; } = true;

    /// <summary>The CIAM tenant's own verified domain (e.g. "twinetwork.onmicrosoft.com"), used as the
    /// `issuer` for the local account's email sign-in identity. Required because Microsoft Entra External ID
    /// (CIAM) tenants don't support classic B2B guest invitations — employees are provisioned as local
    /// accounts instead (see EntraInvitationProvisioningService.CreateLocalAccountAsync).</summary>
    public string? IssuerDomain { get; set; }
}
