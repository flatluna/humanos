using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Request body for <see cref="SynthesizeSpeechFunction"/> (fixed
/// 2026-07-16). <see cref="LanguageCode"/> picks a sensible default voice
/// when <see cref="VoiceName"/> is omitted — see
/// <see cref="SynthesizeSpeechFunction.DefaultVoiceFor"/>.
/// </summary>
public sealed class SynthesizeSpeechRequest
{
    public string Text { get; init; } = string.Empty;

    /// <summary>Explicit Azure neural voice name (e.g. "es-MX-DaliaNeural")
    /// — takes precedence over <see cref="LanguageCode"/> when set.</summary>
    public string? VoiceName { get; init; }

    /// <summary>BCP-47 language code (e.g. "es-MX", "en-US") used to pick a
    /// default voice when <see cref="VoiceName"/> is not given.</summary>
    public string? LanguageCode { get; init; }
}

/// <summary>
/// Real neural Text-to-Speech endpoint (fixed 2026-07-16 — explicit user
/// requirement: direct call to Azure AI Speech's neural voices, no LLM/
/// chatbot involved). Returns raw MP3 bytes (Content-Type: audio/mpeg),
/// not JSON — the frontend plays it directly via an <c>&lt;audio&gt;</c>
/// element / <c>Audio</c> object, replacing the browser-native
/// <c>speechSynthesis</c> Capa 1 fallback with real neural voice quality.
/// </summary>
public sealed class SynthesizeSpeechFunction
{
    private readonly AzureSpeechService _speechService;

    public SynthesizeSpeechFunction(AzureSpeechService speechService)
    {
        _speechService = speechService;
    }

    /// <summary>Small, explicit default-voice map by language code (fixed
    /// 2026-07-16) — Human OS content is Spanish-first (see
    /// /memories/repo/human-os-core-philosophy.md), so "es-MX-DaliaNeural"
    /// is the overall default. Extend this map, never invent per-call
    /// logic elsewhere, as more languages are added.</summary>
    internal static string DefaultVoiceFor(string? languageCode) => languageCode switch
    {
        "es-ES" => "es-ES-ElviraNeural",
        "es-MX" or null or "" => "es-MX-DaliaNeural",
        "en-US" => "en-US-AvaNeural",
        "en-GB" => "en-GB-SoniaNeural",
        "pt-BR" => "pt-BR-FranciscaNeural",
        "fr-FR" => "fr-FR-DeniseNeural",
        "de-DE" => "de-DE-KatjaNeural",
        "ja-JP" => "ja-JP-NanamiNeural",
        _ => "es-MX-DaliaNeural"
    };

    [Function("SynthesizeSpeech")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "speech/synthesize")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_speechService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "SpeechServiceNotConfigured",
                "AzureSpeechService is not configured (missing AzureSpeechKey/AzureSpeechRegion).",
                cancellationToken);
        }

        var body = await request.ReadFromJsonAsync<SynthesizeSpeechRequest>(cancellationToken)
            ?? new SynthesizeSpeechRequest();

        if (string.IsNullOrWhiteSpace(body.Text))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingText",
                "Text is required.", cancellationToken);
        }

        var voiceName = string.IsNullOrWhiteSpace(body.VoiceName)
            ? DefaultVoiceFor(body.LanguageCode)
            : body.VoiceName;

        try
        {
            var audioBytes = await _speechService.SynthesizeAsync(body.Text, voiceName, cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "audio/mpeg");
            await response.Body.WriteAsync(audioBytes, cancellationToken);
            return response;
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "SpeechSynthesisFailed", ex.Message, cancellationToken);
        }
    }
}
