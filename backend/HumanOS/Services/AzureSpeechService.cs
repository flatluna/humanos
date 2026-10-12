using System.Net.Http.Headers;
using System.Security;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Real neural Text-to-Speech via Azure AI Speech's plain REST endpoints
/// (fixed 2026-07-16 — explicit user requirement: no LLM/chatbot
/// involved, a direct call to the Azure Speech service's neural voices).
/// </summary>
/// <remarks>
/// Deliberately uses the plain REST token + synthesis endpoints
/// (<c>issuetoken</c> + <c>cognitiveservices/v1</c>) instead of the
/// <c>Microsoft.CognitiveServices.Speech</c> SDK's WebSocket-based
/// <c>SpeechSynthesizer</c> — found via live testing 2026-07-16: the SDK
/// call failed with "WebSocket upgrade failed: Authentication error
/// (401)" using a key/region PROVEN valid moments earlier via the same
/// key against the plain REST <c>issuetoken</c> endpoint (which
/// succeeded). This strongly suggests a network/firewall layer blocking
/// or mangling the WebSocket (wss://) handshake specifically, not an
/// actual credential problem — plain HTTPS REST calls are far more
/// likely to work through restrictive networks/proxies than a raw
/// WebSocket upgrade.
/// </remarks>
public sealed partial class AzureSpeechService
{
    private static readonly HttpClient HttpClient = new();

    private readonly string? _key;
    private readonly string? _region;

    public AzureSpeechService(IConfiguration configuration)
    {
        _key = configuration["AzureSpeechKey"];
        _region = configuration["AzureSpeechRegion"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_key) && !string.IsNullOrWhiteSpace(_region);

    /// <summary>
    /// Synthesizes <paramref name="text"/> into MP3 audio bytes using the
    /// given neural voice (e.g. "es-MX-DaliaNeural"). Plain text is
    /// escaped into SSML. SSML prosody/break tags (2026-10-08) are inserted
    /// automatically from sentence punctuation to fix "robotic, no pauses"
    /// feedback on the Spanish neural voices — plain punctuation alone was
    /// not giving the synthesizer enough of a breathing gap between
    /// sentences.
    /// </summary>
    public async Task<byte[]> SynthesizeAsync(
        string text, string voiceName, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "AzureSpeechService is not configured (missing AzureSpeechKey/AzureSpeechRegion).");
        }

        var accessToken = await IssueAccessTokenAsync(cancellationToken);

        // Derive the voice's locale from its name (e.g. "es-MX-CecilioNeural"
        // -> "es-MX") for the SSML <voice> element's xml:lang attribute.
        var locale = voiceName.Length >= 5 ? voiceName[..5] : "es-MX";

        var ssml =
            $"""
            <speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="{locale}">
              <voice xml:lang="{locale}" name="{voiceName}"><prosody rate="-8%">{InsertNaturalPauses(SecurityElement.Escape(text))}</prosody></voice>
            </speak>
            """;


        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"https://{_region}.tts.speech.microsoft.com/cognitiveservices/v1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Microsoft-OutputFormat", "audio-16khz-32kbitrate-mono-mp3");
        request.Headers.UserAgent.ParseAdd("HumanOS");
        request.Content = new StringContent(ssml, System.Text.Encoding.UTF8, "application/ssml+xml");

        using var response = await HttpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Azure Speech synthesis failed ({(int)response.StatusCode}): {errorBody}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <summary>Exchanges the subscription key for a short-lived (10 min)
    /// bearer token via the STS <c>issuetoken</c> endpoint — required by
    /// the plain REST synthesis endpoint (unlike the SDK, which handles
    /// this internally).</summary>
    private async Task<string> IssueAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"https://{_region}.api.cognitive.microsoft.com/sts/v1.0/issuetoken");
        request.Headers.Add("Ocp-Apim-Subscription-Key", _key);
        request.Content = new StringContent(string.Empty);

        using var response = await HttpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Azure Speech token request failed ({(int)response.StatusCode}): {errorBody}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>Inserts short SSML &lt;break&gt; pauses after sentence-ending
    /// and comma punctuation so narration breathes naturally instead of
    /// running sentences together in a flat, robotic cadence — applied to
    /// already-XML-escaped text (punctuation characters are never escaped,
    /// so this is safe to run after <see cref="SecurityElement.Escape"/>).</summary>
    private static string InsertNaturalPauses(string escapedText) =>
        NaturalPauseSentenceEnd().Replace(
            NaturalPauseComma().Replace(escapedText, "$1<break time=\"180ms\"/> "),
            "$1<break time=\"450ms\"/> ");

    [System.Text.RegularExpressions.GeneratedRegex(@"([.!?…])\s+")]
    private static partial System.Text.RegularExpressions.Regex NaturalPauseSentenceEnd();

    [System.Text.RegularExpressions.GeneratedRegex(@"(,)\s+")]
    private static partial System.Text.RegularExpressions.Regex NaturalPauseComma();
}
