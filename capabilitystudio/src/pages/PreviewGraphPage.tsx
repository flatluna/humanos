import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Lock, PlayCircle, Star, ChevronDown, Plus, Pencil, Check, X, Trash2, Menu, ListChecks, Search } from 'lucide-react';
import {
  getCapabilityGraph,
  setCapabilityGraphLevelName,
  deleteCapabilityGraphNode,
  PREVIEW_PERSON_ID,
  type BackendCapabilityGraph,
  type BackendCapabilityGraphNode,
  type BackendCapabilityGraphEdge,
  type CapabilityGraphNodeState,
} from '../lib/api/runtimeApi';
import LoadingSpinner from '../components/LoadingSpinner';
import PreviewModeSwitcher from '../components/PreviewModeSwitcher';
import AddGraphNodeModal from '../components/AddGraphNodeModal';
import DeleteGraphNodeModal from '../components/DeleteGraphNodeModal';
import { usePreviewMode, withPreviewMode } from '../lib/previewMode';

const STATE_STYLES: Record<
  CapabilityGraphNodeState,
  { ring: string; bubble: string; icon: string; label: string; badge: string; Icon: typeof Lock }
> = {
  Locked: {
    ring: 'border-white/10',
    bubble: 'bg-white/[0.03]',
    icon: 'text-slate-500',
    label: 'text-slate-500',
    badge: 'bg-slate-500/15 text-slate-500',
    Icon: Lock,
  },
  Available: {
    ring: 'border-transparent',
    bubble: 'bg-gradient-to-br from-brand-500 to-accent-500 shadow-lg shadow-brand-500/30',
    icon: 'text-[#fff]',
    label: 'text-white font-semibold',
    badge: 'bg-brand-500 text-[#fff]',
    Icon: PlayCircle,
  },
  Mastered: {
    ring: 'border-amber-300/50',
    bubble: 'bg-amber-400/10',
    icon: 'text-amber-300',
    label: 'text-amber-100',
    badge: 'bg-amber-500 text-[#fff]',
    Icon: Star,
  },
};

const getStateLabels = (t: (key: string) => string): Record<CapabilityGraphNodeState, string> => ({
  Locked: t('previewGraph.stateLocked'),
  Available: t('previewGraph.stateAvailable'),
  Mastered: t('previewGraph.stateMastered'),
});

// Cycled per level in the sidebar so a long course reads like a colorful
// chaptered table of contents instead of a wall of uniform gray text.
const LEVEL_ACCENTS = [
  { text: 'text-brand-300', dot: 'bg-brand-400', border: 'border-brand-400/50' },
  { text: 'text-sky-300', dot: 'bg-sky-400', border: 'border-sky-400/50' },
  { text: 'text-emerald-300', dot: 'bg-emerald-400', border: 'border-emerald-400/50' },
  { text: 'text-amber-300', dot: 'bg-amber-400', border: 'border-amber-400/50' },
  { text: 'text-fuchsia-300', dot: 'bg-fuchsia-400', border: 'border-fuchsia-400/50' },
  { text: 'text-rose-300', dot: 'bg-rose-400', border: 'border-rose-400/50' },
];

/**
 * Groups nodes into "climb" levels — much friendlier for a kid, since it
 * visually reads as a pyramid.
 *
 * Preferred grouping (2026-08-04): by each node's own ChapterIndex — the
 * source document's chapter/module structure, e.g. "1.1 Propósito de la
 * Política" and "1.2 Importancia del Voluntariado" both sit in ONE row
 * for chapter "1. Introducción", exactly as the document/professor
 * planned it, regardless of any prerequisite edge between them.
 *
 * Falls back to the OLDER edge-depth tiering (a node's tier is 1 + the
 * deepest tier among its prerequisites) only when no node in the graph has
 * a ChapterIndex at all — i.e. capabilities created before that field
 * existed. That older approach could put two sibling nodes from the SAME
 * chapter into different rows, both confusingly auto-named after that one
 * shared chapter — this is exactly what chapter-based grouping fixes.
 */
function groupIntoLevels(
  nodes: BackendCapabilityGraphNode[],
  edges: BackendCapabilityGraphEdge[]
): BackendCapabilityGraphNode[][] {
  const hasChapterIndex = nodes.some((n) => n.ChapterIndex !== null && n.ChapterIndex !== undefined);
  if (hasChapterIndex) {
    return groupByChapterIndex(nodes);
  }
  return groupByPrerequisiteTier(nodes, edges);
}

function groupByChapterIndex(nodes: BackendCapabilityGraphNode[]): BackendCapabilityGraphNode[][] {
  const byChapter = new Map<number, BackendCapabilityGraphNode[]>();
  const unassigned: BackendCapabilityGraphNode[] = [];

  nodes.forEach((n) => {
    if (n.ChapterIndex === null || n.ChapterIndex === undefined) {
      unassigned.push(n);
      return;
    }
    const group = byChapter.get(n.ChapterIndex) ?? [];
    group.push(n);
    byChapter.set(n.ChapterIndex, group);
  });

  const levels = Array.from(byChapter.keys())
    .sort((a, b) => a - b)
    .map((chapterIndex) => {
      const group = byChapter.get(chapterIndex)!;
      group.sort((a, b) => a.SortOrder - b.SortOrder);
      return group;
    });

  if (unassigned.length > 0) {
    unassigned.sort((a, b) => a.SortOrder - b.SortOrder);
    levels.push(unassigned);
  }

  return levels;
}

function groupByPrerequisiteTier(
  nodes: BackendCapabilityGraphNode[],
  edges: BackendCapabilityGraphEdge[]
): BackendCapabilityGraphNode[][] {
  const prerequisitesOf = new Map<string, string[]>();
  nodes.forEach((n) => prerequisitesOf.set(n.CapabilityGraphNodeId, []));
  edges.forEach((e) => prerequisitesOf.get(e.SourceNodeId)?.push(e.TargetNodeId));

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
  nodes.forEach((n) => tierOf(n.CapabilityGraphNodeId, new Set()));

  const maxTier = Math.max(0, ...Array.from(tierCache.values()));
  const levels: BackendCapabilityGraphNode[][] = Array.from({ length: maxTier + 1 }, () => []);
  nodes.forEach((n) => levels[tierCache.get(n.CapabilityGraphNodeId) ?? 0].push(n));
  levels.forEach((level) => level.sort((a, b) => a.SortOrder - b.SortOrder));
  return levels;
}

export default function PreviewGraphPage() {
  const { t } = useTranslation();
  const { capabilityId } = useParams<{ capabilityId: string }>();
  const navigate = useNavigate();
  const [mode, setMode] = usePreviewMode();

  const [graph, setGraph] = useState<BackendCapabilityGraph | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [addNodeLevel, setAddNodeLevel] = useState<number | null>(null);
  const [deletingNode, setDeletingNode] = useState<BackendCapabilityGraphNode | null>(null);
  // Course-contents sidebar (2026-08-05) — a Coursera-style index alongside
  // the existing "climb" map, purely a navigation aid: it never changes
  // Locked/Available/Mastered gating, it just lets a student jump straight
  // to any already-unlocked node and see overall progress at a glance
  // instead of scrolling through the whole map. Collapsed by default on
  // mobile (toggled via this state); always visible at the `lg` breakpoint.
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarQuery, setSidebarQuery] = useState('');

  // Inline editing of a level's professor-given display name (2026-07-30) —
  // see setCapabilityGraphLevelName. `editingLevelIndex` is the level
  // currently being renamed (null when nothing is being edited).
  const [editingLevelIndex, setEditingLevelIndex] = useState<number | null>(null);
  const [editingLevelName, setEditingLevelName] = useState('');
  const [savingLevelName, setSavingLevelName] = useState(false);

  const loadGraph = useCallback(() => {
    if (!capabilityId) return;
    setLoading(true);
    setError(null);

    getCapabilityGraph(capabilityId, PREVIEW_PERSON_ID)
      .then((data) => setGraph(data))
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }, [capabilityId]);

  useEffect(() => {
    loadGraph();
  }, [loadGraph]);

  const levels = useMemo(() => (graph ? groupIntoLevels(graph.Nodes, graph.Edges) : []), [graph]);

  const levelNameByIndex = useMemo(() => {
    const map = new Map<number, string>();
    graph?.Levels?.forEach((l) => map.set(l.LevelIndex, l.Name));
    return map;
  }, [graph]);

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
        const levelName = levelNameByIndex.get(levelIndex) ?? t('previewGraph.level', { n: levelIndex + 1 });
        const levelMatches = normalizeForSearch(levelName).includes(query);
        const matchingNodes = levelMatches ? level : level.filter((node) => normalizeForSearch(node.Name).includes(query));
        return { levelIndex, level: matchingNodes };
      })
      .filter(({ level }) => level.length > 0);
  }, [levels, levelNameByIndex, sidebarQuery, normalizeForSearch, t]);

  const startEditingLevelName = useCallback((levelIndex: number) => {
    setEditingLevelIndex(levelIndex);
    setEditingLevelName(levelNameByIndex.get(levelIndex) ?? '');
  }, [levelNameByIndex]);

  const cancelEditingLevelName = useCallback(() => {
    setEditingLevelIndex(null);
    setEditingLevelName('');
  }, []);

  const saveLevelName = useCallback(async (levelIndex: number) => {
    if (!capabilityId) return;
    setSavingLevelName(true);
    try {
      const result = await setCapabilityGraphLevelName(capabilityId, levelIndex, editingLevelName);
      setGraph((prev) => {
        if (!prev) return prev;
        const otherLevels = (prev.Levels ?? []).filter((l) => l.LevelIndex !== levelIndex);
        const nextLevels = result.Name ? [...otherLevels, { LevelIndex: levelIndex, Name: result.Name }] : otherLevels;
        return { ...prev, Levels: nextLevels };
      });
      setEditingLevelIndex(null);
      setEditingLevelName('');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSavingLevelName(false);
    }
  }, [capabilityId, editingLevelName]);

  const nextUpNodeId = useMemo(() => {
    for (const level of levels) {
      const found = level.find((n) => n.State === 'Available');
      if (found) return found.CapabilityGraphNodeId;
    }
    return null;
  }, [levels]);

  // Both Demo and Edit modes are reviewer-facing (not a real student's
  // progression) — no node should ever look or behave as locked in either.
  // Shared by both the map bubbles AND the new sidebar index below, so the
  // two navigation surfaces can never disagree about what's clickable.
  const nodeEffectiveState = useCallback(
    (node: BackendCapabilityGraphNode): CapabilityGraphNodeState =>
      (mode === 'edit' || mode === 'demo') && node.State === 'Locked' ? 'Available' : node.State,
    [mode]
  );
  const isNodeClickable = useCallback(
    (node: BackendCapabilityGraphNode) => node.State !== 'Locked' || mode === 'demo' || mode === 'edit',
    [mode]
  );
  const goToNode = useCallback(
    (node: BackendCapabilityGraphNode) => {
      navigate(withPreviewMode(`/capabilities/${capabilityId}/preview/nodes/${node.CapabilityGraphNodeId}`, mode));
    },
    [navigate, capabilityId, mode]
  );

  const progress = useMemo(() => {
    const total = graph?.Nodes.length ?? 0;
    const mastered = graph?.Nodes.filter((n) => n.State === 'Mastered').length ?? 0;
    return { total, mastered, percent: total > 0 ? Math.round((mastered / total) * 100) : 0 };
  }, [graph]);

  const scrollToLevel = useCallback((levelIndex: number) => {
    document.getElementById(`preview-graph-level-${levelIndex}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, []);

  if (loading) return <LoadingSpinner label={t('previewGraph.loading')} />;

  if (error || !graph) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16 text-center">
        <p className="text-red-300">{error ?? t('previewGraph.notFound')}</p>
        <Link
          to={`/capabilities/${capabilityId}`}
          className="mt-4 inline-flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300"
        >
          <ArrowLeft className="h-4 w-4" /> {t('previewGraph.back')}
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 xl:max-w-7xl 2xl:max-w-[96rem]">
      <Link
        to={`/capabilities/${capabilityId}`}
        className="inline-flex items-center gap-1.5 text-sm text-slate-400 hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {t('previewGraph.backToCapability')}
      </Link>

      <div className="mt-4 text-center">
        <p className="text-xs font-medium uppercase tracking-wide text-brand-400">{t('previewGraph.studentPreviewLabel')}</p>
        <h1 className="mt-1 text-2xl font-bold text-white">{graph.Name}</h1>
        {graph.Description && <p className="mx-auto mt-2 max-w-2xl text-sm text-slate-400">{graph.Description}</p>}
        <p className="mt-1 text-xs text-slate-500">{t('previewGraph.climbHint')}</p>
        <div className="mt-4 flex justify-center">
          <PreviewModeSwitcher mode={mode} onChange={setMode} />
        </div>
        {mode === 'demo' && (
          <p className="mt-2 text-xs text-amber-300">{t('previewGraph.demoModeNotice')}</p>
        )}
      </div>

      {levels.length > 0 && (
        <button
          type="button"
          onClick={() => setSidebarOpen((open) => !open)}
          className="mx-auto mt-6 flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-4 py-2.5 text-sm font-medium text-slate-200 hover:bg-white/[0.06] lg:hidden"
        >
          {sidebarOpen ? <X className="h-4 w-4" /> : <Menu className="h-4 w-4" />}
          {sidebarOpen ? t('previewGraph.sidebarToggleHide') : t('previewGraph.sidebarToggleShow')}
        </button>
      )}

      <div className="mt-8 flex flex-col gap-8 lg:flex-row lg:items-start lg:gap-10">
        {levels.length > 0 && (
          <aside className={`${sidebarOpen ? 'block' : 'hidden'} w-full flex-none lg:sticky lg:top-6 lg:block lg:w-72 xl:w-80 2xl:w-96`}>
            <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4 lg:flex lg:max-h-[calc(100vh-3rem)] lg:flex-col">
              <div className="flex items-center gap-2 text-sm font-semibold text-white">
                <ListChecks className="h-4 w-4 text-brand-400" /> {t('previewGraph.sidebarTitle')}
              </div>
              <div className="mt-3">
                <div className="h-1.5 w-full overflow-hidden rounded-full bg-white/10">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-brand-500 to-accent-500 transition-all"
                    style={{ width: `${progress.percent}%` }}
                  />
                </div>
                <p className="mt-1.5 text-xs text-slate-400">
                  {t('previewGraph.sidebarProgress', { done: progress.mastered, total: progress.total })}
                </p>
              </div>

              <div className="relative mt-3">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-500" />
                <input
                  type="text"
                  value={sidebarQuery}
                  onChange={(e) => setSidebarQuery(e.target.value)}
                  placeholder={t('previewGraph.sidebarSearchPlaceholder')}
                  className="w-full rounded-lg border border-white/10 bg-white/[0.04] py-1.5 pl-8 pr-7 text-xs text-white placeholder:text-slate-500 focus:border-brand-400/60 focus:outline-none"
                />
                {sidebarQuery && (
                  <button
                    type="button"
                    onClick={() => setSidebarQuery('')}
                    className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-500 hover:text-white"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                )}
              </div>

              <nav className="mt-3 space-y-3 overflow-y-auto pr-1 lg:min-h-0 lg:flex-1">
                {filteredLevels.length === 0 && (
                  <p className="px-1 text-xs text-slate-500">{t('previewGraph.sidebarNoResults')}</p>
                )}
                {filteredLevels.map(({ levelIndex, level }) => {
                  const accent = LEVEL_ACCENTS[levelIndex % LEVEL_ACCENTS.length];
                  return (
                    <div key={levelIndex} className={`rounded-xl border bg-white/[0.02] p-2.5 ${accent.border}`}>
                      <button
                        type="button"
                        onClick={() => scrollToLevel(levelIndex)}
                        className={`flex w-full items-center gap-1.5 truncate text-left text-[11px] font-semibold uppercase tracking-wide hover:brightness-125 ${accent.text}`}
                      >
                        <span className={`h-1.5 w-1.5 flex-none rounded-full ${accent.dot}`} />
                        <span className="truncate">{levelNameByIndex.get(levelIndex) ?? t('previewGraph.level', { n: levelIndex + 1 })}</span>
                      </button>
                      <ul className="mt-1.5 space-y-0.5">
                        {level.map((node) => {
                          const effectiveState = nodeEffectiveState(node);
                          const clickable = isNodeClickable(node);
                          const Icon = STATE_STYLES[effectiveState].Icon;
                          const isNextUp = node.CapabilityGraphNodeId === nextUpNodeId;
                          return (
                            <li key={node.CapabilityGraphNodeId}>
                              <button
                                type="button"
                                disabled={!clickable}
                                onClick={() => goToNode(node)}
                                className={`flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-xs transition ${
                                  clickable ? 'cursor-pointer hover:bg-white/[0.06]' : 'cursor-not-allowed opacity-50'
                                } ${isNextUp ? 'bg-brand-500/10 ring-1 ring-inset ring-brand-400/40' : ''}`}
                              >
                                <Icon
                                  className={`h-3.5 w-3.5 flex-none ${STATE_STYLES[effectiveState].icon}`}
                                  fill={effectiveState === 'Mastered' ? 'currentColor' : 'none'}
                                />
                                <span className={`line-clamp-2 flex-1 ${effectiveState === 'Locked' ? 'text-slate-500' : 'text-slate-200'}`}>
                                  {node.Name}
                                </span>
                                {isNextUp && (
                                  <span className="flex-none rounded-full bg-brand-500 px-1.5 py-0.5 text-[9px] font-semibold uppercase text-[#fff]">
                                    {t('previewGraph.sidebarNextBadge')}
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
              </nav>
            </div>
          </aside>
        )}

        <div className="flex flex-1 flex-col items-center rounded-2xl border border-white/10 bg-white/[0.02] p-6 sm:p-8">
        {levels.map((level, levelIndex) => (
          <div key={levelIndex} id={`preview-graph-level-${levelIndex}`} className="flex w-full scroll-mt-6 flex-col items-center">
            <div className="relative z-10 mb-6 flex items-center gap-2">
              {editingLevelIndex === levelIndex ? (
                <div className="flex items-center gap-1.5">
                  <input
                    type="text"
                    autoFocus
                    value={editingLevelName}
                    onChange={(e) => setEditingLevelName(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') void saveLevelName(levelIndex);
                      if (e.key === 'Escape') cancelEditingLevelName();
                    }}
                    placeholder={t('previewGraph.levelNamePlaceholder', { n: levelIndex + 1 })}
                    disabled={savingLevelName}
                    className="rounded-full border border-brand-400/50 bg-white/[0.05] px-3 py-1 text-[11px] font-medium text-slate-100 outline-none focus:border-brand-400"
                  />
                  <button
                    type="button"
                    onClick={() => void saveLevelName(levelIndex)}
                    disabled={savingLevelName}
                    title={t('previewGraph.saveLevelName')}
                    className="rounded-full p-1 text-emerald-400 hover:bg-emerald-400/10 disabled:opacity-50"
                  >
                    <Check className="h-3.5 w-3.5" />
                  </button>
                  <button
                    type="button"
                    onClick={cancelEditingLevelName}
                    disabled={savingLevelName}
                    title={t('common.cancel')}
                    className="rounded-full p-1 text-slate-400 hover:bg-white/10 disabled:opacity-50"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                </div>
              ) : (
                <button
                  type="button"
                  onClick={() => startEditingLevelName(levelIndex)}
                  title={t('previewGraph.editLevelName')}
                  className="group/level inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-white/[0.03] px-3 py-1 text-[11px] font-medium uppercase tracking-wide text-slate-400 hover:border-brand-400 hover:text-brand-300"
                >
                  {levelNameByIndex.get(levelIndex) ?? t('previewGraph.level', { n: levelIndex + 1 })}
                  <Pencil className="h-3 w-3 opacity-0 transition-opacity group-hover/level:opacity-70" />
                </button>
              )}
              <button
                type="button"
                onClick={() => setAddNodeLevel(levelIndex)}
                title={t('previewGraph.addNodeHere')}
                className="inline-flex items-center gap-1 rounded-full border border-dashed border-white/20 px-2 py-1 text-[11px] font-medium text-slate-400 hover:border-brand-400 hover:text-brand-300"
              >
                <Plus className="h-3 w-3" /> {t('previewGraph.addNode')}
              </button>
            </div>

            <div className="flex flex-wrap justify-center gap-x-6 gap-y-8">
              {level.map((node) => {
                // Both Demo and Edit modes are reviewer-facing (not a real
                // student's progression) — no node should ever look or
                // behave as locked in either. Only "Real" mode (this page
                // isn't rendered there) would show genuine Locked gating.
                const effectiveState = nodeEffectiveState(node);
                const style = STATE_STYLES[effectiveState];
                const Icon = style.Icon;
                const clickable = isNodeClickable(node);
                const isNextUp = node.CapabilityGraphNodeId === nextUpNodeId;

                return (
                  <div key={node.CapabilityGraphNodeId} className="group relative flex w-24 flex-col items-center gap-2 text-center">
                    {(mode === 'edit' || mode === 'demo') && (
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          setDeletingNode(node);
                        }}
                        title={t('previewGraph.deleteNode')}
                        className="absolute -top-2 -right-2 z-20 rounded-full border border-white/20 bg-slate-800 p-1.5 text-slate-200 opacity-70 shadow-md transition-opacity hover:border-red-500 hover:bg-red-500/20 hover:text-red-400 hover:opacity-100 group-hover:opacity-100"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    )}
                    <button
                      type="button"
                      disabled={!clickable}
                      onClick={() => goToNode(node)}
                      className={`flex w-24 flex-col items-center gap-2 text-center transition ${
                        clickable ? 'cursor-pointer' : 'cursor-not-allowed'
                      }`}
                    >
                      <div className="relative">
                        {isNextUp && (
                          <span className="pointer-events-none absolute -top-7 left-1/2 z-0 -translate-x-1/2 whitespace-nowrap rounded-full bg-brand-500 px-2 py-0.5 text-[10px] font-semibold text-[#fff] shadow-md">
                            {t('previewGraph.startHere')}
                          </span>
                        )}
                        {isNextUp && (
                          <span className="pointer-events-none absolute inset-0 -m-1.5 rounded-full border-2 border-brand-400 animate-ping" />
                        )}
                        <div
                          className={`relative flex h-16 w-16 items-center justify-center rounded-full border-2 sm:h-20 sm:w-20 ${style.ring} ${style.bubble} ${
                            clickable ? 'transition-transform group-hover:scale-110' : 'opacity-60'
                          }`}
                        >
                          <Icon className={`h-6 w-6 sm:h-7 sm:w-7 ${style.icon}`} fill={node.State === 'Mastered' ? 'currentColor' : 'none'} />
                        </div>
                      </div>
                      <p className={`line-clamp-2 text-xs leading-tight ${style.label}`}>{node.Name}</p>
                    </button>
                  </div>
                );
              })}
            </div>

            {levelIndex < levels.length - 1 && (
              <div className="my-4 flex w-full max-w-xs items-center gap-3">
                <div className="h-px flex-1 bg-amber-400/40" />
                <ChevronDown className="h-4 w-4 flex-none text-amber-400/70" />
                <div className="h-px flex-1 bg-amber-400/40" />
              </div>
            )}
          </div>
        ))}

        {levels.length > 0 && (
          <button
            type="button"
            onClick={() => setAddNodeLevel(levels.length)}
            className="mt-2 inline-flex items-center gap-1.5 rounded-full border border-dashed border-white/20 px-3 py-1.5 text-xs font-medium text-slate-400 hover:border-brand-400 hover:text-brand-300"
          >
            <Plus className="h-3.5 w-3.5" /> {t('previewGraph.addNewTopLevel')}
          </button>
        )}

        {levels.length === 0 && (
          <div className="w-full rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center">
            <p className="text-sm text-slate-400">{t('previewGraph.emptyGraph')}</p>
            <button
              type="button"
              onClick={() => setAddNodeLevel(0)}
              className="mt-4 inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25"
            >
              <Plus className="h-4 w-4" /> {t('previewGraph.addFirstNode')}
            </button>
          </div>
        )}
        </div>
      </div>

      {addNodeLevel !== null && graph && (
        <AddGraphNodeModal
          capabilityId={capabilityId!}
          levels={levels}
          targetLevelIndex={addNodeLevel}
          onClose={() => setAddNodeLevel(null)}
          onCreated={() => {
            setAddNodeLevel(null);
            loadGraph();
          }}
        />
      )}

      {deletingNode && capabilityId && (
        <DeleteGraphNodeModal
          nodeName={deletingNode.Name}
          onCancel={() => setDeletingNode(null)}
          onConfirm={async () => {
            await deleteCapabilityGraphNode(capabilityId, deletingNode.CapabilityGraphNodeId);
            setDeletingNode(null);
            loadGraph();
          }}
        />
      )}

      {levels.length > 0 && (
        <div className="mt-12 flex flex-wrap justify-center gap-3 border-t border-white/10 pt-6">
          {(Object.keys(getStateLabels(t)) as CapabilityGraphNodeState[]).map((state) => (
            <span key={state} className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-[11px] font-medium ${STATE_STYLES[state].badge}`}>
              {getStateLabels(t)[state]}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}
