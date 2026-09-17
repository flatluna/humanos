using HumanOS.Models.Tenancy;
using HumanOS.Models.OrgStructure;

namespace HumanOS.Models.Processes;

/// <summary>
/// Persisted process document header and metadata.
/// Belongs to a tenant whose Blob container holds the raw/structured JSON artifacts.
/// </summary>
public sealed class ProcessDocumentRecord
{
    public Guid ProcessDocumentId { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public Guid? DepartmentId { get; set; }

    public Department? Department { get; set; }

    public Guid? JobRoleId { get; set; }

    public JobRole? JobRole { get; set; }

    public string Title { get; set; } = null!;

    public string? Code { get; set; }

    public string Version { get; set; } = "1.0";

    public string? Description { get; set; }

    /// <summary>
    /// Consolidated full text extracted from the process manual,
    /// enabling fast SQL search and RAG indexing.
    /// </summary>
    public string? FullText { get; set; }

    /// <summary>
    /// Tenant container name in Blob Storage (e.g. {tenantId}).
    /// </summary>
    public string StorageContainer { get; set; } = string.Empty;

    /// <summary>
    /// Relative blob path to the structured JSON representation.
    /// </summary>
    public string StorageBlobPath { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft"; // Draft, Analyzed, Approved, Published

    public int TotalStepsCount { get; set; }

    public int UniqueSkillsCount { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    public List<ProcessChapterRecord> Chapters { get; set; } = [];

    public List<ProcessStepRecord> Steps { get; set; } = [];
}
