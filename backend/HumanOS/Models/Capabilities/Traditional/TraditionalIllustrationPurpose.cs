namespace HumanOS.Models.Capabilities.Traditional;

/// <summary>What a <see cref="CapabilityTraditionalIllustration"/> is used
/// for — much simpler than Graph mode's <see cref="Graph.IllustrationPurpose"/>
/// since traditional courses have no Hypothesis/Teaching distinction.</summary>
public enum TraditionalIllustrationPurpose
{
    /// <summary>Course-level cover image (one per Capability).</summary>
    Cover = 0,

    /// <summary>One reading card's illustration (one per <see cref="CapabilityLessonCard"/>).</summary>
    Card = 1,

    /// <summary>One exam question's illustration (one per <see cref="CapabilityChapterExamQuestion"/>).</summary>
    ExamQuestion = 2
}
