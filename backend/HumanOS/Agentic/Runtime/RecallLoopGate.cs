namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Deterministic gate for the Recall interaction loop — decides in CODE
/// (never trusted from the LLM) whether a student has mastered a single
/// Recall item, exhausted their attempts on it, or fully cleared the
/// Recall step, same "LLM proposes, code has final say for
/// objectively-checkable rules" discipline as ModuleCompletionGate
/// (Studio) and the Score&gt;=70 Assessment cutoff.
///
/// Product decision (confirmed 2026-07-19): Recall now requires
/// genuinely recalling <see cref="ItemsRequiredToAdvance"/> DIFFERENT
/// things (not just one) before the step advances to Production — each
/// item gets its own budget of <see cref="MaxAttemptsPerItem"/> attempts.
/// If the student exhausts that budget on ANY item without mastering it,
/// the step no longer advances anyway with a low score — instead the
/// whole node regresses back to Teaching so the student reviews the
/// concept before trying Recall again (see
/// InstructorRuntimeOrchestrator.RegressToTeachingAsync).
/// </summary>
public static class RecallLoopGate
{
    /// <summary>Minimum RecallScore (0-100) counted as genuine mastery of one item.</summary>
    public const int MasteryThreshold = 85;

    /// <summary>Maximum number of attempts allowed per item before that
    /// item counts as failed (triggers regression back to Teaching).</summary>
    public const int MaxAttemptsPerItem = 5;

    /// <summary>How many distinct items the student must master before the
    /// Recall step advances to Production.</summary>
    public const int ItemsRequiredToAdvance = 3;

    public static bool IsMastered(int recallScore) => recallScore >= MasteryThreshold;

    /// <summary>Verdict for one Recall attempt, computed from every scored
    /// attempt recorded so far during the step's CURRENT activation (i.e.
    /// since it was last (re)started — a prior regression cycle's earlier
    /// attempts must not be included, see TutorService.SubmitRecallAttemptAsync).</summary>
    public readonly record struct Verdict(
        bool ItemMasteredThisAttempt,
        int ItemsMasteredSoFar,
        int AttemptsUsedForCurrentItem,
        bool StepComplete,
        bool ItemFailed);

    /// <param name="priorScoresThisActivation">Every previous attempt's
    /// RecallScore during the step's current activation, in chronological
    /// order (does NOT include <paramref name="latestScore"/>).</param>
    /// <param name="latestScore">The RecallScore of the attempt just made.</param>
    public static Verdict Evaluate(IReadOnlyList<int> priorScoresThisActivation, int latestScore)
    {
        var itemsMastered = 0;
        var attemptsForCurrentItem = 0;

        foreach (var score in priorScoresThisActivation)
        {
            attemptsForCurrentItem++;
            if (IsMastered(score))
            {
                itemsMastered++;
                attemptsForCurrentItem = 0;
            }
        }

        attemptsForCurrentItem++;
        var masteredThisAttempt = IsMastered(latestScore);
        if (masteredThisAttempt)
        {
            itemsMastered++;
            attemptsForCurrentItem = 0;
        }

        var stepComplete = itemsMastered >= ItemsRequiredToAdvance;
        var itemFailed = !stepComplete && !masteredThisAttempt && attemptsForCurrentItem >= MaxAttemptsPerItem;

        return new Verdict(masteredThisAttempt, itemsMastered, attemptsForCurrentItem, stepComplete, itemFailed);
    }
}
