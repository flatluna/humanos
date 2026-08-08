using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Cross-chapter knowledge lookup for the Realtime voice agents' "search_other_chapters"
/// tool (2026-08-06) -- lets "Agente Engrama"/Voice Tutor answer a student's question
/// about a DIFFERENT node/chapter of the same course than the one currently being
/// reviewed/taught (e.g. asking about "canal de denuncia" while reviewing "Control de
/// edición"), without stuffing the whole course into the session's system prompt --
/// far too costly/large once a course has many chapters, and a live Realtime session
/// only pays for that once per turn's context, not per relevant fact. Reuses the SAME
/// embedded-chunk index and vector search already built for the text-based
/// TutorService's cross-node RAG lookup (see NodeKnowledgeIndexService) -- this
/// endpoint is just a thin HTTP wrapper, since a Realtime tool call (unlike
/// TutorService's in-process call) must be a real round trip from the browser.
/// </summary>
public sealed class SearchNodeKnowledgeFunction
{
    private readonly NodeKnowledgeIndexService _knowledgeIndexService;
    private readonly HumanOsDbContext _dbContext;

    public SearchNodeKnowledgeFunction(NodeKnowledgeIndexService knowledgeIndexService, HumanOsDbContext dbContext)
    {
        _knowledgeIndexService = knowledgeIndexService;
        _dbContext = dbContext;
    }

    public sealed class SearchRequest
    {
        public Guid CapabilityGraphNodeId { get; set; }

        public string Query { get; set; } = string.Empty;
    }

    public sealed class SnippetDto
    {
        public string NodeName { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }

    public sealed class SearchResponse
    {
        public List<SnippetDto> Snippets { get; set; } = [];
    }

    [Function("SearchNodeKnowledge")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/tutor/knowledge/search")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_knowledgeIndexService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "KnowledgeSearchNotConfigured",
                "Cross-chapter knowledge search is not configured (missing AzureOpenAIEmbeddingDeploymentName).",
                cancellationToken);
        }

        SearchRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SearchRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.CapabilityGraphNodeId == Guid.Empty || string.IsNullOrWhiteSpace(body.Query))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "'CapabilityGraphNodeId' and 'Query' are both required.", cancellationToken);
        }

        var capabilityGraphId = await _dbContext.CapabilityGraphNodes
            .AsNoTracking()
            .Where(n => n.CapabilityGraphNodeId == body.CapabilityGraphNodeId)
            .Select(n => n.CapabilityGraphId)
            .FirstOrDefaultAsync(cancellationToken);

        if (capabilityGraphId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotFound", "No CapabilityGraphNode was found with that id.", cancellationToken);
        }

        // Capped so a runaway/adversarial query from the model can't blow up cost.
        var query = body.Query.Length > 500 ? body.Query[..500] : body.Query;

        var snippets = await _knowledgeIndexService.SearchAsync(
            _dbContext, capabilityGraphId, body.CapabilityGraphNodeId, query, topK: 5, cancellationToken: cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new SearchResponse
        {
            Snippets = snippets.Select(s => new SnippetDto { NodeName = s.NodeName, Content = s.Content }).ToList()
        }, cancellationToken: cancellationToken);
    }
}
