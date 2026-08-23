using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>DELETE /studio/structured-learning/topics/{topicId} — removes a
/// Topic catalog row. Documents/entities that already reference this
/// TopicId simply keep the (now dangling) Guid — never cascaded, since a
/// Topic is just a scoping label, not owning data.</summary>
public sealed class DeleteStructuredLearningTopicFunction
{
    private readonly StructuredLearningTopicService _topicService;

    public DeleteStructuredLearningTopicFunction(StructuredLearningTopicService topicService)
    {
        _topicService = topicService;
    }

    [Function("DeleteStructuredLearningTopic")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/structured-learning/topics/{topicId:guid}")]
        HttpRequestData request,
        Guid topicId,
        CancellationToken cancellationToken)
    {
        var deleted = await _topicService.DeleteAsync(topicId, cancellationToken);
        if (!deleted)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "TopicNotFound", "The requested Topic was not found.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { ok = true }, cancellationToken: cancellationToken);
    }
}
