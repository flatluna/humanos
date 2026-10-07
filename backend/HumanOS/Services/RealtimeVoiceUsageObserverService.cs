using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services;

/// <summary>Context carried through one observed voice call \u2014 everything
/// needed to flush its accumulated token usage into the right
/// StructuredLearningEngramRuntimeUsage row once the call ends.</summary>
public sealed class RealtimeVoiceUsageContext
{
    public Guid? PersonId { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid? EngramSessionId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string? StepType { get; set; }
}

/// <summary>
/// Singleton that negotiates the SDP offer on the backend's behalf (2026-10-02)
/// so it learns the Azure-assigned call_id, then opens its OWN server-side
/// observer WebSocket for that call \u2014 receiving the FULL Realtime event
/// stream (including response.done's token usage) independently of whatever
/// the browser's own WebRTC data channel receives. Accumulates input/output
/// tokens IN MEMORY per call (never per-turn SQL writes \u2014 that would add
/// per-utterance latency) and persists exactly ONE row update when the
/// observer socket closes (the call ended, however it ended: student hung
/// up, closed the tab, or lost connection \u2014 Azure closes this socket too,
/// so a dropped tab still gets flushed without any browser cooperation).
/// A per-call idle timeout is a safety net in case Azure never closes it.
/// </summary>
public sealed class RealtimeVoiceUsageObserverService
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(20);

    private readonly RealtimeVoiceSessionService _voiceSessionService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RealtimeVoiceUsageObserverService> _logger;

    public RealtimeVoiceUsageObserverService(
        RealtimeVoiceSessionService voiceSessionService,
        IServiceScopeFactory scopeFactory,
        ILogger<RealtimeVoiceUsageObserverService> logger)
    {
        _voiceSessionService = voiceSessionService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Negotiates the SDP offer against Azure and, if a call_id
    /// comes back, starts a background observer for it. Returns the SDP
    /// answer to send back to the browser \u2014 the observer runs independently
    /// and never blocks this call.</summary>
    public async Task<string> NegotiateAndObserveAsync(
        string clientSecret, string sdpOffer, bool usesTools, RealtimeVoiceUsageContext context, CancellationToken cancellationToken)
    {
        var (answerSdp, callId) = await _voiceSessionService.NegotiateSdpAsync(clientSecret, sdpOffer, usesTools, cancellationToken);

        if (!string.IsNullOrWhiteSpace(callId))
        {
            _ = Task.Run(() => ObserveCallAsync(callId, context), CancellationToken.None);
        }
        else
        {
            _logger.LogError("Realtime call has no call ID; token usage cannot be observed for section {SectionId}", context.SectionId);
        }

        return answerSdp;
    }

    private async Task ObserveCallAsync(string callId, RealtimeVoiceUsageContext context)
    {
        long inputTokens = 0;
        long outputTokens = 0;
        var seenResponses = new HashSet<string>();

        try
        {
            using var idleCts = new CancellationTokenSource(IdleTimeout);
            using var socket = await _voiceSessionService.ConnectObserverWebSocketAsync(callId, idleCts.Token);
            var buffer = new byte[16 * 1024];

            while (socket.State == WebSocketState.Open)
            {
                using var messageStream = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    idleCts.CancelAfter(IdleTimeout);
                    result = await socket.ReceiveAsync(buffer, idleCts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    messageStream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;

                var (input, output) = ExtractUsage(messageStream.ToArray(), seenResponses, _logger);
                inputTokens += input;
                outputTokens += output;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Realtime voice usage observer for call {CallId} ended unexpectedly", callId);
        }
        finally
        {
            await FlushUsageAsync(context, (int)inputTokens, (int)outputTokens);
        }
    }

    internal static (long InputTokens, long OutputTokens) ExtractUsage(byte[] payload,
        HashSet<string> seenResponses, ILogger logger)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String || typeElement.GetString() != "response.done")
            {
                return (0, 0);
            }
            if (!doc.RootElement.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object
                || !response.TryGetProperty("usage", out var usage))
            {
                return (0, 0);
            }

            if (usage.ValueKind == JsonValueKind.Null) return (0, 0);
            if (!response.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(id.GetString())
                || usage.ValueKind != JsonValueKind.Object
                || !usage.TryGetProperty("input_tokens", out var i) || i.ValueKind != JsonValueKind.Number || !i.TryGetInt64(out var input)
                || !usage.TryGetProperty("output_tokens", out var o) || o.ValueKind != JsonValueKind.Number || !o.TryGetInt64(out var output)
                || input < 0 || output < 0)
            {
                logger.LogWarning("Realtime response.done contains invalid usage or response ID; not counted");
                return (0, 0);
            }
            if (!seenResponses.Add(id.GetString()!)) return (0, 0);
            return (input, output);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid JSON in Realtime usage event; not counted");
            return (0, 0);
        }
    }

    private async Task FlushUsageAsync(RealtimeVoiceUsageContext context, int inputTokens, int outputTokens)
    {
        if (inputTokens <= 0 && outputTokens <= 0) return;

        using var scope = _scopeFactory.CreateScope();
        var usageService = scope.ServiceProvider.GetRequiredService<StructuredLearningEngramUsageService>();
        await usageService.RecordVoiceUsageAsync(
            context.PersonId, context.DocumentId, context.SectionId, context.EngramSessionId,
            context.ModelName, durationSeconds: 0, inputTokens, outputTokens, CancellationToken.None, context.StepType);
    }
}
