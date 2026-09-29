namespace HumanOS.Models.Learning;

/// <summary>One graded student answer to one exercise inside a <see cref="PracticeSession"/>.
/// Upserted per (PracticeSessionId, ExerciseIndex) — re-answering replaces the previous grade.</summary>
public sealed class PracticeAttempt
{
    public Guid PracticeAttemptId { get; set; }

    public Guid PracticeSessionId { get; set; }

    public int ExerciseIndex { get; set; }

    public string StudentAnswer { get; set; } = string.Empty;

    /// <summary>0-100, from TecPracticeAgent.GradeAnswerAsync.</summary>
    public int Score { get; set; }

    public string Feedback { get; set; } = string.Empty;

    /// <summary>Points actually credited via GamificationOrchestrator for this exercise
    /// (0 if this exact exercise was already rewarded before — idempotent ledger).</summary>
    public int PointsAwarded { get; set; }

    public DateTime CompletedDate { get; set; }
}
