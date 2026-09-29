using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "tecBot" chat turn (2026-09-28) — POST /tec-contigo/chat. One turn of the
/// student-facing continuous companion agent (see TecContigoAgent's doc
/// comment for its 5 responsibilities). Stateless server-side: the caller
/// sends the running conversation history each turn, same convention as
/// PostStructuredLearningChatFunction.
/// </summary>
public sealed class PostTecContigoChatFunction
{
    private readonly TecContigoAgent _agent;
    private readonly TecContigoContextBuilder _contextBuilder;
    private readonly PerplexitySearchService _perplexitySearch;

    public PostTecContigoChatFunction(
        TecContigoAgent agent,
        TecContigoContextBuilder contextBuilder,
        PerplexitySearchService perplexitySearch)
    {
        _agent = agent;
        _contextBuilder = contextBuilder;
        _perplexitySearch = perplexitySearch;
    }

    public sealed class ChatHistoryEntryDto
    {
        public string Role { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }

    public sealed class ChatRequest
    {
        public Guid? PersonId { get; set; }

        public string Message { get; set; } = string.Empty;

        public List<ChatHistoryEntryDto> History { get; set; } = [];
    }

    [Function("PostTecContigoChat")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tec-contigo/chat")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "TecContigoNotConfigured",
                "tecBot is not configured (missing AzureOpenAIEndpoint/AzureOpenAIDeploymentName settings).", cancellationToken);
        }

        ChatRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ChatRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Message))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MessageRequired", "'message' is required.", cancellationToken);
        }

        var contextBlock = await _contextBuilder.BuildAsync(body.PersonId, cancellationToken);
        var webFindings = await SearchForSupportingInformationAsync(body.Message, cancellationToken);
        var history = body.History
            .Select(h => new TecContigoChatHistoryEntry { Role = h.Role, Text = h.Text })
            .ToList();

        try
        {
            var reply = await _agent.AskAsync(contextBlock, body.Message, history, webFindings, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { message = reply }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "TecContigoNotConfigured", ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "ChatError", $"{ex.GetType().Name}: {ex.Message}", cancellationToken);
        }
    }

    private async Task<string> SearchForSupportingInformationAsync(
        string studentMessage,
        CancellationToken cancellationToken)
    {
        if (!_perplexitySearch.IsConfigured)
        {
            return "No hay búsqueda web configurada.";
        }

        try
        {
            var result = await _perplexitySearch.SearchAsync(
                $"Busca información pública, actual y confiable que pueda apoyar esta pregunta de un estudiante universitario. " +
                $"Prioriza fuentes oficiales, documentación técnica, universidades o investigación académica. " +
                $"No intentes inferir ni responder sus calificaciones o datos personales; solo aporta contexto útil. " +
                $"Pregunta del estudiante: {studentMessage}",
                cancellationToken);

            var citations = result.Citations.Count == 0
                ? "(sin URLs citadas)"
                : string.Join("\n", result.Citations.Select(url => $"- {url}"));

            return $"Respuesta de búsqueda web (Perplexity Sonar):\n{result.Answer}\n\nFuentes web:\n{citations}";
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            return "La búsqueda web no estuvo disponible para este turno; responde con el expediente y conocimiento general, sin inventar fuentes.";
        }
    }
}
