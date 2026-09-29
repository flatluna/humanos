using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class BuddyVoiceSessionRequest
{
    public Guid? PersonId { get; set; }

    public string? PersonName { get; set; }

    /// <summary>"en" | "es" — defaults to Spanish.</summary>
    public string? LanguageCode { get; set; }
}

/// <summary>
/// Buddy (2026-09-22) — mints a short-lived Azure OpenAI Realtime session
/// for the employee portal's main-page voice companion (replaces the
/// previous static "Mia" card). Grounded in the employee's own profile,
/// skills, CQI score, objectives, achievements/completed classes,
/// department and manager (see <see cref="BuddyContextBuilder"/>) — never
/// a course/lesson, and never a grading channel. Same WebRTC ephemeral-
/// token pattern as VoiceTutorSessionFunction: the real Azure OpenAI API
/// key never leaves this backend.
/// </summary>
public sealed class BuddyVoiceSessionFunction
{
    private readonly RealtimeVoiceSessionService _voiceSessionService;
    private readonly BuddyContextBuilder _buddyContextBuilder;
    private readonly PersonService _personService;
    private readonly PerplexitySearchService _perplexityService;

    public BuddyVoiceSessionFunction(
        RealtimeVoiceSessionService voiceSessionService,
        BuddyContextBuilder buddyContextBuilder,
        PersonService personService,
        PerplexitySearchService perplexityService)
    {
        _voiceSessionService = voiceSessionService;
        _buddyContextBuilder = buddyContextBuilder;
        _personService = personService;
        _perplexityService = perplexityService;
    }

    [Function("BuddyVoiceSession")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "humanos/buddy/voice-session")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_voiceSessionService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "BuddyNotConfigured",
                "Buddy is not configured (missing AzureOpenAIRealtimeDeploymentName/AzureOpenAIApiKey settings).",
                cancellationToken);
        }

        BuddyVoiceSessionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<BuddyVoiceSessionRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        body ??= new BuddyVoiceSessionRequest();

        var employeeId = body.PersonId ?? await ResolveAuthenticatedEmployeeIdAsync(request, cancellationToken);
        var isEnglish = string.Equals(body.LanguageCode, "en", StringComparison.OrdinalIgnoreCase);

        var instructions = await _buddyContextBuilder.BuildInstructionsAsync(employeeId, body.PersonName, isEnglish, cancellationToken);
        var tools = _perplexityService.IsConfigured ? BuildTools() : null;

        try
        {
            var session = await _voiceSessionService.CreateEphemeralSessionAsync(instructions, cancellationToken, tools);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new VoiceTutorSessionResponseDto
            {
                ClientSecret = session.ClientSecret,
                RealtimeCallsUrl = session.RealtimeCallsUrl,
                Model = session.Model,
                Voice = session.Voice,
                ExpiresAtUnixSeconds = session.ExpiresAtUnixSeconds
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "VoiceSessionMintFailed", ex.Message, cancellationToken);
        }
    }

    private async Task<Guid?> ResolveAuthenticatedEmployeeIdAsync(HttpRequestData request, CancellationToken cancellationToken)
    {
        if (request.Headers.TryGetValues("X-Azure-OID", out var oidValues) && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
            request.Headers.TryGetValues("X-Azure-TID", out var tidValues) && !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            var person = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
            if (person is not null)
            {
                return person.PersonId;
            }
        }

        return null;
    }

    /// <summary>Buddy's only Realtime tool (2026-09-28): lets the model search the live
    /// web via Perplexity Sonar (see BuddyWebSearchFunction/PerplexitySearchService) instead
    /// of ever inventing external facts. The browser executes the actual call (it can't reach
    /// the Perplexity API key directly) and answers back with a function_call_output
    /// over the Realtime data channel — same pattern as EngramReviewAgent's
    /// search_other_chapters tool.</summary>
    private static object[] BuildTools() =>
    [
        new
        {
            type = "function",
            name = "search_the_web",
            description = "Searches the live web for current, real information (industry facts, definitions, best practices, general knowledge) needed to answer the employee's question without inventing anything.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "The search query." }
                },
                required = new[] { "query" }
            }
        }
    ];
}
