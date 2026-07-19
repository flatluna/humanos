using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// Metadata for one illustration image generated for a CapabilityGraphNode.
/// The actual image binary lives in Azure Data Lake — this table only stores
/// the pointer/metadata (StoragePath) plus generation provenance, never the
/// image bytes themselves.
///
/// Storage path convention (see CapabilityGraphIllustrationStorageService):
///   {tenantId}/capability-graphs/{capabilityId}/{nodeId}/image-{n}.png
/// (IDs only — never human-readable names — to keep paths stable across
/// renames and avoid leaking content through the path itself.)
/// </summary>
public class CapabilityGraphNodeIllustration
{
    /// <summary>Identificador único de la ilustración (GUID).</summary>
    public Guid CapabilityGraphNodeIllustrationId { get; set; } = Guid.NewGuid();

    /// <summary>FK: CapabilityGraphNode al que pertenece esta ilustración.</summary>
    public Guid CapabilityGraphNodeId { get; set; }

    /// <summary>
    /// Path del blob en Azure Data Lake, p.ej.
    /// "{tenantId}/capability-graphs/{capabilityId}/{nodeId}/image-01.png".
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>Prompt de texto usado para generar la imagen (de GraphNodeDto.IllustrationPrompts).</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Leyenda/caption descriptiva de la imagen para uso en UI.</summary>
    public string? Caption { get; set; }

    /// <summary>Which step this illustration is meant to be reused by
    /// (Hypothesis = before-state only, never reveals the answer; Teaching =
    /// full worked example). See <see cref="IllustrationPurpose"/> remarks for
    /// why this exists.</summary>
    public IllustrationPurpose Purpose { get; set; }

    /// <summary>Modelo de generación de imagen usado (p.ej. "gpt-image-1.5").</summary>
    public string ImageModel { get; set; } = string.Empty;

    /// <summary>Ancho de la imagen en píxeles.</summary>
    public int Width { get; set; }

    /// <summary>Alto de la imagen en píxeles.</summary>
    public int Height { get; set; }

    /// <summary>Fecha UTC de creación de la ilustración.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al CapabilityGraphNode padre.</summary>
    public virtual CapabilityGraphNode? CapabilityGraphNode { get; set; }
}
