namespace HumanOS.Models.Capabilities;

/// <summary>
/// Which of Human OS Studio's two course-creation methods produced this
/// Capability's content (2026-08-09 "curso tradicional" feature — see
/// /memories/repo). Orthogonal to every other Capability field.
/// </summary>
public enum CourseLearningMode
{
    /// <summary>The original "Memory Paradox" method: a <see cref="Graph.CapabilityGraph"/>
    /// of nodes (Hypothesis/Teaching/DeepDive/Recall/Production/Assessment
    /// per node). Default — every Capability created before this enum
    /// existed is this mode.</summary>
    GraphMemoryParadox = 0,

    /// <summary>The traditional Coursera-style method: linear chapters,
    /// each with lessons made of reading cards (no Recall, no Production,
    /// no per-card "examine" interaction) and ONE final multiple-choice
    /// exam per chapter. See <see cref="Traditional.CapabilityChapter"/>.</summary>
    TraditionalChapters = 1
}
