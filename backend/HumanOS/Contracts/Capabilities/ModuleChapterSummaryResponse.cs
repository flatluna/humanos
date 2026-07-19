namespace HumanOS.Contracts.Capabilities;

/// <summary>
/// Read-only chapter list for a single module — added 2026-07-16 so the
/// Runtime frontend can let a learner "review" (re-read) a previously
/// seen chapter's raw teaching content without touching their live
/// session/progress. Sourced directly from
/// <see cref="HumanOS.Models.Capabilities.CapabilityModuleChapter"/>, no
/// Runtime session or Tutor Agent involved.
/// </summary>
public sealed class ModuleChapterSummaryResponse
{
    public Guid CapabilityModuleId { get; set; }

    public string ModuleTitle { get; set; } = null!;

    public List<ModuleChapterSummary> Chapters { get; set; } = [];
}

public sealed class ModuleChapterSummary
{
    public Guid CapabilityModuleChapterId { get; set; }

    public int SortOrder { get; set; }

    public string Title { get; set; } = null!;

    public string TeachingContent { get; set; } = null!;

    public bool IsPrimaryWeight { get; set; }
}
