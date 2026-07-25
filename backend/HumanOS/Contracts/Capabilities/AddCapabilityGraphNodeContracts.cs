using System;
using System.Collections.Generic;

namespace HumanOS.Contracts.Capabilities;

/// <summary>
/// Request body for POST /capabilities/{capabilityId}/graph/nodes — Capability
/// Studio's "professor" tool for inserting a brand-new learning node into an
/// already-persisted CapabilityGraph.
///
/// "Nivel" in the Studio UI (see capabilitystudio/PreviewGraphPage.tsx's
/// groupIntoLevels) is never a stored column — it is the node's depth in the
/// Requires-edge DAG. So RequiresNodeIds/RequiredByNodeIds are what actually
/// place the new node at the desired level: connecting it to require one or
/// more nodes from the level below puts it exactly one level above them.
/// </summary>
public sealed class AddCapabilityGraphNodeRequest
{
    /// <summary>Free-text instruction from the professor describing what the
    /// new node should teach (e.g. "Un nodo sobre fracciones equivalentes").</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Existing CapabilityGraphNode ids the new node REQUIRES
    /// (prerequisites — typically the nodes at the level directly below the
    /// desired level). May be empty for a brand-new foundational node.</summary>
    public List<Guid> RequiresNodeIds { get; set; } = [];

    /// <summary>Existing CapabilityGraphNode ids that should now REQUIRE the
    /// new node (dependents — typically nodes at the level directly above).
    /// Optional: leave empty to add the node without rewiring anything
    /// downstream of it.</summary>
    public List<Guid> RequiredByNodeIds { get; set; } = [];

    /// <summary>Whether to generate Hypothesis/Teaching illustrations for the
    /// new node the same way the original capability-creation pipeline does.
    /// Defaults to true; best-effort — a failure here never blocks node
    /// creation.</summary>
    public bool GenerateIllustrations { get; set; } = true;
}

/// <summary>Response body confirming the new node was persisted.</summary>
public sealed class AddCapabilityGraphNodeResponse
{
    public Guid CapabilityGraphNodeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public int IllustrationsGenerated { get; set; }

    /// <summary>Whether a NodeExperienceBlueprint (Memory Paradox steps) was
    /// successfully generated and persisted for the new node. If false, the
    /// node was still created but cannot yet be previewed/taught — see
    /// <see cref="BlueprintWarning"/> for why.</summary>
    public bool BlueprintGenerated { get; set; }

    /// <summary>Best-effort failure reason when <see cref="BlueprintGenerated"/> is false.</summary>
    public string? BlueprintWarning { get; set; }
}
