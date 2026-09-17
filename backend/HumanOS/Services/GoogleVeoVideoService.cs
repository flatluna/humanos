using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace HumanOS.Services;

/// <summary>
/// Calls Google's Gemini API to generate short videos with Veo
/// (model "veo-3.1-fast-generate-preview", 2026-08-21 — see
/// https://ai.google.dev/gemini-api/docs/veo). Video generation is a
/// long-running job: <see cref="StartGenerationAsync"/> kicks it off and
/// returns an opaque operation name, <see cref="PollAsync"/> must be called
/// repeatedly until it reports Done=true (typically ~1-3 minutes, up to 6
/// per Google's own docs). Mirrors the "start async job, poll for
/// completion" shape already used elsewhere in this codebase (e.g.
/// StructuredLearningPedagogyOrchestrator) rather than a synchronous call.
/// </summary>
public sealed class GoogleVeoVideoService
{
    private const string ModelName = "veo-3.1-fast-generate-preview";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public GoogleVeoVideoService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(GoogleVeoVideoService));
        _apiKey = configuration["GoogleVeoApiKey"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public sealed class PollResult
    {
        public bool Done { get; init; }
        public byte[]? VideoBytes { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>Starts an 8-second, 720p video generation job and returns
    /// the long-running operation's name (used to poll for the result) —
    /// never the video itself, which is only ready once
    /// <see cref="PollAsync"/> reports Done. When <paramref name="imageBytes"/>
    /// is provided, this becomes image-to-video generation (2026-08-22):
    /// Veo animates the given image as the video's first frame instead of
    /// generating a scene from scratch — per Google's docs, personGeneration
    /// must be "allow_adult" (not "allow_all") for this mode.</summary>
    public async Task<string> StartGenerationAsync(
        string prompt, byte[]? imageBytes = null, string? imageContentType = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("GoogleVeoApiKey is not configured.");
        }

        // Veo generates audio natively, but if the prompt doesn't explicitly
        // ask for it, it frequently renders a near-silent/no-audio track
        // (browsers then show the volume control as disabled). Previously
        // this was skipped whenever the prompt merely CONTAINED a keyword
        // like "voice" or "sound" — but a topic like sign language
        // ("...without using your voice...") contains "voice" while
        // actually describing the ABSENCE of sound, so the model faithfully
        // rendered a silent video. Always append the cue unconditionally;
        // it's harmless to layer on top of a prompt that already mentions
        // audio, and it's the only way to reliably force a real audio track.
        var isSpanish = prompt.StartsWith("Objetivo:", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("Lección a enseñar", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("Escena:", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains(" Dice:", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains(" Dice en español:", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains(" el ", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains(" la ", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains(" de ", StringComparison.OrdinalIgnoreCase);

        var audioCue = isSpanish
            ? " Incluye narración audible y clara en español describiendo la acción en voz alta, sincronizada con el movimiento, más sonido ambiente natural."
            : " Include clear, audible narration describing the action out loud, synchronized with the movement, plus natural ambient sound.";

        var effectivePrompt = $"{prompt}{audioCue}";

        var hasImage = imageBytes is { Length: > 0 };
        var instance = hasImage
            ? new
            {
                prompt = effectivePrompt,
                image = new { bytesBase64Encoded = Convert.ToBase64String(imageBytes!), mimeType = imageContentType ?? "image/png" }
            }
            : (object)new { prompt = effectivePrompt };

        var requestBody = new
        {
            instances = new[] { instance },
            parameters = new { aspectRatio = "16:9", personGeneration = hasImage ? "allow_adult" : "allow_all" }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{BaseUrl}/models/{ModelName}:predictLongRunning");
        request.Headers.Add("x-goog-api-key", _apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Veo video generation failed to start ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("name").GetString()
            ?? throw new InvalidOperationException("Veo start response did not include an operation 'name'.");
    }

    /// <summary>Checks one long-running video generation operation. When
    /// Done=true and no ErrorMessage, VideoBytes contains the finished
    /// .mp4 file, downloaded using the same API key (the file URI Google
    /// returns requires the key to be re-attached to actually fetch bytes).</summary>
    public async Task<PollResult> PollAsync(string operationName, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/{operationName}");
        request.Headers.Add("x-goog-api-key", _apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new PollResult { Done = true, ErrorMessage = $"Veo poll failed ({(int)response.StatusCode}): {body}" };
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var done = root.TryGetProperty("done", out var doneProp) && doneProp.GetBoolean();
        if (!done)
        {
            return new PollResult { Done = false };
        }

        if (root.TryGetProperty("error", out var errorProp))
        {
            return new PollResult { Done = true, ErrorMessage = errorProp.ToString() };
        }

        // Debug aid: the exact response shape only became clear once we
        // could see a REAL completed payload — logging it here (once per
        // completed operation, not per poll) lets us verify assumptions
        // about audio/track shape without guessing blindly again.
        Console.WriteLine($"[GoogleVeo] Completed operation raw response: {body}");
        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "veo-last-response.json"), body);
        }
        catch { /* best-effort debug dump */ }

        var fileUri = TryFindFirstVideoUri(root);
        if (fileUri is null)
        {
            // Google's own safety/processing filter can reject a generation
            // outright — most commonly triggered by quoted spoken dialogue
            // in the prompt (e.g. `she says "..."`) — surfacing as
            // raiMediaFilteredReasons instead of a generatedSamples entry.
            // Surface that real reason instead of a generic "not found".
            if (root.TryGetProperty("response", out var responseElForFilter) &&
                responseElForFilter.TryGetProperty("generateVideoResponse", out var generateVideoResponseElForFilter) &&
                generateVideoResponseElForFilter.TryGetProperty("raiMediaFilteredReasons", out var reasonsEl) &&
                reasonsEl.ValueKind == JsonValueKind.Array)
            {
                var reasons = string.Join(" ", reasonsEl.EnumerateArray().Select(r => r.GetString()));
                return new PollResult { Done = true, ErrorMessage = $"Google blocked this video: {reasons}" };
            }

            return new PollResult { Done = true, ErrorMessage = "Veo operation completed but no video file URI was found in the response." };
        }

        var videoBytes = await DownloadVideoAsync(fileUri, cancellationToken);
        videoBytes = await Mp4FastStart.TryApplyAsync(videoBytes, cancellationToken);
        return new PollResult { Done = true, VideoBytes = videoBytes };
    }

    /// <summary>Prefers the documented exact path
    /// (response.generateVideoResponse.generatedSamples[].video.uri) —
    /// falling back to a generic tree walk ONLY if that exact path isn't
    /// present, since the generic walk can accidentally match an unrelated
    /// "uri" field elsewhere in the payload (e.g. inside "metadata"),
    /// grabbing the wrong file.</summary>
    private static string? TryFindFirstVideoUri(JsonElement element)
    {
        if (element.TryGetProperty("response", out var responseEl) &&
            responseEl.TryGetProperty("generateVideoResponse", out var generateVideoResponseEl) &&
            generateVideoResponseEl.TryGetProperty("generatedSamples", out var samplesEl) &&
            samplesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var sample in samplesEl.EnumerateArray())
            {
                if (sample.TryGetProperty("video", out var videoEl) &&
                    videoEl.TryGetProperty("uri", out var uriEl) &&
                    uriEl.GetString() is { Length: > 0 } exactUri)
                {
                    return exactUri;
                }
            }
        }

        return TryFindAnyVideoUri(element);
    }

    private static string? TryFindAnyVideoUri(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String &&
                    (property.NameEquals("uri") || property.NameEquals("videoUri") || property.NameEquals("fileUri")) &&
                    property.Value.GetString() is { Length: > 0 } candidate && candidate.Contains("http"))
                {
                    return candidate;
                }

                var nested = TryFindAnyVideoUri(property.Value);
                if (nested is not null) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = TryFindAnyVideoUri(item);
                if (nested is not null) return nested;
            }
        }

        return null;
    }

    private async Task<byte[]> DownloadVideoAsync(string fileUri, CancellationToken cancellationToken)
    {
        // The Generative Language Files API returns JSON metadata (not raw
        // bytes) unless '?alt=media' is present — append it defensively if
        // the returned URI doesn't already include it.
        var downloadUri = fileUri.Contains("alt=media", StringComparison.OrdinalIgnoreCase)
            ? fileUri
            : fileUri + (fileUri.Contains('?') ? "&" : "?") + "alt=media";

        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUri);
        request.Headers.Add("x-goog-api-key", _apiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
