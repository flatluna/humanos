import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { Lock, PlayCircle, Star, Info, ChevronDown, ChevronUp, Network } from 'lucide-react';
import { useI18n } from '../i18n/useI18n';
import { useAuth } from '@/auth/AuthContext';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import {
  getCapabilityGraph,
  type BackendCapabilityGraph,
  type BackendCapabilityGraphNode,
  type BackendCapabilityGraphEdge,
  type CapabilityGraphNodeState,
} from '../api/capabilityGraphApi';

/**
 * Capability Graph Map — "climb" pyramid layout ported from Capability
 * Studio's PreviewGraphPage (2026-07-22, see
 * capabilitystudio/src/pages/PreviewGraphPage.tsx): nodes are grouped into
 * levels 1..N by longest-prerequisite-path depth (see groupIntoLevels)
 * instead of the earlier dagre/xyflow tree, and rendered as colored
 * circular bubbles in centered rows — foundational nodes (no
 * prerequisites) form the first level, more advanced nodes sit in later
 * levels, visually separated by a chevron between rows instead of drawn
 * edge lines.
 */

/**
 * Groups nodes into "climb" levels using the graph's prerequisite edges
 * (Requires/BuildsOn: Source depends on Target) — the foundational nodes
 * with no prerequisites form level 1, and each subsequent level is
 * 1 + its deepest prerequisite's level.
 */
function groupIntoLevels(
  nodes: BackendCapabilityGraphNode[],
  edges: BackendCapabilityGraphEdge[]
): BackendCapabilityGraphNode[][] {
  const prerequisitesOf = new Map<string, string[]>();
  nodes.forEach((n) => prerequisitesOf.set(n.capabilityGraphNodeId, []));
  edges.forEach((e) => prerequisitesOf.get(e.sourceNodeId)?.push(e.targetNodeId));

  const tierCache = new Map<string, number>();
  function tierOf(nodeId: string, visiting: Set<string>): number {
    if (tierCache.has(nodeId)) return tierCache.get(nodeId)!;
    if (visiting.has(nodeId)) return 0; // guards against an accidental cycle
    visiting.add(nodeId);
    const prereqs = prerequisitesOf.get(nodeId) ?? [];
    const tier = prereqs.length === 0 ? 0 : 1 + Math.max(...prereqs.map((p) => tierOf(p, visiting)));
    visiting.delete(nodeId);
    tierCache.set(nodeId, tier);
    return tier;
  }
  nodes.forEach((n) => tierOf(n.capabilityGraphNodeId, new Set()));

  const maxTier = Math.max(0, ...Array.from(tierCache.values()));
  const levels: BackendCapabilityGraphNode[][] = Array.from({ length: maxTier + 1 }, () => []);
  nodes.forEach((n) => levels[tierCache.get(n.capabilityGraphNodeId) ?? 0].push(n));
  levels.forEach((level) => level.sort((a, b) => a.sortOrder - b.sortOrder));
  return levels;
}

const STATE_STYLES: Record<
  CapabilityGraphNodeState,
  { ring: string; bubble: string; icon: string; label: string; Icon: typeof Lock }
> = {
  Locked: {
    ring: 'border-slate-200 dark:border-white/10',
    bubble: 'bg-white dark:bg-white/[0.03]',
    icon: 'text-slate-500',
    label: 'text-slate-500',
    Icon: Lock,
  },
  Available: {
    ring: 'border-transparent',
    bubble: 'bg-gradient-to-br from-brand-500 to-accent-500 shadow-lg shadow-brand-500/30',
    icon: 'text-[#fff]',
    label: 'text-slate-900 dark:text-white font-semibold',
    Icon: PlayCircle,
  },
  Mastered: {
    ring: 'border-amber-300/50',
    bubble: 'bg-amber-400/10',
    icon: 'text-amber-600 dark:text-amber-300',
    label: 'text-amber-700 dark:text-amber-100',
    Icon: Star,
  },
};

export default function CapabilityGraphMapPage() {
  const { capabilityId } = useParams();
  const [searchParams] = useSearchParams();
  const programId = searchParams.get('programId');
  const { t } = useI18n();
  const navigate = useNavigate();
  const { user } = useAuth();
  const personId = user?.personId ?? '';

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
    if (!capabilityId || !personId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    getCapabilityGraph(capabilityId, personId)
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
  }, [capabilityId, personId, t.graphNotFound]);

  const levels = useMemo(() => (graph ? groupIntoLevels(graph.nodes, graph.edges) : []), [graph]);

  const levelNameByIndex = useMemo(() => {
    const map = new Map<number, string>();
    graph?.levels?.forEach((l) => map.set(l.levelIndex, l.name));
    return map;
  }, [graph]);

  const nextUpNodeId = useMemo(() => {
    for (const level of levels) {
      const found = level.find((n) => n.state === 'Available');
      if (found) return found.capabilityGraphNodeId;
    }
    return null;
  }, [levels]);

  // Average score across every node that's already Mastered and has a
  // recorded FinalScore — gives the student one glance-able "how am I
  // doing overall" number instead of only per-node scores.
  const averageScore = useMemo(() => {
    if (!graph) return null;
    const scored = graph.nodes.filter((n): n is BackendCapabilityGraphNode & { finalScore: number } =>
      typeof n.finalScore === 'number'
    );
    if (scored.length === 0) return null;
    return Math.round(scored.reduce((sum, n) => sum + n.finalScore, 0) / scored.length);
  }, [graph]);

  function handleNodeClick(node: BackendCapabilityGraphNode) {
    if (node.state === 'Locked' && !reviewMode) return;
    const suffix = programId ? `?programId=${programId}` : '';
    navigate(`/capabilities/${capabilityId}/nodes/${node.capabilityGraphNodeId}${suffix}`);
  }

  return (
    <div className="mx-auto max-w-3xl p-4 sm:p-8">
      <Link to={programId ? '/programs' : '/capabilities'} className="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:underline">
        ← {programId ? t.myPrograms : t.backToSubjects}
      </Link>

      {graph && (
        <div className="mt-4 text-center">
          <h1 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white">{graph.name}</h1>
          {graph.description && (
            <p className="mx-auto mt-2 max-w-2xl text-sm text-slate-500 dark:text-slate-400">{graph.description}</p>
          )}

          {(graph.executiveSummary || (graph.keyEntities && graph.keyEntities.length > 0)) && (
            <div className="mt-3 flex flex-col items-center">
              <button
                type="button"
                onClick={() => setSummaryOpen((open) => !open)}
                className="flex items-center gap-1.5 rounded-lg border border-slate-200 dark:border-white/10 bg-slate-100 dark:bg-white/[0.04] px-3 py-1.5 text-xs font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-white/[0.08]"
              >
                <Info className="h-3.5 w-3.5" />
                {summaryOpen ? t.graphSummaryHideButton : t.graphSummaryShowButton}
                {summaryOpen ? <ChevronUp className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
              </button>

              {summaryOpen && (
                <div className="mt-2 max-w-2xl rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-4 text-left">
                  {graph.executiveSummary && (
                    <div>
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                        {t.graphExecutiveSummaryTitle}
                      </p>
                      <p className="mt-1 text-sm leading-relaxed text-slate-600 dark:text-slate-300">{graph.executiveSummary}</p>
                    </div>
                  )}

                  {graph.keyEntities && graph.keyEntities.length > 0 && (
                    <div className={graph.executiveSummary ? 'mt-4' : ''}>
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                        {t.graphKeyEntitiesTitle}
                      </p>
                      <ul className="mt-1.5 space-y-1.5">
                        {graph.keyEntities.map((entity) => (
                          <li key={entity.name} className="text-sm text-slate-600 dark:text-slate-300">
                            <span className="font-medium text-slate-900 dark:text-white">{entity.name}</span>{' '}
                            <span className="text-xs text-slate-500">({entity.type})</span>
                            {entity.note && <span className="text-slate-500 dark:text-slate-400"> — {entity.note}</span>}
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

      {loading && (
        <LoadingBackdrop title={t.graphLoadingTitle} description={t.graphLoadingDescription} icon={Network} />
      )}
      {error && !loading && <p className="mt-6 text-center text-red-600 dark:text-red-400">{error}</p>}

      {!loading && !error && graph && (
        <>
          <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
            <button
              type="button"
              onClick={() => setReviewMode((prev) => !prev)}
              className={`rounded-lg border px-3 py-1.5 text-xs font-medium transition ${
                reviewMode
                  ? 'border-amber-400/50 bg-amber-400/10 text-amber-600 dark:text-amber-300 hover:bg-amber-400/20'
                  : 'border-slate-200 dark:border-white/10 bg-slate-100 dark:bg-white/[0.04] text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-white/[0.08]'
              }`}
            >
              {reviewMode ? t.graphReviewModeOn : t.graphReviewModeOff}
            </button>
            {averageScore !== null && (
              <span className="flex items-center gap-1.5 rounded-lg border border-amber-300/50 bg-amber-400/10 px-3 py-1.5 text-xs font-semibold text-amber-700 dark:text-amber-300">
                <Star className="h-3.5 w-3.5" fill="currentColor" />
                {t.graphAverageScoreLabel}: {averageScore}/100
              </span>
            )}
          </div>

          <div className="mt-8 flex flex-col items-center">
            {levels.map((level, levelIndex) => (
              <div key={levelIndex} className="flex w-full flex-col items-center">
                <span className="mb-4 rounded-full border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] px-3 py-1 text-[11px] font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">
                  {levelNameByIndex.get(levelIndex) ?? `${t.graphLevelLabel} ${levelIndex + 1}`}
                </span>

                <div className="flex flex-wrap justify-center gap-x-6 gap-y-8">
                  {level.map((node) => {
                    const style = STATE_STYLES[node.state];
                    const Icon = style.Icon;
                    const clickable = node.state !== 'Locked' || reviewMode;
                    const isNextUp = node.capabilityGraphNodeId === nextUpNodeId;

                    return (
                      <button
                        key={node.capabilityGraphNodeId}
                        type="button"
                        disabled={!clickable}
                        onClick={() => handleNodeClick(node)}
                        className={`group flex w-24 flex-col items-center gap-2 text-center transition ${
                          clickable ? 'cursor-pointer' : 'cursor-not-allowed'
                        }`}
                      >
                        <div className="relative">
                          {isNextUp && (
                            <span className="absolute -top-7 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full bg-brand-500 px-2 py-0.5 text-[10px] font-semibold text-[#fff] shadow-md">
                              {t.graphStartHere}
                            </span>
                          )}
                          {isNextUp && (
                            <span className="absolute inset-0 -m-1.5 rounded-full border-2 border-brand-400 animate-ping" />
                          )}
                          <div
                            className={`relative flex h-16 w-16 items-center justify-center rounded-full border-2 sm:h-20 sm:w-20 ${style.ring} ${style.bubble} ${
                              clickable ? 'transition-transform group-hover:scale-110' : 'opacity-60'
                            }`}
                          >
                            <Icon
                              className={`h-6 w-6 sm:h-7 sm:w-7 ${style.icon}`}
                              fill={node.state === 'Mastered' ? 'currentColor' : 'none'}
                            />
                          </div>
                          {/* Deep Dive mastery badge — a SMALL, visually
                              distinct star (sky blue, corner-pinned) from
                              the large amber Mastered-state star above.
                              Can light up well before the node's overall
                              state becomes 'Mastered' (Deep Dive is only
                              one of the 5 steps) — see
                              GraphProgressionEngine.DeepDiveMastered. */}
                          {node.deepDiveMastered && node.state !== 'Mastered' && (
                            <span
                              title={t.deepDiveMasteredBadgeHint}
                              className="absolute -bottom-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full border-2 border-white bg-sky-500 shadow-md dark:border-slate-950"
                            >
                              <Star className="h-2.5 w-2.5 text-[#fff]" fill="currentColor" />
                            </span>
                          )}
                        </div>
                        <p className={`line-clamp-2 text-xs leading-tight ${style.label}`}>{node.name}</p>
                        {typeof node.finalScore === 'number' && (
                          <span className="rounded-full bg-amber-400/15 px-2 py-0.5 text-[10px] font-semibold text-amber-600 dark:text-amber-300">
                            {node.finalScore}/100
                          </span>
                        )}
                      </button>
                    );
                  })}
                </div>

                {levelIndex < levels.length - 1 && (
                  <div className="my-4 flex w-full max-w-xs items-center gap-3">
                    <div className="h-px flex-1 bg-amber-400/40" />
                    <ChevronDown className="h-4 w-4 flex-none text-amber-500 dark:text-amber-400/70" />
                    <div className="h-px flex-1 bg-amber-400/40" />
                  </div>
                )}
              </div>
            ))}

            {levels.length === 0 && (
              <p className="w-full rounded-2xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-12 text-center text-sm text-slate-500 dark:text-slate-400">
                {t.graphNoNodesYet}
              </p>
            )}
          </div>

          {levels.length > 0 && (
            <div className="mt-12 flex flex-wrap justify-center gap-4 border-t border-slate-200 dark:border-white/10 pt-6 text-xs text-slate-500 dark:text-slate-400">
              <LegendDot colorClass="bg-slate-500" label={t.graphLegendLocked} />
              <LegendDot colorClass="bg-brand-400" label={t.graphLegendAvailable} />
              <LegendDot colorClass="bg-amber-400" label={t.graphLegendMastered} />
            </div>
          )}
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
