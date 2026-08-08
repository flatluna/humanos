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

    /// <summary>
    /// How to obtain this node's illustration(s) (2026-08-06 "imagen del
    /// profesor" feature). One of: "None" (skip illustrations entirely),
    /// "AiGenerated" (default — same as before this feature existed: the AI
    /// invents everything from GraphNodeAdditionAgent's IllustrationPrompts,
    /// optionally guided by the capability's own brand logo if one is set),
    /// "UploadDirect" (the professor's own <see cref="UploadedImageBase64"/>
    /// is used AS-IS as the node's single illustration — no AI image
    /// generation at all; its visual content is described via vision AI and
    /// stored as the illustration's Caption), or "AiGeneratedFromUpload" (the
    /// AI still designs and generates the illustration(s) from
    /// IllustrationPrompts, but uses <see cref="UploadedImageBase64"/> as a
    /// reference image — image-edit, not plain text-to-image — instead of
    /// the capability's brand logo). Always best-effort: a failure here
    /// never blocks node creation.
    /// </summary>
    public string IllustrationMode { get; set; } = "AiGenerated";

    /// <summary>Base64-encoded reference/source image (no data: URL prefix).
    /// Required when <see cref="IllustrationMode"/> is "UploadDirect" or
    /// "AiGeneratedFromUpload"; ignored for "None"/"AiGenerated".</summary>
    public string? UploadedImageBase64 { get; set; }

    /// <summary>MIME content type of <see cref="UploadedImageBase64"/> (e.g.
    /// "image/png", "image/jpeg"). Required whenever UploadedImageBase64 is set.</summary>
    public string? UploadedImageContentType { get; set; }

    /// <summary>
    /// 0-based chapter/level index to assign the new node to (2026-08-06 fix
    /// — see /memories/repo notes on nodes silently landing at the end of
    /// chapter-graded graphs). Capability Studio's frontend computes this
    /// from whichever level card the professor clicked "+ Agregar nodo" on
    /// (see PreviewGraphPage.tsx's groupIntoLevels / AddGraphNodeModal.tsx),
    /// reading an existing sibling node's own ChapterIndex in that level.
    /// Null for capability graphs that still use the older edge-depth
    /// tiering (no node has ChapterIndex set) — in that case the new node's
    /// level continues to be computed automatically from the chosen
    /// Requires/RequiredBy wiring, exactly as before this feature existed.
    /// </summary>
    public int? TargetChapterIndex { get; set; }
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
