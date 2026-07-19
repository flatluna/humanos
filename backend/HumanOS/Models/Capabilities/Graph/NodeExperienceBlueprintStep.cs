using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// One step of a NodeExperienceBlueprint's fixed "Memory Paradox" sequence
/// (Hypothesis → Teaching → Recall → Production → Assessment). SortOrder
/// always mirrors <see cref="ExperienceStepType"/>'s numeric value (0-4) —
/// the order is part of the pedagogical contract and is never reshuffled.
/// </summary>
public class NodeExperienceBlueprintStep
{
    /// <summary>Identificador único del step (GUID).</summary>
    public Guid NodeExperienceBlueprintStepId { get; set; } = Guid.NewGuid();

    /// <summary>FK: NodeExperienceBlueprint al que pertenece este step.</summary>
    public Guid NodeExperienceBlueprintId { get; set; }

    /// <summary>Tipo de step dentro del Memory Paradox (Hypothesis/Teaching/Recall/Production/Assessment).</summary>
    public ExperienceStepType StepType { get; set; }

    /// <summary>Contenido pedagógico del step (texto generado a partir de los campos del nodo indicados por su StepType).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// IDs (JSON array de Guid) de las CapabilityGraphNodeIllustration YA EXISTENTES
    /// que este step reutiliza. NUNCA se generan ni duplican imágenes nuevas aquí —
    /// esto es solo una referencia a filas que ya viven en CapabilityGraphNodeIllustration
    /// (y cuyo binario ya vive en Azure Data Lake).
    /// </summary>
    public string? ReferencedIllustrationIdsJson { get; set; }

    /// <summary>Orden fijo del step (0=Hypothesis .. 4=Assessment) — mirrors ExperienceStepType's numeric value.</summary>
    public int SortOrder { get; set; }

    /// <summary>Fecha UTC de creación del step.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al NodeExperienceBlueprint padre.</summary>
    public virtual NodeExperienceBlueprint? NodeExperienceBlueprint { get; set; }
}
