namespace HumanOS.Contracts.Tenancy;

/// <summary>One course's image/video generation counts, for the tenant
/// dashboard's "Imágenes y videos" histogram (2026-10-01) — only courses
/// actually CREATED (owned) by this tenant, same scope as
/// <see cref="TenantCourseCardResponse"/>.</summary>
public sealed class TenantCourseMediaBreakdownResponse
{
    public Guid CourseId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>"Capability" | "StructuredLearning".</summary>
    public string CourseType { get; set; } = string.Empty;

    public int ImagesCount { get; set; }

    public int VideosCount { get; set; }
}
