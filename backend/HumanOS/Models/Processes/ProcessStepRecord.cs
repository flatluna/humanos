namespace HumanOS.Models.Processes;

/// <summary>
/// An individual operational work step with rich instructions, activities, and criteria.
/// </summary>
public sealed class ProcessStepRecord
{
    public Guid ProcessStepId { get; set; }

    public Guid ProcessDocumentId { get; set; }

    public ProcessDocumentRecord? ProcessDocument { get; set; }

    public Guid? ProcessChapterId { get; set; }

    public ProcessChapterRecord? ProcessChapter { get; set; }

    public string StepNumber { get; set; } = string.Empty;

    public int OrderIndex { get; set; }

    public string Name { get; set; } = null!;

    public string? Objective { get; set; }

    /// <summary>
    /// JSON array of required activity strings.
    /// </summary>
    public string? ActivitiesJson { get; set; }

    /// <summary>
    /// JSON array of observable human abilities.
    /// </summary>
    public string? ObservableAbilitiesJson { get; set; }

    /// <summary>
    /// JSON array of recommended tools (Teams, Excel, Calendar, etc.).
    /// </summary>
    public string? RequiredToolsJson { get; set; }

    /// <summary>
    /// JSON array of expected deliverables.
    /// </summary>
    public string? DeliverablesJson { get; set; }

    public string? CompletionCondition { get; set; }

    /// <summary>
    /// JSON array of page numbers in the source PDF.
    /// </summary>
    public string? SourcePagesJson { get; set; }

    /// <summary>Verbatim (or near-verbatim) excerpt of the source document covering this step's
    /// instructions — the ONLY input later used for per-step deep analysis and for Studio course
    /// generation (NOT the raw PDF). Persisted here (2026-11-03) so it survives after "Approve &amp;
    /// Save" — it used to live ONLY in blob storage and was lost once the process was saved to SQL.</summary>
    public string? SourceContent { get; set; }

    /// <summary>JSON-serialized list of <see cref="HumanOS.Contracts.ProcessDocuments.ProcessStepImage"/>
    /// — the REAL images (screenshots/diagrams/forms) extracted from the source document's page(s)
    /// covering this step (not AI-generated), so Studio can show the actual figure to learners.
    /// Persisted here (2026-11-03) for the same reason as <see cref="SourceContent"/> above.</summary>
    public string? StepImagesJson { get; set; }

    public bool IsExcluded { get; set; }

    public List<ProcessStepSkillRecord> Skills { get; set; } = [];
}
