import { useCallback, useEffect, useMemo, useState, type MouseEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  Handle,
  Position,
  MarkerType,
  type Node,
  type Edge,
  type NodeProps,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import dagre from 'dagre';
import { Lock, PlayCircle, CheckCircle2 } from 'lucide-react';
import { useI18n } from '../i18n';
import { MOCK_USER } from '../components/layout/AppShell';
import {
  getCapabilityGraph,
  type BackendCapabilityGraph,
  type CapabilityGraphNodeState,
} from '../lib/api/capabilityGraphApi';

/**
 * Capability Graph Map — Paso 4. Fetches the full node/edge graph from
 * GET /capabilities/{id}/graph (backed by GraphProgressionEngine.
 * GetFullGraphAsync), auto-layouts it top-to-bottom with dagre (graphs are
 * small — capped at ~20 nodes — so a simple hierarchical layout is enough),
 * and renders it with @xyflow/react. Node color/icon encodes its state:
 * Locked (gray) / Available (blue) / Mastered (green). "Necesita repaso"
 * (amber) is a V2 concept (MasteryStrength decay) — not computed yet, see
 * /memories/repo/student-graph-ui-redesign-final-design.md.
 */

const NODE_WIDTH = 220;
const NODE_HEIGHT = 84;

interface GraphNodeData extends Record<string, unknown> {
  label: string;
  state: CapabilityGraphNodeState;
  reviewMode: boolean;
}

type GraphNode = Node<GraphNodeData>;

/**
 * The backend's CapabilityGraphEdge set can legitimately include transitive
 * edges of the SAME relationship type (e.g. A requires C directly, even
 * though A requires B and B requires C already imply it). Drawing every
 * one of those clutters the map with redundant lines. This keeps only the
 * transitive REDUCTION — an edge (u -> v) is dropped if v is still
 * reachable from u through some other path in the same direct-edge set.
 * Callers run this separately per RelationshipType (Requires vs BuildsOn)
 * so a BuildsOn edge is never treated as redundant just because a Requires
 * edge happens to connect the same two nodes transitively, and vice versa.
 */
function reduceToDirectEdges<T extends { source: string; target: string }>(edges: T[]): T[] {
  const adjacency = new Map<string, string[]>();
  for (const edge of edges) {
    adjacency.set(edge.source, [...(adjacency.get(edge.source) ?? []), edge.target]);
  }

  const isReachableWithoutDirectEdge = (start: string, goal: string, skip: T): boolean => {
    const visited = new Set<string>([start]);
    const stack = [...(adjacency.get(start) ?? [])].filter(
      (next) => !(start === skip.source && next === skip.target)
    );

    while (stack.length > 0) {
      const current = stack.pop()!;
      if (current === goal) return true;
      if (visited.has(current)) continue;
      visited.add(current);
      for (const next of adjacency.get(current) ?? []) {
        stack.push(next);
      }
    }
    return false;
  };

  return edges.filter((edge) => !isReachableWithoutDirectEdge(edge.source, edge.target, edge));
}

function layoutNodes(nodes: GraphNode[], edges: Edge[]): GraphNode[] {
  const g = new dagre.graphlib.Graph();
  g.setDefaultEdgeLabel(() => ({}));
  g.setGraph({ rankdir: 'TB', nodesep: 48, ranksep: 90 });

  nodes.forEach((node) => g.setNode(node.id, { width: NODE_WIDTH, height: NODE_HEIGHT }));
  edges.forEach((edge) => g.setEdge(edge.source, edge.target));

  dagre.layout(g);

  return nodes.map((node) => {
    const position = g.node(node.id);
    return {
      ...node,
      position: { x: position.x - NODE_WIDTH / 2, y: position.y - NODE_HEIGHT / 2 },
    };
  });
}

const STATE_STYLES: Record<
  CapabilityGraphNodeState,
  { border: string; bg: string; text: string; Icon: typeof Lock }
> = {
  Locked: { border: 'border-slate-300', bg: 'bg-slate-100', text: 'text-slate-500', Icon: Lock },
  Available: { border: 'border-blue-400', bg: 'bg-blue-50', text: 'text-blue-700', Icon: PlayCircle },
  Mastered: { border: 'border-green-400', bg: 'bg-green-50', text: 'text-green-700', Icon: CheckCircle2 },
};

function GraphNodeCard({ data }: NodeProps<GraphNode>) {
  const style = STATE_STYLES[data.state];
  const Icon = style.Icon;
  const clickable = data.state !== 'Locked' || data.reviewMode;

  return (
    <div
      className={`flex w-[220px] items-center gap-2 rounded-xl border-2 px-3 py-2.5 shadow-sm transition ${style.border} ${style.bg} ${
        clickable ? 'cursor-pointer hover:shadow-md' : 'cursor-not-allowed opacity-80'
      }`}
    >
      <Handle type="target" position={Position.Top} className="!bg-slate-400" />
      <Icon className={`h-4 w-4 shrink-0 ${style.text}`} />
      <span className={`text-sm font-medium leading-tight ${style.text}`}>{data.label}</span>
      <Handle type="source" position={Position.Bottom} className="!bg-slate-400" />
    </div>
  );
}

const nodeTypes = { graphNode: GraphNodeCard };

export default function CapabilityGraphMapPage() {
  const { capabilityId } = useParams();
  const { t } = useI18n();
  const navigate = useNavigate();

  const [graph, setGraph] = useState<BackendCapabilityGraph | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // Review mode — dev/QA-only toggle that lets every node be opened
  // regardless of its Locked/Available/Mastered state, purely on the
  // client, to spot-check generated content. It never touches backend
  // progression: the server doesn't enforce CanStartNodeAsync when
  // starting a session, so this is safe and fully reversible.
  const [reviewMode, setReviewMode] = useState(false);

  useEffect(() => {
    if (!capabilityId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    getCapabilityGraph(capabilityId, MOCK_USER.oid)
      .then((data) => {
        if (!cancelled) setGraph(data);
      })
      .catch(() => {
        if (!cancelled) setError(t.graphNotFound);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [capabilityId, t.graphNotFound]);

  const handleNodeClick = useCallback(
    (_event: MouseEvent, node: GraphNode) => {
      if (node.data.state === 'Locked' && !node.data.reviewMode) return;
      navigate(`/capabilities/${capabilityId}/nodes/${node.id}`);
    },
    [capabilityId, navigate]
  );

  const { flowNodes, flowEdges } = useMemo(() => {
    if (!graph) {
      return { flowNodes: [] as GraphNode[], flowEdges: [] as Edge[] };
    }

    const rawNodes: GraphNode[] = graph.Nodes.map((n) => ({
      id: n.CapabilityGraphNodeId,
      type: 'graphNode',
      position: { x: 0, y: 0 },
      data: { label: n.Name, state: n.State, reviewMode },
      sourcePosition: Position.Bottom,
      targetPosition: Position.Top,
    }));

    // Backend edges are "Source Requires/BuildsOn Target" (Target = the
    // more foundational node in both cases — a hard prerequisite for
    // Requires, or the base concept being extended for BuildsOn). Reduce
    // each relationship type to its own transitive reduction, then flip
    // direction so arrows flow foundation -> dependent (learning order).
    // IMPORTANT: both types must be included here — dropping either one
    // leaves nodes that are ONLY connected via the other type floating
    // with no visible edge at all, even though the graph is fully
    // connected server-side.
    const directRequiresEdges = reduceToDirectEdges(
      graph.Edges.filter((e) => e.RelationshipType === 0).map((e) => ({
        source: e.SourceNodeId,
        target: e.TargetNodeId,
      }))
    );
    const directBuildsOnEdges = reduceToDirectEdges(
      graph.Edges.filter((e) => e.RelationshipType === 1).map((e) => ({
        source: e.SourceNodeId,
        target: e.TargetNodeId,
      }))
    );

    const rawEdges: Edge[] = [
      ...directRequiresEdges.map((e) => ({
        id: `requires-${e.target}-${e.source}`,
        source: e.target,
        target: e.source,
        style: { stroke: '#94a3b8', strokeWidth: 1.5 },
        markerEnd: { type: MarkerType.ArrowClosed, color: '#94a3b8', width: 18, height: 18 },
      })),
      ...directBuildsOnEdges.map((e) => ({
        id: `buildson-${e.target}-${e.source}`,
        source: e.target,
        target: e.source,
        style: { stroke: '#94a3b8', strokeWidth: 1.5, strokeDasharray: '5 4' },
        markerEnd: { type: MarkerType.ArrowClosed, color: '#94a3b8', width: 18, height: 18 },
      })),
    ];

    return { flowNodes: layoutNodes(rawNodes, rawEdges), flowEdges: rawEdges };
  }, [graph, reviewMode]);

  return (
    <div className="mx-auto flex h-[calc(100vh-4rem)] max-w-6xl flex-col p-4 sm:p-8">
      <Link to="/" className="text-sm text-slate-500 hover:underline">
        ← {t.backToSubjects}
      </Link>

      {graph && (
        <div className="mt-2">
          <h1 className="text-2xl font-semibold text-slate-900">{graph.Name}</h1>
          {graph.Description && (
            <p className="mt-1 max-w-2xl text-sm text-slate-500">{graph.Description}</p>
          )}
        </div>
      )}

      {loading && <p className="mt-6 text-slate-500">{t.graphLoading}</p>}
      {error && !loading && <p className="mt-6 text-red-600">{error}</p>}

      {!loading && !error && graph && (
        <>
          <div className="mt-4 flex flex-wrap items-center justify-between gap-4">
            <div className="flex flex-wrap gap-4 text-xs text-slate-600">
              <LegendDot colorClass="bg-slate-400" label={t.graphLegendLocked} />
              <LegendDot colorClass="bg-blue-500" label={t.graphLegendAvailable} />
              <LegendDot colorClass="bg-green-500" label={t.graphLegendMastered} />
            </div>
            <button
              type="button"
              onClick={() => setReviewMode((prev) => !prev)}
              className={`rounded-lg border px-3 py-1.5 text-xs font-medium transition ${
                reviewMode
                  ? 'border-amber-400 bg-amber-50 text-amber-700 hover:bg-amber-100'
                  : 'border-slate-300 bg-white text-slate-600 hover:bg-slate-50'
              }`}
            >
              {reviewMode ? '✓ Modo revisión activo — todos los nodos abiertos' : 'Modo revisión: ver todos los nodos'}
            </button>
          </div>

          <div className="mt-4 flex-1 overflow-hidden rounded-2xl border border-slate-200 bg-white">
            <ReactFlow
              nodes={flowNodes}
              edges={flowEdges}
              nodeTypes={nodeTypes}
              onNodeClick={handleNodeClick}
              fitView
              proOptions={{ hideAttribution: true }}
              nodesDraggable={false}
              nodesConnectable={false}
              elementsSelectable={false}
            >
              <Background gap={20} />
              <Controls showInteractive={false} />
              <MiniMap pannable zoomable />
            </ReactFlow>
          </div>
        </>
      )}
    </div>
  );
}

function LegendDot({ colorClass, label }: { colorClass: string; label: string }) {
  return (
    <span className="flex items-center gap-1.5">
      <span className={`h-2.5 w-2.5 rounded-full ${colorClass}`} />
      {label}
    </span>
  );
}
