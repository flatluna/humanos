import { useQuery } from '@tanstack/react-query';
import { getEnrolledPrograms } from '@/features/my-programs/api/enrolledProgramsApi';
import { getEnrolledCapabilities } from '@/features/capabilities/api/enrolledCapabilitiesApi';
import { getCapabilityGraph } from '@/features/capabilities/api/capabilityGraphApi';
import { getEnrolledStructuredLearningDocuments } from '@/features/capabilities/api/enrolledStructuredLearningApi';
import {
  getStructuredLearningDocument,
  getActiveStructuredLearningAssessmentRound,
} from '@/features/capabilities/api/structuredLearningRuntimeApi';
import type { NeuronCluster, RootGroup, StructuredLearningBrainNode } from './types';

/** Builds one RootGroup per enrolled Structured Learning document — same
 * "Mi Cerebro" memory-network treatment the original design gave
 * Capability Graph nodes (2026-08-14): each Passed section becomes a
 * StructuredLearningBrainNode (mastered-node shape), so it flows through
 * the exact same BrainRecallGrid/BrainGraph/memoryScience code as a real
 * Capability Graph node. Sections have no dependency graph (linear
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
        reviewCount: 1,
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
      isProgram: false,
      clusters: [cluster],
    });
  }

  return rootGroups;
}

/** Fetches every enrolled Program/standalone Capability's graph and builds
 * the Brain page's `RootGroup[]` (mastered nodes only, grouped by area).
 * Extracted from `BrainPage` into a `useQuery` (2026-07-25) so navigating
 * away (e.g. to redo a node's 5 steps) and back to `/brain` shows the
 * already-fetched data INSTANTLY from React Query's in-memory cache instead
 * of re-running every network call and re-showing a loading state — the
 * app-wide `queryClient` (see `src/api/index.ts`) already has a 5-minute
 * `staleTime`, so a quick round-trip never even triggers a background
 * refetch; a slower one refetches silently while still painting the
 * cached data immediately (default `refetchOnMount` behavior).
 *
 * 2026-08-14: also folds in every enrolled Structured Learning document's
 * mastered sections as their own RootGroups, alongside the original
 * Capability Graph groups — the "Mi Cerebro" memory network now covers
 * both course types this app supports. */
async function fetchBrainGraph(personId: string, language: string): Promise<RootGroup[]> {
  const [programs, standalone] = await Promise.all([
    getEnrolledPrograms(personId).catch(() => []),
    getEnrolledCapabilities(personId).catch(() => []),
  ]);

  const capabilityNames = new Map<string, string>();
  // Which root "area" each capability belongs to: a real Program
  // (e.g. "Matemáticas") when enrolled via one, otherwise the
  // capability becomes its own single-capability root.
  const capabilityRoot = new Map<string, { rootId: string; rootName: string; isProgram: boolean }>();
  programs.forEach((program) =>
    program.capabilities.forEach((c) => {
      capabilityNames.set(c.capabilityId, c.name);
      capabilityRoot.set(c.capabilityId, { rootId: program.programId, rootName: program.name, isProgram: true });
    })
  );
  standalone.forEach((c) => {
    capabilityNames.set(c.capabilityId, c.name);
    if (!capabilityRoot.has(c.capabilityId)) {
      capabilityRoot.set(c.capabilityId, { rootId: c.capabilityId, rootName: c.name, isProgram: false });
    }
  });

  const results = await Promise.all(
    Array.from(capabilityNames.keys()).map((capabilityId) =>
      getCapabilityGraph(capabilityId, personId, language)
        .then((graph) => ({ capabilityId, graph }))
        .catch(() => null)
    )
  );

  const rootMap = new Map<string, RootGroup>();
  for (const result of results) {
    if (!result) continue;
    const masteredNodes = result.graph.nodes.filter((node) => node.state === 'Mastered');
    if (masteredNodes.length === 0) continue;
    const masteredIds = new Set(masteredNodes.map((node) => node.capabilityGraphNodeId));
    const edges = result.graph.edges.filter(
      (edge) => masteredIds.has(edge.sourceNodeId) && masteredIds.has(edge.targetNodeId)
    );
    const cluster: NeuronCluster = {
      capabilityId: result.capabilityId,
      capabilityName: capabilityNames.get(result.capabilityId) ?? result.graph.name,
      nodes: masteredNodes,
      edges,
      subjectCode: result.graph.subjectCode,
      subjectName: result.graph.subjectName,
      category: result.graph.category,
    };

    const root = capabilityRoot.get(result.capabilityId) ?? {
      rootId: result.capabilityId,
      rootName: cluster.capabilityName,
      isProgram: false,
    };
    let group = rootMap.get(root.rootId);
    if (!group) {
      group = { rootId: root.rootId, rootName: root.rootName, isProgram: root.isProgram, clusters: [] };
      rootMap.set(root.rootId, group);
    }
    group.clusters.push(cluster);
  }

  const structuredLearningGroups = await fetchStructuredLearningRootGroups(personId);
  return [...Array.from(rootMap.values()), ...structuredLearningGroups];
}

export function useBrainGraph(personId: string, language: string) {
  return useQuery({
    queryKey: ['brain-graph', personId, language],
    queryFn: () => fetchBrainGraph(personId, language),
    enabled: !!personId,
  });
}
