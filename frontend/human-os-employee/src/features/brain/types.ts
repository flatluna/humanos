import type { BackendCapabilityGraphNode, BackendCapabilityGraphEdge } from '@/features/capabilities/api/capabilityGraphApi';

/** One capability's worth of already-Mastered nodes — real data only, never
 *  fabricated: nodes come straight from GraphProgressionEngine's computed
 *  state, edges are the real prerequisite/builds-on relationships filtered
 *  down to pairs where BOTH ends are already mastered by this employee. */
export interface NeuronCluster {
  capabilityId: string;
  capabilityName: string;
  nodes: BackendCapabilityGraphNode[];
  edges: BackendCapabilityGraphEdge[];
  /** Topical axis (Seguridad, Electrónica...) this capability belongs to —
   * undefined when not categorized yet. Powers the subject badge + filter
   * chips in BrainRecallGrid. */
  subjectCode?: string;
  subjectName?: string;
}

/** One "area" of the brain map. The employee portal has no Programs concept
 * (v1 scope is standalone assigned capabilities only), so every capability
 * is its own single-capability root — kept as a distinct RootGroup type
 * (rather than collapsing straight to NeuronCluster) so this stays a
 * drop-in match for BrainRecallGrid/memoryScience, ported as-is from
 * human-os-web's /brain. */
export interface RootGroup {
  rootId: string;
  rootName: string;
  clusters: NeuronCluster[];
}

/** The payload handed back when an employee clicks a neuron in the grid. */
export interface SelectedNeuron extends BackendCapabilityGraphNode {
  capabilityId: string;
  capabilityName: string;
}
