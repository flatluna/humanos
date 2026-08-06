import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { Lock, PlayCircle, Star, Info, ChevronDown, ChevronUp, Menu, X, ListChecks, Search } from 'lucide-react';
import { useI18n } from '../i18n/useI18n';
import { useAuth } from '@/auth/AuthContext';
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
 *
 * Also ported (2026-08-05, same source page) the "course contents"
 * sidebar — a Coursera-style table of contents alongside the map, purely
 * a navigation aid: it never changes Locked/Available/Mastered gating, it
 * just lets a student search and jump straight to any already-unlocked
 * node and see overall progress at a glance.
 */

// A single consistent brand accent (2026-08-05) instead of a per-level
// rainbow — cycling through 6 saturated colors read as a chaotic wall of
// unrelated colors on longer courses; chapters are differentiated by
// their own name/number instead.
const LEVEL_ACCENT = {
  text: 'text-brand-600 dark:text-brand-300',
  dot: 'bg-brand-500 dark:bg-brand-400',
  border: 'border-brand-200 dark:border-brand-400/30',
};

/**
 * Groups nodes into "climb" levels.
 *
 * Preferred grouping (2026-08-04): by each node's own `chapterIndex` — the
 * source document's chapter/module structure, so nodes from the same
 * chapter sit in one row exactly as the document/professor planned it,
 * regardless of any prerequisite edge between them.
 *
 * Falls back to the OLDER edge-depth tiering only when no node in the
 * graph has a chapterIndex at all — i.e. capabilities created before that
 * field existed.
 */
function groupIntoLevels(
  nodes: BackendCapabilityGraphNode[],
  edges: BackendCapabilityGraphEdge[]
): BackendCapabilityGraphNode[][] {
  const hasChapterIndex = nodes.some((n) => n.chapterIndex !== null && n.chapterIndex !== undefined);
  if (hasChapterIndex) {
    return groupByChapterIndex(nodes);
  }
  return groupByPrerequisiteTier(nodes, edges);
}

function groupByChapterIndex(nodes: BackendCapabilityGraphNode[]): BackendCapabilityGraphNode[][] {
  const byChapter = new Map<number, BackendCapabilityGraphNode[]>();
  const unassigned: BackendCapabilityGraphNode[] = [];

  nodes.forEach((n) => {
    if (n.chapterIndex === null || n.chapterIndex === undefined) {
      unassigned.push(n);
      return;
    }
    const group = byChapter.get(n.chapterIndex) ?? [];
    group.push(n);
    byChapter.set(n.chapterIndex, group);
  });

  const levels = Array.from(byChapter.keys())
    .sort((a, b) => a - b)
    .map((chapterIndex) => {
      const group = byChapter.get(chapterIndex)!;
      group.sort((a, b) => a.sortOrder - b.sortOrder);
      return group;
    });

  if (unassigned.length > 0) {
    unassigned.sort((a, b) => a.sortOrder - b.sortOrder);
    levels.push(unassigned);
  }

  return levels;
}

function groupByPrerequisiteTier(
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
  // Course-contents sidebar (2026-08-05, ported from Capability Studio's
  // PreviewGraphPage) — a Coursera-style index alongside the existing
  // "climb" map, purely a navigation aid: it never changes
  // Locked/Available/Mastered gating, it just lets a student jump straight
  // to any already-unlocked node and see overall progress at a glance
  // instead of scrolling through the whole map. Collapsed by default on
  // mobile (toggled via this state); always visible at the `lg` breakpoint.
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarQuery, setSidebarQuery] = useState('');

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

  // Sidebar search (2026-08-05) — matches either the chapter/level title or
  // an individual node name. A chapter whose title matches is kept whole
  // (so the student can still see every node in it); otherwise only its
  // matching nodes are kept. Case/accent-insensitive for ES/EN content.
  const normalizeForSearch = useCallback(
    (value: string) => value.toLocaleLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, ''),
    []
  );
  const filteredLevels = useMemo(() => {
    const query = normalizeForSearch(sidebarQuery.trim());
    if (!query) return levels.map((level, levelIndex) => ({ levelIndex, level }));

    return levels
      .map((level, levelIndex) => {
        const levelName = levelNameByIndex.get(levelIndex) ?? `${t.graphLevelLabel} ${levelIndex + 1}`;
        const levelMatches = normalizeForSearch(levelName).includes(query);
        const matchingNodes = levelMatches ? level : level.filter((node) => normalizeForSearch(node.name).includes(query));
        return { levelIndex, level: matchingNodes };
      })
      .filter(({ level }) => level.length > 0);
  }, [levels, levelNameByIndex, sidebarQuery, normalizeForSearch, t.graphLevelLabel]);

  const scrollToLevel = useCallback((levelIndex: number) => {
    document.getElementById(`capability-graph-level-${levelIndex}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, []);

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

  const progress = useMemo(() => {
    const total = graph?.nodes.length ?? 0;
    const mastered = graph?.nodes.filter((n) => n.state === 'Mastered').length ?? 0;
    return { total, mastered, percent: total > 0 ? Math.round((mastered / total) * 100) : 0 };
  }, [graph]);

  const isNodeClickable = useCallback(
    (node: BackendCapabilityGraphNode) => node.state !== 'Locked' || reviewMode,
    [reviewMode]
  );

  function handleNodeClick(node: BackendCapabilityGraphNode) {
    if (node.state === 'Locked' && !reviewMode) return;
    const suffix = programId ? `?programId=${programId}` : '';
    navigate(`/capabilities/${capabilityId}/nodes/${node.capabilityGraphNodeId}${suffix}`);
  }

  return (
    <div className="mx-auto max-w-6xl p-4 sm:p-8 xl:max-w-7xl 2xl:max-w-[96rem]">
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

      {loading && <p className="mt-6 text-center text-slate-500 dark:text-slate-400">{t.graphLoading}</p>}
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

          {levels.length > 0 && (
            <div className="mt-6 flex justify-center lg:hidden">
              <button
                type="button"
                onClick={() => setSidebarOpen((open) => !open)}
                className="flex items-center gap-1.5 rounded-lg border border-slate-200 dark:border-white/10 bg-slate-100 dark:bg-white/[0.04] px-3 py-1.5 text-xs font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-white/[0.08]"
              >
                {sidebarOpen ? <X className="h-3.5 w-3.5" /> : <Menu className="h-3.5 w-3.5" />}
                {sidebarOpen ? t.graphSidebarToggleHide : t.graphSidebarToggleShow}
              </button>
            </div>
          )}

          <div className="mt-8 flex flex-col gap-8 lg:flex-row lg:items-start lg:gap-10">
            {levels.length > 0 && (
              <aside className={`w-full flex-none lg:sticky lg:top-20 lg:block lg:w-72 xl:w-80 2xl:w-96 ${sidebarOpen ? 'block' : 'hidden'}`}>
                <div className="rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-4 lg:max-h-[calc(100vh-6rem)] lg:overflow-y-auto">
                  <div className="flex items-center gap-2 text-sm font-semibold text-slate-900 dark:text-white">
                    <ListChecks className="h-4 w-4 text-brand-500" />
                    {t.graphSidebarTitle}
                  </div>

                  <div className="mt-2 flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
                    <span>
                      {t.graphSidebarProgressLabel}: {progress.mastered}/{progress.total}
                    </span>
                    <span>{progress.percent}%</span>
                  </div>
                  <div className="mt-1.5 h-1.5 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                    <div
                      className="h-full rounded-full bg-gradient-to-r from-brand-500 to-accent-500"
                      style={{ width: `${progress.percent}%` }}
                    />
                  </div>

                  <div className="relative mt-4">
                    <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
                    <input
                      type="text"
                      value={sidebarQuery}
                      onChange={(e) => setSidebarQuery(e.target.value)}
                      placeholder={t.graphSidebarSearchPlaceholder}
                      className="w-full rounded-lg border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.04] py-1.5 pl-8 pr-7 text-xs text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-brand-400/50"
                    />
                    {sidebarQuery && (
                      <button
                        type="button"
                        onClick={() => setSidebarQuery('')}
                        className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    )}
                  </div>

                  <nav className="mt-3 space-y-3">
                    {filteredLevels.map(({ levelIndex, level }) => {
                      const accent = LEVEL_ACCENT;
                      return (
                        <div key={levelIndex} className="rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-2.5">
                          <button
                            type="button"
                            onClick={() => scrollToLevel(levelIndex)}
                            className={`flex w-full items-center gap-2 text-left text-xs font-semibold uppercase tracking-wide ${accent.text} hover:underline`}
                          >
                            <span className={`h-1.5 w-1.5 rounded-full ${accent.dot}`} />
                            {levelNameByIndex.get(levelIndex) ?? `${t.graphLevelLabel} ${levelIndex + 1}`}
                          </button>
                          <ul className="mt-1.5 space-y-1">
                            {level.map((node) => {
                              const style = STATE_STYLES[node.state];
                              const Icon = style.Icon;
                              const clickable = isNodeClickable(node);
                              const isNextUp = node.capabilityGraphNodeId === nextUpNodeId;
                              return (
                                <li key={node.capabilityGraphNodeId}>
                                  <button
                                    type="button"
                                    disabled={!clickable}
                                    onClick={() => handleNodeClick(node)}
                                    className={`flex w-full items-center gap-1.5 rounded-md px-1.5 py-1 text-left text-xs ${
                                      clickable
                                        ? 'text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-white/[0.06]'
                                        : 'cursor-not-allowed text-slate-400 dark:text-slate-500 opacity-60'
                                    }`}
                                  >
                                    <Icon className={`h-3.5 w-3.5 flex-none ${style.icon}`} fill={node.state === 'Mastered' ? 'currentColor' : 'none'} />
                                    <span className="line-clamp-1 flex-1">{node.name}</span>
                                    {isNextUp && (
                                      <span className="flex-none rounded-full bg-brand-500 px-1.5 py-0.5 text-[9px] font-semibold text-[#fff]">
                                        {t.graphSidebarNextBadge}
                                      </span>
                                    )}
                                  </button>
                                </li>
                              );
                            })}
                          </ul>
                        </div>
                      );
                    })}

                    {filteredLevels.length === 0 && (
                      <p className="text-xs text-slate-500 dark:text-slate-400">{t.graphSidebarNoResults}</p>
                    )}
                  </nav>
                </div>
              </aside>
            )}

            <div className="flex flex-1 flex-col items-center rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.02] p-6 sm:p-8">
              {levels.map((level, levelIndex) => (
                <div key={levelIndex} id={`capability-graph-level-${levelIndex}`} className="flex w-full scroll-mt-24 flex-col items-center">
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
