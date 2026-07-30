import type { BackendCapabilityGraphNode, BackendCapabilityGraphEdge } from '@/features/capabilities/api/capabilityGraphApi';

/** One capability's worth of already-Mastered nodes — real data only, never
 *  fabricated: nodes come straight from GraphProgressionEngine's computed
 *  state, edges are the real prerequisite/builds-on relationships filtered
 *  down to pairs where BOTH ends are already mastered by this student. */
export interface NeuronCluster {
  capabilityId: string;
  capabilityName: string;
  nodes: BackendCapabilityGraphNode[];
  edges: BackendCapabilityGraphEdge[];
  /** Student-facing topical axis (Matemáticas, Idiomas, Cocina...) this
   * capability belongs to — undefined when not categorized yet. Powers
   * the subject badge + filter chips in BrainRecallGrid. */
  subjectCode?: string;
  subjectName?: string;
}

/** One "area" of the brain map — a real enrolled Program (e.g. "Matemáticas"),
 *  grouping all of its capabilities' mastered-node clusters under one area
 *  node. Standalone capabilities (not part of any Program) become their own
 *  single-capability area. Every area hangs off the single "BRIAN" root
 *  node in the knowledge graph. */
export interface RootGroup {
  rootId: string;
  rootName: string;
  isProgram: boolean;
  clusters: NeuronCluster[];
}

/** The payload handed back when a student clicks a neuron in the graph. */
export interface SelectedNeuron extends BackendCapabilityGraphNode {
  capabilityId: string;
  capabilityName: string;
}
