namespace HumanOS.Models.Capabilities.Traditional;

/// <summary>
/// One "subchapter" of a <see cref="CapabilityChapter"/> — a single
/// learnable topic taught purely through an ordered set of reading
/// <see cref="CapabilityLessonCard"/>s (no Recall/Production/per-card
/// "examine" interaction, unlike Graph mode's DeepDive step).
/// </summary>
public class CapabilityLesson
{
    public Guid CapabilityLessonId { get; set; } = Guid.NewGuid();

    /// <summary>FK: the parent chapter.</summary>
    public Guid CapabilityChapterId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>1-based reading order within the chapter.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    public virtual CapabilityChapter? CapabilityChapter { get; set; }

    public virtual ICollection<CapabilityLessonCard> Cards { get; set; } = new List<CapabilityLessonCard>();
}
