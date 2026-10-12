using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Family;
using HumanOS.Services.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// POST /family/capabilities
/// Saves or updates a capability (used by both the AI Agent and the Admin panel).
/// </summary>
public sealed class SaveFamilyCapabilityFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly FamilyCapabilityService _capabilityService;

    public SaveFamilyCapabilityFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("SaveFamilyCapability")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/capabilities")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        SaveFamilyCapabilityRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SaveFamilyCapabilityRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Code) || string.IsNullOrWhiteSpace(body.NameEs))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingRequiredFields", "Code, StageKey, and NameEs are required.", cancellationToken);
        }

        try
        {
            var result = await _capabilityService.SaveCapabilityAsync(body, cancellationToken);
            return await FunctionResponseFactory.CreatedResponseAsync(request, result, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "SaveFailed", ex.Message, cancellationToken);
        }
    }
}
