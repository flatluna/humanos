using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /studio/structured-learning/topics — creates a new Topic
/// (Type) catalog row within a domain (Subject). See
/// <see cref="Models.Capabilities.Studio.StructuredLearningTopic"/>.</summary>
public sealed class CreateStructuredLearningTopicFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public sealed class CreateRequest
    {
        public Guid TenantId { get; set; }

        public Guid SubjectId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    private readonly StructuredLearningTopicService _topicService;

    public CreateStructuredLearningTopicFunction(StructuredLearningTopicService topicService)
    {
        _topicService = topicService;
    }

    [Function("CreateStructuredLearningTopic")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/topics")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        CreateRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.SubjectId == Guid.Empty || string.IsNullOrWhiteSpace(body.Name))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "'tenantId', 'subjectId' and 'name' are required.", cancellationToken);
        }

        var topic = await _topicService.CreateAsync(body.TenantId, body.SubjectId, body.Name.Trim(), body.Description, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, topic, HttpStatusCode.Created, cancellationToken);
    }
}
