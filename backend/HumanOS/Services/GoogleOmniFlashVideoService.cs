using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace HumanOS.Services;

/// <summary>
/// Calls Google's Interactions API to generate short videos with Gemini
/// Omni Flash (model "gemini-omni-flash-preview", 2026-08-22 — see
/// https://ai.google.dev/gemini-api/docs/omni). Unlike Veo's
/// predictLongRunning shape, the Interactions API call here is made
/// synchronously (background=false, store=false, stream=false, per
/// Google's own "optimized performance" guidance) and returns the finished
/// video inline as base64 in one response — no separate poll step needed.
/// Reuses the same "GoogleVeoApiKey" app setting as <see cref="GoogleVeoVideoService"/>
/// since both are the same Gemini API key/project.
/// </summary>
public sealed class GoogleOmniFlashVideoService
{
    private const string ModelName = "gemini-omni-flash-preview";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public GoogleOmniFlashVideoService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(GoogleOmniFlashVideoService));
        _apiKey = configuration["GoogleVeoApiKey"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public sealed class GenerateResult
    {
        public byte[]? VideoBytes { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>Generates an 8-second video synchronously (per Google's
    /// docs this typically completes in well under the Veo's multi-minute
    /// long-running-job time) — text-to-video, or image-to-video when
    /// <paramref name="imageBytes"/> is provided (sets the image as the
    /// first frame via the "image_to_video" task).</summary>
    public async Task<GenerateResult> GenerateAsync(
        string prompt, byte[]? imageBytes = null, string? imageContentType = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("GoogleVeoApiKey is not configured.");
        }

        // Same rationale as GoogleVeoVideoService: always append an explicit
        // audio cue so the model doesn't render a near-silent track.
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
        object requestBody;
        if (hasImage)
        {
            requestBody = new
            {
                model = ModelName,
                input = new object[]
                {
                    new { type = "image", data = Convert.ToBase64String(imageBytes!), mime_type = imageContentType ?? "image/png" },
                    new { type = "text", text = effectivePrompt }
                },
                generation_config = new { video_config = new { task = "image_to_video" } },
                background = false,
                store = false,
                stream = false,
            };
        }
        else
        {
            requestBody = new
            {
                model = ModelName,
                input = effectivePrompt,
                response_format = new { type = "video", aspect_ratio = "16:9" },
                background = false,
                store = false,
                stream = false,
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/interactions");
        request.Headers.Add("x-goog-api-key", _apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GenerateResult { ErrorMessage = $"Omni Flash video generation failed ({(int)response.StatusCode}): {body}" };
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("status", out var statusEl) && statusEl.GetString() is { } status && status != "completed")
        {
            return new GenerateResult { ErrorMessage = $"Omni Flash interaction did not complete (status: {status})." };
        }

        var base64Video = TryFindVideoData(root, out var errorReason);
        if (base64Video is null)
        {
            return new GenerateResult { ErrorMessage = errorReason ?? "Omni Flash interaction completed but no video data was found in the response." };
        }

        var videoBytes = Convert.FromBase64String(base64Video);
        videoBytes = await Mp4FastStart.TryApplyAsync(videoBytes, cancellationToken);
        return new GenerateResult { VideoBytes = videoBytes };
    }

    /// <summary>Walks the documented "steps[].content[]" shape looking for
    /// the model_output video step's inline base64 "data" field. Also
    /// surfaces any explicit safety-block reason found alongside it (Omni
    /// Flash applies the same kind of content filters as Veo).</summary>
    private static string? TryFindVideoData(JsonElement root, out string? errorReason)
    {
        errorReason = null;
        if (!root.TryGetProperty("steps", out var stepsEl) || stepsEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var step in stepsEl.EnumerateArray())
        {
            if (!step.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "model_output") continue;
            if (!step.TryGetProperty("content", out var contentEl) || contentEl.ValueKind != JsonValueKind.Array) continue;

            foreach (var contentItem in contentEl.EnumerateArray())
            {
                if (contentItem.TryGetProperty("type", out var itemTypeEl) && itemTypeEl.GetString() == "video" &&
                    contentItem.TryGetProperty("data", out var dataEl) && dataEl.GetString() is { Length: > 0 } data)
                {
                    return data;
                }
            }
        }

        return null;
    }
}
