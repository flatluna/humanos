using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio review feature (2026-07-21) — applies a reviewer's
/// free-text instruction to ONE step of a node's blueprint via
/// BlueprintStepEditorAgent, powering Capability Studio's "Edición" preview
/// mode. Overwrites the step's Content (and, if warranted, its
/// illustration) in place — pre-publish review only, never touches any
/// LearningSession/student progress.
/// </summary>
public sealed class EditNodeBlueprintStepFunction
{
    private readonly BlueprintReviewService _service;
    private readonly BlueprintStepEditorAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public EditNodeBlueprintStepFunction(BlueprintReviewService service, BlueprintStepEditorAgent agent, HumanOsDbContext dbContext)
    {
        _service = service;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("EditNodeBlueprintStep")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/nodes/{capabilityGraphNodeId:guid}/blueprint/steps/{stepType}/edit")]
        HttpRequestData request,
        Guid capabilityGraphNodeId,
        string stepType,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "BlueprintStepEditorAgentNotConfigured",
                "BlueprintStepEditorAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        if (!Enum.TryParse<ExperienceStepType>(stepType, ignoreCase: true, out var parsedStepType))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidStepType",
                "Route parameter stepType must be one of: Hypothesis, Teaching, Recall, Production, Assessment.", cancellationToken);
        }

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
            var updated = await _service.EditStepAsync(_dbContext, capabilityGraphNodeId, parsedStepType, body.Instruction.Trim(), cancellationToken);
            var response = BlueprintReviewApiMappers.ToDto(updated);
            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "BlueprintStepNotFound", ex.Message, cancellationToken);
        }
    }
}
