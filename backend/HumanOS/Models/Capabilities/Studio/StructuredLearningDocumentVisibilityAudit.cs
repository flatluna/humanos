namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Lightweight audit trail for IsGlobal/IsPublished changes on a
/// <see cref="StructuredLearningDocument"/>, written by
/// UpdateStructuredLearningVisibilityFunction every time the visibility
/// endpoint is called. Added 2026-11-10 after a document's IsGlobal flag
/// was observed flipping back to true with no clear code path responsible —
/// this table lets us answer "who changed this and when" the next time it
/// happens, since the shared dev database has no other change history.
/// </summary>
public sealed class StructuredLearningDocumentVisibilityAudit
{
    public long StructuredLearningDocumentVisibilityAuditId { get; set; }

    public Guid StructuredLearningDocumentId { get; set; }

    public bool OldIsGlobal { get; set; }

    public bool NewIsGlobal { get; set; }

    public bool OldIsPublished { get; set; }

    public bool NewIsPublished { get; set; }

    /// <summary>Caller's Azure AD OID (X-Azure-OID header), when available.</summary>
    public string? ChangedByOid { get; set; }

    /// <summary>Caller's email (X-Azure-Email header), when available.</summary>
    public string? ChangedByEmail { get; set; }

    /// <summary>Caller's tenant (X-Studio-Tenant-Id header), when available.</summary>
    public Guid? ChangedByTenantId { get; set; }

    public DateTime ChangedAtUtc { get; set; }
}
