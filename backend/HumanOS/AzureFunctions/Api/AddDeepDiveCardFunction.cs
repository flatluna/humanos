using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio review feature (2026-07-30) — appends a brand-new
/// ExampleCard to a node's Deep Dive step pool, from a reviewer's free-text
/// instruction, via ExampleCardEditorAgent. Every existing card is read,
/// left completely untouched, and written straight back alongside the new
/// one. Reuses the same request/response shape as EditDeepDiveCardFunction.
/// </summary>
public sealed class AddDeepDiveCardFunction
{
    private readonly BlueprintReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public AddDeepDiveCardFunction(BlueprintReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("AddDeepDiveCard")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/nodes/{capabilityGraphNodeId:guid}/blueprint/deepdive/cards/add")]
        HttpRequestData request,
        Guid capabilityGraphNodeId,
        CancellationToken cancellationToken)
    {
        EditBlueprintStepRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<EditBlueprintStepRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Instruction))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Instruction is required.", cancellationToken);
        }

        try
        {
            var updated = await _service.AddExampleCardAsync(_dbContext, capabilityGraphNodeId, body.Instruction.Trim(), cancellationToken);
            var response = BlueprintReviewApiMappers.ToDto(updated);
            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            var isNotConfigured = ex.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                isNotConfigured ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NotFound,
                isNotConfigured ? "ExampleCardEditorAgentNotConfigured" : "DeepDiveStepNotFound",
                ex.Message,
                cancellationToken);
        }
    }
}
