import { useQuery } from '@tanstack/react-query';
import { getEnrolledPrograms } from '@/features/my-programs/api/enrolledProgramsApi';
import { getEnrolledCapabilities } from '@/features/capabilities/api/enrolledCapabilitiesApi';
import { getCapabilityGraph } from '@/features/capabilities/api/capabilityGraphApi';
import type { NeuronCluster, RootGroup } from './types';

/** Fetches every enrolled Program/standalone Capability's graph and builds
 * the Brain page's `RootGroup[]` (mastered nodes only, grouped by area).
 * Extracted from `BrainPage` into a `useQuery` (2026-07-25) so navigating
 * away (e.g. to redo a node's 5 steps) and back to `/brain` shows the
 * already-fetched data INSTANTLY from React Query's in-memory cache instead
 * of re-running every network call and re-showing a loading state — the
 * app-wide `queryClient` (see `src/api/index.ts`) already has a 5-minute
 * `staleTime`, so a quick round-trip never even triggers a background
 * refetch; a slower one refetches silently while still painting the
 * cached data immediately (default `refetchOnMount` behavior). */
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

  return Array.from(rootMap.values());
}

export function useBrainGraph(personId: string, language: string) {
  return useQuery({
    queryKey: ['brain-graph', personId, language],
    queryFn: () => fetchBrainGraph(personId, language),
    enabled: !!personId,
  });
}
