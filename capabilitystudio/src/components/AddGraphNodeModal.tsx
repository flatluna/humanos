import { useEffect, useMemo, useRef, useState } from 'react';
import { X, Sparkles, Loader2, ChevronRight, CheckCircle2, Circle } from 'lucide-react';
import {
  addCapabilityGraphNode,
  type BackendCapabilityGraphNode,
} from '../lib/api/runtimeApi';

/** How long (ms) each simulated progress step stays on screen before
 * advancing. The last step stays put (spinning) until the real API response
 * arrives — a single HTTP call gives no true incremental backend progress. */
const STEP_DURATION_MS = 3200;

interface AddGraphNodeModalProps {
  capabilityId: string;
  /** Nodes grouped by computed level (same groupIntoLevels output as
   * PreviewGraphPage), used to build the "requiere" / "es requerido por"
   * checklists with sensible pre-selection. */
  levels: BackendCapabilityGraphNode[][];
  /** Level index (0-based) the professor clicked "+ Agregar nodo" on —
   * pre-checks every node in the level below as a prerequisite, so the new
   * node lands at this level by default. */
  targetLevelIndex: number;
  onClose: () => void;
  onCreated: () => void;
}

/**
 * Professional "add node" authoring modal for Capability Studio (professors/
 * universities). A capability's "Nivel" is never a stored column — it is the
 * node's depth in the Requires-edge DAG — so this UI lets the author pick
 * EXACTLY which existing nodes the new node requires (prerequisites, from
 * the level below) and, optionally, which existing nodes should now require
 * it (dependents, from the level above), which is what actually places the
 * new node at the intended level once saved.
 */
export default function AddGraphNodeModal({
  capabilityId,
  levels,
  targetLevelIndex,
  onClose,
  onCreated,
}: AddGraphNodeModalProps) {
  const belowLevel = targetLevelIndex > 0 ? levels[targetLevelIndex - 1] : [];
  const aboveLevel = targetLevelIndex < levels.length - 1 ? levels[targetLevelIndex + 1] : levels[targetLevelIndex] ?? [];

  const [prompt, setPrompt] = useState('');
  const [requiresIds, setRequiresIds] = useState<Set<string>>(new Set(belowLevel.map((n) => n.CapabilityGraphNodeId)));
  const [requiredByIds, setRequiredByIds] = useState<Set<string>>(new Set());
  const [generateIllustrations, setGenerateIllustrations] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [stepIndex, setStepIndex] = useState(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const steps = useMemo(() => {
    const base = [
      'Analizando el grafo existente y sus prerrequisitos',
      'Generando definición, ejemplos y aplicaciones con IA',
      'Verificando dependencias y evitando ciclos',
    ];
    if (generateIllustrations) base.push('Generando ilustraciones (Hipótesis + Enseñanza)');
    base.push('Guardando el nodo en la base de datos');
    return base;
  }, [generateIllustrations]);

  useEffect(() => {
    if (!submitting) {
      setStepIndex(0);
      if (timerRef.current) clearInterval(timerRef.current);
      return;
    }
    timerRef.current = setInterval(() => {
      setStepIndex((i) => Math.min(i + 1, steps.length - 1));
    }, STEP_DURATION_MS);
    return () => {
      if (timerRef.current) clearInterval(timerRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [submitting]);

  function toggle(set: Set<string>, setSet: (s: Set<string>) => void, id: string) {
    const next = new Set(set);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setSet(next);
  }

  async function handleSubmit() {
    if (!prompt.trim() || submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      await addCapabilityGraphNode(capabilityId, {
        Prompt: prompt.trim(),
        RequiresNodeIds: Array.from(requiresIds),
        RequiredByNodeIds: Array.from(requiredByIds),
        GenerateIllustrations: generateIllustrations,
      });
      onCreated();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el nodo.');
      setSubmitting(false);
    }
  }

  function handleClose() {
    if (submitting) return;
    onClose();
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 px-4 py-8" onClick={handleClose}>
      <div
        className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl border border-white/10 bg-slate-900 p-6 shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between">
          <div>
            <h2 className="flex items-center gap-2 text-lg font-semibold text-white">
              <Sparkles className="h-5 w-5 text-brand-400" />
              Agregar nodo — Nivel {targetLevelIndex + 1}
            </h2>
            <p className="mt-1 text-sm text-slate-400">
              Describe el nuevo concepto o skill. La IA generará su definición, ejemplos, aplicaciones e
              ilustraciones, igual que en la generación original del grafo.
            </p>
          </div>
          <button
            type="button"
            onClick={handleClose}
            disabled={submitting}
            className="rounded-lg p-1.5 text-slate-500 hover:bg-white/[0.06] hover:text-white disabled:cursor-not-allowed disabled:opacity-30"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {submitting ? (
          <ProgressView steps={steps} currentIndex={stepIndex} />
        ) : (
          <>
            <div className="mt-5">
              <label className="mb-1.5 block text-xs font-medium uppercase tracking-wide text-slate-400">
                ¿Qué debe enseñar este nodo?
              </label>
              <textarea
                value={prompt}
                onChange={(e) => setPrompt(e.target.value)}
                rows={4}
                placeholder='Ej: "Un nodo sobre fracciones equivalentes, con ejemplos usando pizzas y barras de chocolate"'
                className="input w-full resize-none"
              />
            </div>

            <div className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-2">
              <NodeChecklist
                title="Requiere (prerrequisitos)"
                hint="El estudiante debe dominar estos nodos ANTES de este nuevo nodo."
                nodes={belowLevel.length > 0 ? belowLevel : levels[targetLevelIndex] ?? []}
                selected={requiresIds}
                onToggle={(id) => toggle(requiresIds, setRequiresIds, id)}
              />
              <NodeChecklist
                title="Es requerido por (dependientes)"
                hint="Estos nodos existentes pasarán a depender del nuevo nodo (opcional)."
                nodes={aboveLevel}
                selected={requiredByIds}
                onToggle={(id) => toggle(requiredByIds, setRequiredByIds, id)}
              />
            </div>

            <label className="mt-5 flex items-center gap-2 text-sm text-slate-300">
              <input
                type="checkbox"
                checked={generateIllustrations}
                onChange={(e) => setGenerateIllustrations(e.target.checked)}
                className="h-4 w-4 rounded border-white/20 bg-white/[0.03] text-brand-500"
              />
              Generar ilustraciones automáticamente (Hipótesis + Enseñanza)
            </label>

            {error && (
              <p className="mt-4 rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-300">
                {error}
              </p>
            )}

            <div className="mt-6 flex items-center justify-end gap-3">
              <button
                type="button"
                onClick={handleClose}
                className="rounded-xl border border-white/10 px-4 py-2 text-sm font-medium text-slate-300 hover:bg-white/[0.06]"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={handleSubmit}
                disabled={!prompt.trim()}
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 disabled:cursor-not-allowed disabled:opacity-40"
              >
                Crear nodo <ChevronRight className="h-4 w-4" />
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function ProgressView({ steps, currentIndex }: { steps: string[]; currentIndex: number }) {
  return (
    <div className="mt-6">
      <div className="space-y-3">
        {steps.map((label, i) => {
          const isDone = i < currentIndex;
          const isCurrent = i === currentIndex;
          return (
            <div key={label} className="flex items-center gap-3">
              {isDone ? (
                <CheckCircle2 className="h-5 w-5 flex-none text-emerald-400" />
              ) : isCurrent ? (
                <Loader2 className="h-5 w-5 flex-none animate-spin text-brand-400" />
              ) : (
                <Circle className="h-5 w-5 flex-none text-slate-700" />
              )}
              <span className={`text-sm ${isDone ? 'text-slate-400 line-through' : isCurrent ? 'text-white' : 'text-slate-600'}`}>
                {label}
              </span>
            </div>
          );
        })}
      </div>
      <p className="mt-6 rounded-lg border border-amber-500/20 bg-amber-500/10 px-3 py-2 text-xs text-amber-300">
        No cierres esta ventana. Esto puede tardar 10–30 segundos, especialmente si se están generando
        ilustraciones.
      </p>
    </div>
  );
}

function NodeChecklist({
  title,
  hint,
  nodes,
  selected,
  onToggle,
}: {
  title: string;
  hint: string;
  nodes: BackendCapabilityGraphNode[];
  selected: Set<string>;
  onToggle: (id: string) => void;
}) {
  return (
    <div className="rounded-xl border border-white/10 bg-white/[0.02] p-3">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-300">{title}</p>
      <p className="mt-0.5 text-[11px] text-slate-500">{hint}</p>
      <div className="mt-2 max-h-40 space-y-1 overflow-y-auto">
        {nodes.length === 0 && <p className="py-2 text-xs text-slate-600">No hay nodos disponibles.</p>}
        {nodes.map((node) => (
          <label
            key={node.CapabilityGraphNodeId}
            className="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-sm text-slate-300 hover:bg-white/[0.05]"
          >
            <input
              type="checkbox"
              checked={selected.has(node.CapabilityGraphNodeId)}
              onChange={() => onToggle(node.CapabilityGraphNodeId)}
              className="h-3.5 w-3.5 rounded border-white/20 bg-white/[0.03] text-brand-500"
            />
            {node.Name}
          </label>
        ))}
      </div>
    </div>
  );
}
