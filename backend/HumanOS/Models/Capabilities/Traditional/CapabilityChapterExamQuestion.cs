namespace HumanOS.Models.Capabilities.Traditional;

/// <summary>
/// One multiple-choice question in a <see cref="CapabilityChapter"/>'s
/// final exam. Grading is 100% deterministic (selected index ==
/// <see cref="CorrectOptionIndex"/>, in code) — no LLM-graded free text,
/// unlike Graph mode's Assessment step.
/// </summary>
public class CapabilityChapterExamQuestion
{
    public Guid CapabilityChapterExamQuestionId { get; set; } = Guid.NewGuid();

    /// <summary>FK: the chapter this question belongs to.</summary>
    public Guid CapabilityChapterId { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    /// <summary>JSON-serialized List&lt;string&gt; of answer options (typically 4).</summary>
    public string OptionsJson { get; set; } = "[]";

    /// <summary>0-based index into the deserialized Options list — the correct answer.</summary>
    public int CorrectOptionIndex { get; set; }

    /// <summary>1-based order within the chapter's exam.</summary>
    public int SortOrder { get; set; }

    /// <summary>Optional FK to a generated illustration for this question.</summary>
    public Guid? CapabilityTraditionalIllustrationId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    public virtual CapabilityChapter? CapabilityChapter { get; set; }

    public virtual CapabilityTraditionalIllustration? Illustration { get; set; }
}
