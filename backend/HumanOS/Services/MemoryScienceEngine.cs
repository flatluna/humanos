namespace HumanOS.Services;

/// <summary>
/// Neuroscience-based memory stability and retrievability calculations.
/// Implements the power-law forgetting curve (FSRS / Wixted &amp; Ebbesen):
/// R(t, S) = (1 + t / (9 * S)) ^ -1
/// Provides exact parity with the employee portal's client-side memoryScience.ts.
/// </summary>
public static class MemoryScienceEngine
{
    private static readonly double[] StabilityDaysByReview = [1.0, 1.0, 3.0, 7.0, 15.0, 30.0];
    private const int AutomaticityReviewThreshold = 5;
    private const double AutomaticStabilityDays = 3650.0; // ~10 years (procedural memory)

    /// <summary>
    /// Gets stability (S) in days based on cumulative review count.
    /// </summary>
    public static double GetStabilityDays(int reviewCount)
    {
        if (reviewCount >= AutomaticityReviewThreshold)
        {
            return AutomaticStabilityDays;
        }

        if (reviewCount <= 0)
        {
            return StabilityDaysByReview[0];
        }

        if (reviewCount < StabilityDaysByReview.Length)
        {
            return StabilityDaysByReview[reviewCount];
        }

        int extraReviews = reviewCount - (StabilityDaysByReview.Length - 1);
        return StabilityDaysByReview[^1] + (extraReviews * 30.0);
    }

    /// <summary>
    /// Checks if the engram has transitioned into automatic/procedural memory (5+ reviews).
    /// </summary>
    public static bool IsAutomatic(int reviewCount) => reviewCount >= AutomaticityReviewThreshold;

    /// <summary>
    /// Calculates retrievability R in [0.0, 1.0] from the last completion date and review count.
    /// </summary>
    public static double? CalculateRetrievability(DateTime? lastCompletedDate, int reviewCount, DateTime? nowUtc = null)
    {
        if (!lastCompletedDate.HasValue)
        {
            return null;
        }

        var now = nowUtc ?? DateTime.UtcNow;
        double elapsedDays = Math.Max(0.0, (now - lastCompletedDate.Value).TotalDays);
        double stability = GetStabilityDays(reviewCount);

        return 1.0 / (1.0 + (elapsedDays / (9.0 * stability)));
    }

    /// <summary>
    /// Calculates recall percent in [0, 100].
    /// </summary>
    public static int CalculateRecallPercent(DateTime? lastCompletedDate, int reviewCount, DateTime? nowUtc = null)
    {
        var r = CalculateRetrievability(lastCompletedDate, reviewCount, nowUtc);
        if (!r.HasValue)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Round(r.Value * 100.0), 0, 100);
    }

    /// <summary>
    /// Determines memory state label: "Strong" | "Fading" | "AlmostForgotten".
    /// </summary>
    public static string DetermineMemoryState(int recallPercent)
    {
        if (recallPercent >= 75)
        {
            return "Strong";
        }

        if (recallPercent >= 50)
        {
            return "Fading";
        }

        return "AlmostForgotten";
    }

    /// <summary>
    /// Estimates when projected recall drops to ~70% (optimal spaced review target).
    /// R(t, S) = 0.70 =&gt; t = (27 / 7) * S ~= 3.857 * S days from last review.
    /// </summary>
    public static DateTime? CalculateNextReviewDate(DateTime? lastCompletedDate, int reviewCount)
    {
        if (!lastCompletedDate.HasValue)
        {
            return null;
        }

        double stability = GetStabilityDays(reviewCount);
        double daysUntilReview = (27.0 / 7.0) * stability;

        return lastCompletedDate.Value.AddDays(daysUntilReview);
    }
}
