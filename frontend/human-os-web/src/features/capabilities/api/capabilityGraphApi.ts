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
  personId: string
): Promise<BackendCapabilityGraph> {
  return apiGet<BackendCapabilityGraph>(
    `/capabilities/${capabilityId}/graph?personId=${personId}`
  );
}
