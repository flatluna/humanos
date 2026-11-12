using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public interface IProcessWideLearningRecommendationAnalyzer
{
    bool IsConfigured { get; }
    string? DeploymentName { get; }

    Task<ProcessWideLearningRecommendationResult> AnalyzeProcessAsync(
        string documentId,
        string processName,
        List<ProcessChapter> chapters,
        string? webFindings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Second pass: given an already-identified, FIXED list of skill gaps (names/categories must not change)
    /// and internet search findings labeled per skill (via "### Skill: &lt;name&gt;" headers), extracts only the
    /// real courses found under each skill's own labeled section and assigns them to that gap. Gaps whose
    /// labeled section has no real course get an empty "recommendedCourses" and a warning instead.
    /// </summary>
    Task<List<ProcessSkillGapItem>> AssignCoursesToGapsAsync(
        string processName,
        List<ProcessSkillGapItem> gaps,
        string labeledFindings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PHASE 0: given the fixed list of skill gaps and the tenant's existing dbo.Skill catalog
    /// (name + description only, no course content), marks each gap as "isFromCatalog" when an
    /// existing catalog skill genuinely already covers it — those gaps skip internet search
    /// entirely. Gaps with no good catalog match are returned unchanged (isFromCatalog = false)
    /// so the caller proceeds to the normal internet-search phases for them.
    /// </summary>
    Task<List<ProcessSkillGapItem>> MatchGapsToCatalogAsync(
        List<ProcessSkillGapItem> gaps,
        List<CatalogSkillEntry> catalogSkills,
        CancellationToken cancellationToken = default);
}
