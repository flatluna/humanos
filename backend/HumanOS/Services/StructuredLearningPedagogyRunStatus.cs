using System.Text.Json.Serialization;

namespace HumanOS.Services;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StructuredLearningPedagogyStage
{
    Running,
    Completed,
    Failed
}

/// <summary>Status snapshot for one in-progress or finished Step 3
/// (pedagogy generation) run — same non-blocking polling pattern as
/// <see cref="StructuredLearningExtractionRunStatus"/>.</summary>
public sealed class StructuredLearningPedagogyRunStatus
{
    public Guid RunId { get; set; }

    /// <summary>The document this run belongs to — set on every status so
    /// a caller with no other context (e.g. the global "active run" banner,
    /// which never saw this run's own RunId) can still link back to the
    /// right document (2026-08-30).</summary>
    public Guid DocumentId { get; set; }

    public string? DocumentName { get; set; }

    public StructuredLearningPedagogyStage Stage { get; set; }

    public string CurrentStepDescription { get; set; } = string.Empty;

    public int? TotalSections { get; set; }

    public int? CompletedSections { get; set; }

    /// <summary>Titles of sections currently being generated right now —
    /// plural because sections are processed with bounded concurrency, not
    /// one at a time.</summary>
    public IReadOnlyList<string>? ActiveSectionTitles { get; set; }

    /// <summary>1-based inclusive engram range this run was scoped to, if the
    /// designer chose "redo engrams X to Y" instead of the whole document —
    /// lets the UI say "Redoing engrams X to Y" instead of a misleading
    /// "0/Total remaining sections done" (2026-09-20).</summary>
    public int? FromEngramNumber { get; set; }

    public int? ToEngramNumber { get; set; }

    public string? ErrorMessage { get; set; }

    public StructuredLearningPedagogyResult? Result { get; set; }

    public static StructuredLearningPedagogyRunStatus Running(
        Guid runId, Guid documentId, string? documentName, string step, int? totalSections, int? completedSections, IReadOnlyList<string>? activeSectionTitles,
        int? fromEngramNumber = null, int? toEngramNumber = null) => new()
    {
        RunId = runId,
        DocumentId = documentId,
        DocumentName = documentName,
        Stage = StructuredLearningPedagogyStage.Running,
        CurrentStepDescription = step,
        TotalSections = totalSections,
        CompletedSections = completedSections,
        ActiveSectionTitles = activeSectionTitles,
        FromEngramNumber = fromEngramNumber,
        ToEngramNumber = toEngramNumber
    };

    public static StructuredLearningPedagogyRunStatus Failed(Guid runId, Guid documentId, string error) => new()
    {
        RunId = runId,
        DocumentId = documentId,
        Stage = StructuredLearningPedagogyStage.Failed,
        ErrorMessage = error
    };

    public static StructuredLearningPedagogyRunStatus Completed(Guid runId, Guid documentId, StructuredLearningPedagogyResult result) => new()
    {
        RunId = runId,
        DocumentId = documentId,
        Stage = StructuredLearningPedagogyStage.Completed,
        CurrentStepDescription = "Completed",
        TotalSections = result.SectionsCompleted + result.SectionsFailed,
        CompletedSections = result.SectionsCompleted,
        Result = result
    };
}

/// <summary>Final summary of a completed (or partially completed) Step 3 run.</summary>
public sealed class StructuredLearningPedagogyResult
{
    public Guid StructuredLearningDocumentId { get; set; }

    public int SectionsCompleted { get; set; }

    /// <summary>Sections whose generated content failed the quality
    /// guardrails (see <see cref="HumanOS.Agents.Studio.StructuredLearningSectionEvaluator"/>)
    /// or errored — left at PedagogyStatus=Failed, so a later re-run of the
    /// same document only retries these (never re-pays for already-Completed ones).</summary>
    public int SectionsFailed { get; set; }

    public int TotalInputTokens { get; set; }

    public int TotalOutputTokens { get; set; }
}
