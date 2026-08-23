using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>PUT /studio/structured-learning/topics/{topicId} — renames/
/// re-describes a Topic catalog row.</summary>
public sealed class UpdateStructuredLearningTopicFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public sealed class UpdateRequest
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    private readonly StructuredLearningTopicService _topicService;

    public UpdateStructuredLearningTopicFunction(StructuredLearningTopicService topicService)
    {
        _topicService = topicService;
    }

    [Function("UpdateStructuredLearningTopic")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "studio/structured-learning/topics/{topicId:guid}")]
        HttpRequestData request,
        Guid topicId,
        CancellationToken cancellationToken)
    {
        UpdateRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<UpdateRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Name))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'name' is required.", cancellationToken);
        }

        var topic = await _topicService.UpdateAsync(topicId, body.Name.Trim(), body.Description, cancellationToken);
        if (topic is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "TopicNotFound", "The requested Topic was not found.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, topic, cancellationToken: cancellationToken);
    }
}
