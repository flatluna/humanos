using System.Net;
using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /tenants/{tenantId}/responsibilities — creates a catalog Responsibility.</summary>
public sealed class CreateResponsibilityFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ResponsibilityService _responsibilityService;

    public CreateResponsibilityFunction(ResponsibilityService responsibilityService)
    {
        _responsibilityService = responsibilityService;
    }

    [Function("CreateResponsibility")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId:guid}/responsibilities")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        SaveResponsibilityRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SaveResponsibilityRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Name))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "NameRequired", "The responsibility name is required.", cancellationToken);
        }

        try
        {
            var resp = await _responsibilityService.CreateAsync(tenantId, body, cancellationToken);
            return await FunctionResponseFactory.CreatedResponseAsync(request, resp, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidResponsibility", ex.Message, cancellationToken);
        }
    }
}
