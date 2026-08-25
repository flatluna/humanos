using System.Text.Json.Serialization;

namespace HumanOS.Services;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StructuredLearningExtractionStage
{
    Running,
    Completed,
    Failed
}

/// <summary>Status snapshot for one in-progress or finished Structured
/// Learning Document extraction run (Step 2 of the new TOC-driven Studio
/// pipeline) — same non-blocking polling pattern as
/// <see cref="TraditionalCourseRunStatus"/>.</summary>
public sealed class StructuredLearningExtractionRunStatus
{
    public Guid RunId { get; set; }

    public StructuredLearningExtractionStage Stage { get; set; }

    public string CurrentStepDescription { get; set; } = string.Empty;

    public int? TotalSections { get; set; }

    public int? CompletedSections { get; set; }

    public string? ErrorMessage { get; set; }

    public StructuredLearningExtractionResult? Result { get; set; }

    public static StructuredLearningExtractionRunStatus Running(
        Guid runId, string step, int? totalSections = null, int? completedSections = null) => new()
    {
        RunId = runId,
        Stage = StructuredLearningExtractionStage.Running,
        CurrentStepDescription = step,
        TotalSections = totalSections,
        CompletedSections = completedSections
    };

    public static StructuredLearningExtractionRunStatus Failed(Guid runId, string error) => new()
    {
        RunId = runId,
        Stage = StructuredLearningExtractionStage.Failed,
        ErrorMessage = error
    };

    public static StructuredLearningExtractionRunStatus Completed(Guid runId, StructuredLearningExtractionResult result) => new()
    {
        RunId = runId,
        Stage = StructuredLearningExtractionStage.Completed,
        CurrentStepDescription = "Completed",
        TotalSections = result.SectionCount,
        CompletedSections = result.SectionCount,
        Result = result
    };
}

/// <summary>Final summary of a completed Structured Learning Document
/// extraction run.</summary>
public sealed class StructuredLearningExtractionResult
{
    public Guid StructuredLearningDocumentId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public int SectionCount { get; set; }

    public int TotalCharacters { get; set; }

    public int TotalTokens { get; set; }
}
