using System.Net;
using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>PUT /responsibilities/{responsibilityId} — updates a catalog Responsibility.</summary>
public sealed class UpdateResponsibilityFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ResponsibilityService _responsibilityService;

    public UpdateResponsibilityFunction(ResponsibilityService responsibilityService)
    {
        _responsibilityService = responsibilityService;
    }

    [Function("UpdateResponsibility")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "responsibilities/{responsibilityId:guid}")]
        HttpRequestData request,
        Guid responsibilityId,
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
            var resp = await _responsibilityService.UpdateAsync(responsibilityId, body, cancellationToken);

            if (resp is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "ResponsibilityNotFound", "The requested responsibility was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, resp, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidResponsibility", ex.Message, cancellationToken);
        }
    }
}
