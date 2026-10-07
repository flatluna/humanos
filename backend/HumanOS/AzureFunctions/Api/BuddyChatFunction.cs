using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class BuddyChatFunction
{
    private readonly BuddyAgentService _agent;
    private readonly PersonService _people;

    public BuddyChatFunction(BuddyAgentService agent, PersonService people)
    {
        _agent = agent;
        _people = people;
    }

    public sealed class Request
    {
        public string Message { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = "es";
        public List<HistoryEntry> History { get; set; } = [];
    }

    public sealed class HistoryEntry
    {
        public string Role { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    [Function("BuddyChat")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "humanos/buddy/chat")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<Request>(
            request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.Message))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'message' is required.", cancellationToken);
        }

        var personId = await ResolveEmployeeIdAsync(request, cancellationToken);
        if (!personId.HasValue)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "EmployeeNotResolved", "The authenticated employee could not be resolved.", cancellationToken);
        }
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotImplemented, "NotConfigured", "Buddy chat is not configured.", cancellationToken);
        }

        try
        {
            var history = body.History.Select(x => new BuddyChatHistoryEntry(x.Role, x.Text)).ToList();
            var answer = await _agent.AskAsync(personId.Value, body.Message, history, body.LanguageCode, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { message = answer }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "BuddyChatFailed", ex.Message, cancellationToken);
        }
    }

    private async Task<Guid?> ResolveEmployeeIdAsync(HttpRequestData request, CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValues("X-Azure-OID", out var oid) ||
            !request.Headers.TryGetValues("X-Azure-TID", out var tid))
        {
            return null;
        }
        var person = await _people.GetByAzureIdentityAsync(oid.FirstOrDefault() ?? string.Empty, tid.FirstOrDefault() ?? string.Empty, cancellationToken);
        return person?.PersonId;
    }
}
