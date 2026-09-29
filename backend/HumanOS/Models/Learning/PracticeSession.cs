namespace HumanOS.Models.Learning;

/// <summary>
/// "Practica y refuerza" (Tec Contigo, 2026-09-28) — one set of AI-generated reinforcement
/// exercises for a course/Capability the student already completed. Generated on demand
/// (one click) and cached here so reopening the tab never regenerates it. Individual
/// answers/scores live in <see cref="PracticeAttempt"/>.
/// </summary>
public sealed class PracticeSession
{
    public Guid PracticeSessionId { get; set; }

    public Guid PersonId { get; set; }

    public Guid CapabilityId { get; set; }

    public string CapabilityName { get; set; } = string.Empty;

    /// <summary>Serialized <c>List&lt;TecPracticeExercise&gt;</c> (Agents.TecPracticeExerciseSet.Exercises).</summary>
    public string ExercisesJson { get; set; } = "[]";

    public DateTime CreatedDate { get; set; }
}
