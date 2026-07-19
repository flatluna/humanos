namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Deterministic gate for the Production ("Aplícalo") step's formative
/// grading — decides in CODE (never trusted from the LLM) whether a
/// submission counts as correct, same "LLM proposes, code has final say
/// for objectively-checkable rules" discipline as RecallLoopGate and the
/// Assessment Score&gt;=80 cutoff.
///
/// Product decision (confirmed 2026-07-18, "no importa la logica la meta
/// es aprender"): unlike Recall, there is NO attempt cap here — a student
/// may retry as many times as they want. The step only advances once the
/// student actually submits a correct application; the goal is genuine
/// learning, not throughput.
/// </summary>
public static class ProductionEvaluationGate
{
    /// <summary>Minimum score (0-100) counted as a correct application.</summary>
    public const int PassThreshold = 70;

    public static bool IsCorrect(int score) => score >= PassThreshold;
}
