import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Graph from 'graphology';
import {
  SigmaContainer,
  useRegisterEvents,
  useSigma,
  useLoadGraph,
  ControlsContainer,
  ZoomControl,
} from '@react-sigma/core';
import { forceSimulation, forceManyBody, forceLink, forceCollide, forceCenter, type SimulationNodeDatum } from 'd3-force';
import { drawDiscNodeLabel } from 'sigma/rendering';
import '@react-sigma/core/lib/style.css';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getCapabilities, type BackendCapabilitySummary } from '../lib/api/capabilitiesApi';
import { getCapabilityGraph } from '../lib/api/capabilityGraphApi';
import { MOCK_USER } from './layout/AppShell';

const SUBJECT_COLORS = ['#6366f1', '#0d9488', '#e11d48', '#d97706', '#7c3aed', '#0284c7', '#059669'];

function colorFor(id: string, palette: string[]): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return palette[hash % palette.length];
}

/** Mastery progress for a single Capability, aggregated client-side from
 * GET /capabilities/{id}/graph's per-person node states. */
type ProgressState = 'not-started' | 'in-progress' | 'mastered' | 'unknown';

const PROGRESS_COLORS: Record<ProgressState, string> = {
  'not-started': '#94a3b8',
  'in-progress': '#2563eb',
  mastered: '#16a34a',
  unknown: '#cbd5e1',
};

/** Demo-student fallback data — shown (with a badge) when the backend
 * func host is unreachable, so the graph is always viewable. */
const DEMO_SUBJECTS: Subject[] = [
  { code: 'matematicas', name: 'Matemáticas', iconKey: 'matematicas', description: '' },
  { code: 'ciencias', name: 'Ciencias', iconKey: 'ciencias', description: '' },
  { code: 'historia', name: 'Historia', iconKey: 'historia', description: '' },
  { code: 'ingles', name: 'Inglés', iconKey: 'ingles', description: '' },
];

/** Demo-only fallback percentage per progress category, since demo data
 * has no real per-node mastery counts to derive a score from. */
const DEMO_PROGRESS_SCORE: Record<ProgressState, number> = {
  'not-started': 0,
  'in-progress': 50,
  mastered: 100,
  unknown: 0,
};

const DEMO_CAPABILITIES: (BackendCapabilitySummary & { progress: ProgressState })[] = [
  { CapabilityId: 'demo-1', Name: 'Álgebra básica', SubjectCode: 'matematicas', progress: 'mastered' },
  { CapabilityId: 'demo-2', Name: 'Geometría', SubjectCode: 'matematicas', progress: 'in-progress' },
  { CapabilityId: 'demo-3', Name: 'Fracciones', SubjectCode: 'matematicas', progress: 'not-started' },
  { CapabilityId: 'demo-4', Name: 'El cuerpo humano', SubjectCode: 'ciencias', progress: 'in-progress' },
  { CapabilityId: 'demo-5', Name: 'Los ecosistemas', SubjectCode: 'ciencias', progress: 'not-started' },
  { CapabilityId: 'demo-6', Name: 'La Revolución Industrial', SubjectCode: 'historia', progress: 'mastered' },
  { CapabilityId: 'demo-7', Name: 'Civilizaciones antiguas', SubjectCode: 'historia', progress: 'not-started' },
  { CapabilityId: 'demo-8', Name: 'Verbos irregulares', SubjectCode: 'ingles', progress: 'in-progress' },
  { CapabilityId: 'demo-9', Name: 'Conversación básica', SubjectCode: 'ingles', progress: 'mastered' },
];

type NodeMeta = {
  kind: 'root' | 'subject' | 'capability';
  subjectCode?: string;
  capabilityId?: string;
};

/** Draws a node's normal external label (unchanged default sigma behavior),
 * plus — when the node carries a numeric `score` attribute (0-100, set on
 * capability nodes in `buildGraph`) — a small percentage badge centered
 * INSIDE the node's own circle, so the learner's mastery score is visible
 * right on the node itself instead of only via its color. Sigma passes the
 * full merged node attributes as `data`, so custom attributes like `score`
 * are present at runtime even though they aren't part of sigma's own
 * `NodeDisplayData` type — hence the local cast below. */
function drawNodeLabelWithScore(
  context: CanvasRenderingContext2D,
  data: Parameters<typeof drawDiscNodeLabel>[1],
  settings: Parameters<typeof drawDiscNodeLabel>[2],
): void {
  drawDiscNodeLabel(context, data, settings);

  const score = (data as unknown as { score?: number }).score;
  if (score === undefined || score === null) return;

  const fontSize = Math.max(9, Math.min(data.size * 0.85, 13));
  context.save();
  context.font = `700 ${fontSize}px ${settings.labelFont}`;
  context.textAlign = 'center';
  context.textBaseline = 'middle';
  context.lineWidth = 3;
  context.strokeStyle = 'rgba(15, 23, 42, 0.55)';
  context.fillStyle = '#ffffff';
  const text = `${Math.round(score)}%`;
  context.strokeText(text, data.x, data.y);
  context.fillText(text, data.x, data.y);
  context.restore();
}

/** Builds the graphology graph with randomized initial positions (a small
 * deterministic circular scatter, seeded per node id) — real placement is
 * left to the d3-force physics simulation (see PhysicsSimulation below),
 * which pulls connected nodes together and pushes everything else apart,
 * so the whole map dynamically self-arranges instead of using a fixed
 * hand-computed shape. */
function buildOverviewGraph(
  subjects: Subject[],
  capabilities: BackendCapabilitySummary[],
  progressById: Record<string, ProgressState>,
  scoreById: Record<string, number>,
): { graph: Graph; meta: Map<string, NodeMeta> } {
  const graph = new Graph();
  const meta = new Map<string, NodeMeta>();

  const bySubject = new Map<string, BackendCapabilitySummary[]>();
  capabilities
    .filter((c) => c.SubjectCode)
    .forEach((c) => {
      const list = bySubject.get(c.SubjectCode!) ?? [];
      list.push(c);
      bySubject.set(c.SubjectCode!, list);
    });

  let seed = 0;
  const scatter = () => {
    seed += 1;
    const angle = seed * 2.399963; // golden angle — avoids clustering on a line
    const radius = 60 + seed * 12;
    return { x: Math.cos(angle) * radius, y: Math.sin(angle) * radius };
  };

  graph.addNode('memory-root', {
    label: 'Memoria',
    ...scatter(),
    size: 22,
    color: '#1e293b',
  });
  meta.set('memory-root', { kind: 'root' });

  subjects.forEach((subject) => {
    const subjectId = `subject-${subject.code}`;
    graph.addNode(subjectId, {
      label: subject.name,
      ...scatter(),
      size: 17,
      color: colorFor(subject.code, SUBJECT_COLORS),
    });
    meta.set(subjectId, { kind: 'subject', subjectCode: subject.code });
    graph.addEdge('memory-root', subjectId, { color: '#cbd5e1', size: 1.5 });

    const childCapabilities = bySubject.get(subject.code) ?? [];
    childCapabilities.forEach((c) => {
      const capId = `capability-${c.CapabilityId}`;
      const progress = progressById[c.CapabilityId] ?? 'unknown';
      const score = scoreById[c.CapabilityId];
      graph.addNode(capId, {
        label: c.Name,
        ...scatter(),
        size: 11,
        color: PROGRESS_COLORS[progress],
        ...(score !== undefined ? { score } : {}),
      });
      meta.set(capId, { kind: 'capability', capabilityId: c.CapabilityId });
      graph.addEdge(subjectId, capId, { color: '#e2e8f0', size: 1 });
    });
  });

  return { graph, meta };
}

interface SimNode extends SimulationNodeDatum {
  id: string;
  size: number;
}

/** 5th iteration's physics engine: `d3-force`, a synchronous MAIN-THREAD
 * simulation (not a web worker) — chosen specifically because it's the
 * standard, battle-tested tool for INTERACTIVE force-directed graphs where
 * dragging a node must visibly push/pull its neighbors in real time.
 *
 * Why the previous attempt (`@react-sigma/layout-forceatlas2`, a web-worker
 * supervisor) failed at exactly that: the worker keeps its OWN internal
 * position state and only pushes its own computed positions back into the
 * graph on each tick — it never reads back manual position edits made
 * during a drag. So a manual `graph.setNodeAttribute(...)` during drag was
 * silently overwritten by the worker's next tick, meaning the drag never
 * actually influenced the physics at all (nodes moved on their own from FA2,
 * but dragging didn't push anyone). `d3-force` runs its integration loop
 * synchronously on the node objects we own directly, so setting `fx`/`fy`
 * (d3's standard "pin this node here" mechanism) on the dragged node is
 * immediately visible to every other force calculation on the very next
 * tick — the repulsion/link forces genuinely react to the pinned position,
 * which is exactly "arrastrar un nodo mueve/empuja a los demás".
 *
 * `alphaTarget(0.05)` (kept non-zero permanently, restored after each drag)
 * means the simulation never fully cools down to a static equilibrium, so
 * the whole graph keeps gently, continuously floating even with no user
 * interaction — the "ambient" motion the user asked for. */
function PhysicsSimulation() {
  const sigma = useSigma();

  useEffect(() => {
    const graph = sigma.getGraph();
    const captor = sigma.getMouseCaptor();

    const nodes: SimNode[] = graph.nodes().map((id) => {
      const attrs = graph.getNodeAttributes(id);
      return { id, x: attrs.x, y: attrs.y, size: attrs.size ?? 12 };
    });
    const nodeById = new Map(nodes.map((n) => [n.id, n]));

    const links = graph.edges().map((edge) => ({
      source: nodeById.get(graph.source(edge))!,
      target: nodeById.get(graph.target(edge))!,
    }));

    const simulation = forceSimulation(nodes)
      .force('charge', forceManyBody().strength(-1100))
      .force('link', forceLink(links).distance(95).strength(0.5))
      .force('collide', forceCollide<SimNode>().radius((n) => n.size + 14))
      .force('center', forceCenter(0, 0).strength(0.015))
      .alphaDecay(0.02)
      .velocityDecay(0.35)
      .alphaTarget(0.05);

    simulation.on('tick', () => {
      nodes.forEach((n) => {
        graph.setNodeAttribute(n.id, 'x', n.x);
        graph.setNodeAttribute(n.id, 'y', n.y);
      });
    });

    let draggedNode: SimNode | null = null;

    const handleDownNode = (e: { node: string }) => {
      const n = nodeById.get(e.node);
      if (!n) return;
      draggedNode = n;
      n.fx = n.x;
      n.fy = n.y;
      simulation.alphaTarget(0.35).restart();
      if (!sigma.getCustomBBox()) sigma.setCustomBBox(sigma.getBBox());
    };
    const handleMouseMoveBody = (e: { x: number; y: number; preventSigmaDefault: () => void; original: MouseEvent }) => {
      if (!draggedNode) return;
      const pos = sigma.viewportToGraph(e);
      draggedNode.fx = pos.x;
      draggedNode.fy = pos.y;
      e.preventSigmaDefault();
      e.original.preventDefault();
      e.original.stopPropagation();
    };
    const handleMouseUp = () => {
      if (draggedNode) {
        draggedNode.fx = null;
        draggedNode.fy = null;
      }
      draggedNode = null;
      simulation.alphaTarget(0.05);
    };

    sigma.on('downNode', handleDownNode);
    captor.on('mousemovebody', handleMouseMoveBody);
    captor.on('mouseup', handleMouseUp);

    const fitTimer = setTimeout(() => sigma.getCamera().animatedReset({ duration: 300 }), 1200);

    return () => {
      clearTimeout(fitTimer);
      simulation.stop();
      sigma.off('downNode', handleDownNode);
      captor.off('mousemovebody', handleMouseMoveBody);
      captor.off('mouseup', handleMouseUp);
    };
  }, [sigma]);

  return null;
}

function GraphInteractions({ meta }: { meta: Map<string, NodeMeta> }) {
  const registerEvents = useRegisterEvents();
  const sigma = useSigma();
  const navigate = useNavigate();

  useEffect(() => {
    registerEvents({
      // Navigation only happens on double-click so a single click can be used
      // to start a drag (see PhysicsSimulation's downNode/mousemovebody
      // handlers) without immediately leaving the graph view.
      doubleClickNode: (event) => {
        event.preventSigmaDefault(); // cancel sigma's default zoom-in-on-double-click behavior
        const info = meta.get(event.node);
        if (!info) return;
        if (info.kind === 'subject' && info.subjectCode) {
          navigate(`/subjects/${info.subjectCode}`);
        } else if (info.kind === 'capability' && info.capabilityId) {
          // Full page navigation into CapabilityGraphMapPage, which already
          // has its own "← Volver a materias" link back to this Home graph.
          navigate(`/capabilities/${info.capabilityId}`);
        }
      },
      enterNode: () => {
        sigma.getContainer().style.cursor = 'pointer';
      },
      leaveNode: () => {
        sigma.getContainer().style.cursor = 'default';
      },
    });
  }, [registerEvents, meta, navigate, sigma]);

  return null;
}

/** Pushes the currently computed graphology graph into the ONE, permanent
 * Sigma instance created by `SigmaContainer` (see MemoryGraphView below,
 * which passes a stable empty graph that is never replaced). This avoids
 * `SigmaContainer` tearing down/recreating the whole Sigma/WebGL canvas
 * (which it otherwise does whenever its `graph` prop reference changes —
 * see `node_modules/@react-sigma/core/src/components/SigmaContainer.tsx`)
 * every time progress/score data arrives and `buildOverviewGraph` produces
 * a new `Graph` instance. */
function GraphSync({ graph }: { graph: Graph }) {
  const loadGraph = useLoadGraph();

  useEffect(() => {
    loadGraph(graph);
  }, [graph, loadGraph]);

  return null;
}

/**
 * Home "memory graph" of courses (2026-07-21, 4th iteration).
 *
 * History: attempt 1 used react-force-graph-2d on a dark background
 * (rejected, "esta horrible"). Attempt 2 switched to Cytoscape.js with an
 * organic cose-bilkent clustering layout (rejected, "muy vieja" — wanted a
 * more modern library). Attempt 3 used Sigma.js with a hand-computed
 * static pyramid layout (rejected — wanted a fully dynamic graph where
 * "los nodos se mueven", referencing Neo4j NVL and then Linkurious Ogma).
 *
 * Both NVL (`@neo4j-nvl/react`) and Ogma (`@linkurious/ogma-react`) were
 * re-checked and ruled out again: NVL's license restricts it to Neo4j
 * Aura/Neo4j's own database products, and Ogma's React wrapper is Apache-
 * 2.0 but its actual rendering engine (`@linkurious/ogma`) isn't on public
 * npm — installing it requires a paid API key from get.linkurio.us
 * (Linkurious commercial license, same vendor family as ReGraph/KeyLines).
 * Neither is usable here without buying a license.
 *
 * This 4th attempt kept Sigma.js (still the most modern free/open-source,
 * WebGL-rendered option) but replaced the static layout with the free
 * ForceAtlas2 web-worker physics simulation (`@react-sigma/layout-
 * forceatlas2`) — nodes started scattered and visibly moved/settled into
 * place. That was replaced again (5th iteration, same day) by `d3-force`
 * (see `PhysicsSimulation` above) after discovering the FA2 worker
 * silently ignored manual drag position changes (see its own doc comment
 * for the full root-cause explanation) — dragging a node never actually
 * pushed its neighbors, which was the whole point of the request.
 *
 * 6th iteration (same day): briefly prototyped an in-place "zoom into a
 * Capability" level that swapped the SAME Sigma canvas's content to that
 * Capability's own node graph — reverted per user feedback ("va a ser
 * mejor llevarlo directamente al link ... pero poder regresar al grafo").
 * Double-clicking a Capability node now simply navigates to its existing
 * full-page map (`CapabilityGraphMapPage`, route `/capabilities/:id`, with
 * dagre layout + the 5-step node workflow), which already has its own
 * "← Volver a materias" link back to this Home graph — simpler than an
 * in-canvas transition, and reuses a page that already existed. See
 * student-graph-ui-redesign-final-design.md / adaptive-learning-engine-
 * design.md for the longer-term "same graph, per-student MasteryStrength
 * overlay" vision this is a step toward.
 */
export default function MemoryGraphView({
  onStats,
}: {
  /** Reports the learner's actual progress (capabilities mastered vs. total
   * tracked) up to the parent so it can show a STATUS counter in the page
   * header — deliberately not "how many capabilities exist", which is
   * catalog/availability info, not the person's own learning status. */
  onStats?: (stats: { mastered: number; total: number }) => void;
}) {
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [capabilities, setCapabilities] = useState<BackendCapabilitySummary[]>([]);
  const [progressById, setProgressById] = useState<Record<string, ProgressState>>({});
  const [scoreById, setScoreById] = useState<Record<string, number>>({});
  const [loading, setLoading] = useState(true);
  const [isDemoData, setIsDemoData] = useState(false);

  useEffect(() => {
    Promise.all([getSubjects(), getCapabilities()])
      .then(([subjectsResult, capabilitiesResult]) => {
        setSubjects(subjectsResult);
        setCapabilities(capabilitiesResult);
        setLoading(false);

        capabilitiesResult
          .filter((c) => c.SubjectCode)
          .forEach((c) => {
            getCapabilityGraph(c.CapabilityId, MOCK_USER.oid)
              .then((graph) => {
                const total = graph.Nodes.length;
                const mastered = graph.Nodes.filter((n) => n.State === 'Mastered').length;
                const started = graph.Nodes.some((n) => n.State !== 'Locked');
                const state: ProgressState =
                  total === 0 ? 'unknown' : mastered === total ? 'mastered' : started ? 'in-progress' : 'not-started';
                setProgressById((prev) => ({ ...prev, [c.CapabilityId]: state }));
                if (total > 0) {
                  setScoreById((prev) => ({ ...prev, [c.CapabilityId]: Math.round((mastered / total) * 100) }));
                }
              })
              .catch(() => setProgressById((prev) => ({ ...prev, [c.CapabilityId]: 'unknown' })));
          });
      })
      .catch(() => {
        setSubjects(DEMO_SUBJECTS);
        setCapabilities(DEMO_CAPABILITIES);
        setProgressById(
          Object.fromEntries(DEMO_CAPABILITIES.map((c) => [c.CapabilityId, c.progress])) as Record<string, ProgressState>,
        );
        setScoreById(
          Object.fromEntries(
            DEMO_CAPABILITIES.map((c) => [c.CapabilityId, DEMO_PROGRESS_SCORE[c.progress]]),
          ),
        );
        setIsDemoData(true);
        setLoading(false);
      });
  }, []);

  const { graph, meta } = useMemo(
    () => buildOverviewGraph(subjects, capabilities, progressById, scoreById),
    [subjects, capabilities, progressById, scoreById],
  );

  const masteredCount = useMemo(
    () => capabilities.filter((c) => progressById[c.CapabilityId] === 'mastered').length,
    [capabilities, progressById],
  );

  useEffect(() => {
    onStats?.({ mastered: masteredCount, total: capabilities.length });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [masteredCount, capabilities.length]);

  // Stable graph instance handed to SigmaContainer ONCE — never replaced, so
  // the Sigma/WebGL canvas itself never gets torn down and recreated every
  // time progress/score data arrives and `buildOverviewGraph` produces a new
  // `Graph` instance. All actual content updates flow through `GraphSync`
  // (via useLoadGraph) into this same instance.
  const [sigmaGraph] = useState(() => new Graph());

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-4 text-xs text-slate-400">
        <LegendDot color={PROGRESS_COLORS['not-started']} label="Sin empezar" />
        <LegendDot color={PROGRESS_COLORS['in-progress']} label="En progreso" />
        <LegendDot color={PROGRESS_COLORS.mastered} label="Dominado" />
        <span className="text-slate-500">Doble clic en una capacidad para ver su mapa de nodos</span>
        {isDemoData && (
          <span className="ml-auto rounded-full bg-amber-400/10 px-2.5 py-1 font-medium text-amber-300">
            Datos de ejemplo (estudiante demo) — backend no disponible
          </span>
        )}
      </div>

      <div className="h-[calc(100vh-16rem)] min-h-[420px] overflow-hidden rounded-3xl border border-white/10 bg-white shadow-2xl shadow-black/20">
        {loading ? (
          <p className="p-6 text-sm text-slate-500">Cargando tu mapa de conocimiento...</p>
        ) : (
          <SigmaContainer
            graph={sigmaGraph}
            style={{ width: '100%', height: '100%', backgroundColor: '#ffffff' }}
            settings={{
              allowInvalidContainer: true,
              renderLabels: true,
              labelSize: 12,
              labelColor: { color: '#334155' },
              labelRenderedSizeThreshold: 0,
              defaultDrawNodeLabel: drawNodeLabelWithScore,
              defaultEdgeColor: '#e2e8f0',
              minCameraRatio: 0.15,
              maxCameraRatio: 3,
            }}
          >
            <GraphSync graph={graph} />
            <GraphInteractions meta={meta} />
            <PhysicsSimulation />
            <ControlsContainer position="bottom-right">
              <ZoomControl />
            </ControlsContainer>
          </SigmaContainer>
        )}
      </div>
    </div>
  );
}

function LegendDot({ color, label }: { color: string; label: string }) {
  return (
    <span className="flex items-center gap-1.5">
      <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: color }} />
      {label}
    </span>
  );
}
