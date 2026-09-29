using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Calls Kling AI's video generation API (2026-09-24, added as a cheaper
/// alternative to Google Veo — see https://kling.ai/document-api).
/// Single Bearer API key, no JWT signing needed (current API design,
/// unlike the legacy Access/Secret Key scheme). Video generation is a
/// long-running job: <see cref="StartGenerationAsync"/> kicks it off and
/// returns a task id, <see cref="PollAsync"/> must be called repeatedly
/// until the task reports status "succeeded"/"failed".
/// </summary>
public sealed class KlingVideoService
{
    private const string BaseUrl = "https://api-singapore.klingai.com";

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public KlingVideoService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(KlingVideoService));
        _apiKey = configuration["KlingApiKey"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public sealed class PollResult
    {
        public bool Done { get; init; }
        public byte[]? VideoBytes { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>Starts an 8-second, 720p video generation task and returns
    /// the system-generated task id (used to poll for the result). When
    /// <paramref name="imageBytes"/> is provided, uses image-to-video
    /// (the image is sent as raw base64 in the "first_frame" content's
    /// "url" field — Kling accepts either a real URL or base64 there);
    /// otherwise uses text-to-video.</summary>
    public async Task<string> StartGenerationAsync(
        string prompt, byte[]? imageBytes = null, string? imageContentType = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("KlingApiKey is not configured.");
        }

        var hasImage = imageBytes is { Length: > 0 };
        string path;
        object requestBody;

        if (hasImage)
        {
            path = "/image-to-video/kling-3.0";
            requestBody = new
            {
                contents = new object[]
                {
                    new { type = "prompt", text = prompt },
                    new { type = "first_frame", url = Convert.ToBase64String(imageBytes!) },
                },
                settings = new { resolution = "720p", duration = 8, audio = "native" },
            };
        }
        else
        {
            path = "/text-to-video/kling-3.0";
            requestBody = new
            {
                prompt,
                settings = new { resolution = "720p", aspect_ratio = "16:9", duration = 8, audio = "native" },
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{path}");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Kling video generation failed to start ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var code = doc.RootElement.GetProperty("code").GetInt32();
        if (code != 0)
        {
            var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : body;
            throw new InvalidOperationException($"Kling video generation failed to start: {message}");
        }

        return doc.RootElement.GetProperty("data").GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Kling start response did not include a task id.");
    }

    /// <summary>Checks one generation task. When Done=true and no
    /// ErrorMessage, VideoBytes contains the finished .mp4 file, downloaded
    /// from the result's own hotlink-protected URL.</summary>
    public async Task<PollResult> PollAsync(string taskId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/tasks?task_ids={taskId}");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new PollResult { Done = true, ErrorMessage = $"Kling poll failed ({(int)response.StatusCode}): {body}" };
        }

        using var doc = JsonDocument.Parse(body);
        var code = doc.RootElement.GetProperty("code").GetInt32();
        if (code != 0)
        {
            var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : body;
            return new PollResult { Done = true, ErrorMessage = $"Kling poll failed: {message}" };
        }

        var dataArray = doc.RootElement.GetProperty("data");
        if (dataArray.GetArrayLength() == 0)
        {
            return new PollResult { Done = false };
        }

        var task = dataArray[0];
        var status = task.GetProperty("status").GetString();

        if (status == "failed")
        {
            var message = task.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
            return new PollResult { Done = true, ErrorMessage = $"Kling task failed: {message}" };
        }

        if (status != "succeeded")
        {
            return new PollResult { Done = false };
        }

        if (!task.TryGetProperty("outputs", out var outputs) || outputs.GetArrayLength() == 0)
        {
            return new PollResult { Done = true, ErrorMessage = "Kling task succeeded but returned no outputs." };
        }

        string? videoUrl = null;
        foreach (var output in outputs.EnumerateArray())
        {
            if (output.TryGetProperty("type", out var t) && t.GetString() == "video" && output.TryGetProperty("url", out var u))
            {
                videoUrl = u.GetString();
                break;
            }
        }

        if (videoUrl is null)
        {
            return new PollResult { Done = true, ErrorMessage = "Kling task succeeded but no video output URL was found." };
        }

        var videoBytes = await _httpClient.GetByteArrayAsync(videoUrl, cancellationToken);
        return new PollResult { Done = true, VideoBytes = videoBytes };
    }
}
