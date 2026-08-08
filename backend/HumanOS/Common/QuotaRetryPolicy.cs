namespace HumanOS.Common;

/// <summary>
/// Shared retry helper for Azure OpenAI calls that can be momentarily
/// throttled by a 429 "quota exceeded" response (2026-08-06: gpt-realtime-mini
/// and gpt-image-2 both run pinned right at their subscription-wide RPM
/// quota ceiling on twinet-resource — see /memories/repo dual-resource
/// migration notes). Retries ONLY when <paramref name="classify"/> says so,
/// honoring a server-provided Retry-After delay when available, otherwise a
/// short exponential backoff — total retry budget capped at ~60 seconds so
/// one busy minute doesn't crash the caller's whole request, it just waits
/// briefly and tries again.
/// </summary>
internal static class QuotaRetryPolicy
{
    private static readonly TimeSpan MaxTotalWait = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(25),
    ];

    /// <summary>
    /// Runs <paramref name="action"/>, retrying while <paramref name="classify"/>
    /// returns a non-null suggested delay for a thrown exception (TimeSpan.Zero
    /// means "quota error, no server-provided delay — use the default backoff").
    /// Returns null (not retryable, or budget/attempts exhausted) to rethrow.
    /// </summary>
    /// <param name="maxTotalWait">
    /// Overrides the default ~60s total retry budget. Only raise this for
    /// calls made from background pipelines (never a live interactive
    /// request like the Realtime voice session token fetch) — e.g. image
    /// generation during course creation, where gpt-image-2's occasional
    /// longer/hourly-style throttling needs more room than a 60s budget
    /// gives it (2026-08-06).
    /// </param>
    /// <param name="backoffDelays">Overrides the default backoff steps used
    /// when the server gives no explicit Retry-After delay.</param>
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        Func<Exception, TimeSpan?> classify,
        CancellationToken cancellationToken,
        TimeSpan? maxTotalWait = null,
        TimeSpan[]? backoffDelays = null)
    {
        var totalWaitBudget = maxTotalWait ?? MaxTotalWait;
        var delays = backoffDelays ?? BackoffDelays;
        var elapsed = TimeSpan.Zero;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                var suggestedDelay = classify(ex);
                if (suggestedDelay is null || attempt >= delays.Length || elapsed >= totalWaitBudget)
                    throw;

                var delay = suggestedDelay.Value > TimeSpan.Zero
                    ? suggestedDelay.Value
                    : delays[attempt];
                if (elapsed + delay > totalWaitBudget)
                    delay = totalWaitBudget - elapsed;
                if (delay <= TimeSpan.Zero)
                    throw;

                await Task.Delay(delay, cancellationToken);
                elapsed += delay;
            }
        }
    }
}

/// <summary>Thrown internally by services that make raw HttpClient calls
/// (rather than going through the OpenAI/System.ClientModel SDK, whose own
/// exceptions already carry a status code) to signal a 429 to
/// <see cref="QuotaRetryPolicy"/>, optionally carrying the server's
/// Retry-After delay.</summary>
internal sealed class QuotaExceededHttpException(TimeSpan? retryAfter, string message) : Exception(message)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
