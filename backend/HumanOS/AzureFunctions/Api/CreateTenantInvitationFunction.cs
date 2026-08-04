using System.Net;
using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class CreateTenantInvitationFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly TenantInvitationService _invitationService;

    public CreateTenantInvitationFunction(TenantInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [Function("CreateTenantInvitation")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId:guid}/invitations")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        CreateTenantInvitationRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateTenantInvitationRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Email))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "EmailRequired", "Email is required.", cancellationToken);
        }

        try
        {
            var created = await _invitationService.CreateAsync(tenantId, body, cancellationToken);
            return await FunctionResponseFactory.CreatedResponseAsync(request, created, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidInvitation", ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadGateway,
                "AzureInvitationFailed",
                $"Failed to provision Azure invitation: {ex.Message}",
                cancellationToken);
        }
    }
}
