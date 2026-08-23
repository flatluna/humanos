using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Knowledge Graph (2026-08-14) — backfill/retry endpoint for documents
/// that were extracted (Step 2) before this feature existed, before it was
/// configured, or whose knowledge-graph pass was skipped by a transient
/// error. Synchronous (small enough to not need the run/poll pattern —
/// unlike Step 2/3, there's no LLM-generated pedagogical content to review
/// afterward, just entity rows).
/// </summary>
public sealed class BackfillStructuredLearningKnowledgeGraphFunction
{
    private readonly StructuredLearningExtractionService _extractionService;

    public BackfillStructuredLearningKnowledgeGraphFunction(StructuredLearningExtractionService extractionService)
    {
        _extractionService = extractionService;
    }

    [Function("BackfillStructuredLearningKnowledgeGraph")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/{documentId:guid}/knowledge-graph/backfill")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!_extractionService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StudioAgentsNotConfigured",
                "El grafo de conocimiento no está configurado (falta configuración de Azure OpenAI).", cancellationToken);
        }

        await _extractionService.BackfillKnowledgeGraphAsync(documentId, _ => { }, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, new { ok = true }, cancellationToken: cancellationToken);
    }
}
