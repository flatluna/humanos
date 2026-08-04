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
}
