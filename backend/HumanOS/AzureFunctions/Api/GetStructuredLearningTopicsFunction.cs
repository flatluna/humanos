using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Knowledge Graph — Catálogo de Tipos/Temas (2026-08-16). Lists every
/// Topic already catalogued for one (tenantId, subjectId) domain — used by
/// the admin management page and by the "pick a Topic" step of document
/// creation.
/// </summary>
public sealed class GetStructuredLearningTopicsFunction
{
    private readonly StructuredLearningTopicService _topicService;

    public GetStructuredLearningTopicsFunction(StructuredLearningTopicService topicService)
    {
        _topicService = topicService;
    }

    [Function("GetStructuredLearningTopics")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/topics")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var tenantIdRaw = request.Query["tenantId"];
        var subjectIdRaw = request.Query["subjectId"];
        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, System.Net.HttpStatusCode.BadRequest, "InvalidRequest",
                "'tenantId' query parameter is required.", cancellationToken);
        }

        var topics = Guid.TryParse(subjectIdRaw, out var subjectId)
            ? await _topicService.GetForSubjectAsync(tenantId, subjectId, cancellationToken)
            : await _topicService.GetForTenantAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, topics, cancellationToken: cancellationToken);
    }
}
