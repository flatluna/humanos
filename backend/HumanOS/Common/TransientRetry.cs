namespace HumanOS.Common;

/// <summary>
/// Bounded-retry helper for a whole multi-step content-generation operation
/// (e.g. design + illustrate + persist a NodeExperienceBlueprint), as
/// opposed to <see cref="QuotaRetryPolicy"/> which retries a SINGLE HTTP
/// call on a 429. One flaky attempt anywhere in the wrapped operation
/// (an LLM hiccup, a transient DB blip, an image-quota burst that outlasts
/// QuotaRetryPolicy's own shorter per-call budget) used to permanently
/// leave a node without its blueprint — see /memories/repo notes on the
/// 2026-08-06 "Control de Edición" orphaned-node bug. Retries the ENTIRE
/// operation from scratch; callers must be safe to re-run (idempotent
/// enough that a retry after a partial failure doesn't duplicate rows —
/// true for blueprint design+persist, since PersistAsync only runs once
/// the whole in-memory design succeeded).
/// </summary>
internal static class TransientRetry
{
    private static readonly TimeSpan[] DefaultDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
    ];

    /// <summary>Runs <paramref name="action"/>, retrying up to
    /// <paramref name="maxAttempts"/> times total (so maxAttempts-1 retries)
    /// on any exception other than cancellation. <paramref name="onRetry"/>
    /// is invoked (for logging) before each wait/retry. Rethrows the last
    /// exception once attempts are exhausted.</summary>
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        int maxAttempts,
        CancellationToken cancellationToken,
        Action<Exception, int>? onRetry = null,
        TimeSpan[]? delays = null)
    {
        delays ??= DefaultDelays;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < maxAttempts)
            {
                onRetry?.Invoke(ex, attempt);
                var delay = delays[Math.Min(attempt - 1, delays.Length - 1)];
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
