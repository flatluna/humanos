import { useQuery } from '@tanstack/react-query';
import { getEnrolledCapabilities } from '@/features/capabilities/api/enrolledCapabilitiesApi';
import { getCapabilityGraph } from '@/features/capabilities/api/capabilityGraphApi';
import { getEnrolledStructuredLearningDocuments } from '@/features/capabilities/api/enrolledStructuredLearningApi';
import {
  getStructuredLearningDocument,
  getActiveStructuredLearningAssessmentRound,
} from '@/features/capabilities/api/structuredLearningRuntimeApi';
import type { NeuronCluster, RootGroup, StructuredLearningBrainNode } from './types';

/** Builds one RootGroup per enrolled/active Structured Learning document:
 * each Passed section becomes a StructuredLearningBrainNode (mastered-node shape),
 * so it flows through the exact same BrainRecallGrid/BrainGraph/memoryScience code
 * as a real Capability Graph node. Sections have no dependency graph (linear
 * document), so `edges` is always empty here. */
async function fetchStructuredLearningRootGroups(personId: string): Promise<RootGroup[]> {
  const documents = await getEnrolledStructuredLearningDocuments(personId).catch(() => []);

  const results = await Promise.all(
    documents.map((doc) =>
      getStructuredLearningDocument(doc.structuredLearningDocumentId)
        .then((detail) => ({ doc, detail }))
        .catch(() => null)
    )
  );

  const rootGroups: RootGroup[] = [];
  for (const result of results) {
    if (!result) continue;
    const { doc, detail } = result;

    const sectionRounds = await Promise.all(
      detail.Sections.map((section) =>
        getActiveStructuredLearningAssessmentRound(section.StructuredLearningDocumentSectionId, personId)
          .then((round) => ({ section, round }))
          .catch(() => ({ section, round: null }))
      )
    );

    const masteredNodes: StructuredLearningBrainNode[] = sectionRounds
      .filter(({ round }) => round?.Status === 'Passed')
      .map(({ section, round }) => ({
        capabilityGraphNodeId: section.StructuredLearningDocumentSectionId,
        name: section.SubchapterTitle,
        state: 'Mastered',
        lastCompletedDate: round?.CompletedDate ?? undefined,
        reviewCount: round?.RoundNumber ?? 1,
        finalScore: round?.FinalScore ?? undefined,
        isStructuredLearning: true,
        structuredLearningDocumentId: doc.structuredLearningDocumentId,
        structuredLearningSectionId: section.StructuredLearningDocumentSectionId,
      }));

    if (masteredNodes.length === 0) continue;

    const cluster: NeuronCluster = {
      capabilityId: doc.structuredLearningDocumentId,
      capabilityName: doc.documentName,
      nodes: masteredNodes,
      edges: [],
    };

    rootGroups.push({
      rootId: doc.structuredLearningDocumentId,
      rootName: doc.documentName,
      clusters: [cluster],
    });
  }

  return rootGroups;
}

/** Fetches every assigned capability's graph and structured learning documents
 * and builds the Brain page's `RootGroup[]` (mastered nodes only). Wrapped in
 * `useQuery` so navigating away and back to `/brain` shows the already-fetched
 * data instantly from React Query's cache. */
async function fetchBrainGraph(personId: string): Promise<RootGroup[]> {
  const capabilities = await getEnrolledCapabilities(personId).catch(() => []);

  const results = await Promise.all(
    capabilities.map((cap) =>
      getCapabilityGraph(cap.capabilityId, personId, 'es')
        .then((graph) => ({ capability: cap, graph }))
        .catch(() => null),
    ),
  );

  const rootGroups: RootGroup[] = [];
  for (const result of results) {
    if (!result) continue;
    const masteredNodes = result.graph.nodes.filter((node) => node.state === 'Mastered');
    if (masteredNodes.length === 0) continue;
    const masteredIds = new Set(masteredNodes.map((node) => node.capabilityGraphNodeId));
    const edges = result.graph.edges.filter(
      (edge) => masteredIds.has(edge.sourceNodeId) && masteredIds.has(edge.targetNodeId),
    );
    const cluster: NeuronCluster = {
      capabilityId: result.capability.capabilityId,
      capabilityName: result.capability.name,
      nodes: masteredNodes,
      edges,
      subjectCode: result.graph.subjectCode,
      subjectName: result.graph.subjectName,
      category: result.graph.category,
    };

    rootGroups.push({
      rootId: result.capability.capabilityId,
      rootName: result.capability.name,
      clusters: [cluster],
    });
  }

  const structuredLearningGroups = await fetchStructuredLearningRootGroups(personId);
  return [...rootGroups, ...structuredLearningGroups];
}

export function useBrainGraph(personId: string) {
  return useQuery({
    queryKey: ['brain-graph', personId],
    queryFn: () => fetchBrainGraph(personId),
    enabled: !!personId,
  });
}
