import { apiGet } from './httpClient';

/** Node states as computed server-side by GraphProgressionEngine.GetFullGraphAsync. */
export type CapabilityGraphNodeState = 'Locked' | 'Available' | 'Mastered';

/** Backend response shape (camelCase — FunctionResponseFactory serializes
 * with JsonSerializerDefaults.Web) for a single graph node. */
export interface BackendCapabilityGraphNode {
  capabilityGraphNodeId: string;
  name: string;
  description?: string;
  sortOrder: number;
  state: CapabilityGraphNodeState;
  illustrationId?: string;
  /** Best passed assessment score (0-100) of the most recent completed
   * attempt at this node. Undefined while the node isn't Mastered yet. */
  finalScore?: number;
  /** ISO date string of this person's MOST RECENT completed attempt at this
   * node (max CompletedDate across all their Completed LearningSessionNode
   * rows for it). Undefined while the node isn't Mastered yet. Used to show
   * recency ("hace cuánto lo repasaste") without a separate call per node. */
  lastCompletedDate?: string;
  /** How many times this person has genuinely mastered this node (distinct
   * Completed + Passed attempts). Drives the client-side forgetting-curve
   * "stability" used by the Brain recall grid's memory-strength ring. 0
   * while the node isn't Mastered yet. */
  reviewCount?: number;
  /** True once this node's Deep Dive step has been fully mastered (every
   * example card, in order) — can happen well before the node's overall
   * `state` becomes 'Mastered' (that still requires passing Assessment).
   * Drives a small distinct "star" badge on the node bubble, separate from
   * the large Mastered-state star icon (see DeepDiveLoopGate.cs /
   * GraphProgressionEngine.GetDeepDiveMasteredNodeIdsAsync). */
  deepDiveMastered?: boolean;
}

/** RelationshipType enum values from backend (Requires=0, BuildsOn=1). */
export type BackendRelationshipType = 0 | 1;

export interface BackendCapabilityGraphEdge {
  sourceNodeId: string;
  targetNodeId: string;
  relationshipType: BackendRelationshipType;
}

/** One key named entity explicitly grounded in the source material —
 * mirrors backend DocumentEntityDto (Agents/Studio/DocumentContextAgent.cs). */
export interface BackendDocumentEntity {
  name: string;
  type: string;
  note?: string;
}

export interface BackendCapabilityGraph {
  capabilityGraphId: string;
  name: string;
  description?: string;
  nodes: BackendCapabilityGraphNode[];
  edges: BackendCapabilityGraphEdge[];
  /** Document-wide executive summary (DocumentContextAgent, 2026-07-20).
   * Undefined/null for capabilities created before that agent existed. */
  executiveSummary?: string;
  /** Named entities explicitly grounded in the source material. Empty
   * array when none were found. */
  keyEntities?: BackendDocumentEntity[];
  /** Student-facing topical axis this Capability belongs to (Matemáticas,
   * Idiomas, Cocina...) — Subject, NOT CapabilityDomain (Studio-only
   * authoring metadata never shown to students). Undefined when the
   * Capability hasn't been categorized yet. */
  subjectCode?: string;
  subjectName?: string;
}

/**
 * GET /capabilities/{capabilityId}/graph?personId=... — the full graph
 * (all nodes + edges) for a Capability, with each node's state already
 * computed for this person (Locked/Available/Mastered). Backed by
 * GraphProgressionEngine.GetFullGraphAsync (see backend/HumanOS/Services/
 * GraphProgressionEngine.cs).
 */
export function getCapabilityGraph(
  capabilityId: string,
  personId: string,
  languageCode?: string
): Promise<BackendCapabilityGraph> {
  const languageQuery = languageCode ? `&language=${languageCode}` : '';
  return apiGet<BackendCapabilityGraph>(
    `/capabilities/${capabilityId}/graph?personId=${personId}${languageQuery}`
  );
}
