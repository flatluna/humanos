import { useEffect, useState } from 'react';
import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Sparkles, ArrowDown, ChevronDown, ChevronRight, Code2, RefreshCw } from 'lucide-react';
import {
  getStructuredLearningDocumentEntities,
  getStructuredLearningDocumentConnections,
  getStructuredLearningDocument,
  type StructuredLearningDocumentEntity,
  type StructuredLearningConnection,
  type StructuredLearningConnectionCategory,
  type StructuredLearningSectionDetail,
  getStructuredLearningKnowledgeFacts,
  generateStructuredLearningKnowledgeFacts,
  generateStructuredLearningConnections,
  type StructuredLearningKnowledgeFactsResponse,
} from '../lib/api/structuredLearningApi';

interface EngramGraph {
  section: StructuredLearningSectionDetail;
  entities: StructuredLearningDocumentEntity[];
  connections: StructuredLearningConnection[];
}

interface TreeEdge {
  label: string;
  category: StructuredLearningConnectionCategory;
  epistemicStatus: StructuredLearningConnection['EpistemicStatus'];
  target: TreeNodeData;
}

interface TreeNodeData {
  entity: StructuredLearningDocumentEntity;
  edges: TreeEdge[];
}

const MAX_TREE_DEPTH = 4;

/** Coarse visual "kind" bucket for an EntityType free-text string (2026-08-15
 * UX request — color/shape-code node types instead of a uniform look).
 * Best-effort keyword match since EntityType is free text from the LLM, not
 * a fixed enum — falls back to 'Entity' when nothing matches. */
type NodeKind = 'Document' | 'Rule' | 'Action' | 'Value' | 'Entity';

function getNodeKind(entityType: string): NodeKind {
  const t = entityType.toLowerCase();
  if (/(document|law|regulation|contract|reference|reglament)/.test(t)) return 'Document';
  if (/(rule|norm)/.test(t)) return 'Rule';
  if (/(activity|action|process|procedure|service)/.test(t)) return 'Action';
  if (/(quantity|rate|date|time|formula|variable|number|value|result)/.test(t)) return 'Value';
  return 'Entity';
}

const NODE_KIND_STYLES: Record<NodeKind, string> = {
  Document: 'border-sky-400/50 bg-sky-500/10 text-sky-100',
  Rule: 'border-violet-400/50 bg-violet-500/10 text-violet-100',
  Action: 'border-emerald-400/50 bg-emerald-500/10 text-emerald-100',
  Value: 'border-orange-400/50 bg-orange-500/10 text-orange-100',
  Entity: 'border-brand-400/30 bg-slate-950/40 text-slate-100',
};

/** Secondary/lower-confidence relations (2026-08-15 UX request) — hidden by
 * default to reduce visual clutter; a toggle reveals them per Engram. */
function isSecondaryEdge(status: StructuredLearningConnection['EpistemicStatus']): boolean {
  return status === 'inferred' || status === 'referenced' || status === 'hypothetical' || status === 'attributed';
}

/** Turns the flat entities/connections lists of one Engram into one or more
 * root-down trees (root = an entity nothing points TO), so the graph can be
 * rendered as Nodo => vértice => Nodo like the reference diagram, instead of
 * a flat chip/row list (2026-08-14 UX request). Cuts off at MAX_TREE_DEPTH
 * and skips already-visited entities to stay safe against cycles. */
function buildEngramTrees(entities: StructuredLearningDocumentEntity[], connections: StructuredLearningConnection[], rootEntityName: string | null | undefined, showSecondary: boolean): { trees: TreeNodeData[]; orphanIds: Set<string> } {
  const entityById = new Map(entities.map((e) => [e.EntityId, e]));
  const visibleConnections = showSecondary ? connections : connections.filter((c) => !isSecondaryEdge(c.EpistemicStatus));
  const targetIds = new Set(visibleConnections.map((c) => c.TargetEntityId));

  // Prefer the hub the model itself picked (RootEntityName, chosen alongside the
  // connections themselves) over the "nothing points to me" heuristic below —
  // the model reasons about which concept the whole section actually centers on,
  // rather than us guessing from degree count (2026-08-15 UX request).
  const modelRoot = rootEntityName
    ? entities.find((e) => e.CanonicalName.toLowerCase() === rootEntityName.toLowerCase())
    : undefined;
  const rootIds = modelRoot
    ? [modelRoot.EntityId, ...entities.filter((e) => !targetIds.has(e.EntityId) && e.EntityId !== modelRoot.EntityId).map((e) => e.EntityId)]
    : entities.filter((e) => !targetIds.has(e.EntityId)).map((e) => e.EntityId);

  const visited = new Set<string>();
  const buildNode = (entityId: string, depth: number): TreeNodeData | null => {
    const entity = entityById.get(entityId);
    if (!entity || visited.has(entityId)) return null;
    visited.add(entityId);
    const edges: TreeEdge[] = depth >= MAX_TREE_DEPTH
      ? []
      : visibleConnections
          .filter((c) => c.SourceEntityId === entityId)
          .map((c) => {
            const target = buildNode(c.TargetEntityId, depth + 1);
            return target ? { label: c.RelationshipLabel, category: c.RelationshipCategory, epistemicStatus: c.EpistemicStatus, target } : null;
          })
          .filter((e): e is TreeEdge => e !== null);
    return { entity, edges };
  };

  const trees = rootIds
    .map((id) => buildNode(id, 0))
    .filter((n): n is TreeNodeData => n !== null);

  const inTreeIds = new Set(entities.map((e) => e.EntityId).filter((id) => visited.has(id)));
  const orphanIds = new Set(entities.map((e) => e.EntityId).filter((id) => !inTreeIds.has(id)));
  return { trees, orphanIds };
}

function TreeNode({ node, large = false }: { node: TreeNodeData; large?: boolean }) {
  const kind = getNodeKind(node.entity.EntityType);
  return (
    <div className="flex flex-col items-center">
      <div className={`rounded-lg border text-center shadow-sm ${NODE_KIND_STYLES[kind]} ${large ? 'px-5 py-3 text-base' : 'px-3 py-1.5 text-xs'}`}>
        <div className="font-medium">{node.entity.CanonicalName}</div>
        <div className={`opacity-70 ${large ? 'text-xs' : 'text-[10px]'}`}>{node.entity.EntityType}</div>
        {node.entity.OntologicalClass && (
          <div className={`italic opacity-60 ${large ? 'text-[11px]' : 'text-[9px]'}`}>Clase: {node.entity.OntologicalClass}</div>
        )}
      </div>

      {node.edges.length > 0 && (
        <div className={`relative mt-1 flex items-start border-t border-slate-600/60 ${large ? 'gap-10 pt-4' : 'gap-6 pt-3'}`}>
          {node.edges.map((edge, i) => (
            <div key={i} className="flex flex-col items-center">
              <span className={`mb-0.5 whitespace-nowrap ${large ? 'text-sm' : 'text-[10px]'} ${isSecondaryEdge(edge.epistemicStatus) ? 'italic text-slate-500' : 'text-amber-400'}`}>
                {edge.label}
              </span>
              <ArrowDown className={`mb-1 shrink-0 ${large ? 'h-5 w-5' : 'h-3 w-3'} ${isSecondaryEdge(edge.epistemicStatus) ? 'text-slate-600' : 'text-slate-500'}`} />
              <TreeNode node={edge.target} large={large} />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function KnowledgeFactsPanel({
  data,
  expandedEvidence,
  onToggleEvidence,
}: {
  data: StructuredLearningKnowledgeFactsResponse;
  expandedEvidence: Record<string, boolean>;
  onToggleEvidence: (key: string) => void;
}) {
  const { t } = useTranslation();
  return (
    <section className="space-y-3 rounded-xl border border-brand-400/20 bg-brand-500/[0.04] p-3" aria-label={t('structuredLearningRunProgress.knowledgeFactsTitle')}>
      <div className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-5">
        {[
          [t('structuredLearningRunProgress.knowledgeFactsTotal'), data.TotalFacts],
          [t('structuredLearningRunProgress.knowledgeFactsCore'), data.CoreFacts],
          [t('structuredLearningRunProgress.knowledgeFactsSupporting'), data.SupportingFacts],
          [t('structuredLearningRunProgress.knowledgeFactsAtomic'), data.AtomicFacts],
          [t('structuredLearningRunProgress.knowledgeFactsComposite'), data.CompositeFacts],
        ].map(([label, value]) => (
          <div key={String(label)} className="rounded-lg border border-white/10 bg-slate-950/30 p-2 text-center">
            <div className="text-[10px] uppercase tracking-wide text-slate-500">{label}</div>
            <div className="mt-1 text-lg font-semibold text-white">{value}</div>
          </div>
        ))}
      </div>
      {data.KnowledgeFacts.length === 0 ? (
        <p className="text-xs text-slate-400">{t('structuredLearningRunProgress.knowledgeFactsEmpty')}</p>
      ) : data.KnowledgeFacts.map((fact) => (
        <article key={fact.KnowledgeFactId} className="rounded-lg border border-white/10 bg-slate-950/30 p-3">
          <div className="flex flex-wrap gap-1.5 text-[10px] font-medium uppercase tracking-wide text-slate-400">
            <span className={fact.Importance === 'Core' ? 'text-amber-300' : 'text-slate-300'}>{fact.Importance}</span>
            <span>·</span><span>{fact.FactType}</span>
          </div>
          <h4 className="mt-1 text-sm font-semibold leading-6 text-white">{fact.Statement}</h4>
          <p className="mt-1 text-[11px] text-slate-400">{fact.DerivationType} · {fact.EpistemicStatus} · {Math.round(fact.Confidence * 100)}% {t('structuredLearningRunProgress.knowledgeFactsConfidence')}</p>
          <div className="mt-3">
            <div className="text-[10px] font-semibold uppercase tracking-wide text-slate-500">{t('structuredLearningRunProgress.knowledgeFactsGraphPath')}</div>
            {fact.GraphPath.length === 0 ? <p className="mt-1 text-xs text-slate-500">-</p> : (
              <div className="mt-1 flex flex-wrap items-center gap-1 text-xs text-slate-200">
                {fact.GraphPath.sort((a, b) => a.StepOrder - b.StepOrder).flatMap((step, index) => [
                  index > 0 ? <span key={`${step.ConnectionId}-arrow`} aria-hidden="true">→</span> : null,
                  <span key={`${step.ConnectionId}-source`}>{step.SourceEntityName}</span>,
                  <span key={`${step.ConnectionId}-label`} className="text-amber-300">→ {step.RelationshipLabel} →</span>,
                  <span key={`${step.ConnectionId}-target`}>{step.TargetEntityName}</span>,
                ].filter((item): item is ReactElement => item !== null))}
              </div>
            )}
          </div>
          {fact.GraphPath.some((step) => step.Evidence) && (
            <div className="mt-3">
              <button type="button" className="text-xs text-brand-200 underline focus:outline-none focus:ring-2 focus:ring-brand-400" aria-expanded={!!expandedEvidence[fact.KnowledgeFactId]} onClick={() => onToggleEvidence(fact.KnowledgeFactId)}>
                {expandedEvidence[fact.KnowledgeFactId] ? t('structuredLearningRunProgress.knowledgeFactsHideEvidence') : t('structuredLearningRunProgress.knowledgeFactsViewEvidence')}
              </button>
              {expandedEvidence[fact.KnowledgeFactId] && <div className="mt-2 space-y-2 border-l border-brand-400/30 pl-3 text-xs leading-5 text-slate-300">{fact.GraphPath.filter((step) => step.Evidence).map((step) => <p key={`${fact.KnowledgeFactId}-${step.StepOrder}`}>{step.Evidence}</p>)}</div>}
            </div>
          )}
        </article>
      ))}
    </section>
  );
}

/** Read-only results viewer for the Knowledge Graph (2026-08-14) —
 * entities/connections were already extracted automatically as part of
 * Step 2, one small graph PER ENGRAM (section) — see
 * GenerateKnowledgeGraphAsync on the backend. This groups the flat
 * entities/connections lists back by their own Engram (2026-08-15
 * redesign — a single global list was unreadable past a handful of
 * sections), collapsible per Engram so a designer can actually see how
 * extraction went for each individual section. Shared between the
 * extraction run progress page and the document detail page. */
export default function KnowledgeGraphSection({ documentId, sectionId, initialView = 'graph', canRegenerateFacts = false }: { documentId: string; sectionId?: string; initialView?: 'graph' | 'facts'; canRegenerateFacts?: boolean }) {
  const { t } = useTranslation();
  const [engramGraphs, setEngramGraphs] = useState<EngramGraph[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [expandedSectionId, setExpandedSectionId] = useState<string | null>(null);
  const [showSecondaryBySection, setShowSecondaryBySection] = useState<Record<string, boolean>>({});
  const [showJsonBySection, setShowJsonBySection] = useState<Record<string, boolean>>({});
  const [factsBySection, setFactsBySection] = useState<Record<string, StructuredLearningKnowledgeFactsResponse | null>>({});
  const [factsLoading, setFactsLoading] = useState<Record<string, boolean>>({});
  const [factsError, setFactsError] = useState<Record<string, string | null>>({});
  const [factsRegenerating, setFactsRegenerating] = useState<Record<string, boolean>>({});
  const [expandedEvidence, setExpandedEvidence] = useState<Record<string, boolean>>({});
  const [showAllFacts, setShowAllFacts] = useState(false);
  // Independent show/hide toggles for the graph tree and the Knowledge
  // Facts panel within one engram (2026-08-17) — previously the facts
  // panel appeared automatically once loaded with no way to hide it again
  // (and no separate control for the graph), so opening facts effectively
  // pushed the graph out of view with no way back. Both default to shown.
  const [showGraphBySection, setShowGraphBySection] = useState<Record<string, boolean>>({});
  const [showFactsBySection, setShowFactsBySection] = useState<Record<string, boolean>>({});
  // Fullscreen + larger-node view for one Engram's graph at a time
  // (2026-08-19 UX request) — the tree is plain flex/divs, not a canvas, so
  // "bigger" just means swapping to larger Tailwind classes and covering
  // the viewport instead of the normal card width.
  const [fullscreenGraphSectionId, setFullscreenGraphSectionId] = useState<string | null>(null);
  // Raw section Text viewer (2026-08-17) — lets a designer confirm sparse-content
  // expansion actually produced real generated content per engram.
  const [showTextBySection, setShowTextBySection] = useState<Record<string, boolean>>({});
  // Per-engram collapse state for the "Ver todos" facts list (2026-08-17) —
  // collapsed by default so opening "Ver todos" doesn't dump every engram's
  // facts down the whole page at once; the designer opens one at a time.
  const [expandedFactsSectionIds, setExpandedFactsSectionIds] = useState<Record<string, boolean>>({});

  // Facts are derived from the graph's own connections — regenerating
  // Facts alone can never add a fact for an edge the graph never extracted
  // in the first place, so this replays connection extraction FIRST (2026-08-17:
  // this now REPLACES the section's existing connections/facts with a fresh
  // extraction, rather than only adding missed edges, to avoid accumulating
  // duplicate/near-duplicate edges across repeated clicks) before regenerating
  // Facts from the now-clean graph, then reloads both.
  const regenerateFacts = async (currentSectionId: string) => {
    setFactsRegenerating((previous) => ({ ...previous, [currentSectionId]: true }));
    setFactsError((previous) => ({ ...previous, [currentSectionId]: null }));
    try {
      await generateStructuredLearningConnections(currentSectionId);
      await generateStructuredLearningKnowledgeFacts(currentSectionId);
      await Promise.all([loadFacts(currentSectionId), loadGraphs()]);
    } catch (error) {
      setFactsError((previous) => ({ ...previous, [currentSectionId]: error instanceof Error ? error.message : String(error) }));
    } finally {
      setFactsRegenerating((previous) => ({ ...previous, [currentSectionId]: false }));
    }
  };

  const loadFacts = async (currentSectionId: string) => {
    setFactsLoading((previous) => ({ ...previous, [currentSectionId]: true }));
    setFactsError((previous) => ({ ...previous, [currentSectionId]: null }));
    try {
      const facts = await getStructuredLearningKnowledgeFacts(currentSectionId);
      setFactsBySection((previous) => ({ ...previous, [currentSectionId]: facts }));
    } catch (error) {
      setFactsError((previous) => ({ ...previous, [currentSectionId]: error instanceof Error ? error.message : String(error) }));
    } finally {
      setFactsLoading((previous) => ({ ...previous, [currentSectionId]: false }));
    }
  };

  const loadAllFacts = async () => {
    if (!engramGraphs) return;
    await Promise.all(engramGraphs.map((graph) => loadFacts(graph.section.StructuredLearningDocumentSectionId)));
    setShowAllFacts(true);
  };

  const loadGraphs = async () => {
    const [document, entities, connections] = await Promise.all([
      getStructuredLearningDocument(documentId),
      getStructuredLearningDocumentEntities(documentId),
      getStructuredLearningDocumentConnections(documentId).catch(() => [] as StructuredLearningConnection[]),
    ]);

    const sortedSections = [...document.Sections]
      .filter((section) => !sectionId || section.StructuredLearningDocumentSectionId === sectionId)
      .sort((a, b) => a.SortOrder - b.SortOrder);
    const graphs = sortedSections.map((section) => ({
      section,
      entities: entities.filter((e) => e.Occurrences.some((o) => o.EngramId === section.StructuredLearningDocumentSectionId)),
      connections: connections.filter((c) => c.SourceSectionId === section.StructuredLearningDocumentSectionId),
    }));
    setEngramGraphs(graphs);
    return graphs;
  };

  useEffect(() => {
    let cancelled = false;
    loadGraphs()
      .then((graphs) => {
        if (cancelled) return;
        if (sectionId && graphs.length > 0) {
          setExpandedSectionId(sectionId);
          if (initialView === 'facts') void loadFacts(sectionId);
        }
      })
      .catch((err) => { if (!cancelled) setLoadError(err instanceof Error ? err.message : String(err)); });
    return () => { cancelled = true; };
  }, [documentId, sectionId, initialView]);

  const totalEntities = engramGraphs?.reduce((sum, g) => sum + g.entities.length, 0) ?? 0;
  const totalConnections = engramGraphs?.reduce((sum, g) => sum + g.connections.length, 0) ?? 0;

  if (loadError || (engramGraphs && totalEntities === 0)) {
    return null; // Nothing extracted (or feature unconfigured) — stay silent, same as the executive summary's graceful skip.
  }

  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-5">
      <div className="flex items-center gap-2">
        <Sparkles className="h-4 w-4 text-brand-400" />
        <h3 className="text-sm font-semibold text-white">{t('structuredLearningRunProgress.knowledgeGraphTitle')}</h3>
        {engramGraphs && (
          <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
            <span className="text-xs text-slate-400">
              {t('structuredLearningRunProgress.knowledgeGraphCount', { count: totalEntities })}
              {totalConnections > 0 && ` · ${totalConnections} ${t('structuredLearningRunProgress.knowledgeGraphConnectionsTitle').toLowerCase()}`}
            </span>
            <button
              type="button"
              onClick={loadAllFacts}
              className="rounded-full border border-brand-400/40 bg-brand-500/10 px-2.5 py-1 text-[11px] font-medium text-brand-200 hover:bg-brand-500/20 focus:outline-none focus:ring-2 focus:ring-brand-400"
              aria-label={t('structuredLearningRunProgress.knowledgeFactsViewAll')}
            >
              {t('structuredLearningRunProgress.knowledgeFactsViewAll')}
            </button>
          </div>
        )}
      </div>

      {!engramGraphs && <p className="mt-3 text-xs text-slate-500">{t('structuredLearningRunProgress.knowledgeGraphLoading')}</p>}

      {engramGraphs && totalEntities > 0 && (
        <div className="mt-3 flex flex-wrap gap-3 text-[11px] text-slate-400">
          {(['Document', 'Rule', 'Action', 'Value', 'Entity'] as const).map((kind) => (
            <span key={kind} className="flex items-center gap-1.5">
              <span className={`h-2.5 w-2.5 rounded-full border ${NODE_KIND_STYLES[kind]}`} />
              {kind}
            </span>
          ))}
        </div>
      )}

      <div className="mt-4 space-y-2">
        {engramGraphs?.map(({ section, entities, connections }) => {
          const expanded = expandedSectionId === section.StructuredLearningDocumentSectionId;
          const hasData = entities.length > 0 || connections.length > 0;
          return (
            <div key={section.StructuredLearningDocumentSectionId} className="rounded-xl border border-white/10 bg-white/[0.02]">
              <button
                type="button"
                onClick={() => setExpandedSectionId(expanded ? null : section.StructuredLearningDocumentSectionId)}
                className="flex w-full items-center gap-2 px-3 py-2.5 text-left"
              >
                {expanded ? (
                  <ChevronDown className="h-3.5 w-3.5 shrink-0 text-slate-400" />
                ) : (
                  <ChevronRight className="h-3.5 w-3.5 shrink-0 text-slate-400" />
                )}
                <span className="min-w-0 flex-1 truncate text-sm text-white">
                  {section.SortOrder + 1}. {section.SubchapterTitle || section.ChapterTitle}
                </span>
                <span className={`shrink-0 text-xs ${hasData ? 'text-slate-400' : 'text-slate-600'}`}>
                  {hasData
                    ? `${entities.length} · ${connections.length}`
                    : t('structuredLearningRunProgress.knowledgeGraphEngramEmpty')}
                </span>
              </button>

              {expanded && (
                <div className="space-y-3 border-t border-white/10 px-3 py-3">
                  {entities.length === 0 && connections.length === 0 && (
                    <p className="text-xs text-slate-500">{t('structuredLearningRunProgress.knowledgeGraphEngramEmpty')}</p>
                  )}

                  {(() => {
                    const secondaryCount = connections.filter((c) => isSecondaryEdge(c.EpistemicStatus)).length;
                    const showSecondary = showSecondaryBySection[section.StructuredLearningDocumentSectionId] ?? false;
                    const showJson = showJsonBySection[section.StructuredLearningDocumentSectionId] ?? false;
                    const showGraph = showGraphBySection[section.StructuredLearningDocumentSectionId] ?? true;
                    const showFacts = showFactsBySection[section.StructuredLearningDocumentSectionId] ?? true;
                    const hasFactsLoaded = !!factsBySection[section.StructuredLearningDocumentSectionId];
                    return (
                      <div className="flex flex-wrap items-center gap-2">
                        <button
                          type="button"
                          onClick={() => {
                            if (hasFactsLoaded) {
                              setShowFactsBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showFacts }));
                            } else {
                              setShowFactsBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: true }));
                              void loadFacts(section.StructuredLearningDocumentSectionId);
                            }
                          }}
                          className="rounded-full border border-brand-400/40 bg-brand-500/10 px-2.5 py-1 text-[11px] font-medium text-brand-200 hover:bg-brand-500/20 focus:outline-none focus:ring-2 focus:ring-brand-400"
                          aria-label={t('structuredLearningRunProgress.knowledgeFactsView')}
                        >
                          {hasFactsLoaded ? (showFacts ? 'Ocultar Knowledge Facts' : t('structuredLearningRunProgress.knowledgeFactsView')) : t('structuredLearningRunProgress.knowledgeFactsView')}
                        </button>
                        <button
                          type="button"
                          onClick={() => setShowGraphBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showGraph }))}
                          className="rounded-full border border-white/10 bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-400 hover:border-white/20 hover:text-slate-200"
                        >
                          {showGraph ? 'Ocultar grafo' : 'Mostrar grafo'}
                        </button>
                        {showGraph && (
                          <button
                            type="button"
                            onClick={() => setFullscreenGraphSectionId(section.StructuredLearningDocumentSectionId)}
                            className="rounded-full border border-white/10 bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-400 hover:border-white/20 hover:text-slate-200"
                          >
                            Ver grafo en grande
                          </button>
                        )}
                        {canRegenerateFacts && (
                          <button
                            type="button"
                            onClick={() => regenerateFacts(section.StructuredLearningDocumentSectionId)}
                            disabled={factsRegenerating[section.StructuredLearningDocumentSectionId] ?? false}
                            className="flex items-center gap-1 rounded-full border border-amber-400/40 bg-amber-500/10 px-2.5 py-1 text-[11px] font-medium text-amber-200 hover:bg-amber-500/20 focus:outline-none focus:ring-2 focus:ring-amber-400 disabled:cursor-not-allowed disabled:opacity-50"
                          >
                            <RefreshCw className={`h-3 w-3 ${factsRegenerating[section.StructuredLearningDocumentSectionId] ? 'animate-spin' : ''}`} />
                            {factsRegenerating[section.StructuredLearningDocumentSectionId] ? 'Regenerando…' : 'Regenerar Grafo y Knowledge Facts'}
                          </button>
                        )}
                        {secondaryCount > 0 && (
                          <button
                            type="button"
                            onClick={() => setShowSecondaryBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showSecondary }))}
                            className="rounded-full border border-white/10 bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-400 hover:border-white/20 hover:text-slate-200"
                          >
                            {showSecondary ? `Ocultar relaciones secundarias (${secondaryCount})` : `Mostrar relaciones secundarias (${secondaryCount})`}
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={() => setShowJsonBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showJson }))}
                          className="flex items-center gap-1 rounded-full border border-white/10 bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-400 hover:border-white/20 hover:text-slate-200"
                        >
                          <Code2 className="h-3 w-3" /> {showJson ? 'Ocultar JSON' : 'Ver JSON'}
                        </button>
                        <button
                          type="button"
                          onClick={() => setShowTextBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !(prev[section.StructuredLearningDocumentSectionId] ?? false) }))}
                          className="flex items-center gap-1 rounded-full border border-white/10 bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-400 hover:border-white/20 hover:text-slate-200"
                        >
                          {showTextBySection[section.StructuredLearningDocumentSectionId] ? 'Ocultar contenido' : 'Ver contenido generado'}
                        </button>
                      </div>
                    );
                  })()}

                  {showTextBySection[section.StructuredLearningDocumentSectionId] && (
                    <div className="rounded-lg border border-white/10 bg-slate-950/30 p-3">
                      <div className="mb-2 text-[10px] font-semibold uppercase tracking-wide text-slate-500">
                        Contenido generado ({section.TotalCharacters.toLocaleString()} caracteres · {section.TotalTokens.toLocaleString()} tokens)
                      </div>
                      <p className="max-h-96 overflow-y-auto whitespace-pre-wrap text-xs leading-5 text-slate-300">
                        {section.Text || 'Sin contenido.'}
                      </p>
                    </div>
                  )}

                  {factsLoading[section.StructuredLearningDocumentSectionId] && (
                    <p className="rounded-lg border border-white/10 bg-slate-950/30 p-3 text-xs text-slate-400" role="status">{t('structuredLearningRunProgress.knowledgeFactsLoading')}</p>
                  )}
                  {factsError[section.StructuredLearningDocumentSectionId] && (
                    <div className="rounded-lg border border-red-400/30 bg-red-500/10 p-3 text-xs text-red-200" role="alert">
                      <p>{t('structuredLearningRunProgress.knowledgeFactsError')}</p>
                      <button type="button" className="mt-2 underline focus:outline-none focus:ring-2 focus:ring-red-300" onClick={() => loadFacts(section.StructuredLearningDocumentSectionId)}>
                        {t('structuredLearningRunProgress.knowledgeFactsRetry')}
                      </button>
                    </div>
                  )}
                  {factsBySection[section.StructuredLearningDocumentSectionId] && (showFactsBySection[section.StructuredLearningDocumentSectionId] ?? true) && (
                    <KnowledgeFactsPanel
                      data={factsBySection[section.StructuredLearningDocumentSectionId]!}
                      expandedEvidence={expandedEvidence}
                      onToggleEvidence={(key) => setExpandedEvidence((previous) => ({ ...previous, [key]: !previous[key] }))}
                    />
                  )}

                  {(showJsonBySection[section.StructuredLearningDocumentSectionId] ?? false) && (
                    <pre className="max-h-96 overflow-auto rounded-lg border border-white/10 bg-slate-950/60 p-3 text-[11px] text-slate-300">
                      {JSON.stringify({ section: { title: section.SubchapterTitle || section.ChapterTitle, rootEntityName: section.KnowledgeGraphRootEntityName }, entities, connections }, null, 2)}
                    </pre>
                  )}

                  {(showGraphBySection[section.StructuredLearningDocumentSectionId] ?? true) && (() => {
                    const showSecondary = showSecondaryBySection[section.StructuredLearningDocumentSectionId] ?? false;
                    const { trees, orphanIds } = buildEngramTrees(entities, connections, section.KnowledgeGraphRootEntityName, showSecondary);
                    const orphans = entities.filter((e) => orphanIds.has(e.EntityId));
                    const isFullscreen = fullscreenGraphSectionId === section.StructuredLearningDocumentSectionId;

                    const renderTrees = (large: boolean) => (
                      <>
                        {trees.length > 0 && (
                          <div className="flex flex-col items-center">
                            {/* Engram title as the visual root (2026-08-15) — more reliable than
                             * relying on the model having captured the section's own subject as an
                             * entity (e.g. "Artículo 1o.-A BIS" for a legal article); always
                             * available, always makes the tree make sense as a whole. */}
                            <div className={`rounded-xl border-2 border-amber-400/60 bg-amber-500/15 text-center font-bold text-amber-200 shadow-md ${large ? 'px-8 py-4 text-xl' : 'px-5 py-3 text-base'}`}>
                              {section.SubchapterTitle || section.ChapterTitle}
                            </div>
                            {/* Fan each top-level tree out from the title with its own connector
                             * (same visual language as TreeNode's own edges below), instead of a
                             * single generic line above a floating row — makes it explicit that
                             * every tree hangs directly off the engram title, none are orphaned. */}
                            <div className={`relative mt-1 flex flex-wrap items-start justify-center overflow-x-auto border-t border-amber-400/40 pt-3 ${large ? 'gap-14' : 'gap-8'}`}>
                              {trees.map((tree) => (
                                <div key={tree.entity.EntityId} className="flex flex-col items-center">
                                  <ArrowDown className={`mb-1 shrink-0 text-amber-400/70 ${large ? 'h-5 w-5' : 'h-3 w-3'}`} />
                                  <TreeNode node={tree} large={large} />
                                </div>
                              ))}
                            </div>
                          </div>
                        )}

                        {orphans.length > 0 && (
                          <div className="flex flex-wrap gap-1.5">
                            {orphans.map((entity) => (
                              <span
                                key={entity.EntityId}
                                className={`rounded-full border border-white/10 bg-white/[0.03] text-slate-300 ${large ? 'px-3.5 py-1.5 text-sm' : 'px-2.5 py-1 text-xs'}`}
                                title={entity.Description ?? undefined}
                              >
                                {entity.CanonicalName}
                                <span className={`ml-1.5 text-slate-500 ${large ? 'text-xs' : 'text-[10px]'}`}>{entity.EntityType}</span>
                              </span>
                            ))}
                          </div>
                        )}
                      </>
                    );

                    return (
                      <>
                        {renderTrees(false)}
                        {isFullscreen && (
                          <div className="fixed inset-0 z-50 flex flex-col bg-slate-950 p-6">
                            <div className="mb-4 flex shrink-0 items-center justify-between">
                              <h4 className="text-lg font-semibold text-white">{section.SubchapterTitle || section.ChapterTitle}</h4>
                              <button
                                type="button"
                                onClick={() => setFullscreenGraphSectionId(null)}
                                className="rounded-lg border border-white/10 bg-white/[0.05] px-4 py-2 text-sm font-medium text-white hover:bg-white/10"
                              >
                                Cerrar
                              </button>
                            </div>
                            <div className="flex-1 overflow-auto">
                              <div className="mx-auto w-max py-4">{renderTrees(true)}</div>
                            </div>
                          </div>
                        )}
                      </>
                    );
                  })()}
                </div>
              )}
            </div>
          );
        })}
      </div>
      {showAllFacts && engramGraphs && (
        <section className="mt-4 space-y-2 border-t border-white/10 pt-4" aria-label={t('structuredLearningRunProgress.knowledgeFactsViewAll')}>
          {engramGraphs.map(({ section }) => {
            const facts = factsBySection[section.StructuredLearningDocumentSectionId];
            if (!facts) return null;
            const isOpen = !!expandedFactsSectionIds[section.StructuredLearningDocumentSectionId];
            return (
              <div key={section.StructuredLearningDocumentSectionId} className="rounded-lg border border-white/10">
                <button
                  type="button"
                  onClick={() =>
                    setExpandedFactsSectionIds((previous) => ({
                      ...previous,
                      [section.StructuredLearningDocumentSectionId]: !previous[section.StructuredLearningDocumentSectionId],
                    }))
                  }
                  className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm font-semibold text-white hover:bg-white/[0.03]"
                >
                  {isOpen ? <ChevronDown className="h-4 w-4 shrink-0" /> : <ChevronRight className="h-4 w-4 shrink-0" />}
                  {section.SortOrder + 1}. {section.SubchapterTitle || section.ChapterTitle}
                  <span className="ml-auto text-xs font-normal text-slate-500">{facts.TotalFacts}</span>
                </button>
                {isOpen && (
                  <div className="border-t border-white/10 p-3">
                    <KnowledgeFactsPanel data={facts} expandedEvidence={expandedEvidence} onToggleEvidence={(key) => setExpandedEvidence((previous) => ({ ...previous, [key]: !previous[key] }))} />
                  </div>
                )}
              </div>
            );
          })}
        </section>
      )}
    </div>
  );
}

