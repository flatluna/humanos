namespace HumanOS.Models.Capabilities.Traditional;

/// <summary>
/// One top-level chapter of a <see cref="CourseLearningMode.TraditionalChapters"/>
/// Capability — the "level" of this simpler, linear course method. Owns an
/// ordered list of <see cref="CapabilityLesson"/> (its "subchapters") plus
/// ONE final multiple-choice <see cref="CapabilityChapterExamQuestion"/> set.
/// </summary>
public class CapabilityChapter
{
    public Guid CapabilityChapterId { get; set; } = Guid.NewGuid();

    /// <summary>FK: the traditional-mode Capability this chapter belongs to.</summary>
    public Guid CapabilityId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>1-based reading order.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    public virtual Capability? Capability { get; set; }

    public virtual ICollection<CapabilityLesson> Lessons { get; set; } = new List<CapabilityLesson>();

    /// <summary>The chapter's final exam questions — all multiple-choice,
    /// no free-text grading, no per-node Recall/Production.</summary>
    public virtual ICollection<CapabilityChapterExamQuestion> ExamQuestions { get; set; } = new List<CapabilityChapterExamQuestion>();
}
