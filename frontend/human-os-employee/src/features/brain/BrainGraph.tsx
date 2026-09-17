import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import ForceGraph2D, { type ForceGraphMethods, type NodeObject } from 'react-force-graph-2d';
import { forceCollide } from 'd3-force';
import { LocateFixed, Sparkles, X, Network, ArrowRight, Bot, Search } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { SelectedNeuron } from './types';
import { getMemoryColor, getRetrievability } from './memoryScience';
import {
  useKnowledgeNetwork,
  isArticleOrStructuralEntity,
  type BridgeConceptItem,
  type MasteredEngramItem,
} from './useKnowledgeNetwork';
import { ConceptAgentModal } from './ConceptAgentModal';

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
  Article: '#f59e0b', // Amber
  LegalArticle: '#f59e0b', // Amber
  LawArticle: '#f59e0b', // Amber
  LegalProvision: '#f59e0b', // Amber
  Clause: '#fbbf24', // Warm Amber
  LegalReference: '#fbbf24', // Warm Amber
  LegalDocument: '#38bdf8', // Light sky blue
  LegalRule: '#818cf8', // Indigo
  Protocol: '#38bdf8', // Light sky blue
  Technology: '#818cf8', // Indigo
  GeneralConcept: '#60a5fa', // Blue
  SocialGroup: '#a78bfa', // Purple
  PersonRole: '#c084fc', // Fuchsia
  FinancialConcept: '#34d399', // Emerald
  FinancialStructure: '#10b981', // Emerald
  Crime: '#f43f5e', // Rose
  CriminalOrganization: '#e11d48', // Crimson
  AdministrativeMeasure: '#a855f7', // Purple
  Procedure: '#06b6d4', // Cyan
  AuthorityRole: '#6366f1', // Indigo
};

export function BrainGraph({ personId, courseId, query = '', onSelectNeuron, onSelectCourse }: BrainGraphProps) {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement>(null);
  const fgRef = useRef<ForceGraphMethods<NetworkNode, NetworkLink> | undefined>(undefined);
  const [size, setSize] = useState({ width: 800, height: 600 });
  const [selectedConcept, setSelectedConcept] = useState<BridgeConceptItem | null>(null);
  const [agentConcept, setAgentConcept] = useState<BridgeConceptItem | null>(null);

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
  // When a concept is selected (e.g. HTTP), isolate the graph to ONLY show that
  // concept and its connected engrams, closing/hiding all other unrelated nodes.
  const graphData = useMemo(() => {
    if (!networkData) return { nodes: [], links: [] };

    const nodes: NetworkNode[] = [];
    const links: NetworkLink[] = [];
    const { masteredEngrams, bridgeConcepts } = networkData;

    // Filter concepts and engrams if a concept is selected
    const filteredConcepts = selectedConcept
      ? bridgeConcepts.filter((c) => c.id === selectedConcept.id)
      : bridgeConcepts;

    const visibleEngramIds = new Set<string>();
    if (selectedConcept) {
      for (const engram of selectedConcept.connectedEngrams) {
        visibleEngramIds.add(engram.sectionId);
      }
    }

    // 1. Add Bridge Concepts (Large central spheres, blue/violet tone by type)
    for (const concept of filteredConcepts) {
      const baseColor = ENTITY_TYPE_COLORS[concept.entityType] || '#38bdf8';
      const connectsCount = concept.connectedEngrams.length;

      nodes.push({
        id: concept.id,
        kind: 'concept',
        label: concept.name,
        sublabel: concept.entityType,
        color: baseColor,
        radius: selectedConcept ? Math.min(32, 18 + connectsCount * 4) : Math.min(26, 14 + connectsCount * 4),
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
    const filteredEngrams = selectedConcept
      ? masteredEngrams.filter((e) => visibleEngramIds.has(e.sectionId))
      : masteredEngrams;

    for (const engram of filteredEngrams) {
      const retrievability = getRetrievability(engram.completedDate, engram.reviewCount);
      const recallPercent = retrievability !== null ? Math.round(retrievability * 100) : null;
      const memColor = getMemoryColor(retrievability);

      nodes.push({
        id: engram.sectionId,
        kind: 'engram',
        label: engram.title,
        sublabel: `${recallPercent ?? 100}% ${t('brainNetwork.retention')}`,
        color: memColor,
        radius: selectedConcept ? 15 : 12,
        recallPercent,
        engram,
      });
    }

    return { nodes, links };
  }, [networkData, selectedConcept, t]);

  // Configure physics for clean radial layout (Concepts pull toward center, engrams distribute around)
  useEffect(() => {
    const fg = fgRef.current;
    if (!fg) return;
    fg.d3Force('charge')?.strength(selectedConcept ? -480 : -320);
    fg.d3Force(
      'collide',
      forceCollide<NetworkNode>((n) => n.radius + (selectedConcept ? 36 : 24)) as never
    );
    const linkForce = fg.d3Force('link') as unknown as { distance?: (fn: (l: NetworkLink) => number) => void } | undefined;
    linkForce?.distance?.(() => (selectedConcept ? 120 : 90));
    fg.d3ReheatSimulation();

    // Smoothly center and fit the view when focus changes
    const timer = setTimeout(() => {
      fg.zoomToFit(400, selectedConcept ? 140 : 80);
    }, 200);
    return () => clearTimeout(timer);
  }, [graphData, selectedConcept]);

  const handleCenterView = useCallback(() => {
    fgRef.current?.zoomToFit(400, selectedConcept ? 140 : 80);
  }, [selectedConcept]);

  const [conceptFilterTab, setConceptFilterTab] = useState<'all' | 'articles' | 'concepts'>('all');
  const [conceptSearch, setConceptSearch] = useState('');

  const { allConcepts, articleConcepts, regularConcepts } = useMemo(() => {
    if (!networkData) return { allConcepts: [], articleConcepts: [], regularConcepts: [] };
    const all = networkData.bridgeConcepts;
    const articles: BridgeConceptItem[] = [];
    const regulars: BridgeConceptItem[] = [];

    for (const c of all) {
      if (isArticleOrStructuralEntity(c.entityType, c.name)) {
        articles.push(c);
      } else {
        regulars.push(c);
      }
    }

    // Natural sort for articles (e.g. Artículo 1, Artículo 2, Artículo 10...)
    articles.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' }));

    // Most-connected first for regular concepts
    regulars.sort((a, b) => b.connectedEngrams.length - a.connectedEngrams.length || a.name.localeCompare(b.name));

    return { allConcepts: all, articleConcepts: articles, regularConcepts: regulars };
  }, [networkData]);

  const displayedConcepts = useMemo(() => {
    let list =
      conceptFilterTab === 'articles'
        ? articleConcepts
        : conceptFilterTab === 'concepts'
        ? regularConcepts
        : [...articleConcepts, ...regularConcepts];

    if (conceptSearch.trim()) {
      const q = conceptSearch.trim().toLowerCase();
      list = list.filter((c) => c.name.toLowerCase().includes(q) || c.entityType.toLowerCase().includes(q));
    }

    return list;
  }, [conceptFilterTab, conceptSearch, articleConcepts, regularConcepts]);

  const handleSelectConceptFromList = useCallback((concept: BridgeConceptItem) => {
    setSelectedConcept((prev) => (prev?.id === concept.id ? null : concept));
  }, []);

  const handleNodeClick = useCallback(
    (node: NodeObject<NetworkNode>) => {
      if (node.kind === 'engram' && node.engram) {
        onSelectNeuron(node.engram.neuron);
      } else if (node.kind === 'concept' && node.concept) {
        setSelectedConcept((prev) => (prev?.id === node.concept?.id ? null : node.concept ?? null));
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

          {selectedConcept ? (
            <div className="flex items-center gap-2 rounded-xl bg-indigo-500/20 border border-indigo-400/40 px-2.5 py-1 text-xs text-white">
              <span className="h-2 w-2 rounded-full" style={{ background: ENTITY_TYPE_COLORS[selectedConcept.entityType] || '#38bdf8' }} />
              <span className="font-semibold">{t('brainNetwork.focusOn', { concept: selectedConcept.name })}</span>
              <span className="text-[11px] text-indigo-200">
                ({selectedConcept.connectedEngrams.length} {t('brainNetwork.engramNode').toLowerCase()})
              </span>
              <button
                type="button"
                onClick={() => setSelectedConcept(null)}
                className="ml-1.5 flex items-center gap-1 rounded-lg bg-white/10 px-2 py-0.5 text-[11px] font-bold text-white hover:bg-white/20 transition"
                title={t('brainNetwork.showFullGraph')}
              >
                <X className="h-3 w-3" />
                <span>{t('brainNetwork.showFullGraph')}</span>
              </button>
            </div>
          ) : (
            <>
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
            </>
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
        onBackgroundClick={() => setSelectedConcept(null)}
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

      {/* Left "Entities & Concepts" list */}
      {(articleConcepts.length > 0 || regularConcepts.length > 0) && (
        <div className="absolute left-4 top-16 bottom-4 z-10 flex w-72 flex-col rounded-3xl border border-white/10 bg-slate-900/90 p-3.5 shadow-2xl backdrop-blur-md">
          {/* Header */}
          <div className="flex items-center justify-between px-1 pb-2 border-b border-white/10 text-xs font-bold text-slate-200">
            <span className="flex items-center gap-1.5 uppercase tracking-wide text-sky-400">
              <Network className="h-3.5 w-3.5" />
              {t('brainNetwork.conceptsListTitle')}
            </span>
            <span className="rounded-full bg-white/10 px-2 py-0.5 text-[10px] font-semibold text-slate-300">
              {allConcepts.length}
            </span>
          </div>

          {/* Filter Tabs: Todos | Artículos | Conceptos */}
          <div className="mt-2.5 flex items-center gap-1 rounded-xl bg-slate-950/60 p-1 border border-white/5 text-[11px]">
            <button
              type="button"
              onClick={() => setConceptFilterTab('all')}
              className={`flex-1 rounded-lg py-1 text-center font-medium transition cursor-pointer ${
                conceptFilterTab === 'all'
                  ? 'bg-indigo-500/30 text-white font-semibold shadow-xs border border-indigo-400/40'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              {t('brainNetwork.tabAll')} ({allConcepts.length})
            </button>
            {articleConcepts.length > 0 && (
              <button
                type="button"
                onClick={() => setConceptFilterTab('articles')}
                className={`flex-1 rounded-lg py-1 text-center font-medium transition cursor-pointer ${
                  conceptFilterTab === 'articles'
                    ? 'bg-amber-500/25 text-amber-200 font-semibold shadow-xs border border-amber-400/40'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                {t('brainNetwork.tabArticles')} ({articleConcepts.length})
              </button>
            )}
            {regularConcepts.length > 0 && (
              <button
                type="button"
                onClick={() => setConceptFilterTab('concepts')}
                className={`flex-1 rounded-lg py-1 text-center font-medium transition cursor-pointer ${
                  conceptFilterTab === 'concepts'
                    ? 'bg-sky-500/25 text-sky-200 font-semibold shadow-xs border border-sky-400/40'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                {t('brainNetwork.tabConcepts')} ({regularConcepts.length})
              </button>
            )}
          </div>

          {/* Search Input */}
          <div className="relative mt-2">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-500" />
            <input
              value={conceptSearch}
              onChange={(e) => setConceptSearch(e.target.value)}
              placeholder={t('brainNetwork.searchPlaceholder')}
              className="w-full rounded-xl border border-white/10 bg-white/[0.04] py-1.5 pl-8 pr-6 text-xs text-white placeholder:text-slate-500 focus:border-indigo-400 focus:outline-none"
            />
            {conceptSearch && (
              <button
                type="button"
                onClick={() => setConceptSearch('')}
                className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white cursor-pointer"
              >
                <X className="h-3 w-3" />
              </button>
            )}
          </div>

          {/* Items List */}
          <div className="mt-2.5 flex-1 space-y-1 overflow-y-auto pr-1">
            {displayedConcepts.length === 0 ? (
              <p className="p-3 text-center text-xs text-slate-500">{t('brainNetwork.noEntitiesFound')}</p>
            ) : (
              displayedConcepts.map((concept) => {
                const isArt = isArticleOrStructuralEntity(concept.entityType, concept.name);
                const count = concept.connectedEngrams.length;
                const color = ENTITY_TYPE_COLORS[concept.entityType] || (isArt ? '#f59e0b' : '#38bdf8');
                const isActive = selectedConcept?.id === concept.id;
                return (
                  <button
                    key={concept.id}
                    type="button"
                    onClick={() => handleSelectConceptFromList(concept)}
                    className={`flex w-full items-center justify-between gap-2 rounded-xl border px-2.5 py-1.5 text-left text-xs transition cursor-pointer ${
                      isActive
                        ? isArt
                          ? 'border-amber-400/60 bg-amber-500/20 text-white shadow-xs'
                          : 'border-indigo-400/50 bg-indigo-500/20 text-white shadow-xs'
                        : isArt
                        ? 'border-amber-500/10 bg-amber-500/[0.04] text-slate-200 hover:border-amber-400/30 hover:bg-amber-500/10'
                        : 'border-transparent bg-white/[0.03] text-slate-300 hover:bg-white/[0.08] hover:text-white'
                    }`}
                  >
                    <span className="flex min-w-0 items-center gap-1.5">
                      <span className="h-2 w-2 shrink-0 rounded-full" style={{ background: color }} />
                      <span className={`truncate ${isArt ? 'font-semibold text-amber-100' : 'font-medium'}`}>
                        {concept.name}
                      </span>
                    </span>
                    <span className="shrink-0 rounded-full bg-white/10 px-1.5 py-0.2 text-[10px] font-bold text-slate-300">
                      {count}
                    </span>
                  </button>
                );
              })
            )}
          </div>
        </div>
      )}

      {/* Concept Inspector Sidebar Panel when clicking a Bridge Concept */}
      {selectedConcept && (
        <div className="absolute right-4 top-16 bottom-4 w-80 max-w-[90%] z-20 flex flex-col rounded-3xl border border-indigo-500/30 bg-slate-900/95 p-5 shadow-2xl backdrop-blur-md overflow-hidden animate-fade-in text-white">
          {/* Agent Trigger Button Above Title */}
          <div className="mb-3">
            <button
              type="button"
              onClick={() => setAgentConcept(selectedConcept)}
              className="flex w-full items-center justify-center gap-2 rounded-2xl bg-gradient-to-r from-brand-500 via-indigo-500 to-accent-500 px-4 py-2.5 text-xs font-bold text-white shadow-lg shadow-brand-500/25 transition-all hover:scale-[1.02] active:scale-[0.98]"
            >
              <Bot className="h-4 w-4" />
              <span>{t('brainNetwork.talkWithAgent')}</span>
              <Sparkles className="h-3.5 w-3.5 opacity-80" />
            </button>
          </div>

          <div className="flex items-center justify-between pb-3 border-b border-white/10">
            <div className="flex items-center gap-2 min-w-0">
              <Network className="h-4 w-4 text-sky-400 shrink-0" />
              <h3 className="text-sm font-bold text-white truncate">{selectedConcept.name}</h3>
            </div>
            <button
              type="button"
              onClick={() => setSelectedConcept(null)}
              className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white transition shrink-0 ml-2"
              aria-label="Close"
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="mt-2 flex items-center gap-2 text-xs text-slate-400">
            <span
              className={`rounded-md border px-1.5 py-0.5 text-[10px] font-semibold ${
                isArticleOrStructuralEntity(selectedConcept.entityType, selectedConcept.name)
                  ? 'border-amber-400/40 bg-amber-500/15 text-amber-300'
                  : 'border-sky-400/30 bg-sky-500/10 text-sky-300'
              }`}
            >
              {selectedConcept.entityType}
            </span>
            {selectedConcept.ontologicalClass && (
              <span className="text-[11px] text-slate-400">· Clase: {selectedConcept.ontologicalClass}</span>
            )}
          </div>

          {selectedConcept.description && (
            <p className="mt-2 rounded-xl border border-white/10 bg-white/[0.03] p-2.5 text-xs leading-5 text-slate-200">
              {selectedConcept.description}
            </p>
          )}

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
                    <div className="mt-2 rounded-lg bg-sky-500/10 px-2 py-1">
                      <div className="flex items-center gap-1.5 text-[10px] text-sky-300 italic">
                        <ArrowRight className="h-3 w-3 shrink-0" />
                        <span className="truncate">{rel.relationshipLabel}</span>
                      </div>
                      {rel.evidence && (
                        <p className="mt-1 text-[10px] italic text-slate-400 line-clamp-2">“{rel.evidence}”</p>
                      )}
                    </div>
                  )}
                </button>
              );
            })}
          </div>
        </div>
      )}

      {/* Full AI Concept Agent Modal */}
      {agentConcept && (
        <ConceptAgentModal
          concept={agentConcept}
          onClose={() => setAgentConcept(null)}
          onSelectNeuron={(neuron) => {
            setAgentConcept(null);
            onSelectNeuron(neuron);
          }}
        />
      )}
    </div>
  );
}

