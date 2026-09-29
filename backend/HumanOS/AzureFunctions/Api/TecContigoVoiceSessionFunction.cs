using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class TecContigoVoiceSessionRequest
{
    public Guid? PersonId { get; set; }

    public string? PersonName { get; set; }

    /// <summary>"en" | "es" — defaults to Spanish.</summary>
    public string? LanguageCode { get; set; }
}

/// <summary>
/// "tecBot" realtime voice (2026-09-28) — mints a short-lived Azure OpenAI
/// Realtime session for the SAME "Tec Contigo" student companion used by
/// the text chat (see PostTecContigoChatFunction/TecContigoAgent), not
/// Buddy (employee onboarding voice) and not the old HermanaScience tutor.
/// Grounded in the exact same CONTEXT block built by
/// <see cref="TecContigoContextBuilder"/> — interests, profile/résumé, the
/// full course catalog, the complete official TEC academic record, Human
/// OS/Engrams progress, and assessment evidence — so voice and text always
/// know the same facts about the student. Same ephemeral-token WebRTC
/// pattern as BuddyVoiceSessionFunction/VoiceTutorSessionFunction: the real
/// Azure OpenAI API key never reaches the browser.
/// </summary>
public sealed class TecContigoVoiceSessionFunction
{
    private readonly RealtimeVoiceSessionService _voiceSessionService;
    private readonly TecContigoContextBuilder _contextBuilder;
    private readonly PerplexitySearchService _perplexityService;

    public TecContigoVoiceSessionFunction(
        RealtimeVoiceSessionService voiceSessionService,
        TecContigoContextBuilder contextBuilder,
        PerplexitySearchService perplexityService)
    {
        _voiceSessionService = voiceSessionService;
        _contextBuilder = contextBuilder;
        _perplexityService = perplexityService;
    }

    [Function("TecContigoVoiceSession")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tec-contigo/voice-session")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_voiceSessionService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "TecContigoVoiceNotConfigured",
                "tecBot voice is not configured (missing AzureOpenAIRealtimeDeploymentName/AzureOpenAIApiKey settings).",
                cancellationToken);
        }

        TecContigoVoiceSessionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<TecContigoVoiceSessionRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        body ??= new TecContigoVoiceSessionRequest();

        var contextBlock = await _contextBuilder.BuildAsync(body.PersonId, cancellationToken);
        var isEnglish = string.Equals(body.LanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        var instructions = TecContigoAgent.BuildVoiceInstructions(contextBlock, body.PersonName, isEnglish);
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

    /// <summary>Same single Realtime tool as Buddy (2026-09-28): lets the model search the
    /// live web via Perplexity Sonar instead of ever inventing external facts. The browser
    /// executes the actual call and answers back with a function_call_output over the
    /// Realtime data channel.</summary>
    private static object[] BuildTools() =>
    [
        new
        {
            type = "function",
            name = "search_the_web",
            description = "Searches the live web for current, real information (definitions, best practices, general knowledge) needed to answer the student's question without inventing anything.",
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
