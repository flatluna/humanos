import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Lock, PlayCircle, Star, ChevronDown } from 'lucide-react';
import {
  getCapabilityGraph,
  PREVIEW_PERSON_ID,
  type BackendCapabilityGraph,
  type BackendCapabilityGraphNode,
  type BackendCapabilityGraphEdge,
  type CapabilityGraphNodeState,
} from '../lib/api/runtimeApi';
import LoadingSpinner from '../components/LoadingSpinner';
import PreviewModeSwitcher from '../components/PreviewModeSwitcher';
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

const STATE_LABELS: Record<CapabilityGraphNodeState, string> = {
  Locked: 'Bloqueado',
  Available: 'Disponible',
  Mastered: '¡Dominado!',
};

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
  const { capabilityId } = useParams<{ capabilityId: string }>();
  const navigate = useNavigate();
  const [mode, setMode] = usePreviewMode();

  const [graph, setGraph] = useState<BackendCapabilityGraph | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!capabilityId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    getCapabilityGraph(capabilityId, PREVIEW_PERSON_ID)
      .then((data) => {
        if (!cancelled) setGraph(data);
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [capabilityId]);

  const levels = useMemo(() => (graph ? groupIntoLevels(graph.Nodes, graph.Edges) : []), [graph]);

  const nextUpNodeId = useMemo(() => {
    for (const level of levels) {
      const found = level.find((n) => n.State === 'Available');
      if (found) return found.CapabilityGraphNodeId;
    }
    return null;
  }, [levels]);

  if (loading) return <LoadingSpinner label="Cargando experiencia del estudiante..." />;

  if (error || !graph) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16 text-center">
        <p className="text-red-300">{error ?? 'No se encontró el grafo de esta capability.'}</p>
        <Link
          to={`/capabilities/${capabilityId}`}
          className="mt-4 inline-flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300"
        >
          <ArrowLeft className="h-4 w-4" /> Volver
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
        <ArrowLeft className="h-4 w-4" /> Volver a la capability
      </Link>

      <div className="mt-4 text-center">
        <p className="text-xs font-medium uppercase tracking-wide text-brand-400">Vista previa del estudiante</p>
        <h1 className="mt-1 text-2xl font-bold text-white">{graph.Name}</h1>
        {graph.Description && <p className="mx-auto mt-2 max-w-2xl text-sm text-slate-400">{graph.Description}</p>}
        <p className="mt-1 text-xs text-slate-500">🏔️ Sube nivel por nivel hasta llegar a la cima</p>
        <div className="mt-4 flex justify-center">
          <PreviewModeSwitcher mode={mode} onChange={setMode} />
        </div>
        {mode === 'demo' && (
          <p className="mt-2 text-xs text-amber-300">Modo Demo: todos los nodos están desbloqueados para revisión.</p>
        )}
      </div>

      <div className="mt-10 flex flex-col items-center">
        {levels.map((level, levelIndex) => (
          <div key={levelIndex} className="flex w-full flex-col items-center">
            <span className="mb-4 rounded-full border border-white/10 bg-white/[0.03] px-3 py-1 text-[11px] font-medium uppercase tracking-wide text-slate-400">
              Nivel {levelIndex + 1}
            </span>

            <div className="flex flex-wrap justify-center gap-x-6 gap-y-8">
              {level.map((node) => {
                const style = STATE_STYLES[node.State];
                const Icon = style.Icon;
                const clickable = node.State !== 'Locked' || mode === 'demo';
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
                        <span className="absolute -top-7 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full bg-brand-500 px-2 py-0.5 text-[10px] font-semibold text-[#fff] shadow-md">
                          ¡Empieza aquí!
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
                        <Icon className={`h-6 w-6 sm:h-7 sm:w-7 ${style.icon}`} fill={node.State === 'Mastered' ? 'currentColor' : 'none'} />
                      </div>
                    </div>
                    <p className={`line-clamp-2 text-xs leading-tight ${style.label}`}>{node.Name}</p>
                  </button>
                );
              })}
            </div>

            {levelIndex < levels.length - 1 && (
              <ChevronDown className="my-3 h-5 w-5 flex-none text-slate-600" />
            )}
          </div>
        ))}

        {levels.length === 0 && (
          <p className="w-full rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center text-sm text-slate-400">
            Este grafo todavía no tiene nodos.
          </p>
        )}
      </div>

      {levels.length > 0 && (
        <div className="mt-12 flex flex-wrap justify-center gap-3 border-t border-white/10 pt-6">
          {(Object.keys(STATE_LABELS) as CapabilityGraphNodeState[]).map((state) => (
            <span key={state} className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-[11px] font-medium ${STATE_STYLES[state].badge}`}>
              {STATE_LABELS[state]}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}
