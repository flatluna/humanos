using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio review feature (2026-07-30) — applies a reviewer's
/// free-text instruction to ONE ExampleCard (by index) of a node's Deep
/// Dive step, via ExampleCardEditorAgent. Card-scoped counterpart to
/// EditNodeBlueprintStepFunction, which only ever edits a step's plain
/// Content and has no concept of ExampleCards — using it on the Deep Dive
/// step wiped every card from the reviewer's screen (see
/// BlueprintReviewService.EditExampleCardAsync's doc comment for the full
/// incident). Every OTHER card in the pool is read, left completely
/// untouched, and written straight back.
/// </summary>
public sealed class EditDeepDiveCardFunction
{
    private readonly BlueprintReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public EditDeepDiveCardFunction(BlueprintReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("EditDeepDiveCard")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/nodes/{capabilityGraphNodeId:guid}/blueprint/deepdive/cards/{cardIndex:int}/edit")]
        HttpRequestData request,
        Guid capabilityGraphNodeId,
        int cardIndex,
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
            var updated = await _service.EditExampleCardAsync(_dbContext, capabilityGraphNodeId, cardIndex, body.Instruction.Trim(), cancellationToken);
            var response = BlueprintReviewApiMappers.ToDto(updated);
            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            var isNotConfigured = ex.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                isNotConfigured ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NotFound,
                isNotConfigured ? "ExampleCardEditorAgentNotConfigured" : "DeepDiveCardNotFound",
                ex.Message,
                cancellationToken);
        }
    }
}
