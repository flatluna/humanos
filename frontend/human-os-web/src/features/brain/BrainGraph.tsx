import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import ForceGraph2D, { type ForceGraphMethods, type NodeObject } from 'react-force-graph-2d';
import { forceCollide } from 'd3-force';
import { LocateFixed, Maximize2 } from 'lucide-react';
import type { RootGroup, SelectedNeuron } from './types';

/** Same 8-color palette used across the feature — index shared between a
 *  root's area, its capabilities and its neurons so everything belonging
 *  to one enrolled Program/Capability reads as one visual "family". */
const CLUSTER_HEX = ['#22d3ee', '#e879f9', '#34d399', '#fbbf24', '#fb7185', '#38bdf8', '#a3e635', '#f87171'];

type NodeKind = 'brain' | 'area' | 'capability' | 'neuron';

interface GNode {
  id: string;
  kind: NodeKind;
  label: string;
  colorIndex: number;
  radius: number;
  neuron?: SelectedNeuron;
  x?: number;
  y?: number;
}

interface GLink {
  source: string;
  target: string;
  kind: 'hierarchy' | 'prereq';
  hex: string;
}

const BRAIN_ID = 'brain-root';

/** Static structural description of the whole graph — every area, every
 *  capability, every mastered neuron, and every real prerequisite
 *  relationship between neurons — plus a `childrenOf` map used to
 *  expand/collapse subtrees. Rebuilt only when the underlying enrolled
 *  data changes (not on every physics tick). */
function buildStructure(rootGroups: RootGroup[]) {
  const nodes = new Map<string, GNode>();
  const links: GLink[] = [];
  const childrenOf = new Map<string, string[]>();
  const addChild = (parent: string, child: string) => {
    const list = childrenOf.get(parent) ?? [];
    list.push(child);
    childrenOf.set(parent, list);
  };

  nodes.set(BRAIN_ID, { id: BRAIN_ID, kind: 'brain', label: 'BRIAN', colorIndex: 0, radius: 22 });

  rootGroups.forEach((root, i) => {
    const colorIndex = i % CLUSTER_HEX.length;
    const areaId = `area-${root.rootId}`;
    nodes.set(areaId, { id: areaId, kind: 'area', label: root.rootName, colorIndex, radius: 15 });
    links.push({ source: BRAIN_ID, target: areaId, kind: 'hierarchy', hex: '#c084fc' });
    addChild(BRAIN_ID, areaId);

    root.clusters.forEach((cluster) => {
      const capId = `cap-${cluster.capabilityId}`;
      nodes.set(capId, { id: capId, kind: 'capability', label: cluster.capabilityName, colorIndex, radius: 11 });
      links.push({ source: areaId, target: capId, kind: 'hierarchy', hex: CLUSTER_HEX[colorIndex] });
      addChild(areaId, capId);

      cluster.nodes.forEach((node) => {
        const nid = node.capabilityGraphNodeId;
        nodes.set(nid, {
          id: nid,
          kind: 'neuron',
          label: node.name,
          colorIndex,
          radius: 8,
          neuron: { ...node, capabilityId: cluster.capabilityId, capabilityName: cluster.capabilityName },
        });
        links.push({ source: capId, target: nid, kind: 'hierarchy', hex: CLUSTER_HEX[colorIndex] });
        addChild(capId, nid);
      });

      // Real prerequisite relationships between already-mastered neurons —
      // this is what makes it a genuine knowledge graph instead of a plain
      // tree: extra edges that don't follow the area/capability hierarchy.
      cluster.edges.forEach((edge) => {
        links.push({ source: edge.sourceNodeId, target: edge.targetNodeId, kind: 'prereq', hex: '#fde047' });
      });
    });
  });

  return { nodes, links, childrenOf };
}

function truncate(label: string, max: number) {
  return label.length > max ? `${label.slice(0, max - 1)}…` : label;
}

export interface BrainGraphProps {
  rootGroups: RootGroup[];
  query: string;
  onSelectNeuron: (neuron: SelectedNeuron) => void;
}

/** A living, physics-driven knowledge graph (à la Neo4j Bloom) built on
 *  `react-force-graph-2d`: nodes drift/repel/settle via a real d3-force
 *  simulation, can be dragged and re-pinned, panned/zoomed freely, and
 *  every hub (area/capability) can be clicked to collapse/expand its
 *  subtree — scaling comfortably to hundreds of nodes since everything is
 *  canvas-rendered rather than real DOM elements. */
export function BrainGraph({ rootGroups, query, onSelectNeuron }: BrainGraphProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const fgRef = useRef<ForceGraphMethods<GNode, GLink> | undefined>(undefined);
  const nodePool = useRef(new Map<string, GNode>());
  const [size, setSize] = useState({ width: 800, height: 600 });
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (!entry) return;
      setSize({ width: entry.contentRect.width, height: entry.contentRect.height });
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const structure = useMemo(() => buildStructure(rootGroups), [rootGroups]);

  const normalizedQuery = query.trim().toLowerCase();
  const matchesQuery = useCallback(
    (label: string) => normalizedQuery === '' || label.toLowerCase().includes(normalizedQuery),
    [normalizedQuery]
  );

  // Recompute which nodes/links are visible given the current collapse
  // state — a BFS from BRIAN that doesn't descend past a collapsed node,
  // reusing the SAME node object references across rebuilds (via
  // `nodePool`) so react-force-graph preserves x/y positions and the
  // physics simulation doesn't jump/reset on every expand/collapse click.
  const graphData = useMemo(() => {
    const visible = new Set<string>();
    const stack = [BRAIN_ID];
    while (stack.length > 0) {
      const id = stack.pop();
      if (!id || visible.has(id)) continue;
      visible.add(id);
      if (collapsed.has(id)) continue;
      const children = structure.childrenOf.get(id) ?? [];
      stack.push(...children);
    }

    const pool = nodePool.current;
    const nodes: GNode[] = [];
    visible.forEach((id) => {
      const source = structure.nodes.get(id);
      if (!source) return;
      let n = pool.get(id);
      if (!n) {
        n = { ...source };
        pool.set(id, n);
      } else {
        n.label = source.label;
        n.colorIndex = source.colorIndex;
        n.neuron = source.neuron;
      }
      nodes.push(n);
    });

    // `react-force-graph-2d`'s underlying d3-force `forceLink` mutates each
    // link's `source`/`target` in place, replacing the string id with the
    // actual resolved node object once the simulation initializes. Since
    // `structure.links` is a stable, reused array, filtering it directly
    // (and reusing the very same link objects) would let that mutation
    // leak into our source-of-truth data — after the first physics tick,
    // `l.source`/`l.target` would no longer be strings, so `visible.has(...)`
    // would silently stop matching and every link would vanish on the next
    // recompute (e.g. any expand/collapse click). Guard against both forms
    // and hand the graph FRESH link objects every time so mutation never
    // touches `structure.links`.
    const idOf = (endpoint: string | GNode) => (typeof endpoint === 'object' ? endpoint.id : endpoint);
    const links: GLink[] = structure.links
      .filter((l) => visible.has(idOf(l.source)) && visible.has(idOf(l.target)))
      .map((l) => ({ ...l }));

    return { nodes, links };
  }, [structure, collapsed]);

  const handleNodeClick = useCallback(
    (node: NodeObject<GNode>) => {
      if (node.kind === 'neuron') {
        if (node.neuron) onSelectNeuron(node.neuron);
        return;
      }
      if (node.id === BRAIN_ID) {
        fgRef.current?.zoomToFit(400, 60);
        return;
      }
      setCollapsed((prev) => {
        const next = new Set(prev);
        const id = String(node.id);
        if (next.has(id)) next.delete(id);
        else next.add(id);
        return next;
      });
    },
    [onSelectNeuron]
  );

  const handleResetView = useCallback(() => {
    setCollapsed(new Set());
    window.setTimeout(() => fgRef.current?.zoomToFit(400, 60), 50);
  }, []);

  // Just re-fit/re-center the camera on whatever is CURRENTLY visible —
  // unlike "Expand all", this does NOT touch collapse state, so it's safe
  // to use anytime the user drags/zooms/pans and wants to snap back.
  const handleCenterView = useCallback(() => {
    fgRef.current?.zoomToFit(400, 60);
  }, []);

  // Configure the physics ONCE, on mount (ForceGraph2D's own mount effect
  // always flushes before this parent effect, so `fgRef.current` is
  // guaranteed to exist here) — a `structureRef` lets the link-distance
  // function always see the latest structural data without needing to
  // re-register (and re-race) the force config on every graphData change.
  // Adds a real collision force so same-generation nodes never overlap,
  // which is what keeps a 100+ node graph legible instead of a knot.
  const structureRef = useRef(structure);
  structureRef.current = structure;

  useEffect(() => {
    const fg = fgRef.current;
    if (!fg) return;
    fg.d3Force('charge')?.strength(-220);
    fg.d3Force(
      'collide',
      forceCollide<GNode>((n) => n.radius + 10) as never
    );
    const linkForce = fg.d3Force('link') as unknown as { distance?: (fn: (l: GLink) => number) => void } | undefined;
    linkForce?.distance?.((l: GLink) => {
      if (l.kind === 'prereq') return 60;
      const targetKind =
        typeof l.target === 'object'
          ? (l.target as GNode).kind
          : structureRef.current.nodes.get(String(l.target))?.kind;
      if (targetKind === 'area') return 160;
      if (targetKind === 'capability') return 100;
      return 60;
    });
    fg.d3ReheatSimulation();
    // Mount-only: force config is applied once and read fresh data via refs.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Whenever the visible node/link set changes (data load, expand/collapse,
  // search), reheat the simulation so it re-settles with the already
  // configured forces above.
  useEffect(() => {
    fgRef.current?.d3ReheatSimulation();
  }, [graphData]);

  return (
    <div ref={containerRef} className="relative h-full w-full">
      <div className="absolute right-3 top-3 z-10 flex items-center gap-2">
        <button
          type="button"
          onClick={handleCenterView}
          title="Center graph"
          className="flex items-center gap-1.5 rounded-lg border border-white/10 bg-slate-900/80 px-2.5 py-1.5 text-xs font-medium text-white/80 shadow-lg backdrop-blur transition hover:bg-slate-800 hover:text-white"
        >
          <LocateFixed className="h-3.5 w-3.5" />
          Center
        </button>
        <button
          type="button"
          onClick={handleResetView}
          className="flex items-center gap-1.5 rounded-lg border border-white/10 bg-slate-900/80 px-2.5 py-1.5 text-xs font-medium text-white/80 shadow-lg backdrop-blur transition hover:bg-slate-800 hover:text-white"
        >
          <Maximize2 className="h-3.5 w-3.5" />
          Expand all
        </button>
      </div>
      <ForceGraph2D<GNode, GLink>
        ref={fgRef as React.MutableRefObject<ForceGraphMethods<GNode, GLink> | undefined>}
        width={size.width}
        height={size.height}
        graphData={graphData}
        backgroundColor="transparent"
        nodeId="id"
        nodeVal={(n) => n.radius}
        nodeLabel={(n) => n.label}
        nodeCanvasObject={(node, ctx, globalScale) => {
          const n = node as GNode;
          if (n.x === undefined || n.y === undefined) return;
          const dim = !matchesQuery(n.label) && n.kind !== 'brain';
          const hex = n.kind === 'brain' ? '#e879f9' : CLUSTER_HEX[n.colorIndex];
          ctx.save();
          ctx.globalAlpha = dim ? 0.25 : 1;
          const grad = ctx.createRadialGradient(n.x, n.y, 0.5, n.x, n.y, n.radius);
          grad.addColorStop(0, '#ffffff');
          grad.addColorStop(0.25, hex);
          grad.addColorStop(1, n.kind === 'brain' ? '#4338ca' : hex);
          ctx.beginPath();
          ctx.arc(n.x, n.y, n.radius, 0, 2 * Math.PI);
          ctx.fillStyle = grad;
          ctx.fill();
          ctx.lineWidth = n.kind === 'neuron' ? 1.25 : 2;
          ctx.strokeStyle = 'rgba(255,255,255,0.55)';
          ctx.stroke();
          ctx.restore();

          if (globalScale > 0.35 || n.kind !== 'neuron') {
            ctx.save();
            ctx.globalAlpha = dim ? 0.35 : 1;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'top';
            if (n.kind === 'brain') {
              const fontSize = Math.max(9, n.radius * 0.55);
              ctx.font = `bold ${fontSize}px sans-serif`;
              ctx.textBaseline = 'middle';
              ctx.fillStyle = '#fff';
              ctx.fillText('BRIAN', n.x, n.y);
            } else {
              const fontSize = Math.max(2.4, 9 / globalScale);
              ctx.font = `${fontSize}px sans-serif`;
              ctx.fillStyle = '#e2e8f0';
              ctx.fillText(truncate(n.label, n.kind === 'area' ? 20 : 16), n.x, n.y + n.radius + 2);
            }
            ctx.restore();
          }
        }}
        nodePointerAreaPaint={(node, color, ctx) => {
          const n = node as GNode;
          if (n.x === undefined || n.y === undefined) return;
          ctx.fillStyle = color;
          ctx.beginPath();
          ctx.arc(n.x, n.y, n.radius + 2, 0, 2 * Math.PI);
          ctx.fill();
        }}
        onNodeClick={handleNodeClick as (node: NodeObject<GNode>) => void}
        linkColor={(l) => (l as unknown as GLink).hex}
        linkWidth={(l) => ((l as unknown as GLink).kind === 'prereq' ? 1 : 1.5)}
        linkLineDash={(l) => ((l as unknown as GLink).kind === 'prereq' ? [3, 3] : null)}
        linkDirectionalParticles={(l) => ((l as unknown as GLink).kind === 'hierarchy' ? 1 : 0)}
        linkDirectionalParticleWidth={2}
        linkDirectionalParticleSpeed={0.004}
        linkDirectionalParticleColor={(l) => (l as unknown as GLink).hex}
        cooldownTime={15000}
        onEngineStop={() => fgRef.current?.zoomToFit(400, 60)}
        enableNodeDrag
        minZoom={0.1}
        maxZoom={6}
      />
    </div>
  );
}
