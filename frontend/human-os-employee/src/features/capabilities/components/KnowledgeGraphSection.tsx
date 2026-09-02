import { useEffect, useState } from 'react';
import type { ReactElement } from 'react';
import { Sparkles, ArrowDown, ChevronDown, ChevronRight, Code2, RefreshCw, X } from 'lucide-react';
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
} from '../api/structuredLearningRuntimeApi';

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
  Entity: 'border-indigo-400/30 bg-slate-950/40 text-slate-100',
};

function isSecondaryEdge(status: StructuredLearningConnection['EpistemicStatus']): boolean {
  return status === 'inferred' || status === 'referenced' || status === 'hypothetical' || status === 'attributed';
}

function buildEngramTrees(
  entities: StructuredLearningDocumentEntity[],
  connections: StructuredLearningConnection[],
  rootEntityName: string | null | undefined,
  showSecondary: boolean
): { trees: TreeNodeData[]; orphanIds: Set<string> } {
  const entityById = new Map(entities.map((e) => [e.EntityId, e]));
  const visibleConnections = showSecondary ? connections : connections.filter((c) => !isSecondaryEdge(c.EpistemicStatus));
  const targetIds = new Set(visibleConnections.map((c) => c.TargetEntityId));

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
    const edges: TreeEdge[] =
      depth >= MAX_TREE_DEPTH
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
          <div className={`italic opacity-60 ${large ? 'text-[11px]' : 'text-[9px]'}`}>Class: {node.entity.OntologicalClass}</div>
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
  return (
    <section className="space-y-3 rounded-xl border border-indigo-400/20 bg-indigo-500/[0.04] p-3" aria-label="Knowledge Facts">
      <div className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-5">
        {[
          ['Total Facts', data.TotalFacts],
          ['Core Facts', data.CoreFacts],
          ['Supporting Facts', data.SupportingFacts],
          ['Atomic Facts', data.AtomicFacts],
          ['Composite Facts', data.CompositeFacts],
        ].map(([label, value]) => (
          <div key={String(label)} className="rounded-lg border border-white/10 bg-slate-950/30 p-2 text-center">
            <div className="text-[10px] uppercase tracking-wide text-slate-400">{label}</div>
            <div className="mt-1 text-lg font-semibold text-white">{value}</div>
          </div>
        ))}
      </div>
      {data.KnowledgeFacts.length === 0 ? (
        <p className="text-xs text-slate-400">No Knowledge Facts extracted for this Engram.</p>
      ) : (
        data.KnowledgeFacts.map((fact) => (
          <article key={fact.KnowledgeFactId} className="rounded-lg border border-white/10 bg-slate-950/30 p-3">
            <div className="flex flex-wrap gap-1.5 text-[10px] font-medium uppercase tracking-wide text-slate-400">
              <span className={fact.Importance === 'Core' ? 'text-amber-300' : 'text-slate-300'}>{fact.Importance}</span>
              <span>·</span>
              <span>{fact.FactType}</span>
            </div>
            <h4 className="mt-1 text-sm font-semibold leading-6 text-white">{fact.Statement}</h4>
            <p className="mt-1 text-[11px] text-slate-400">
              {fact.DerivationType} · {fact.EpistemicStatus} · {Math.round(fact.Confidence * 100)}% Confidence
            </p>
            <div className="mt-3">
              <div className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Graph Path</div>
              {fact.GraphPath.length === 0 ? (
                <p className="mt-1 text-xs text-slate-500">-</p>
              ) : (
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
                <button
                  type="button"
                  className="text-xs text-indigo-300 underline focus:outline-none focus:ring-2 focus:ring-indigo-400"
                  aria-expanded={!!expandedEvidence[fact.KnowledgeFactId]}
                  onClick={() => onToggleEvidence(fact.KnowledgeFactId)}
                >
                  {expandedEvidence[fact.KnowledgeFactId] ? 'Hide Evidence' : 'View Evidence'}
                </button>
                {expandedEvidence[fact.KnowledgeFactId] && (
                  <div className="mt-2 space-y-2 border-l border-indigo-400/30 pl-3 text-xs leading-5 text-slate-300">
                    {fact.GraphPath.filter((step) => step.Evidence).map((step) => (
                      <p key={`${fact.KnowledgeFactId}-${step.StepOrder}`}>{step.Evidence}</p>
                    ))}
                  </div>
                )}
              </div>
            )}
          </article>
        ))
      )}
    </section>
  );
}

export default function KnowledgeGraphSection({
  documentId,
  sectionId,
  initialView = 'graph',
  canRegenerateFacts = false,
  onClose,
}: {
  documentId: string;
  sectionId?: string;
  initialView?: 'graph' | 'facts';
  canRegenerateFacts?: boolean;
  onClose?: () => void;
}) {
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
  const [showGraphBySection, setShowGraphBySection] = useState<Record<string, boolean>>({});
  const [showFactsBySection, setShowFactsBySection] = useState<Record<string, boolean>>({});
  const [fullscreenGraphSectionId, setFullscreenGraphSectionId] = useState<string | null>(null);
  const [expandedFactsSectionIds, setExpandedFactsSectionIds] = useState<Record<string, boolean>>({});

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
      .catch((err) => {
        if (!cancelled) setLoadError(err instanceof Error ? err.message : String(err));
      });
    return () => {
      cancelled = true;
    };
  }, [documentId, sectionId, initialView]);

  const totalEntities = engramGraphs?.reduce((sum, g) => sum + g.entities.length, 0) ?? 0;
  const totalConnections = engramGraphs?.reduce((sum, g) => sum + g.connections.length, 0) ?? 0;

  if (loadError || (engramGraphs && totalEntities === 0)) {
    return null;
  }

  return (
    <div className="rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-900/90 p-5 shadow-xl text-slate-900 dark:text-white">
      <div className="flex flex-wrap items-center gap-2">
        <Sparkles className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
        <h3 className="text-sm font-bold text-slate-900 dark:text-white">Knowledge Graph & Concepts</h3>
        {engramGraphs && (
          <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
            <span className="text-xs text-slate-600 dark:text-slate-400 font-medium">
              {totalEntities} entities {totalConnections > 0 && `· ${totalConnections} connections`}
            </span>
            <button
              type="button"
              onClick={() => (showAllFacts ? setShowAllFacts(false) : loadAllFacts())}
              className="rounded-full border border-indigo-200 dark:border-indigo-400/40 bg-indigo-50 dark:bg-indigo-500/10 px-2.5 py-1 text-[11px] font-semibold text-indigo-700 dark:text-indigo-200 hover:bg-indigo-100 dark:hover:bg-indigo-500/20 focus:outline-none focus:ring-2 focus:ring-indigo-400"
            >
              {showAllFacts ? 'Hide all Knowledge Facts' : 'View all Knowledge Facts'}
            </button>
            {onClose && (
              <button
                type="button"
                onClick={onClose}
                className="flex items-center gap-1.5 rounded-full border border-red-300 dark:border-red-400/40 bg-red-50 dark:bg-red-500/10 px-3 py-1 text-xs font-semibold text-red-700 dark:text-red-200 hover:border-red-400 hover:bg-red-100 dark:hover:bg-red-500/20 hover:text-red-900 dark:hover:text-white transition shadow-sm"
                aria-label="Hide Knowledge Graph"
              >
                <X className="h-3.5 w-3.5 text-red-600 dark:text-red-400" />
                <span>Hide Knowledge Graph</span>
              </button>
            )}
          </div>
        )}
      </div>

      {!engramGraphs && <p className="mt-3 text-xs text-slate-500 dark:text-slate-400">Loading Knowledge Graph...</p>}

      {engramGraphs && totalEntities > 0 && (
        <div className="mt-3 flex flex-wrap gap-3 text-[11px] text-slate-600 dark:text-slate-400 font-medium">
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
            <div key={section.StructuredLearningDocumentSectionId} className="rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02]">
              <button
                type="button"
                onClick={() => setExpandedSectionId(expanded ? null : section.StructuredLearningDocumentSectionId)}
                className="flex w-full items-center gap-2 px-3 py-2.5 text-left hover:bg-slate-100 dark:hover:bg-white/[0.04] transition rounded-xl"
              >
                {expanded ? (
                  <ChevronDown className="h-3.5 w-3.5 shrink-0 text-slate-500 dark:text-slate-400" />
                ) : (
                  <ChevronRight className="h-3.5 w-3.5 shrink-0 text-slate-500 dark:text-slate-400" />
                )}
                <span className="min-w-0 flex-1 truncate text-sm font-semibold text-slate-900 dark:text-white">
                  {section.SortOrder + 1}. {section.SubchapterTitle || section.ChapterTitle}
                </span>
                <span className={`shrink-0 text-xs ${hasData ? 'text-slate-500 dark:text-slate-400 font-medium' : 'text-slate-400 dark:text-slate-500'}`}>
                  {hasData ? `${entities.length} · ${connections.length}` : 'Empty'}
                </span>
              </button>

              {expanded && (
                <div className="space-y-3 border-t border-slate-200 dark:border-white/10 px-3 py-3">
                  {entities.length === 0 && connections.length === 0 && (
                    <p className="text-xs text-slate-400">No entities or connections extracted for this Engram.</p>
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
                          className="rounded-full border border-indigo-400/40 bg-indigo-500/10 px-2.5 py-1 text-[11px] font-medium text-indigo-200 hover:bg-indigo-500/20 focus:outline-none focus:ring-2 focus:ring-indigo-400"
                        >
                          {hasFactsLoaded ? (showFacts ? 'Hide Knowledge Facts' : 'View Knowledge Facts') : 'View Knowledge Facts'}
                        </button>
                        <button
                          type="button"
                          onClick={() => setShowGraphBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showGraph }))}
                          className="rounded-full border border-white/10 bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-400 hover:border-white/20 hover:text-slate-200"
                        >
                          {showGraph ? 'Hide Graph' : 'Show Graph'}
                        </button>
                        {showGraph && (
                          <button
                            type="button"
                            onClick={() => setFullscreenGraphSectionId(section.StructuredLearningDocumentSectionId)}
                            className="rounded-full border border-white/10 bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-400 hover:border-white/20 hover:text-slate-200"
                          >
                            Expand Graph
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
                            {factsRegenerating[section.StructuredLearningDocumentSectionId] ? 'Regenerating...' : 'Regenerate Graph & Facts'}
                          </button>
                        )}
                        {secondaryCount > 0 && (
                          <button
                            type="button"
                            onClick={() => setShowSecondaryBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showSecondary }))}
                            className="rounded-full border border-white/10 bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-400 hover:border-white/20 hover:text-slate-200"
                          >
                            {showSecondary ? `Hide secondary relations (${secondaryCount})` : `Show secondary relations (${secondaryCount})`}
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={() => setShowJsonBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showJson }))}
                          className="flex items-center gap-1 rounded-full border border-white/10 bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-400 hover:border-white/20 hover:text-slate-200"
                        >
                          <Code2 className="h-3 w-3" /> {showJson ? 'Hide JSON' : 'View JSON'}
                        </button>
                      </div>
                    );
                  })()}

                  {factsLoading[section.StructuredLearningDocumentSectionId] && (
                    <p className="rounded-lg border border-white/10 bg-slate-950/30 p-3 text-xs text-slate-400" role="status">
                      Loading facts...
                    </p>
                  )}
                  {factsError[section.StructuredLearningDocumentSectionId] && (
                    <div className="rounded-lg border border-red-400/30 bg-red-500/10 p-3 text-xs text-red-200" role="alert">
                      <p>Error loading facts</p>
                      <button type="button" className="mt-2 underline focus:outline-none focus:ring-2 focus:ring-red-300" onClick={() => loadFacts(section.StructuredLearningDocumentSectionId)}>
                        Retry
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
                            <div className={`rounded-xl border-2 border-amber-400/60 bg-amber-500/15 text-center font-bold text-amber-200 shadow-md ${large ? 'px-8 py-4 text-xl' : 'px-5 py-3 text-base'}`}>
                              {section.SubchapterTitle || section.ChapterTitle}
                            </div>
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
                          <div className="flex flex-wrap gap-1.5 mt-3">
                            {orphans.map((entity) => (
                              <span
                                key={entity.EntityId}
                                className={`rounded-full border border-white/10 bg-white/[0.03] text-slate-300 ${large ? 'px-3.5 py-1.5 text-sm' : 'px-2.5 py-1 text-xs'}`}
                                title={entity.Description ?? undefined}
                              >
                                {entity.CanonicalName}
                                <span className={`ml-1.5 text-slate-400 ${large ? 'text-xs' : 'text-[10px]'}`}>{entity.EntityType}</span>
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
                                Close
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
        <section className="mt-4 space-y-2 border-t border-white/10 pt-4" aria-label="All Knowledge Facts">
          <div className="flex items-center gap-2">
            <h4 className="text-sm font-semibold text-white">All Knowledge Facts</h4>
            <button
              type="button"
              onClick={() => setShowAllFacts(false)}
              className="ml-auto rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white focus:outline-none focus:ring-2 focus:ring-indigo-400"
              aria-label="Hide All"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          </div>
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
                  <span className="ml-auto text-xs font-normal text-slate-400">{facts.TotalFacts}</span>
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
