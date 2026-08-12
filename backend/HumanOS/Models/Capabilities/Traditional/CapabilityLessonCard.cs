namespace HumanOS.Models.Capabilities.Traditional;

/// <summary>
/// One reading card within a <see cref="CapabilityLesson"/> — pure content,
/// no "examine"/graded interaction (unlike Graph mode's DeepDive
/// ExampleCard, which requires the student to answer about each card
/// before advancing). The student just reads Title/Text (optionally
/// illustrated) and moves to the next card.
/// </summary>
public class CapabilityLessonCard
{
    public Guid CapabilityLessonCardId { get; set; } = Guid.NewGuid();

    /// <summary>FK: the parent lesson.</summary>
    public Guid CapabilityLessonId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    /// <summary>1-based reading order within the lesson.</summary>
    public int SortOrder { get; set; }

    /// <summary>Optional FK to a generated illustration for this card.</summary>
    public Guid? CapabilityTraditionalIllustrationId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    public virtual CapabilityLesson? CapabilityLesson { get; set; }

    public virtual CapabilityTraditionalIllustration? Illustration { get; set; }
}
