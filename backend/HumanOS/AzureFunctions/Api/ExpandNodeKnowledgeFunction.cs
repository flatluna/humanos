using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Response DTO for a knowledge expansion — mirrors the shape a
/// NodeExperienceBlueprintStep already renders (Content as sanitized HTML)
/// plus an optional diagram illustration id, servable via the EXISTING
/// GetIllustrationImageFunction endpoint (no new image-serving
/// infrastructure needed).</summary>
public sealed class KnowledgeExpansionResponseDto
{
    public Guid CapabilityGraphNodeId { get; set; }

    public string Content { get; set; } = string.Empty;

    public Guid? DiagramIllustrationId { get; set; }
}

/// <summary>
/// On-demand "Profundizar" (Knowledge Expansion) endpoint (2026-07-20) — the
/// learner explicitly clicks a button on a node's Teaching step to get a
/// deeper explanation combining the LLM's own knowledge with a live Bing
/// Grounding search, plus an optional diagram. Get-or-create: the first
/// call for a node generates and caches the result; every later call
/// (any learner) returns the same cached row instantly.
///
/// Route deliberately does NOT start with the bare "runtime" segment — see
/// /memories/user/azure-functions-gotchas.md ("runtime" is a reserved
/// Azure Functions route prefix and fails to load silently).
/// </summary>
public sealed class ExpandNodeKnowledgeFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly KnowledgeExpansionService _knowledgeExpansionService;

    public ExpandNodeKnowledgeFunction(HumanOsDbContext dbContext, KnowledgeExpansionService knowledgeExpansionService)
    {
        _dbContext = dbContext;
        _knowledgeExpansionService = knowledgeExpansionService;
    }

    [Function("ExpandNodeKnowledge")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "capability-graph-nodes/{nodeId:guid}/knowledge-expansion")]
        HttpRequestData request,
        Guid nodeId,
        CancellationToken cancellationToken)
    {
        if (!_knowledgeExpansionService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "KnowledgeExpansionNotConfigured",
                "KnowledgeExpansionAgent is not configured (missing 'AzureOpenAIEndpoint'/'AzureOpenAIDeploymentName').",
                cancellationToken);
        }

        try
        {
            var expansion = await _knowledgeExpansionService.GetOrCreateAsync(_dbContext, nodeId, cancellationToken);

            var response = new KnowledgeExpansionResponseDto
            {
                CapabilityGraphNodeId = expansion.CapabilityGraphNodeId,
                Content = expansion.Content,
                DiagramIllustrationId = expansion.DiagramIllustrationId
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotFound", ex.Message, cancellationToken);
        }
    }
}
