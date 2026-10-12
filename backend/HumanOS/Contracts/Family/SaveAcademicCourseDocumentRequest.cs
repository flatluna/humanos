namespace HumanOS.Contracts.Family;

/// <summary>Request body to persist (upsert, keyed by <see cref="CourseCode"/>) the
/// complete academic course document authored by the ProfesorExpert AI agent —
/// see <see cref="HumanOS.Services.Family.FamilyCapabilityService.SaveAcademicCourseDocumentAsync"/>.</summary>
public sealed class SaveAcademicCourseDocumentRequest
{
    public string CourseCode { get; set; } = null!;

    public string TitleEs { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string GradeLevel { get; set; } = string.Empty;

    public string Language { get; set; } = "es";

    public int EstimatedHours { get; set; } = 35;

    public string CourseDocumentMarkdown { get; set; } = string.Empty;

    public string? TeacherIntroduction { get; set; }

    public string? PedagogicalApproach { get; set; }

    /// <summary>Serialized JSON of the architecture/pipeline summary (research, groundedness, etc.)
    /// kept as-is for traceability without needing its own relational columns.</summary>
    public string? ArchitectureSummaryJson { get; set; }

    public bool IsApprovedByEducator { get; set; }

    public string? EducatorReviewNotes { get; set; }
}

/// <summary>Response returned after a successful save, confirming the document identity.</summary>
public sealed class SaveAcademicCourseDocumentResponse
{
    public Guid DocumentId { get; set; }

    public string CourseCode { get; set; } = null!;

    public DateTime UpdatedDate { get; set; }
}

/// <summary>Full saved academic course document, returned by the GET lookup endpoint.</summary>
public sealed class AcademicCourseDocumentResponse
{
    public Guid DocumentId { get; set; }

    public string CourseCode { get; set; } = null!;

    public string TitleEs { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string GradeLevel { get; set; } = string.Empty;

    public string Language { get; set; } = "es";

    public int EstimatedHours { get; set; }

    public string CourseDocumentMarkdown { get; set; } = string.Empty;

    public string? TeacherIntroduction { get; set; }

    public string? PedagogicalApproach { get; set; }

    public string? ArchitectureSummaryJson { get; set; }

    public bool IsApprovedByEducator { get; set; }

    public string? EducatorReviewNotes { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}

/// <summary>Lightweight listing row for the "Catálogo Académico" page —
/// see FamilyCapabilityService.ListAcademicCourseDocumentsAsync.</summary>
public sealed class AcademicCourseDocumentSummaryResponse
{
    public string CourseCode { get; set; } = null!;

    public string TitleEs { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string GradeLevel { get; set; } = string.Empty;

    public string Language { get; set; } = "es";

    public int EstimatedHours { get; set; }

    public int CourseDocumentMarkdownLength { get; set; }

    public bool IsApprovedByEducator { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
