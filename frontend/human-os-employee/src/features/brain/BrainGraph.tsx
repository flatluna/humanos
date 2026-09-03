import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import ForceGraph2D, { type ForceGraphMethods, type NodeObject } from 'react-force-graph-2d';
import { forceCollide } from 'd3-force';
import { LocateFixed, Sparkles, X, Network, ArrowRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { SelectedNeuron } from './types';
import { getMemoryColor, getRetrievability } from './memoryScience';
import { useKnowledgeNetwork, type BridgeConceptItem, type MasteredEngramItem } from './useKnowledgeNetwork';

export interface BrainGraphProps {
  personId: string;
  courseId?: string | null;
  query?: string;
  onSelectNeuron: (neuron: SelectedNeuron) => void;
  onSelectCourse?: (courseId: string | null) => void;
}

type NetworkNodeKind = 'engram' | 'concept';

interface NetworkNode {
  id: string;
  kind: NetworkNodeKind;
  label: string;
  sublabel?: string;
  color: string;
  radius: number;
  recallPercent?: number | null;
  engram?: MasteredEngramItem;
  concept?: BridgeConceptItem;
  x?: number;
  y?: number;
}

interface NetworkLink {
  source: string | NetworkNode;
  target: string | NetworkNode;
  label: string;
  color: string;
}

const ENTITY_TYPE_COLORS: Record<string, string> = {
  Protocol: '#38bdf8', // Light sky blue
  Technology: '#818cf8', // Indigo
  GeneralConcept: '#60a5fa', // Blue
  SocialGroup: '#a78bfa', // Purple
  PersonRole: '#c084fc', // Fuchsia
};

export function BrainGraph({ personId, courseId, query = '', onSelectNeuron, onSelectCourse }: BrainGraphProps) {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement>(null);
  const fgRef = useRef<ForceGraphMethods<NetworkNode, NetworkLink> | undefined>(undefined);
  const [size, setSize] = useState({ width: 800, height: 600 });
  const [selectedConcept, setSelectedConcept] = useState<BridgeConceptItem | null>(null);

  const { data: networkData } = useKnowledgeNetwork(personId, courseId);

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

  const normalizedQuery = query.trim().toLowerCase();
  const matchesQuery = useCallback(
    (label: string) => normalizedQuery === '' || label.toLowerCase().includes(normalizedQuery),
    [normalizedQuery]
  );

  // Build the 2D Force Graph structure (Only Mastered Engrams + Bridge Concepts)
  const graphData = useMemo(() => {
    if (!networkData) return { nodes: [], links: [] };

    const nodes: NetworkNode[] = [];
    const links: NetworkLink[] = [];
    const { masteredEngrams, bridgeConcepts } = networkData;

    // 1. Add Bridge Concepts (Large central spheres, blue/violet tone by type)
    for (const concept of bridgeConcepts) {
      const baseColor = ENTITY_TYPE_COLORS[concept.entityType] || '#38bdf8';
      const connectsCount = concept.connectedEngrams.length;

      nodes.push({
        id: concept.id,
        kind: 'concept',
        label: concept.name,
        sublabel: concept.entityType,
        color: baseColor,
        radius: Math.min(26, 14 + connectsCount * 4),
        concept,
      });

      // Connect Bridge Concept to each Mastered Engram with real relationship label
      for (const engram of concept.connectedEngrams) {
        // Find specific relationship label if available
        const matchingConn = concept.connections.find((c) => c.targetEngramId === engram.sectionId);
        const relLabel = matchingConn ? matchingConn.relationshipLabel : 'relates to';

        links.push({
          source: concept.id,
          target: engram.sectionId,
          label: relLabel,
          color: 'rgba(56, 189, 248, 0.45)',
        });
      }
    }

    // 2. Add Mastered Engrams (Medium spheres, color = FSRS memory retrievability)
    for (const engram of masteredEngrams) {
      const retrievability = getRetrievability(engram.completedDate, engram.reviewCount);
      const recallPercent = retrievability !== null ? Math.round(retrievability * 100) : null;
      const memColor = getMemoryColor(retrievability);

      nodes.push({
        id: engram.sectionId,
        kind: 'engram',
        label: engram.title,
        sublabel: `${recallPercent ?? 100}% ${t('brainNetwork.retention')}`,
        color: memColor,
        radius: 12,
        recallPercent,
        engram,
      });
    }

    return { nodes, links };
  }, [networkData, t]);

  // Configure physics for clean radial layout (Concepts pull toward center, engrams distribute around)
  useEffect(() => {
    const fg = fgRef.current;
    if (!fg) return;
    fg.d3Force('charge')?.strength(-320);
    fg.d3Force(
      'collide',
      forceCollide<NetworkNode>((n) => n.radius + 24) as never
    );
    const linkForce = fg.d3Force('link') as unknown as { distance?: (fn: (l: NetworkLink) => number) => void } | undefined;
    linkForce?.distance?.(() => 90);
    fg.d3ReheatSimulation();
  }, [graphData]);

  const handleCenterView = useCallback(() => {
    fgRef.current?.zoomToFit(400, 80);
  }, []);

  const handleNodeClick = useCallback(
    (node: NodeObject<NetworkNode>) => {
      if (node.kind === 'engram' && node.engram) {
        onSelectNeuron(node.engram.neuron);
      } else if (node.kind === 'concept' && node.concept) {
        setSelectedConcept(node.concept);
      }
    },
    [onSelectNeuron]
  );

  return (
    <div ref={containerRef} className="relative h-full w-full select-none">
      {/* Top Header Overlay with Counter & Course Selector */}
      <div className="absolute left-4 top-4 right-4 z-10 flex flex-wrap items-center justify-between gap-3 pointer-events-none">
        <div className="pointer-events-auto flex flex-wrap items-center gap-2 rounded-2xl border border-white/10 bg-slate-900/85 px-3.5 py-2 backdrop-blur-md shadow-lg">
          <div className="flex items-center gap-2 mr-1">
            <Sparkles className="h-4 w-4 text-amber-400" />
            <span className="text-xs font-bold text-white tracking-wide">
              {t('brainNetwork.title')}
            </span>
          </div>

          {networkData?.availableCourses && networkData.availableCourses.length > 0 && onSelectCourse && (
            <div className="flex items-center gap-1 rounded-xl bg-slate-800/80 p-0.5 border border-white/10 text-[11px]">
              <button
                type="button"
                onClick={() => onSelectCourse(null)}
                className={`px-2 py-0.5 rounded-lg font-medium transition ${
                  !courseId
                    ? 'bg-indigo-600 text-white shadow-xs'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                🌐 {t('brain.all')}
              </button>
              {networkData.availableCourses.map((c) => (
                <button
                  key={c.id}
                  type="button"
                  onClick={() => onSelectCourse(c.id)}
                  className={`px-2 py-0.5 rounded-lg font-medium transition max-w-[140px] truncate ${
                    courseId === c.id
                      ? 'bg-indigo-600 text-white shadow-xs'
                      : 'text-slate-400 hover:text-white'
                  }`}
                  title={c.name}
                >
                  📘 {c.name}
                </button>
              ))}
            </div>
          )}

          {networkData && (
            <span className="inline-flex items-center gap-1 rounded-full bg-indigo-500/20 border border-indigo-400/30 px-2.5 py-0.5 text-[11px] font-semibold text-indigo-200">
              {t('brainNetwork.statsCount', {
                engrams: networkData.masteredEngrams.length,
                bridges: networkData.bridgeConcepts.length,
              })}
            </span>
          )}
        </div>

        <button
          type="button"
          onClick={handleCenterView}
          title={t('brainNetwork.centerView')}
          className="pointer-events-auto flex items-center gap-1.5 rounded-xl border border-white/10 bg-slate-900/80 px-3 py-1.5 text-xs font-medium text-white shadow-lg backdrop-blur transition hover:bg-slate-800"
        >
          <LocateFixed className="h-3.5 w-3.5 text-indigo-400" />
          <span>{t('brainNetwork.centerView')}</span>
        </button>
      </div>

      {/* ForceGraph2D Canvas */}
      <ForceGraph2D<NetworkNode, NetworkLink>
        ref={fgRef as React.MutableRefObject<ForceGraphMethods<NetworkNode, NetworkLink> | undefined>}
        width={size.width}
        height={size.height}
        graphData={graphData}
        backgroundColor="transparent"
        nodeId="id"
        nodeVal={(n) => n.radius}
        nodeLabel={(n) => {
          if (n.kind === 'engram') {
            return `${n.label} (${n.recallPercent ?? 100}% ${t('brainNetwork.retention')}) — ${t('brainNetwork.clickEngramHint')}`;
          }
          return `${n.label} (${n.concept?.connectedEngrams.length} ${t('brainNetwork.engramNode').toLowerCase()}s) — ${t('brainNetwork.clickConceptHint')}`;
        }}
        onNodeClick={handleNodeClick as any}
        linkCanvasObjectMode={() => 'after'}
        linkCanvasObject={(link, ctx, globalScale) => {
          if (globalScale < 0.8) return; // Keep clean when zoomed far out
          const l = link as NetworkLink;
          const s = typeof l.source === 'object' ? l.source : (l.source as any);
          const trg = typeof l.target === 'object' ? l.target : (l.target as any);
          if (!s || !trg || s.x === undefined || trg.x === undefined) return;

          const midX = (s.x + trg.x) / 2;
          const midY = (s.y + trg.y) / 2;
          const label = l.label;
          if (!label) return;

          ctx.save();
          const fontSize = Math.max(9 / globalScale, 3.5);
          ctx.font = `italic ${fontSize}px sans-serif`;
          const textWidth = ctx.measureText(label).width;

          // Background pill behind label
          ctx.fillStyle = 'rgba(15, 23, 42, 0.85)';
          ctx.fillRect(midX - textWidth / 2 - 2, midY - fontSize / 2 - 1, textWidth + 4, fontSize + 2);

          // Label text
          ctx.fillStyle = '#94a3b8';
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.fillText(label, midX, midY);
          ctx.restore();
        }}
        nodeCanvasObject={(node, ctx, globalScale) => {
          const n = node as NetworkNode;
          if (n.x === undefined || n.y === undefined) return;
          const dim = !matchesQuery(n.label);
          const isConcept = n.kind === 'concept';

          ctx.save();
          ctx.globalAlpha = dim ? 0.25 : 1;

          // Radial glow gradient for node sphere
          const grad = ctx.createRadialGradient(n.x, n.y, 0.5, n.x, n.y, n.radius);
          grad.addColorStop(0, '#ffffff');
          grad.addColorStop(0.3, n.color);
          grad.addColorStop(1, isConcept ? '#1e1b4b' : n.color);

          ctx.beginPath();
          ctx.arc(n.x, n.y, n.radius, 0, 2 * Math.PI);
          ctx.fillStyle = grad;
          ctx.fill();

          ctx.lineWidth = isConcept ? 2.5 : 1.5;
          ctx.strokeStyle = isConcept ? 'rgba(255, 255, 255, 0.8)' : 'rgba(255, 255, 255, 0.5)';
          ctx.stroke();
          ctx.restore();

          // Permanent Crisp Text Labels
          ctx.save();
          ctx.globalAlpha = dim ? 0.35 : 1;
          const fontSize = isConcept
            ? Math.max(12 / globalScale, 4.2)
            : Math.max(10 / globalScale, 3.8);

          ctx.font = `${isConcept ? 'bold' : '600'} ${fontSize}px sans-serif`;
          ctx.textAlign = 'center';
          ctx.textBaseline = 'top';

          // Text shadow outline for readability over dark background
          ctx.lineWidth = 3;
          ctx.strokeStyle = 'rgba(2, 6, 23, 0.9)';
          ctx.strokeText(n.label, n.x, n.y + n.radius + 3);

          ctx.fillStyle = isConcept ? '#e0e7ff' : '#ffffff';
          ctx.fillText(n.label, n.x, n.y + n.radius + 3);

          if (n.sublabel && (globalScale > 0.65 || isConcept)) {
            const subFontSize = Math.max(8.5 / globalScale, 3);
            ctx.font = `italic ${subFontSize}px sans-serif`;
            ctx.fillStyle = isConcept ? '#38bdf8' : '#a7f3d0';
            ctx.strokeText(n.sublabel, n.x, n.y + n.radius + fontSize + 4);
            ctx.fillText(n.sublabel, n.x, n.y + n.radius + fontSize + 4);
          }

          ctx.restore();
        }}
        nodePointerAreaPaint={(node, color, ctx) => {
          const n = node as NetworkNode;
          if (n.x === undefined || n.y === undefined) return;
          ctx.fillStyle = color;
          ctx.beginPath();
          ctx.arc(n.x, n.y, n.radius + 4, 0, 2 * Math.PI);
          ctx.fill();
        }}
        enableNodeDrag
        minZoom={0.2}
        maxZoom={5}
        onEngineStop={() => fgRef.current?.zoomToFit(400, 80)}
      />

      {/* Concept Inspector Sidebar Panel when clicking a Bridge Concept */}
      {selectedConcept && (
        <div className="absolute right-4 top-16 bottom-4 w-80 max-w-[90%] z-20 flex flex-col rounded-3xl border border-indigo-500/30 bg-slate-900/95 p-5 shadow-2xl backdrop-blur-md overflow-hidden animate-fade-in text-white">
          <div className="flex items-center justify-between pb-3 border-b border-white/10">
            <div className="flex items-center gap-2">
              <Network className="h-4 w-4 text-sky-400" />
              <h3 className="text-sm font-bold text-white">{selectedConcept.name}</h3>
            </div>
            <button
              type="button"
              onClick={() => setSelectedConcept(null)}
              className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white transition"
              aria-label="Close"
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="mt-2 text-xs text-slate-400">
            <span className="font-semibold text-sky-300">{selectedConcept.entityType}</span>
            {selectedConcept.ontologicalClass && ` · Class: ${selectedConcept.ontologicalClass}`}
          </div>

          <p className="mt-3 text-xs text-indigo-200">
            {t('brainNetwork.connectedEngramsTitle', {
              concept: selectedConcept.name,
              count: selectedConcept.connectedEngrams.length,
            })}
          </p>

          <div className="mt-3 flex-1 overflow-y-auto space-y-2.5 pr-1">
            {selectedConcept.connectedEngrams.map((engram) => {
              const rel = selectedConcept.connections.find((c) => c.targetEngramId === engram.sectionId);
              const retrievability = getRetrievability(engram.completedDate, engram.reviewCount);
              const percent = retrievability !== null ? Math.round(retrievability * 100) : 100;
              const memColor = getMemoryColor(retrievability);

              return (
                <button
                  key={engram.sectionId}
                  type="button"
                  onClick={() => onSelectNeuron(engram.neuron)}
                  className="w-full text-left rounded-2xl border border-white/10 bg-white/[0.03] p-3 hover:bg-white/[0.08] hover:border-indigo-400/40 transition group"
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-xs font-bold text-white group-hover:text-indigo-300 transition line-clamp-1">
                      {engram.title}
                    </span>
                    <span
                      className="px-2 py-0.5 rounded-full text-[10px] font-bold shrink-0"
                      style={{ background: `${memColor}25`, color: memColor }}
                    >
                      {percent}%
                    </span>
                  </div>

                  <div className="mt-1 text-[11px] text-slate-400 truncate">
                    {engram.documentName}
                  </div>

                  {rel && (
                    <div className="mt-2 flex items-center gap-1.5 text-[10px] text-sky-300 italic bg-sky-500/10 rounded-lg px-2 py-1">
                      <ArrowRight className="h-3 w-3 shrink-0" />
                      <span className="truncate">{rel.relationshipLabel}</span>
                    </div>
                  )}
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}

