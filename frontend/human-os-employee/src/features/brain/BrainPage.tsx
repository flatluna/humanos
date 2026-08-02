import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { motion, AnimatePresence } from 'framer-motion';
import { Brain, Search, Sparkles, X, Bot, Layers, RotateCcw, AlertTriangle } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { getNodeSummary, type NodeSummaryDto } from '@/features/capabilities/api/runtimeSessionApi';
import EngramReviewAgent from '@/features/capabilities/components/EngramReviewAgent';
import { BrainRecallGrid } from './BrainRecallGrid';
import { MemoryScoreGauge } from './MemoryScoreGauge';
import { MemoryScienceExplainer } from './MemoryScienceExplainer';
import { getSubjectColor } from './subjectColor';
import { computeGlobalMemoryScore, getRetrievability, isAutomatic } from './memoryScience';
import { useBrainGraph } from './useBrainGraph';
import type { SelectedNeuron } from './types';

/** Below this retrievability, the panel nudges the employee that this memory
 * is fading and a full 5-step redo (not just a chat with the agent) will
 * rebuild it more strongly — mirrors the reviewCount-driven stability model
 * in memoryScience.ts. */
const FADING_THRESHOLD = 0.5;

function SummaryStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl bg-white/5 p-3">
      <p className="text-[11px] uppercase tracking-wide text-white/50">{label}</p>
      <p className="mt-0.5 text-sm font-semibold text-white">{value}</p>
    </div>
  );
}

/** "Mi Cerebro" — employee portal's mastered-engrams dashboard, ported from
 * human-os-web's /brain (2026-08-01). Every mastered CapabilityGraphNode
 * across the employee's assigned capabilities is shown as a card with a
 * live "memory strength" ring driven by a real forgetting-curve algorithm
 * (memoryScience.ts) — the fading-memory feature the employee portal was
 * missing. Trimmed vs. the original: no force-directed graph view toggle
 * (grid view only, which is the default there too) and no Programs
 * grouping (this portal has no Programs concept — every root is one
 * assigned capability). */
export function BrainPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const { data: rootGroups = [], isLoading: loading } = useBrainGraph(personId);
  const [query, setQuery] = useState('');

  const [selectedNode, setSelectedNode] = useState<SelectedNeuron | null>(null);
  const [summary, setSummary] = useState<NodeSummaryDto | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [engramOpen, setEngramOpen] = useState(false);
  const [subjectFilter, setSubjectFilter] = useState<string | null>(null);

  const totalNeurons = useMemo(
    () => rootGroups.reduce((sum, r) => sum + r.clusters.reduce((s, c) => s + c.nodes.length, 0), 0),
    [rootGroups],
  );
  const totalAreas = rootGroups.length;
  const memoryScore = useMemo(() => computeGlobalMemoryScore(rootGroups), [rootGroups]);
  const subjectOptions = useMemo(() => {
    const bySubject = new Map<string, string>();
    rootGroups.forEach((r) =>
      r.clusters.forEach((c) => {
        if (c.subjectCode && c.subjectName) {
          bySubject.set(c.subjectCode, c.subjectName);
        }
      }),
    );
    return Array.from(bySubject.entries())
      .map(([code, name]) => ({ code, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [rootGroups]);

  const filteredRootGroups = useMemo(() => {
    if (!subjectFilter) return rootGroups;
    return rootGroups
      .map((r) => ({ ...r, clusters: r.clusters.filter((c) => c.subjectCode === subjectFilter) }))
      .filter((r) => r.clusters.length > 0);
  }, [rootGroups, subjectFilter]);

  const totalNeedsReview = useMemo(() => {
    return rootGroups.reduce(
      (sum, r) =>
        sum +
        r.clusters.reduce((s, c) => {
          return (
            s +
            c.nodes.filter((n) => {
              if (isAutomatic(n.reviewCount ?? 0)) return false; // near-permanent, never "needs review"
              const retrievability = getRetrievability(n.lastCompletedDate, n.reviewCount ?? 1);
              return retrievability === null || retrievability < FADING_THRESHOLD;
            }).length
          );
        }, 0),
      0,
    );
  }, [rootGroups]);

  function handleSelectNode(node: SelectedNeuron) {
    setSelectedNode(node);
    setSummary(null);
    setSummaryLoading(true);
    getNodeSummary(personId, node.capabilityGraphNodeId)
      .then((data) => setSummary(data))
      .catch(() => setSummary(null))
      .finally(() => setSummaryLoading(false));
  }

  const selectedRetrievability = selectedNode
    ? getRetrievability(selectedNode.lastCompletedDate, selectedNode.reviewCount ?? 1)
    : null;
  const selectedIsFading = selectedRetrievability !== null && selectedRetrievability < FADING_THRESHOLD;

  const isEmpty = !loading && totalNeurons === 0;

  return (
    <div className="mx-auto max-w-6xl p-4 sm:p-8">
      <div className="flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-accent-500 to-brand-600 text-white shadow-lg shadow-accent-500/30">
          <Brain className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">Mi Cerebro</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Lo que ya dominas de tus cursos asignados — y qué se está debilitando.
          </p>
        </div>
      </div>

      <MemoryScienceExplainer />

      {!isEmpty && (
        <div className="mt-6 flex flex-wrap items-stretch gap-3">
          <div className="grid grid-cols-3 gap-3 sm:max-w-lg">
            <Card className="p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Engramas</p>
              <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">{totalNeurons}</p>
            </Card>
            <Card className="p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Cursos</p>
              <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">{totalAreas}</p>
            </Card>
            <Card className="p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Por repasar</p>
              <p className="mt-1 text-2xl font-bold text-amber-500 dark:text-amber-400">{totalNeedsReview}</p>
            </Card>
          </div>
          <MemoryScoreGauge
            score={memoryScore}
            label="Fuerza de memoria global"
            caption="Qué tan fresco está tu conocimiento dominado ahora mismo, según la curva real del olvido."
          />
        </div>
      )}

      {!isEmpty && (
        <Card className="mt-4 flex items-start gap-3 border-dashed p-4">
          <Bot className="mt-0.5 h-5 w-5 shrink-0 text-slate-400" />
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Próximamente: pregúntale a tu agente sobre todo lo que ya dominas a la vez.
          </p>
        </Card>
      )}

      {loading && <LoadingBackdrop title="Cargando tu cerebro..." description="Reuniendo todo lo que ya dominas." />}

      {isEmpty && (
        <Card className="mt-8 flex flex-col items-center gap-3 p-10 text-center">
          <Brain className="h-10 w-10 text-slate-300 dark:text-slate-600" />
          <p className="text-base font-semibold text-slate-900 dark:text-white">Todavía no tienes engramas dominados.</p>
          <p className="max-w-sm text-sm text-slate-500 dark:text-slate-400">
            A medida que domines temas en tus cursos, aparecerán aquí como engramas que puedes repasar.
          </p>
          <Link
            to="/capabilities"
            className="mt-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-accent-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
          >
            Ver mis cursos
          </Link>
        </Card>
      )}

      {!loading && !isEmpty && (
        <>
          <div className="mt-6 flex flex-wrap items-center gap-3">
            <div className="relative max-w-md flex-1">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
              <input
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Buscar un engrama..."
                className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-9 pr-3 text-sm text-slate-900 shadow-sm outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/[0.03] dark:text-white"
              />
            </div>
          </div>

          <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
            Elige un engrama para ver el detalle o hablar con tu agente.
          </p>

          {subjectOptions.length > 1 && (
            <div className="mt-3 flex flex-wrap items-center gap-2">
              <span className="text-xs font-medium uppercase tracking-wide text-slate-400 dark:text-slate-500">
                Filtrar por área:
              </span>
              <button
                type="button"
                onClick={() => setSubjectFilter(null)}
                className={`rounded-full px-3 py-1 text-xs font-medium transition ${
                  subjectFilter === null
                    ? 'bg-gradient-to-r from-brand-500 to-accent-500 text-white shadow'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-white/[0.06] dark:text-slate-300 dark:hover:bg-white/[0.12]'
                }`}
              >
                Todas
              </button>
              {subjectOptions.map((s) => (
                <button
                  key={s.code}
                  type="button"
                  onClick={() => setSubjectFilter(subjectFilter === s.code ? null : s.code)}
                  className={`flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium transition ${
                    subjectFilter === s.code
                      ? 'text-white shadow'
                      : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-white/[0.06] dark:text-slate-300 dark:hover:bg-white/[0.12]'
                  }`}
                  style={subjectFilter === s.code ? { background: getSubjectColor(s.code) } : undefined}
                >
                  <span className="h-2 w-2 rounded-full" style={{ background: getSubjectColor(s.code) }} />
                  {s.name}
                </button>
              ))}
            </div>
          )}

          <div className="relative mt-4 h-[560px] w-full overflow-hidden rounded-3xl border border-white/10 bg-[radial-gradient(ellipse_at_center,_#1e1b4b_0%,_#0f172a_55%,_#020617_100%)] shadow-2xl sm:h-[680px]">
            <BrainRecallGrid rootGroups={filteredRootGroups} query={query} onSelectNeuron={handleSelectNode} />
          </div>
        </>
      )}

      <AnimatePresence>
        {selectedNode && (
          <>
            <motion.div
              key="backdrop"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="fixed inset-0 z-40 bg-black/40"
              onClick={() => setSelectedNode(null)}
            />
            <motion.div
              key="panel"
              initial={{ x: 400, opacity: 0 }}
              animate={{ x: 0, opacity: 1 }}
              exit={{ x: 400, opacity: 0 }}
              transition={{ type: 'tween', duration: 0.25 }}
              className="fixed right-0 top-0 z-50 h-full w-full overflow-y-auto bg-slate-900 p-6 shadow-2xl sm:w-96"
            >
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-fuchsia-300">
                  <Layers className="h-3.5 w-3.5" />
                  {selectedNode.capabilityName}
                </div>
                <button
                  type="button"
                  onClick={() => setSelectedNode(null)}
                  className="rounded-full p-1 text-white/60 hover:bg-white/10 hover:text-white"
                  aria-label="Cerrar panel"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>

              <h2 className="mt-2 text-xl font-semibold text-white">{selectedNode.name}</h2>
              {selectedNode.description && <p className="mt-2 text-sm text-white/70">{selectedNode.description}</p>}

              {selectedIsFading && (
                <div className="mt-4 flex items-start gap-2 rounded-xl border border-amber-400/30 bg-amber-400/10 p-3">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-400" />
                  <p className="text-xs text-amber-200">
                    Este recuerdo se está desvaneciendo. Platicar con el agente te ayuda a repasarlo, pero rehacer el
                    ciclo completo de 5 pasos lo reconstruye mucho más fuerte.
                  </p>
                </div>
              )}

              {summaryLoading && <p className="mt-6 text-sm text-white/50">Cargando resumen...</p>}

              {!summaryLoading && summary && (
                <div className="mt-6 grid grid-cols-2 gap-3">
                  <SummaryStat label="Intentos" value={String(summary.AttemptCount)} />
                  <SummaryStat
                    label="Puntaje"
                    value={typeof summary.FinalScore === 'number' ? `${summary.FinalScore}/100` : '—'}
                  />
                  <SummaryStat
                    label="Primera vez dominado"
                    value={
                      summary.FirstCompletedDate
                        ? new Date(summary.FirstCompletedDate).toLocaleDateString('es', { dateStyle: 'medium' })
                        : '—'
                    }
                  />
                  <SummaryStat
                    label="Último repaso"
                    value={
                      summary.LastCompletedDate
                        ? new Date(summary.LastCompletedDate).toLocaleDateString('es', { dateStyle: 'medium' })
                        : '—'
                    }
                  />
                </div>
              )}

              <button
                type="button"
                onClick={() => setEngramOpen(true)}
                className="mt-6 flex w-full items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-accent-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
              >
                <Sparkles className="h-4 w-4" />
                Hablar con tu agente
              </button>

              <Link
                to={`/capabilities/${selectedNode.capabilityId}/nodes/${selectedNode.capabilityGraphNodeId}?from=brain`}
                className="mt-2.5 flex w-full items-center justify-center gap-1.5 rounded-xl border border-white/15 bg-white/5 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-white/10"
              >
                <RotateCcw className="h-4 w-4" />
                Rehacer los 5 pasos
              </Link>
            </motion.div>
          </>
        )}
      </AnimatePresence>

      {engramOpen && selectedNode && (
        <EngramReviewAgent
          personId={personId}
          capabilityGraphNodeId={selectedNode.capabilityGraphNodeId}
          onExit={() => setEngramOpen(false)}
        />
      )}
    </div>
  );
}
