using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Mints short-lived ephemeral client secrets for the Azure OpenAI GPT
/// Realtime API (e.g. "gpt-realtime-mini") via the GA REST endpoint
/// <c>POST {endpoint}openai/v1/realtime/client_secrets</c> — see
/// https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/realtime-audio-webrtc.
///
/// SECURITY (OWASP — never expose secrets to the client): the real Azure
/// OpenAI API key NEVER leaves this backend. The browser only ever
/// receives the short-lived ephemeral token returned by
/// <see cref="CreateEphemeralSessionAsync"/>, which it then uses directly
/// against Azure's own WebRTC endpoint (<c>openai/v1/realtime/calls</c>)
/// to negotiate a peer-to-peer audio session. This backend never proxies
/// the actual audio stream — it only mints the token and builds the
/// per-turn <c>instructions</c> text server-side (grounded in the
/// Runtime's own step content), so the browser can never see or tamper
/// with the system prompt.
/// </summary>
public sealed class RealtimeVoiceSessionService
{
    private readonly HttpClient _httpClient;
    private readonly string? _endpoint;
    private readonly string? _deploymentName;
    private readonly string? _apiKey;
    private readonly string _voice;
    private readonly string _transcriptionModel;

    public RealtimeVoiceSessionService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(RealtimeVoiceSessionService));
        _endpoint = configuration["AzureOpenAIEndpoint"];
        _deploymentName = configuration["AzureOpenAIRealtimeDeploymentName"];
        _apiKey = configuration["AzureOpenAIApiKey"];
        _voice = configuration["AzureOpenAIRealtimeVoice"] is { Length: > 0 } configuredVoice
            ? configuredVoice
            : "marin";
        _transcriptionModel = configuration["AzureOpenAIRealtimeTranscriptionModel"] is { Length: > 0 } configuredTranscriptionModel
            ? configuredTranscriptionModel
            : "whisper-1";
    }

    /// <summary>
    /// True only when a realtime-capable deployment name and API key are
    /// both configured. Deliberately requires its OWN
    /// 'AzureOpenAIRealtimeDeploymentName' setting (NOT the plain-chat
    /// 'AzureOpenAIDeploymentName' every other agent uses) — the Realtime
    /// API only works against a deployment of a realtime-family model
    /// (gpt-realtime-mini/gpt-realtime/gpt-4o-realtime-preview), never a
    /// regular chat deployment.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_endpoint)
        && !string.IsNullOrWhiteSpace(_deploymentName)
        && !string.IsNullOrWhiteSpace(_apiKey);

    public sealed class EphemeralSession
    {
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>The exact URL the browser should POST its SDP offer to.
        /// Includes <c>?webrtcfilter=on</c> ONLY for sessions with NO
        /// function-calling <c>tools</c> (e.g. the plain Hypothesis/Teaching
        /// Voice Tutor) -- that filter limits the data-channel events Azure
        /// sends back to a fixed allowlist that keeps the session's
        /// <c>instructions</c> private, per Microsoft's own WebRTC doc
        /// (https://learn.microsoft.com/azure/ai-foundry/openai/how-to/realtime-audio-webrtc).
        /// CONFIRMED 2026-07-24 (via that same doc): that allowlist does NOT
        /// include <c>response.done</c>/<c>response.output_item.done</c> --
        /// the ONLY events that carry function-call data. With the filter on,
        /// the browser never learns a tool was even called, so it never
        /// sends back a <c>function_call_output</c> and the model's turn
        /// stalls forever (exactly the "asked the Agent to write something,
        /// got zero response at all" bug). So whenever <paramref name="tools"/>
        /// is non-null, the filter is omitted entirely so those events reach
        /// the browser -- trading a small amount of prompt-instructions
        /// exposure (visible in that student's own browser dev tools only)
        /// for function-calling to actually work at all.</summary>
        public string RealtimeCallsUrl { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string Voice { get; set; } = string.Empty;

        public long? ExpiresAtUnixSeconds { get; set; }
    }

    /// <summary>
    /// Mints one ephemeral client secret scoped to a single voice session.
    /// <paramref name="instructions"/> must be built ENTIRELY server-side by
    /// the caller (from the Runtime's own grounded step content) — never
    /// pass anything derived from unsanitized user input here, since this
    /// text becomes the Realtime session's system prompt.
    /// </summary>
    /// <param name="tools">
    /// Optional Realtime "tools" (function-calling) definition, passed
    /// through verbatim to the session config's <c>tools</c> field (see
    /// https://platform.openai.com/docs/guides/realtime-conversations#function-calling).
    /// Null (the default) omits the field entirely — existing callers
    /// (VoiceTutorSessionFunction) are unaffected. Introduced for
    /// "Reactivar Engrama"'s <c>show_illustration</c> tool (2026-07-24):
    /// lets the model itself decide, mid-conversation, when a STORY or
    /// real-life example it's telling would benefit from showing one of
    /// the node's ALREADY-GENERATED illustrations — no on-demand image
    /// generation (and its latency) is ever involved, only picking an
    /// existing image by id.
    /// </param>
    public async Task<EphemeralSession> CreateEphemeralSessionAsync(
        string instructions, CancellationToken cancellationToken, object? tools = null)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "RealtimeVoiceSessionService is not configured. Set 'AzureOpenAIEndpoint', " +
                "'AzureOpenAIRealtimeDeploymentName' and 'AzureOpenAIApiKey' application settings.");
        }

        var baseUri = _endpoint!.TrimEnd('/');
        var requestUri = $"{baseUri}/openai/v1/realtime/client_secrets";

        // Built as a JsonObject (rather than an anonymous type) so `tools`/
        // `tool_choice` can be omitted entirely when the caller passes none,
        // instead of serializing as an explicit `"tools": null`.
        var sessionNode = new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "realtime",
            ["model"] = _deploymentName,
            ["instructions"] = instructions,
            ["audio"] = new System.Text.Json.Nodes.JsonObject
            {
                ["output"] = new System.Text.Json.Nodes.JsonObject { ["voice"] = _voice },
                // Lets the browser receive `conversation.item.input_audio_transcription.completed`
                // events with a text transcript of what the student said — used by the Recall
                // step's voice flow to fill the existing answer textbox (see VoiceTutorAgent.tsx),
                // WITHOUT this channel ever grading anything itself.
                ["input"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["transcription"] = new System.Text.Json.Nodes.JsonObject { ["model"] = _transcriptionModel }
                }
            }
        };

        if (tools is not null)
        {
            sessionNode["tools"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(tools));
            sessionNode["tool_choice"] = "auto";
        }

        var sessionConfig = new { session = sessionNode };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(JsonSerializer.Serialize(sessionConfig), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("api-key", _apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Azure OpenAI Realtime client_secrets request failed ({(int)response.StatusCode}): {responseBody}");
        }

        using var parsedResponse = JsonDocument.Parse(responseBody);
        var root = parsedResponse.RootElement;

        if (!root.TryGetProperty("value", out var valueElement) || valueElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"Azure OpenAI Realtime client_secrets response did not contain a 'value' field: {responseBody}");
        }

        long? expiresAt = root.TryGetProperty("expires_at", out var expiresElement)
            && expiresElement.ValueKind == JsonValueKind.Number
                ? expiresElement.GetInt64()
                : null;

        return new EphemeralSession
        {
            ClientSecret = valueElement.GetString()!,
            // See RealtimeCallsUrl's doc comment: the webrtcfilter is only
            // safe to use when there are no function-calling `tools`, since
            // its fixed event allowlist drops the function_call-carrying
            // response.done/response.output_item.done events entirely.
            RealtimeCallsUrl = tools is null
                ? $"{baseUri}/openai/v1/realtime/calls?webrtcfilter=on"
                : $"{baseUri}/openai/v1/realtime/calls",
            Model = _deploymentName!,
            Voice = _voice,
            ExpiresAtUnixSeconds = expiresAt
        };
    }
}
