using System.Linq;
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
        // 'AzureOpenAIRealtimeEndpoint'/'AzureOpenAIRealtimeApiKey' (2026-08-06, dual-resource
        // migration): gpt-realtime-mini stayed pinned on twinet-resource (its RPM quota is
        // maxed out subscription-wide, see /memories/repo migration notes) while every other
        // deployment moved to engramsediucation-resource — these let THIS service point at a
        // different account than 'AzureOpenAIEndpoint'/'AzureOpenAIApiKey'. Falls back to the
        // shared settings when not set, so nothing breaks for anyone on a single-resource setup.
        _endpoint = configuration["AzureOpenAIRealtimeEndpoint"] ?? configuration["AzureOpenAIEndpoint"];
        _deploymentName = configuration["AzureOpenAIRealtimeDeploymentName"];
        _apiKey = configuration["AzureOpenAIRealtimeApiKey"] ?? configuration["AzureOpenAIApiKey"];
        // Switched to a male-sounding voice (2026-09-03, explicit user request) —
        // "echo" is the classic deep male voice in OpenAI Realtime API, unmistakably
        // male in both English and Spanish (the Realtime voice itself is language-
        // agnostic; the model still speaks whichever language the prompt requires).
        _voice = configuration["AzureOpenAIRealtimeVoice"] is { Length: > 0 } configuredVoice
            ? configuredVoice
            : "echo";
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

    public string DeploymentName => _deploymentName ?? string.Empty;

    /// <summary>Proxies the browser's SDP offer to Azure instead of letting
    /// the browser negotiate directly (2026-10-02) \u2014 this is the ONLY way
    /// to learn the call's Azure-assigned call_id (returned as the
    /// response's Location header), which is required to open a
    /// server-side observer WebSocket for token usage (see
    /// <see cref="RealtimeVoiceUsageObserverService"/>). The browser's own
    /// WebRTC data channel is completely unaffected \u2014 media/ICE/DTLS stay
    /// peer-to-peer browser\u2194Azure exactly as before, and
    /// <paramref name="usesTools"/> still controls webrtcfilter the same
    /// way <see cref="CreateEphemeralSessionAsync"/> did, so the browser
    /// keeps receiving the exact same filtered/unfiltered event set it
    /// always has \u2014 nothing new is exposed to it.</summary>
    public async Task<(string AnswerSdp, string? CallId)> NegotiateSdpAsync(
        string clientSecret, string sdpOffer, bool usesTools, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("RealtimeVoiceSessionService is not configured.");
        }

        var baseUri = _endpoint!.TrimEnd('/');
        var requestUri = usesTools
            ? $"{baseUri}/openai/v1/realtime/calls"
            : $"{baseUri}/openai/v1/realtime/calls?webrtcfilter=on";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(sdpOffer, Encoding.UTF8, "application/sdp")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", clientSecret);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var answerSdp = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure OpenAI Realtime SDP negotiation failed ({(int)response.StatusCode}): {answerSdp}");
        }

        string? callId = null;
        if (response.Headers.Location is { } location)
        {
            // Azure doesn't always return an ABSOLUTE Location header — when it
            // comes back relative, Uri.Segments throws "This operation is not
            // supported for a relative URI." (2026-10-03 fix: caused every Q&A
            // voice session to fail with a 502). OriginalString works for both.
            var raw = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
            callId = raw.TrimEnd('/').Split('/').LastOrDefault();
        }

        return (answerSdp, callId);
    }

    /// <summary>Opens the server-side observer WebSocket for a given call_id
    /// (2026-10-02) \u2014 receives the FULL, unfiltered event stream (including
    /// response.done's token usage) regardless of the webrtcfilter used on
    /// the browser's own WebRTC connection. Caller owns reading frames;
    /// this just establishes the authenticated connection.</summary>
    public async Task<System.Net.WebSockets.ClientWebSocket> ConnectObserverWebSocketAsync(string callId, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("RealtimeVoiceSessionService is not configured.");
        }

        var wsUri = new Uri(_endpoint!.Replace("https://", "wss://").TrimEnd('/') + $"/openai/v1/realtime?call_id={callId}");
        var socket = new System.Net.WebSockets.ClientWebSocket();
        socket.Options.SetRequestHeader("api-key", _apiKey);
        await socket.ConnectAsync(wsUri, cancellationToken);
        return socket;
    }

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
                    ["transcription"] = new System.Text.Json.Nodes.JsonObject { ["model"] = _transcriptionModel },
                    // FIXED 2026-07-25: this GA REST schema nests turn_detection/
                    // noise_reduction under audio.input (same as transcription
                    // above) — NOT at the top level of `session` like the legacy
                    // Realtime API. The client's WebRTC data-channel
                    // `session.update` was previously sending a top-level
                    // `turn_detection`, which this GA schema silently ignores,
                    // so the connection ran with the server's DEFAULT VAD
                    // sensitivity (much more trigger-happy than intended) the
                    // whole time. Setting it here, nested correctly, from the
                    // very first turn fixes the root cause of the Agent
                    // self-interrupting on its own voice bleeding back into an
                    // open mic (no headphones) and hallucinating a reply to
                    // silence (e.g. "gracias por compartir" when the student
                    // said nothing).
                    ["turn_detection"] = new System.Text.Json.Nodes.JsonObject
                    {
                        ["type"] = "server_vad",
                        // Tuned 2026-08-27: 0.85 never detected interruptions;
                        // 0.5 was too sensitive (agent stopped waiting for the
                        // student to actually finish a thought before
                        // responding/self-interrupting). 0.65 + a longer
                        // silence_duration_ms is the middle ground — still
                        // interruptible, but requires an actual sustained
                        // pause before treating speech as "done".
                        ["threshold"] = 0.65,
                        ["prefix_padding_ms"] = 300,
                        ["silence_duration_ms"] = 650,
                        ["create_response"] = true,
                        ["interrupt_response"] = true
                    },
                    // Azure/OpenAI Realtime's purpose-built fix for exactly
                    // this speaker-bleed-into-mic scenario (no headphones):
                    // suppresses the Agent's own voice picked back up by the
                    // mic before VAD evaluation, instead of just raising the
                    // threshold and hoping. "near_field" fits a laptop/desktop
                    // mic+speakers setup (vs. "far_field" conference-room mics).
                    ["noise_reduction"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "near_field" }
                }
            }
        };


        if (tools is not null)
        {
            sessionNode["tools"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(tools));
            sessionNode["tool_choice"] = "auto";
        }

        var sessionConfig = new { session = sessionNode };
        var sessionConfigJson = JsonSerializer.Serialize(sessionConfig);

        // Quota-retry (2026-08-06): gpt-realtime-mini on twinet-resource is pinned right
        // at its subscription-wide RPM quota ceiling, so a transient 429 here is expected
        // occasionally under load — wait it out instead of failing the whole session mint.
        // HttpRequestMessage is single-use, so it's rebuilt fresh on every attempt.
        var responseBody = await HumanOS.Common.QuotaRetryPolicy.ExecuteAsync(
            async () =>
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(sessionConfigJson, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("api-key", _apiKey);

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new HumanOS.Common.QuotaExceededHttpException(
                        response.Headers.RetryAfter?.Delta,
                        $"Azure OpenAI Realtime client_secrets request was rate-limited (429): {body}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Azure OpenAI Realtime client_secrets request failed ({(int)response.StatusCode}): {body}");
                }

                return body;
            },
            classify: ex => ex is HumanOS.Common.QuotaExceededHttpException qe ? (qe.RetryAfter ?? TimeSpan.Zero) : null,
            cancellationToken);

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
