import { useQuery } from '@tanstack/react-query';
import { getEnrolledCapabilities } from '@/features/capabilities/api/enrolledCapabilitiesApi';
import { getCapabilityGraph } from '@/features/capabilities/api/capabilityGraphApi';
import type { NeuronCluster, RootGroup } from './types';

/** Fetches every assigned capability's graph and builds the Brain page's
 * `RootGroup[]` (mastered nodes only, one root per capability — this
 * portal has no Programs concept, unlike human-os-web's version this was
 * ported from). Wrapped in `useQuery` so navigating away (e.g. to redo a
 * node's 5 steps) and back to `/brain` shows the already-fetched data
 * instantly from React Query's cache instead of re-running every network
 * call (the app-wide `queryClient` already has a 5-minute `staleTime`). */
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
    };

    rootGroups.push({
      rootId: result.capability.capabilityId,
      rootName: result.capability.name,
      clusters: [cluster],
    });
  }

  return rootGroups;
}

export function useBrainGraph(personId: string) {
  return useQuery({
    queryKey: ['brain-graph', personId],
    queryFn: () => fetchBrainGraph(personId),
    enabled: !!personId,
  });
}
