import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Lock, PlayCircle, Star, ChevronDown, Plus, Pencil, Check, X } from 'lucide-react';
import {
  getCapabilityGraph,
  setCapabilityGraphLevelName,
  PREVIEW_PERSON_ID,
  type BackendCapabilityGraph,
  type BackendCapabilityGraphNode,
  type BackendCapabilityGraphEdge,
  type CapabilityGraphNodeState,
} from '../lib/api/runtimeApi';
import LoadingSpinner from '../components/LoadingSpinner';
import PreviewModeSwitcher from '../components/PreviewModeSwitcher';
import AddGraphNodeModal from '../components/AddGraphNodeModal';
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

/**
 * Groups nodes into "climb" levels using the graph's prerequisite edges
 * (Requires/BuildsOn: Source depends on Target) instead of a flat list —
 * much friendlier for a kid, since it visually reads as a pyramid: the
 * foundational nodes with no prerequisites form the wide base (top of the
 * page, first to unlock) and each level narrows towards the few advanced
 * nodes at the summit, connected by a simple path instead of a bare list.
 */
function groupIntoLevels(
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
    <div className="mx-auto max-w-3xl px-6 py-10">
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

      <div className="mt-10 flex flex-col items-center">
        {levels.map((level, levelIndex) => (
          <div key={levelIndex} className="flex w-full flex-col items-center">
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
                const effectiveState = (mode === 'edit' || mode === 'demo') && node.State === 'Locked' ? 'Available' : node.State;
                const style = STATE_STYLES[effectiveState];
                const Icon = style.Icon;
                const clickable = node.State !== 'Locked' || mode === 'demo' || mode === 'edit';
                const isNextUp = node.CapabilityGraphNodeId === nextUpNodeId;

                return (
                  <button
                    key={node.CapabilityGraphNodeId}
                    type="button"
                    disabled={!clickable}
                    onClick={() =>
                      navigate(withPreviewMode(`/capabilities/${capabilityId}/preview/nodes/${node.CapabilityGraphNodeId}`, mode))
                    }
                    className={`group flex w-24 flex-col items-center gap-2 text-center transition ${
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
