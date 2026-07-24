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
import { Lock, PlayCircle, CheckCircle2, Info, ChevronDown, ChevronUp } from 'lucide-react';
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
  Locked: { border: 'border-slate-700', bg: 'bg-slate-800/60', text: 'text-slate-500', Icon: Lock },
  Available: { border: 'border-brand-400/60', bg: 'bg-brand-500/10', text: 'text-brand-300', Icon: PlayCircle },
  Mastered: { border: 'border-emerald-400/60', bg: 'bg-emerald-500/10', text: 'text-emerald-300', Icon: CheckCircle2 },
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
      <Handle type="target" position={Position.Top} className="!bg-slate-500" />
      <Icon className={`h-4 w-4 shrink-0 ${style.text}`} />
      <span className={`text-sm font-medium leading-tight ${style.text}`}>{data.label}</span>
      <Handle type="source" position={Position.Bottom} className="!bg-slate-500" />
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
  // Course-level executive summary + key entities panel (2026-07-20) —
  // collapsed by default so it never competes with the graph map for
  // attention; the student opens it deliberately via "About this course".
  const [summaryOpen, setSummaryOpen] = useState(false);

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
      <Link to="/" className="text-sm text-slate-400 hover:text-white hover:underline">
        ← {t.backToSubjects}
      </Link>

      {graph && (
        <div className="mt-2">
          <h1 className="text-2xl font-semibold tracking-tight text-white">{graph.Name}</h1>
          {graph.Description && (
            <p className="mt-1 max-w-2xl text-sm text-slate-400">{graph.Description}</p>
          )}

          {(graph.ExecutiveSummary || (graph.KeyEntities && graph.KeyEntities.length > 0)) && (
            <div className="mt-3">
              <button
                type="button"
                onClick={() => setSummaryOpen((open) => !open)}
                className="flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.04] px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-white/[0.08]"
              >
                <Info className="h-3.5 w-3.5" />
                {summaryOpen ? t.graphSummaryHideButton : t.graphSummaryShowButton}
                {summaryOpen ? <ChevronUp className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
              </button>

              {summaryOpen && (
                <div className="mt-2 max-w-2xl rounded-xl border border-white/10 bg-white/[0.03] p-4">
                  {graph.ExecutiveSummary && (
                    <div>
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                        {t.graphExecutiveSummaryTitle}
                      </p>
                      <p className="mt-1 text-sm leading-relaxed text-slate-300">{graph.ExecutiveSummary}</p>
                    </div>
                  )}

                  {graph.KeyEntities && graph.KeyEntities.length > 0 && (
                    <div className={graph.ExecutiveSummary ? 'mt-4' : ''}>
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                        {t.graphKeyEntitiesTitle}
                      </p>
                      <ul className="mt-1.5 space-y-1.5">
                        {graph.KeyEntities.map((entity) => (
                          <li key={entity.Name} className="text-sm text-slate-300">
                            <span className="font-medium text-white">{entity.Name}</span>{' '}
                            <span className="text-xs text-slate-500">({entity.Type})</span>
                            {entity.Note && <span className="text-slate-400"> — {entity.Note}</span>}
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {loading && <p className="mt-6 text-slate-400">{t.graphLoading}</p>}
      {error && !loading && <p className="mt-6 text-red-400">{error}</p>}

      {!loading && !error && graph && (
        <>
          <div className="mt-4 flex flex-wrap items-center justify-between gap-4">
            <div className="flex flex-wrap gap-4 text-xs text-slate-400">
              <LegendDot colorClass="bg-slate-500" label={t.graphLegendLocked} />
              <LegendDot colorClass="bg-brand-400" label={t.graphLegendAvailable} />
              <LegendDot colorClass="bg-emerald-400" label={t.graphLegendMastered} />
            </div>
            <button
              type="button"
              onClick={() => setReviewMode((prev) => !prev)}
              className={`rounded-lg border px-3 py-1.5 text-xs font-medium transition ${
                reviewMode
                  ? 'border-amber-400/50 bg-amber-400/10 text-amber-300 hover:bg-amber-400/20'
                  : 'border-white/10 bg-white/[0.04] text-slate-300 hover:bg-white/[0.08]'
              }`}
            >
              {reviewMode ? '✓ Modo revisión activo — todos los nodos abiertos' : 'Modo revisión: ver todos los nodos'}
            </button>
          </div>

          <div className="mt-4 flex-1 overflow-hidden rounded-2xl border border-white/10 bg-slate-900">
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
              colorMode="dark"
            >
              <Background gap={20} color="#334155" />
              <Controls showInteractive={false} />
              <MiniMap pannable zoomable maskColor="rgba(2, 6, 23, 0.6)" bgColor="#0f172a" />
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
