import { apiGet } from './httpClient';

/** Node states as computed server-side by GraphProgressionEngine.GetFullGraphAsync. */
export type CapabilityGraphNodeState = 'Locked' | 'Available' | 'Mastered';

/** Backend response shape (PascalCase) for a single graph node. */
export interface BackendCapabilityGraphNode {
  CapabilityGraphNodeId: string;
  Name: string;
  Description?: string;
  SortOrder: number;
  State: CapabilityGraphNodeState;
  IllustrationId?: string;
}

/** RelationshipType enum values from backend (Requires=0, BuildsOn=1). */
export type BackendRelationshipType = 0 | 1;

export interface BackendCapabilityGraphEdge {
  SourceNodeId: string;
  TargetNodeId: string;
  RelationshipType: BackendRelationshipType;
}

export interface BackendCapabilityGraph {
  CapabilityGraphId: string;
  Name: string;
  Description?: string;
  Nodes: BackendCapabilityGraphNode[];
  Edges: BackendCapabilityGraphEdge[];
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
