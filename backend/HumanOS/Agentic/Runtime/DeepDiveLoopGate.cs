namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Deterministic gate for the Deep Dive interaction loop — the LLM
/// (TutorAgentV2) only PROPOSES a 0-100 score for the current attempt;
/// THIS class has the final say on whether the current card is mastered
/// and whether the whole step is done, exactly like <see cref="RecallLoopGate"/>.
///
/// Unlike RecallLoopGate (mastering <see cref="RecallLoopGate.ItemsRequiredToAdvance"/>
/// DISTINCT items out of an open-ended, dynamically-generated pool), Deep
/// Dive requires the student to master EVERY SINGLE ExampleCard in the
/// node's coverage pool, strictly in the order they appear — product
/// decision (2026-07-29): "no puede pasar al siguiente sin aprender cada
/// card secuencialmente, de card en card tiene que hacer todas".
///
/// No attempt cap and no regression-to-Teaching path (unlike Recall) — the
/// student simply keeps retrying the SAME card, with the Tutor re-teaching/
/// rephrasing each time, until they clear it. This mirrors
/// ProductionEvaluationGate's more encouraging, non-punitive philosophy
/// rather than Recall's stricter attempt-budget one, since the whole point
/// of Deep Dive is unhurried mastery practice, not a graded checkpoint.
/// </summary>
public static class DeepDiveLoopGate
{
    /// <summary>Minimum score (0-100) counted as mastery of the current card.</summary>
    public const int MasteryThreshold = 80;

    public static bool IsMastered(int score) => score >= MasteryThreshold;

    /// <param name="CardMasteredThisAttempt">True if THIS attempt's score cleared <see cref="MasteryThreshold"/>.</param>
    /// <param name="NextCardIndex">The card index the student should be quizzed on next — unchanged
    /// (retry the same card) when not mastered, or +1 when mastered.</param>
    /// <param name="StepComplete">True once every card (0..totalCards-1) has been mastered in order.</param>
    public readonly record struct Verdict(bool CardMasteredThisAttempt, int NextCardIndex, bool StepComplete);

    /// <param name="currentCardIndex">0-based index of the card the student was just quizzed on.</param>
    /// <param name="totalCards">Total number of ExampleCards in this node's Deep Dive pool.</param>
    /// <param name="latestScore">TutorAgentV2's proposed 0-100 score for THIS attempt.</param>
    public static Verdict Evaluate(int currentCardIndex, int totalCards, int latestScore)
    {
        var mastered = IsMastered(latestScore);
        var nextCardIndex = mastered ? currentCardIndex + 1 : currentCardIndex;
        var stepComplete = mastered && nextCardIndex >= totalCards;

        return new Verdict(mastered, nextCardIndex, stepComplete);
    }
}
