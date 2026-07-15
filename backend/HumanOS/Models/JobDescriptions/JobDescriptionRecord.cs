using HumanOS.Models.People;

namespace HumanOS.Models.JobDescriptions;

/// <summary>
/// A structured Job Description extracted by
/// <see cref="HumanOS.Agents.JobDescriptionExtractionAgent"/> from a
/// source PDF the employee (or organization) uploaded to Data Lake.
///
/// This is intentionally kept separate from any "confirmed role
/// requirements" concept: everything here starts as an agent proposal
/// (<see cref="ExtractionStatus"/> = "Extracted") and only becomes usable
/// as context for a Development Plan once an employee reviews and
/// confirms it (<see cref="ExtractionStatus"/> = "Confirmed",
/// <see cref="ConfirmedDate"/> set). The agent extracts and proposes; the
/// employee reviews and confirms — this record captures both states so
/// the extraction stays auditable even after confirmation.
/// </summary>
public sealed class JobDescriptionRecord
{
    public Guid JobDescriptionId { get; set; }

    public Guid TenantId { get; set; }

    public Guid PersonId { get; set; }

    /*
     * Source document (the uploaded PDF/DOCX in Data Lake).
     */

    /// <summary>
    /// Blob path within the tenant's Data Lake container (container name
    /// = TenantId, folder = "jobdescriptions" — see
    /// RoleDocumentStorageService.UploadJobDescriptionAsync).
    /// </summary>
    public string SourceStoragePath { get; set; } = null!;

    public string SourceFileName { get; set; } = null!;

    public DateTime SourceUploadedDate { get; set; }

    /*
     * Structured content proposed by the extraction agent. Provisional
     * until ExtractionStatus reaches "Confirmed".
     */

    public string JobTitle { get; set; } = null!;

    public string? RolePurpose { get; set; }

    public string? RoleSummary { get; set; }

    /// <summary>JSON-serialized string[] — kept simple rather than a
    /// separate child table, since these are always read/written as a
    /// whole list alongside the rest of the extraction.</summary>
    public string PrimaryResponsibilitiesJson { get; set; } = "[]";

    public string ExpectedOutcomesJson { get; set; } = "[]";

    public string? RequiredExperience { get; set; }

    public string ToolsMentionedJson { get; set; } = "[]";

    /// <summary>The agent's suggested professional level (e.g. "Senior") —
    /// a proposal for the employee to confirm, never auto-applied to
    /// PersonProfile.</summary>
    public string? SuggestedProfessionalLevel { get; set; }

    /*
     * Extraction / audit metadata.
     */

    /// <summary>Pending | Extracted | Failed | Confirmed.</summary>
    public string ExtractionStatus { get; set; } = "Pending";

    /// <summary>The LLM deployment/model used for extraction (e.g.
    /// "gpt-5-mini"), for audit purposes.</summary>
    public string? ExtractionModel { get; set; }

    /// <summary>The raw JSON returned by the extraction agent, kept for
    /// audit even if the structured columns above are later edited during
    /// employee review.</summary>
    public string? RawExtractionJson { get; set; }

    public DateTime? ExtractedDate { get; set; }

    public DateTime? ConfirmedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;
}
