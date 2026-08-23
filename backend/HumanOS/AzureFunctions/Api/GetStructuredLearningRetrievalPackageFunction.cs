using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Knowledge Graph (2026-08-14) — agent-facing retrieval endpoint: given a
/// free-text user query, returns the full
/// <see cref="StructuredLearningRetrievalPackage"/> (matched entities,
/// their Engrams + Schemas, plus one hop of related entities/Engrams) for
/// an LLM tutor agent to explain from. See
/// StructuredLearningKnowledgeRetrievalService for the retrieval flow.
/// </summary>
public sealed class GetStructuredLearningRetrievalPackageFunction
{
    private readonly StructuredLearningKnowledgeRetrievalService _retrieval;

    public GetStructuredLearningRetrievalPackageFunction(StructuredLearningKnowledgeRetrievalService retrieval)
    {
        _retrieval = retrieval;
    }

    public sealed class RetrievalRequest
    {
        public Guid TenantId { get; set; }

        public string Query { get; set; } = string.Empty;
    }

    [Function("GetStructuredLearningRetrievalPackage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/knowledge-graph/retrieve")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<RetrievalRequest>(
            request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        if (body is null || body.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(body.Query))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'tenantId' and 'query' are required.", cancellationToken);
        }

        var package = await _retrieval.BuildRetrievalPackageAsync(body.TenantId, body.Query, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, package, cancellationToken: cancellationToken);
    }
}
