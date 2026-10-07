namespace HumanOS.Contracts.Tenancy;

/// <summary>One course card for a tenant's "Cursos" tab (2026-10-01) — only
/// includes Capabilities/Structured Learning Documents actually CREATED
/// (owned) by this tenant, never purely-global content authored elsewhere.
/// <see cref="IsGlobal"/> tells whether this tenant's own course has ALSO
/// been published to the shared/global catalog (only meaningful for
/// Structured Learning Documents today — see doc comment on
/// <see cref="TenantCoursesService"/>).</summary>
public sealed class TenantCourseCardResponse
{
    public Guid CourseId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>"Capability" | "StructuredLearning".</summary>
    public string CourseType { get; set; } = string.Empty;

    public bool IsGlobal { get; set; }

    public string? LanguageCode { get; set; }

    public DateTime CreatedDate { get; set; }
}
