namespace HumanOS.Models.Capabilities.Traditional;

/// <summary>
/// Metadata for one illustration image generated for a traditional
/// (Coursera-style) course — the actual image binary lives in Azure Data
/// Lake (same "capability-graphs" container/service as Graph-mode
/// illustrations, see CapabilityGraphIllustrationStorageService, reused
/// as-is since it only needs opaque GUIDs to build the blob path). This
/// table only stores the pointer/metadata.
/// </summary>
public class CapabilityTraditionalIllustration
{
    public Guid CapabilityTraditionalIllustrationId { get; set; } = Guid.NewGuid();

    /// <summary>FK: the Capability (course) this illustration belongs to.</summary>
    public Guid CapabilityId { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string? Caption { get; set; }

    public TraditionalIllustrationPurpose Purpose { get; set; }

    public string ImageModel { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
