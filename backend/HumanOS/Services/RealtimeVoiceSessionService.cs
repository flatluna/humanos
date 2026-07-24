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

        /// <summary>The exact URL the browser should POST its SDP offer to
        /// (includes <c>?webrtcfilter=on</c>, which limits the data-channel
        /// events Azure sends back to the browser to a safe subset that
        /// never echoes the session's <c>instructions</c> back to the
        /// client — see the WebRTC doc's own recommendation).</summary>
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
    public async Task<EphemeralSession> CreateEphemeralSessionAsync(
        string instructions, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "RealtimeVoiceSessionService is not configured. Set 'AzureOpenAIEndpoint', " +
                "'AzureOpenAIRealtimeDeploymentName' and 'AzureOpenAIApiKey' application settings.");
        }

        var baseUri = _endpoint!.TrimEnd('/');
        var requestUri = $"{baseUri}/openai/v1/realtime/client_secrets";

        var sessionConfig = new
        {
            session = new
            {
                type = "realtime",
                model = _deploymentName,
                instructions,
                audio = new
                {
                    output = new { voice = _voice },
                    // Lets the browser receive `conversation.item.input_audio_transcription.completed`
                    // events with a text transcript of what the student said — used by the Recall
                    // step's voice flow to fill the existing answer textbox (see VoiceTutorAgent.tsx),
                    // WITHOUT this channel ever grading anything itself.
                    input = new { transcription = new { model = _transcriptionModel } }
                }
            }
        };

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
            RealtimeCallsUrl = $"{baseUri}/openai/v1/realtime/calls?webrtcfilter=on",
            Model = _deploymentName!,
            Voice = _voice,
            ExpiresAtUnixSeconds = expiresAt
        };
    }
}
