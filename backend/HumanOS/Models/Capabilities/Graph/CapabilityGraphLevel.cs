using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// A professor-given name for one "climb level" (a computed tier of the
/// prerequisite DAG — see PreviewGraphPage.tsx's groupIntoLevels) of a
/// CapabilityGraph, e.g. "Capítulo 1: Vocales" instead of the generic
/// "Nivel 1" fallback.
///
/// Levels are NOT a persisted structural concept — they are recomputed from
/// <see cref="CapabilityGraphEdge"/> every time the graph is fetched, and can
/// shift if the graph's nodes/edges change. This entity only stores an
/// OPTIONAL display name keyed by the level's current index; if the index
/// no longer matches after a graph edit, the name simply stops being shown
/// (falls back to "Nivel N") rather than being silently wrong — the
/// professor can always rename it again.
/// </summary>
public class CapabilityGraphLevel
{
    /// <summary>Identificador único (GUID).</summary>
    public Guid CapabilityGraphLevelId { get; set; } = Guid.NewGuid();

    /// <summary>FK: CapabilityGraph al que pertenece este nivel.</summary>
    public Guid CapabilityGraphId { get; set; }

    /// <summary>0-based tier index, matching groupIntoLevels' output order.</summary>
    public int LevelIndex { get; set; }

    /// <summary>Professor-given display name (e.g. "Capítulo 1: Vocales", "Unit 1: Learn Vowels").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Fecha UTC de creación.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha UTC de la última actualización del nombre.</summary>
    public DateTime? UpdatedDate { get; set; }

    // === Navigation Properties ===

    /// <summary>Referencia al CapabilityGraph padre.</summary>
    public virtual CapabilityGraph? CapabilityGraph { get; set; }
}
