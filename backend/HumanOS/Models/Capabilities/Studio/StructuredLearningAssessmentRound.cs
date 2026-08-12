namespace HumanOS.Models.Capabilities.Studio;

/// <summary>Mirrors <see cref="HumanOS.Models.Learning.AssessmentRoundStatus"/> for
/// the Structured Learning pipeline's own real-time Assessment.</summary>
public enum StructuredLearningAssessmentRoundStatus
{
    InProgress,
    Passed,
    Failed
}

/// <summary>
/// One real-time Assessment attempt cycle for a Structured Learning
/// section's Assessment step (2026-08-10) — replaces the OLD design where
/// <see cref="StructuredLearningSectionDesignerAgent"/> baked a FIXED set
/// of questions once at generation time. Mirrors
/// <see cref="HumanOS.Models.Learning.AssessmentRound"/> 1:1: each round is
/// N questions generated fresh, one at a time, never a static bank.
///
/// Keyed directly by <see cref="StructuredLearningDocumentSectionId"/>
/// (no per-student session concept exists yet for Structured Learning,
/// same limitation as the rest of this pipeline) — a future per-student
/// runtime can extend this with a session/person FK without needing to
/// redesign the generation logic itself.
/// </summary>
public class StructuredLearningAssessmentRound
{
    public Guid StructuredLearningAssessmentRoundId { get; set; } = Guid.NewGuid();

    public Guid StructuredLearningDocumentSectionId { get; set; }

    public int RoundNumber { get; set; }

    public StructuredLearningAssessmentRoundStatus Status { get; set; } = StructuredLearningAssessmentRoundStatus.InProgress;

    /// <summary>Average of the round's questions' ScoreContribution (0-100). Null while InProgress.</summary>
    public int? FinalScore { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedDate { get; set; }

    public StructuredLearningDocumentSection Section { get; set; } = null!;

    public ICollection<StructuredLearningAssessmentQuestionAttempt> Questions { get; set; } = new List<StructuredLearningAssessmentQuestionAttempt>();
}
