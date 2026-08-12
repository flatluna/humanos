namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One real-time Recall practice attempt for a Structured Learning
/// section's Recall step (2026-08-10) — replaces the OLD design where
/// <see cref="StructuredLearningSectionDesignerAgent"/> baked a FIXED list
/// of Recall items once at generation time. Mirrors the Graph/Memory
/// Paradox pipeline's <c>LearningEvidence</c>-via-<c>TutorAgentV2</c>
/// pattern: <see cref="TutorPromptShown"/> is the question asked (by
/// <see cref="Agents.Studio.StructuredLearningRecallAgent"/>) right before
/// this attempt, generated fresh each time — never a static bank.
///
/// Keyed directly by <see cref="StructuredLearningDocumentSectionId"/> —
/// same single-active-progress-per-section simplification as
/// <see cref="StructuredLearningAssessmentRound"/> (no per-student session
/// concept exists yet for Structured Learning).
/// </summary>
public class StructuredLearningRecallAttempt
{
    public Guid StructuredLearningRecallAttemptId { get; set; } = Guid.NewGuid();

    public Guid StructuredLearningDocumentSectionId { get; set; }

    /// <summary>The question this attempt answers (asked by the PREVIOUS turn).</summary>
    public string TutorPromptShown { get; set; } = string.Empty;

    public string StudentResponse { get; set; } = string.Empty;

    /// <summary>0-100 — always set (only graded attempts are persisted; the
    /// very first "bootstrap" call that only fetches Q1 with nothing to
    /// grade yet is never persisted as an attempt row).</summary>
    public int RecallScore { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public StructuredLearningDocumentSection Section { get; set; } = null!;
}
