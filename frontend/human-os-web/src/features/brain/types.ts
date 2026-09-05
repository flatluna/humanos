import type { BackendCapabilityGraphNode, BackendCapabilityGraphEdge } from '@/features/capabilities/api/capabilityGraphApi';

/** A mastered Structured Learning section, reshaped to the same field
 * names BrainRecallGrid/memoryScience already read off a
 * BackendCapabilityGraphNode (name/state/lastCompletedDate/reviewCount/
 * finalScore) — lets both course types render through the exact same
 * grid/graph components with zero changes there. `capabilityGraphNodeId`
 * holds the section id (generic id field, not a real Graph node).
 * 2026-08-14: ports the "Mi Cerebro" memory network to also cover
 * Structured Learning documents, alongside the original Capability Graph
 * design this page shipped with first. */
export interface StructuredLearningBrainNode {
  capabilityGraphNodeId: string;
  name: string;
  description?: string;
  state: 'Mastered';
  lastCompletedDate?: string;
  reviewCount?: number;
  finalScore?: number;
  /** Marks this node as a Structured Learning section rather than a real
   * Capability Graph node — read by BrainPage's click handler to route to
   * the right detail view instead of Capability Graph's NodeSummary panel. */
  isStructuredLearning: true;
  structuredLearningDocumentId: string;
  structuredLearningSectionId: string;
}

/** One capability's worth of already-Mastered nodes — real data only, never
 *  fabricated: nodes come straight from GraphProgressionEngine's computed
 *  state, edges are the real prerequisite/builds-on relationships filtered
 *  down to pairs where BOTH ends are already mastered by this student.
 *  Also used for a Structured Learning document's mastered sections
 *  (2026-08-14) — those clusters simply have `edges: []` (no dependency
 *  graph exists for Structured Learning, it's linear). */
export interface NeuronCluster {
  capabilityId: string;
  capabilityName: string;
  nodes: (BackendCapabilityGraphNode | StructuredLearningBrainNode)[];
  edges: BackendCapabilityGraphEdge[];
  /** Student-facing topical axis (Matemáticas, Idiomas, Cocina...) this
   * capability belongs to — undefined when not categorized yet. Powers
   * the subject badge + filter chips in BrainRecallGrid. */
  subjectCode?: string;
  subjectName?: string;
  /** "job-skills" | "soft-skills" | "department-processes" |
   * "hr-processes" | undefined — which employer career-plan training
   * track this capability belongs to, if any. Powers the "tipo de curso"
   * filter chips in BrainPage (2026-08-02). */
  category?: string;
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

/** The payload handed back when a student clicks a neuron in the graph.
 * Structural union (not `extends`) so a Structured Learning selection can
 * carry its own `isStructuredLearning`/document+section ids instead of a
 * real capabilityGraphNodeId. */
export type SelectedNeuron =
  | (BackendCapabilityGraphNode & { capabilityId: string; capabilityName: string; isStructuredLearning?: false })
  | (StructuredLearningBrainNode & { capabilityId: string; capabilityName: string });

