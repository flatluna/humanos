import { useEffect, useMemo, useState } from 'react';
import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Sparkles, ArrowDown, ChevronDown, ChevronRight, Code2, RefreshCw, X, CheckCircle2, ListFilter, HelpCircle } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { getMemoryColor, getMemoryTextColor, getRetrievability } from '../../brain/memoryScience';
import { CourseGalaxy3DGraph } from './CourseGalaxy3DGraph';
import {
  getStructuredLearningDocumentEntities,
  getStructuredLearningDocumentConnections,
  getStructuredLearningDocument,
  getActiveStructuredLearningAssessmentRound,
  type StructuredLearningDocumentDetail,
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
  Document: 'border-sky-400/50 bg-sky-50 dark:bg-sky-500/10 text-sky-900 dark:text-sky-100',
  Rule: 'border-violet-400/50 bg-violet-50 dark:bg-violet-500/10 text-violet-900 dark:text-violet-100',
  Action: 'border-emerald-400/50 bg-emerald-50 dark:bg-emerald-500/10 text-emerald-900 dark:text-emerald-100',
  Value: 'border-orange-400/50 bg-orange-50 dark:bg-orange-500/10 text-orange-900 dark:text-orange-100',
  Entity: 'border-indigo-400/30 bg-slate-100 dark:bg-slate-950/40 text-slate-800 dark:text-slate-100',
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

function TreeNode({
  node,
  large = false,
  isMastered = false,
  memColor,
}: {
  node: TreeNodeData;
  large?: boolean;
  isMastered?: boolean;
  memColor?: string;
}) {
  const kind = getNodeKind(node.entity.EntityType);
  return (
    <div className="flex flex-col items-center">
      <div
        className={`rounded-lg border text-center transition-all duration-300 ${NODE_KIND_STYLES[kind]} ${
          large ? 'px-5 py-3 text-base' : 'px-3 py-1.5 text-xs'
        } ${
          isMastered
            ? 'shadow-md ring-1 ring-emerald-400/40 shadow-emerald-950/20 brightness-110'
            : 'border-slate-300 dark:border-white/10'
        }`}
        style={isMastered && memColor ? { borderColor: memColor } : undefined}
      >
        <div className="font-medium">{node.entity.CanonicalName}</div>
        <div className={`text-slate-600 dark:text-slate-300 ${large ? 'text-xs' : 'text-[10px]'}`}>{node.entity.EntityType}</div>
        {node.entity.OntologicalClass && (
          <div className={`italic text-slate-500 dark:text-slate-400 ${large ? 'text-[11px]' : 'text-[9px]'}`}>Class: {node.entity.OntologicalClass}</div>
        )}
      </div>

      {node.edges.length > 0 && (
        <div
          className={`relative mt-1 flex items-start ${large ? 'gap-10 pt-4' : 'gap-6 pt-3'} ${
            isMastered ? 'border-t border-emerald-500/40' : 'border-t border-slate-300 dark:border-slate-700/40'
          }`}
        >
          {node.edges.map((edge, i) => (
            <div key={i} className="flex flex-col items-center">
              <span
                className={`mb-0.5 whitespace-nowrap ${large ? 'text-sm' : 'text-[10px]'} ${
                  isSecondaryEdge(edge.epistemicStatus)
                    ? 'italic text-slate-500 dark:text-slate-400'
                    : isMastered
                    ? 'text-emerald-300 dark:text-emerald-200 font-semibold drop-shadow-xs'
                    : 'text-slate-600 dark:text-slate-300'
                }`}
              >
                {edge.label}
              </span>
              <ArrowDown
                className={`mb-1 shrink-0 ${large ? 'h-5 w-5' : 'h-3 w-3'} ${
                  isSecondaryEdge(edge.epistemicStatus)
                    ? 'text-slate-500 dark:text-slate-500'
                    : isMastered
                    ? 'text-emerald-400 dark:text-emerald-300 drop-shadow-xs'
                    : 'text-slate-500 dark:text-slate-400'
                }`}
              />
              <TreeNode node={edge.target} large={large} isMastered={isMastered} memColor={memColor} />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/** Exported (2026-09-03) so other pages (e.g. Brain's mastered-engram
 * detail panel) can show "things to know about this engram" using the
 * exact same already-generated, already-persisted Knowledge Facts —
 * no new agent call, just reusing this rendering. */
export function KnowledgeFactsPanel({
  data,
  expandedEvidence,
  onToggleEvidence,
}: {
  data: StructuredLearningKnowledgeFactsResponse;
  expandedEvidence: Record<string, boolean>;
  onToggleEvidence: (key: string) => void;
}) {
  const [showStatsHelp, setShowStatsHelp] = useState(false);
  const STAT_EXPLANATIONS: Record<string, string> = {
    'Total Facts': 'All Knowledge Facts derived for this Engram — the sum of every category below.',
    'Core Facts': 'The essential, must-know facts that define this Engram’s central concept.',
    'Supporting Facts': 'Secondary facts that add context or detail but aren’t essential on their own.',
    'Atomic Facts': 'A fact built from a single step in the Knowledge Graph (one relationship).',
    'Composite Facts': 'A fact built by chaining multiple Knowledge Graph relationships together.',
  };
  return (
    <section className="space-y-3 rounded-xl border border-indigo-400/20 bg-indigo-50 dark:bg-indigo-500/[0.04] p-3" aria-label="Knowledge Facts">
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={() => setShowStatsHelp((v) => !v)}
          className="flex items-center gap-1 text-[11px] font-medium text-indigo-700 dark:text-indigo-300 hover:underline focus:outline-none focus:ring-2 focus:ring-indigo-400 rounded"
        >
          <HelpCircle className="h-3.5 w-3.5" />
          {showStatsHelp ? 'Hide explanation' : 'What do these numbers mean?'}
        </button>
      </div>
      {showStatsHelp && (
        <ul className="space-y-1 rounded-lg border border-indigo-200 dark:border-indigo-400/20 bg-white dark:bg-slate-950/40 p-3 text-[11px] leading-relaxed text-slate-600 dark:text-slate-300">
          {Object.entries(STAT_EXPLANATIONS).map(([label, explanation]) => (
            <li key={label}>
              <span className="font-semibold text-slate-900 dark:text-white">{label}:</span> {explanation}
            </li>
          ))}
        </ul>
      )}
      <div className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-5">
        {[
          ['Total Facts', data.TotalFacts],
          ['Core Facts', data.CoreFacts],
          ['Supporting Facts', data.SupportingFacts],
          ['Atomic Facts', data.AtomicFacts],
          ['Composite Facts', data.CompositeFacts],
        ].map(([label, value]) => (
          <div
            key={String(label)}
            title={STAT_EXPLANATIONS[String(label)]}
            className="rounded-lg border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-950/30 p-2 text-center cursor-help"
          >
            <div className="text-[10px] uppercase tracking-wide text-slate-500 dark:text-slate-400">{label}</div>
            <div className="mt-1 text-lg font-semibold text-slate-900 dark:text-white">{value}</div>
          </div>
        ))}
      </div>
      {data.KnowledgeFacts.length === 0 ? (
        <p className="text-xs text-slate-500 dark:text-slate-400">No Knowledge Facts extracted for this Engram.</p>
      ) : (
        data.KnowledgeFacts.map((fact) => (
          <article key={fact.KnowledgeFactId} className="rounded-lg border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-950/30 p-3">
            <div className="flex flex-wrap gap-1.5 text-[10px] font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">
              <span className={fact.Importance === 'Core' ? 'text-amber-600 dark:text-amber-300' : 'text-slate-600 dark:text-slate-300'}>{fact.Importance}</span>
              <span>·</span>
              <span>{fact.FactType}</span>
            </div>
            <h4 className="mt-1 text-sm font-semibold leading-6 text-slate-900 dark:text-white">{fact.Statement}</h4>
            <p className="mt-1 text-[11px] text-slate-500 dark:text-slate-400">
              {fact.DerivationType} · {fact.EpistemicStatus} · {Math.round(fact.Confidence * 100)}% Confidence
            </p>
            <div className="mt-3">
              <div className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Graph Path</div>
              {fact.GraphPath.length === 0 ? (
                <p className="mt-1 text-xs text-slate-500">-</p>
              ) : (
                <div className="mt-1 flex flex-wrap items-center gap-1 text-xs text-slate-700 dark:text-slate-200">
                  {fact.GraphPath.sort((a, b) => a.StepOrder - b.StepOrder).flatMap((step, index) => [
                    index > 0 ? <span key={`${step.ConnectionId}-arrow`} aria-hidden="true">→</span> : null,
                    <span key={`${step.ConnectionId}-source`}>{step.SourceEntityName}</span>,
                    <span key={`${step.ConnectionId}-label`} className="text-amber-600 dark:text-amber-300">→ {step.RelationshipLabel} →</span>,
                    <span key={`${step.ConnectionId}-target`}>{step.TargetEntityName}</span>,
                  ].filter((item): item is ReactElement => item !== null))}
                </div>
              )}
            </div>
            {fact.GraphPath.some((step) => step.Evidence) && (
              <div className="mt-3">
                <button
                  type="button"
                  className="text-xs text-indigo-700 dark:text-indigo-300 underline focus:outline-none focus:ring-2 focus:ring-indigo-400"
                  aria-expanded={!!expandedEvidence[fact.KnowledgeFactId]}
                  onClick={() => onToggleEvidence(fact.KnowledgeFactId)}
                >
                  {expandedEvidence[fact.KnowledgeFactId] ? 'Hide Evidence' : 'View Evidence'}
                </button>
                {expandedEvidence[fact.KnowledgeFactId] && (
                  <div className="mt-2 space-y-2 border-l border-indigo-400/30 pl-3 text-xs leading-5 text-slate-600 dark:text-slate-300">
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
  personId,
  initialView = 'graph',
  canRegenerateFacts = false,
  onClose,
  onSelectEngram,
}: {
  documentId: string;
  sectionId?: string;
  personId?: string;
  initialView?: 'graph' | 'facts';
  canRegenerateFacts?: boolean;
  onClose?: () => void;
  onSelectEngram?: (section: StructuredLearningSectionDetail) => void;
}) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const effectivePersonId = personId ?? user?.personId;
  const [documentDetail, setDocumentDetail] = useState<StructuredLearningDocumentDetail | null>(null);
  const [rawEntities, setRawEntities] = useState<StructuredLearningDocumentEntity[]>([]);
  const [rawConnections, setRawConnections] = useState<StructuredLearningConnection[]>([]);
  const [viewMode, setViewMode] = useState<'tree' | 'galaxy'>(sectionId ? 'tree' : 'galaxy');
  const [engramGraphs, setEngramGraphs] = useState<EngramGraph[] | null>(null);
  const [sectionRounds, setSectionRounds] = useState<Record<string, { status: string; completedDate?: string; finalScore?: number; roundNumber?: number }>>({});
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
  // Per-engram entity list facet + per-engram 3D view (2026-09-26) — the
  // per-section tree already showed connections as a diagram, but there
  // was no plain filterable list of just this engram's own entities, and
  // no 3D view scoped to a single engram (only the whole-course galaxy).
  const [showEntitiesBySection, setShowEntitiesBySection] = useState<Record<string, boolean>>({});
  const [show3DBySection, setShow3DBySection] = useState<Record<string, boolean>>({});

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

    setDocumentDetail(document);
    setRawEntities(entities);
    setRawConnections(connections);

    const sortedSections = [...document.Sections]
      .filter((section) => !sectionId || section.StructuredLearningDocumentSectionId === sectionId)
      .sort((a, b) => a.SortOrder - b.SortOrder);
    const graphs = sortedSections.map((section) => ({
      section,
      entities: entities.filter((e) => e.Occurrences.some((o) => o.EngramId === section.StructuredLearningDocumentSectionId)),
      connections: connections.filter((c) => c.SourceSectionId === section.StructuredLearningDocumentSectionId),
    }));
    setEngramGraphs(graphs);

    if (effectivePersonId && document.Sections.length > 0) {
      Promise.all(
        document.Sections.map((sec) =>
          getActiveStructuredLearningAssessmentRound(sec.StructuredLearningDocumentSectionId, effectivePersonId)
            .then((round) => ({ sectionId: sec.StructuredLearningDocumentSectionId, round }))
            .catch(() => ({ sectionId: sec.StructuredLearningDocumentSectionId, round: null }))
        )
      ).then((roundResults) => {
        const roundMap: Record<string, { status: string; completedDate?: string; finalScore?: number; roundNumber?: number }> = {};
        for (const item of roundResults) {
          if (item.round) {
            roundMap[item.sectionId] = {
              status: item.round.Status,
              completedDate: item.round.CompletedDate ?? undefined,
              finalScore: item.round.FinalScore ?? undefined,
              roundNumber: item.round.RoundNumber ?? 1,
            };
          }
        }
        setSectionRounds(roundMap);
      }).catch(() => {});
    }

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
  }, [documentId, sectionId, initialView, effectivePersonId]);

  // Unique concepts scoped to THIS engram only when a sectionId is given
  // (2026-09-26 fix) — previously this always counted/showed the WHOLE
  // course's entities/connections even on a single-engram page, since
  // rawEntities/rawConnections come from the whole-document API calls.
  // engramGraphs is already filtered to just this section by loadGraphs
  // below, so derive totals from it directly instead of the raw lists.
  const totalEntities = useMemo(() => {
    if (sectionId) {
      return engramGraphs?.reduce((sum, g) => sum + g.entities.length, 0) ?? 0;
    }
    const names = new Set(rawEntities.map((e) => e.CanonicalName.trim().toLowerCase()));
    return names.size;
  }, [rawEntities, sectionId, engramGraphs]);
  const totalConnections = engramGraphs?.reduce((sum, g) => sum + g.connections.length, 0) ?? 0;
  const masteredSections = engramGraphs?.filter((g) => sectionRounds[g.section.StructuredLearningDocumentSectionId]?.status === 'Passed') ?? [];
  const masteredCount = masteredSections.length;
  const masteredSectionIds = new Set(masteredSections.map((g) => g.section.StructuredLearningDocumentSectionId));
  const masteredEntityIds = new Set(
    masteredSections.flatMap((g) => g.entities.map((e) => e.EntityId))
  );
  // Paper principle: Schema = only consolidated connections strictly between mastered entities
  const masteredConnectionsCount = (engramGraphs ?? [])
    .filter((g) => masteredSectionIds.has(g.section.StructuredLearningDocumentSectionId))
    .flatMap((g) => g.connections)
    .filter((c) => masteredEntityIds.has(c.SourceEntityId) && masteredEntityIds.has(c.TargetEntityId))
    .length;

  if (loadError || (engramGraphs && totalEntities === 0)) {
    return null;
  }

  return (
    <div className="rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-900/90 p-5 shadow-xl text-slate-900 dark:text-white">
      <div className="flex flex-wrap items-center gap-2">
        <Sparkles className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
        <h3 className="text-sm font-bold text-slate-900 dark:text-white">{t('knowledgeGraph.title')}</h3>
        {masteredCount > 0 && (
          <div className="inline-flex items-center gap-1.5 rounded-full bg-emerald-500/10 dark:bg-emerald-500/20 border border-emerald-500/30 px-2.5 py-0.5 text-xs font-semibold text-emerald-700 dark:text-emerald-300">
            <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
            <span>{t('knowledgeGraph.schemaSummary', { mastered: masteredCount, connections: masteredConnectionsCount })}</span>
          </div>
        )}
        {engramGraphs && (
          <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
            {!sectionId && (
              <div className="inline-flex rounded-xl bg-slate-100 dark:bg-slate-800/90 p-1 border border-slate-200 dark:border-white/10 shadow-inner">
                <button
                  type="button"
                  onClick={() => setViewMode('galaxy')}
                  className={`rounded-lg px-3 py-1 text-xs font-semibold transition ${
                    viewMode === 'galaxy'
                      ? 'bg-indigo-600 text-white shadow-xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                  }`}
                >
                  🌌 {t('courseMap.galaxyView')}
                </button>
                <button
                  type="button"
                  onClick={() => setViewMode('tree')}
                  className={`rounded-lg px-3 py-1 text-xs font-semibold transition ${
                    viewMode === 'tree'
                      ? 'bg-indigo-600 text-white shadow-xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                  }`}
                >
                  🌳 {t('courseMap.treeView')}
                </button>
              </div>
            )}

            <span className="text-xs text-slate-600 dark:text-slate-400 font-medium">
              {t('knowledgeGraph.entitiesAndConnections', { entities: totalEntities, connections: totalConnections })}
            </span>
            <button
              type="button"
              onClick={() => (showAllFacts ? setShowAllFacts(false) : loadAllFacts())}
              className="rounded-full border border-indigo-200 dark:border-indigo-400/40 bg-indigo-50 dark:bg-indigo-500/10 px-2.5 py-1 text-[11px] font-semibold text-indigo-700 dark:text-indigo-200 hover:bg-indigo-100 dark:hover:bg-indigo-500/20 focus:outline-none focus:ring-2 focus:ring-indigo-400"
            >
              {showAllFacts ? t('knowledgeGraph.hideAllFacts') : t('knowledgeGraph.viewAllFacts')}
            </button>
            {onClose && (
              <button
                type="button"
                onClick={onClose}
                className="flex items-center gap-1.5 rounded-full border border-red-300 dark:border-red-400/40 bg-red-50 dark:bg-red-500/10 px-3 py-1 text-xs font-semibold text-red-700 dark:text-red-200 hover:border-red-400 hover:bg-red-100 dark:hover:bg-red-500/20 hover:text-red-900 dark:hover:text-white transition shadow-sm"
                aria-label={t('knowledgeGraph.hideKnowledgeGraph')}
              >
                <X className="h-3.5 w-3.5 text-red-600 dark:text-red-400" />
                <span>{t('knowledgeGraph.hideKnowledgeGraph')}</span>
              </button>
            )}
          </div>
        )}
      </div>

      {!engramGraphs && <p className="mt-3 text-xs text-slate-500 dark:text-slate-400">{t('knowledgeGraph.loading')}</p>}

      {!sectionId && viewMode === 'galaxy' && documentDetail && (
        <div className="mt-4">
          <CourseGalaxy3DGraph
            document={documentDetail}
            entities={rawEntities}
            connections={rawConnections}
            sectionRounds={sectionRounds}
            onSelectSection={(sec) => {
              if (onSelectEngram) {
                onSelectEngram(sec);
              } else {
                setExpandedSectionId(sec.StructuredLearningDocumentSectionId);
                setViewMode('tree');
              }
            }}
            height={560}
          />
        </div>
      )}

      {viewMode === 'tree' && engramGraphs && totalEntities > 0 && (
        <div className="mt-3 flex flex-wrap gap-3 text-[11px] text-slate-600 dark:text-slate-400 font-medium">
          {(['Document', 'Rule', 'Action', 'Value', 'Entity'] as const).map((kind) => (
            <span key={kind} className="flex items-center gap-1.5">
              <span className={`h-2.5 w-2.5 rounded-full border ${NODE_KIND_STYLES[kind]}`} />
              {kind}
            </span>
          ))}
        </div>
      )}

      {viewMode === 'tree' && (
        <div className="mt-4 space-y-2">
        {engramGraphs?.map(({ section, entities, connections }) => {
          const expanded = expandedSectionId === section.StructuredLearningDocumentSectionId;
          const hasData = entities.length > 0 || connections.length > 0;
          const round = sectionRounds[section.StructuredLearningDocumentSectionId];
          const isMastered = round?.status === 'Passed';
          const reviewCount = round?.roundNumber ?? 1;
          const retrievability = isMastered ? getRetrievability(round.completedDate, reviewCount) : null;
          const percent = retrievability !== null ? Math.round(retrievability * 100) : null;
          const memColor = getMemoryColor(retrievability);
          const memTextColor = getMemoryTextColor(retrievability);

          return (
            <div
              key={section.StructuredLearningDocumentSectionId}
              className={`rounded-xl border transition-all duration-300 ${
                isMastered
                  ? 'border-emerald-500/40 bg-emerald-500/[0.04] dark:border-emerald-500/30 dark:bg-emerald-500/[0.03] shadow-xs'
                  : 'border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] opacity-85'
              }`}
            >
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
                <span className="min-w-0 flex-1 truncate text-sm font-semibold text-slate-900 dark:text-white flex items-center gap-2">
                  <span>{section.SortOrder + 1}. {section.SubchapterTitle || section.ChapterTitle}</span>
                  {isMastered && (
                    <span
                      className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-bold shadow-xs"
                      style={{ background: memColor, color: memTextColor }}
                    >
                      <CheckCircle2 className="h-2.5 w-2.5" />
                      {percent !== null ? `${percent}%` : t('knowledgeGraph.mastered')}
                    </span>
                  )}
                  {!isMastered && (
                    <span className="inline-flex items-center rounded-full bg-slate-200 dark:bg-slate-800 px-2 py-0.5 text-[10px] font-medium text-slate-600 dark:text-slate-400">
                      {t('knowledgeGraph.notMastered')}
                    </span>
                  )}
                </span>
                <span className={`shrink-0 text-xs ${hasData ? 'text-slate-500 dark:text-slate-400 font-medium' : 'text-slate-400 dark:text-slate-500'}`}>
                  {hasData ? `${entities.length} · ${connections.length}` : 'Empty'}
                </span>
              </button>

              {expanded && (
                <div className="space-y-3 border-t border-slate-200 dark:border-white/10 px-3 py-3">
                  {entities.length === 0 && connections.length === 0 && (
                    <p className="text-xs text-slate-500 dark:text-slate-400">{t('knowledgeGraph.emptyEngram')}</p>
                  )}

                  {(() => {
                    const secondaryCount = connections.filter((c) => isSecondaryEdge(c.EpistemicStatus)).length;
                    const showSecondary = showSecondaryBySection[section.StructuredLearningDocumentSectionId] ?? false;
                    const showJson = showJsonBySection[section.StructuredLearningDocumentSectionId] ?? false;
                    const showGraph = showGraphBySection[section.StructuredLearningDocumentSectionId] ?? true;
                    const showFacts = showFactsBySection[section.StructuredLearningDocumentSectionId] ?? true;
                    const showEntities = showEntitiesBySection[section.StructuredLearningDocumentSectionId] ?? false;
                    const show3D = show3DBySection[section.StructuredLearningDocumentSectionId] ?? false;
                    const hasFactsLoaded = !!factsBySection[section.StructuredLearningDocumentSectionId];
                    return (
                      <div className="flex flex-wrap items-center gap-2">
                        <button
                          type="button"
                          onClick={() => setShowEntitiesBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showEntities }))}
                          className={`flex items-center gap-1 rounded-full border px-2.5 py-1 text-[11px] font-medium transition ${
                            showEntities
                              ? 'border-emerald-400/60 bg-emerald-500/20 text-emerald-700 dark:text-emerald-200'
                              : 'border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-500/20'
                          }`}
                        >
                          <ListFilter className="h-3 w-3" />
                          {showEntities ? t('knowledgeGraph.hideEntities', { count: entities.length }) : t('knowledgeGraph.showEntities', { count: entities.length })}
                        </button>
                        <button
                          type="button"
                          onClick={() => setShow3DBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !show3D }))}
                          className={`rounded-full border px-2.5 py-1 text-[11px] font-medium transition ${
                            show3D
                              ? 'border-indigo-400/60 bg-indigo-500/20 text-indigo-700 dark:text-indigo-200'
                              : 'border-indigo-500/30 bg-indigo-500/10 text-indigo-700 dark:text-indigo-300 hover:bg-indigo-500/20'
                          }`}
                        >
                          🌌 {show3D ? t('knowledgeGraph.hide3D') : t('knowledgeGraph.show3D')}
                        </button>
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
                          {hasFactsLoaded
                            ? showFacts
                              ? t('knowledgeGraph.hideFacts')
                              : t('knowledgeGraph.viewFacts')
                            : t('knowledgeGraph.viewFacts')}
                        </button>
                        <button
                          type="button"
                          onClick={() => setShowGraphBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showGraph }))}
                          className="rounded-full border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-600 dark:text-slate-400 hover:border-slate-300 dark:hover:border-white/20 hover:text-slate-900 dark:hover:text-slate-200"
                        >
                          {showGraph ? t('knowledgeGraph.hideGraph') : t('knowledgeGraph.showGraph')}
                        </button>
                        {showGraph && (
                          <button
                            type="button"
                            onClick={() => setFullscreenGraphSectionId(section.StructuredLearningDocumentSectionId)}
                            className="rounded-full border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-600 dark:text-slate-400 hover:border-slate-300 dark:hover:border-white/20 hover:text-slate-900 dark:hover:text-slate-200"
                          >
                            {t('knowledgeGraph.expandGraph')}
                          </button>
                        )}
                        {canRegenerateFacts && (
                          <button
                            type="button"
                            onClick={() => regenerateFacts(section.StructuredLearningDocumentSectionId)}
                            disabled={factsRegenerating[section.StructuredLearningDocumentSectionId] ?? false}
                            className="flex items-center gap-1 rounded-full border border-amber-400/40 bg-amber-50 dark:bg-amber-500/10 px-2.5 py-1 text-[11px] font-medium text-amber-800 dark:text-amber-200 hover:bg-amber-100 dark:hover:bg-amber-500/20 focus:outline-none focus:ring-2 focus:ring-amber-400 disabled:cursor-not-allowed disabled:opacity-50"
                          >
                            <RefreshCw className={`h-3 w-3 ${factsRegenerating[section.StructuredLearningDocumentSectionId] ? 'animate-spin' : ''}`} />
                            {factsRegenerating[section.StructuredLearningDocumentSectionId] ? t('knowledgeGraph.regenerating') : t('knowledgeGraph.regenerateGraph')}
                          </button>
                        )}
                        {secondaryCount > 0 && (
                          <button
                            type="button"
                            onClick={() => setShowSecondaryBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showSecondary }))}
                            className="rounded-full border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-600 dark:text-slate-400 hover:border-slate-300 dark:hover:border-white/20 hover:text-slate-900 dark:hover:text-slate-200"
                          >
                            {showSecondary
                              ? t('knowledgeGraph.hideSecondary', { count: secondaryCount })
                              : t('knowledgeGraph.showSecondary', { count: secondaryCount })}
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={() => setShowJsonBySection((prev) => ({ ...prev, [section.StructuredLearningDocumentSectionId]: !showJson }))}
                          className="flex items-center gap-1 rounded-full border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.02] px-2.5 py-1 text-[11px] text-slate-600 dark:text-slate-400 hover:border-slate-300 dark:hover:border-white/20 hover:text-slate-900 dark:hover:text-slate-200"
                        >
                          <Code2 className="h-3 w-3" /> {showJson ? t('knowledgeGraph.hideJson') : t('knowledgeGraph.viewJson')}
                        </button>
                      </div>
                    );
                  })()}

                  {(showEntitiesBySection[section.StructuredLearningDocumentSectionId] ?? false) && (
                    <div className="rounded-xl border border-emerald-500/30 bg-emerald-50 dark:bg-emerald-950/20 p-4 shadow-inner">
                      <div className="mb-3 flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          <span className="flex h-6 w-6 items-center justify-center rounded-lg bg-emerald-500/20 text-emerald-700 dark:text-emerald-300 text-xs font-bold">
                            {entities.length}
                          </span>
                          <h4 className="text-xs font-semibold uppercase tracking-wider text-emerald-700 dark:text-emerald-300">
                            {t('knowledgeGraph.entitiesTitle')}
                          </h4>
                        </div>
                        <span className="text-[11px] text-slate-500 dark:text-slate-400">
                          {t('knowledgeGraph.entityTypes', { count: new Set(entities.map((e) => e.EntityType)).size })}
                        </span>
                      </div>
                      {entities.length === 0 ? (
                        <p className="text-xs text-slate-500 dark:text-slate-400">{t('knowledgeGraph.noEntities')}</p>
                      ) : (
                        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
                          {entities.map((entity) => {
                            const kind = getNodeKind(entity.EntityType);
                            return (
                              <div
                                key={entity.EntityId}
                                className="flex flex-col justify-between rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-900/80 p-3 shadow-xs hover:border-emerald-400/40 transition"
                              >
                                <div>
                                  <div className="flex items-start justify-between gap-2">
                                    <span className="text-sm font-semibold text-slate-900 dark:text-white leading-snug">{entity.CanonicalName}</span>
                                    <span className={`shrink-0 rounded-md border px-1.5 py-0.5 text-[10px] font-medium ${NODE_KIND_STYLES[kind]}`}>
                                      {entity.EntityType}
                                    </span>
                                  </div>
                                  {entity.Description && (
                                    <p className="mt-1.5 text-xs text-slate-600 dark:text-slate-300 leading-relaxed line-clamp-3">{entity.Description}</p>
                                  )}
                                  {entity.OntologicalClass && (
                                    <div className="mt-1 text-[10px] text-slate-500 dark:text-slate-400 italic">Class: {entity.OntologicalClass}</div>
                                  )}
                                </div>
                                {entity.Aliases && entity.Aliases.length > 0 && (
                                  <div className="mt-2.5 flex flex-wrap items-center gap-1 border-t border-slate-100 dark:border-white/5 pt-2 text-[10px] text-slate-500 dark:text-slate-400">
                                    <span>Alias:</span>
                                    {entity.Aliases.map((alias, aIdx) => (
                                      <span key={aIdx} className="rounded bg-slate-100 dark:bg-white/5 px-1.5 py-0.5 font-mono text-[10px] text-slate-700 dark:text-slate-300">
                                        {alias}
                                      </span>
                                    ))}
                                  </div>
                                )}
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  )}

                  {(show3DBySection[section.StructuredLearningDocumentSectionId] ?? false) && documentDetail && (
                    <div className="overflow-hidden rounded-xl border border-indigo-500/30 bg-slate-950">
                      <CourseGalaxy3DGraph
                        document={{ ...documentDetail, Sections: [section] }}
                        entities={entities}
                        connections={connections}
                        sectionRounds={sectionRounds}
                        height={420}
                      />
                    </div>
                  )}

                  {factsLoading[section.StructuredLearningDocumentSectionId] && (
                    <p className="rounded-lg border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-950/30 p-3 text-xs text-slate-500 dark:text-slate-400" role="status">
                      {t('knowledgeGraph.loadingFacts')}
                    </p>
                  )}
                  {factsError[section.StructuredLearningDocumentSectionId] && (
                    <div className="rounded-lg border border-red-300 dark:border-red-400/30 bg-red-50 dark:bg-red-500/10 p-3 text-xs text-red-700 dark:text-red-200" role="alert">
                      <p>{t('knowledgeGraph.errorLoadingFacts')}</p>
                      <button type="button" className="mt-2 underline focus:outline-none focus:ring-2 focus:ring-red-300" onClick={() => loadFacts(section.StructuredLearningDocumentSectionId)}>
                        {t('knowledgeGraph.retry')}
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
                    <pre className="max-h-96 overflow-auto rounded-lg border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-slate-950/60 p-3 text-[11px] text-slate-700 dark:text-slate-300">
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
                            <div
                              className={`rounded-xl border-2 text-center font-bold shadow-md transition-all duration-300 ${
                                large ? 'px-8 py-4 text-xl' : 'px-5 py-3 text-base'
                              } ${
                                isMastered
                                  ? 'border-emerald-400/80 bg-emerald-50 dark:bg-emerald-500/15 text-emerald-800 dark:text-emerald-100 shadow-emerald-950/30'
                                  : 'border-slate-300 dark:border-slate-700/60 bg-slate-100 dark:bg-slate-900/40 text-slate-600 dark:text-slate-400'
                              }`}
                              style={isMastered && memColor ? { borderColor: memColor } : undefined}
                            >
                              <div className="flex items-center justify-center gap-2">
                                <span>{section.SubchapterTitle || section.ChapterTitle}</span>
                                {isMastered && percent !== null && (
                                  <span
                                    className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold shadow-xs"
                                    style={{ background: memColor, color: memTextColor }}
                                  >
                                    <CheckCircle2 className="h-3 w-3" />
                                    {percent}%
                                  </span>
                                )}
                              </div>
                            </div>
                            <div
                              className={`relative mt-1 flex flex-wrap items-start justify-center overflow-x-auto pt-3 ${
                                large ? 'gap-14' : 'gap-8'
                              } ${
                                isMastered ? 'border-t border-emerald-400/50' : 'border-t border-slate-300 dark:border-slate-700/50'
                              }`}
                            >
                              {trees.map((tree) => (
                                <div key={tree.entity.EntityId} className="flex flex-col items-center">
                                  <ArrowDown
                                    className={`mb-1 shrink-0 ${
                                      large ? 'h-5 w-5' : 'h-3 w-3'
                                    } ${
                                      isMastered ? 'text-emerald-500 dark:text-emerald-400 drop-shadow-xs' : 'text-slate-500 dark:text-slate-600'
                                    }`}
                                  />
                                  <TreeNode node={tree} large={large} isMastered={isMastered} memColor={memColor} />
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
                                className={`rounded-full border text-slate-700 dark:text-slate-300 ${
                                  large ? 'px-3.5 py-1.5 text-sm' : 'px-2.5 py-1 text-xs'
                                } ${
                                  isMastered
                                    ? 'border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 text-emerald-800 dark:text-emerald-200'
                                    : 'border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.03] text-slate-500 dark:text-slate-400 opacity-80 dark:opacity-60'
                                }`}
                                title={entity.Description ?? undefined}
                              >
                                {entity.CanonicalName}
                                <span className={`ml-1.5 text-slate-500 dark:text-slate-400 ${large ? 'text-xs' : 'text-[10px]'}`}>{entity.EntityType}</span>
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
                          <div className="fixed inset-0 z-50 flex flex-col bg-white dark:bg-slate-950 p-6">
                            <div className="mb-4 flex shrink-0 items-center justify-between">
                              <h4 className="text-lg font-semibold text-slate-900 dark:text-white">{section.SubchapterTitle || section.ChapterTitle}</h4>
                              <button
                                type="button"
                                onClick={() => setFullscreenGraphSectionId(null)}
                                className="rounded-lg border border-slate-200 dark:border-white/10 bg-slate-100 dark:bg-white/[0.05] px-4 py-2 text-sm font-medium text-slate-900 dark:text-white hover:bg-slate-200 dark:hover:bg-white/10"
                              >
                                {t('knowledgeGraph.close')}
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
      )}
      {showAllFacts && engramGraphs && (
        <section className="mt-4 space-y-2 border-t border-slate-200 dark:border-white/10 pt-4" aria-label="All Knowledge Facts">
          <div className="flex items-center gap-2">
            <h4 className="text-sm font-semibold text-slate-900 dark:text-white">All Knowledge Facts</h4>
            <button
              type="button"
              onClick={() => setShowAllFacts(false)}
              className="ml-auto rounded-full p-1 text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10 hover:text-slate-900 dark:hover:text-white focus:outline-none focus:ring-2 focus:ring-indigo-400"
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
              <div key={section.StructuredLearningDocumentSectionId} className="rounded-lg border border-slate-200 dark:border-white/10">
                <button
                  type="button"
                  onClick={() =>
                    setExpandedFactsSectionIds((previous) => ({
                      ...previous,
                      [section.StructuredLearningDocumentSectionId]: !previous[section.StructuredLearningDocumentSectionId],
                    }))
                  }
                  className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm font-semibold text-slate-900 dark:text-white hover:bg-slate-50 dark:hover:bg-white/[0.03]"
                >
                  {isOpen ? <ChevronDown className="h-4 w-4 shrink-0" /> : <ChevronRight className="h-4 w-4 shrink-0" />}
                  {section.SortOrder + 1}. {section.SubchapterTitle || section.ChapterTitle}
                  <span className="ml-auto text-xs font-normal text-slate-500 dark:text-slate-400">{facts.TotalFacts}</span>
                </button>
                {isOpen && (
                  <div className="border-t border-slate-200 dark:border-white/10 p-3">
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
