using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Real, non-speech sound-effect generation via ElevenLabs' Sound Generation
/// API (2026-10-08) — deliberately separate from <see cref="AzureSpeechService"/>
/// (which only produces spoken-word TTS). This service exists specifically
/// for Engram Cards whose teaching point depends on the child hearing a
/// CONCRETE real-world sound (an animal, a machine, nature, an object, a
/// physical impact like clapping) — never a voice saying words, which is
/// already fully covered by the narration's own TTS and needs no separate
/// generation. See <see cref="HumanOS.Models.Capabilities.Studio.EngramCard.SoundEffectLabel"/>.
/// </summary>
public sealed class ElevenLabsSoundEffectService
{
    private static readonly HttpClient HttpClient = new();
    private const string SoundGenerationEndpoint = "https://api.elevenlabs.io/v1/sound-generation";

    private readonly string? _apiKey;

    public ElevenLabsSoundEffectService(IConfiguration configuration)
    {
        _apiKey = configuration["ElevenLabsApiKey"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    /// <summary>
    /// Generates a short (default ~3s) real sound-effect clip (MP3 bytes)
    /// from a concrete sound label, e.g. "Mugido de vaca", "Sonido de
    /// tren", "Dos palmadas". The label is used almost verbatim as the
    /// prompt — ElevenLabs' sound-generation model already handles short,
    /// concrete descriptions well; no extra prompt engineering needed.
    /// </summary>
    public async Task<byte[]> GenerateAsync(
        string soundLabel, double durationSeconds, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "ElevenLabsSoundEffectService is not configured (missing ElevenLabsApiKey).");
        }

        var clampedDuration = Math.Clamp(durationSeconds, 0.5, 22.0);
        var payload = JsonSerializer.Serialize(new
        {
            text = soundLabel,
            duration_seconds = clampedDuration,
            prompt_influence = 0.3,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, SoundGenerationEndpoint);
        request.Headers.Add("xi-api-key", _apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"ElevenLabs sound generation failed ({(int)response.StatusCode}): {errorBody}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
