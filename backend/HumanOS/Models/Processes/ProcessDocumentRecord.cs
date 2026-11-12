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

    /// <summary>Container-prefixed blob path (same shape as <see cref="StorageBlobPath"/>) of the
    /// "enriched" PDF generated at save time — index + extracted text + correctly-matched images, inline.
    /// When present, this is the file handed to Studio for course generation instead of the original
    /// source PDF (see ProcessDocumentFunction.SaveProcessAsCapability).</summary>
    public string? EnrichedPdfStoragePath { get; set; }

    /// <summary>Display file name for <see cref="EnrichedPdfStoragePath"/>.</summary>
    public string? EnrichedPdfFileName { get; set; }

    public int TotalStepsCount { get; set; }

    public int UniqueSkillsCount { get; set; }

    /// <summary>When true, this process applies to every Department/Role in the
    /// tenant and <see cref="ScopedDepartments"/>/<see cref="ScopedJobRoles"/>
    /// are ignored. Distinct from the single-value <see cref="DepartmentId"/>/
    /// <see cref="JobRoleId"/> above (those are unused by the current UI).</summary>
    public bool AppliesToAllDepartments { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    public List<ProcessChapterRecord> Chapters { get; set; } = [];

    public List<ProcessStepRecord> Steps { get; set; } = [];

    /// <summary>Departments this process must be followed by (many-to-many; ignored when <see cref="AppliesToAllDepartments"/> is true).</summary>
    public List<ProcessDocumentDepartment> ScopedDepartments { get; set; } = [];

    /// <summary>Job Roles this process must be followed by (many-to-many; ignored when <see cref="AppliesToAllDepartments"/> is true).</summary>
    public List<ProcessDocumentJobRole> ScopedJobRoles { get; set; } = [];

    /// <summary>Skill gaps from the Step 4 whole-process Learning Recommendation that HR
    /// confirmed as actual training requirements for this process.</summary>
    public List<ProcessSkillRequirement> SkillRequirements { get; set; } = [];

    /// <summary>Set once this whole process has been saved to the Capability Catalog
    /// (see ProcessDocumentFunction.SaveProcessAsCapability) as a "department-processes"
    /// Skill — lets re-saving update the same catalog entry instead of duplicating it.</summary>
    public Guid? CapabilitySkillId { get; set; }
}
