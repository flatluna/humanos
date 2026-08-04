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

        var invitation = new Invitation
        {
            InvitedUserEmailAddress = email.Trim(),
            InvitedUserDisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            InvitedUserType = "Member",
            InviteRedirectUrl = _options.InviteRedirectUrl,
            SendInvitationMessage = _options.SendInvitationMessage,
        };

        var created = await _graphClient.Invitations
            .PostAsync(invitation, cancellationToken: cancellationToken);

        if (created is null)
        {
            throw new InvalidOperationException("Microsoft Graph did not return an invitation response.");
        }

        return new EntraInvitationResult
        {
            InvitationId = created.Id,
            InvitedUserId = created.InvitedUser?.Id,
            InviteRedeemUrl = created.InviteRedeemUrl,
        };
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
}
