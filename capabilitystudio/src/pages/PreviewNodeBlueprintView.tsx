import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import DOMPurify from 'dompurify';
import { Lightbulb, BookOpen, RotateCcw, Hammer, ClipboardCheck, Check, Sparkles } from 'lucide-react';
import { apiImageUrl } from '../lib/api/httpClient';
import { getNodeBlueprint, editNodeBlueprintStep, type ExperienceStepType, type NodeBlueprintDto, type BlueprintStepDto } from '../lib/api/runtimeApi';
import LoadingSpinner from '../components/LoadingSpinner';
import { withPreviewMode, type PreviewMode } from '../lib/previewMode';

const STEP_ORDER: ExperienceStepType[] = ['Hypothesis', 'Teaching', 'Recall', 'Production', 'Assessment'];

const STEP_ICONS: Record<ExperienceStepType, typeof Lightbulb> = {
  Hypothesis: Lightbulb,
  Teaching: BookOpen,
  Recall: RotateCcw,
  Production: Hammer,
  Assessment: ClipboardCheck,
};

const STEP_LABELS: Record<ExperienceStepType, string> = {
  Hypothesis: 'Hipótesis',
  Teaching: 'Enseñanza',
  Recall: 'Recordar',
  Production: 'Aplícalo',
  Assessment: 'Evaluación',
};

/**
 * "Demo"/"Edición" preview modes (2026-07-21) — renders a node's Memory
 * Paradox blueprint DIRECTLY (all 5 steps at once, via
 * GetNodeBlueprintFunction), with a fully clickable stepper (no
 * Locked/sequential gating at all) so a reviewer can jump freely between
 * Hypothesis/Teaching/Recall/Production/Assessment to inspect what the AI
 * generated. In "Edición" mode, also shows a free-text instruction box
 * next to the step's illustration that calls EditNodeBlueprintStepFunction
 * to rewrite that step's content (and, if warranted, its illustration) via
 * BlueprintStepEditorAgent. Completely independent of any LearningSession
 * — never affects a real student's "Real" mode progress.
 */
export default function PreviewNodeBlueprintView({ mode }: { mode: PreviewMode }) {
  const { capabilityId, nodeId } = useParams<{ capabilityId: string; nodeId: string }>();

  const [phase, setPhase] = useState<'loading' | 'ready' | 'error'>('loading');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [blueprint, setBlueprint] = useState<NodeBlueprintDto | null>(null);
  const [selectedStep, setSelectedStep] = useState<ExperienceStepType>('Hypothesis');

  const [instruction, setInstruction] = useState('');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [savedNotice, setSavedNotice] = useState(false);

  useEffect(() => {
    if (!nodeId) return;
    let cancelled = false;

    (async () => {
      setPhase('loading');
      setErrorMessage(null);
      try {
        const data = await getNodeBlueprint(nodeId);
        if (cancelled) return;
        setBlueprint(data);
        setSelectedStep(data.Steps[0]?.StepType ?? 'Hypothesis');
        setPhase('ready');
      } catch {
        if (!cancelled) {
          setErrorMessage('No se pudo cargar el blueprint de este nodo.');
          setPhase('error');
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [nodeId]);

  const backLink = capabilityId ? withPreviewMode(`/capabilities/${capabilityId}/preview`, mode) : '/';
  const currentStep: BlueprintStepDto | undefined = blueprint?.Steps.find((s) => s.StepType === selectedStep);

  const handleApplyEdit = async () => {
    if (!nodeId || !instruction.trim() || saving) return;
    setSaving(true);
    setSaveError(null);
    setSavedNotice(false);
    try {
      const updated = await editNodeBlueprintStep(nodeId, selectedStep, instruction.trim());
      setBlueprint((prev) => (prev ? { ...prev, Steps: prev.Steps.map((s) => (s.StepType === selectedStep ? updated : s)) } : prev));
      setInstruction('');
      setSavedNotice(true);
    } catch {
      setSaveError('Ocurrió un error al aplicar la edición. Intenta de nuevo.');
    } finally {
      setSaving(false);
    }
  };

  if (phase === 'loading') {
    return <LoadingSpinner label="Cargando blueprint..." />;
  }

  if (phase === 'error' || !blueprint) {
    return (
      <div className="mx-auto max-w-2xl px-6 py-16 text-center">
        <p className="text-red-300">{errorMessage ?? 'No se pudo cargar el nodo.'}</p>
        <Link to={backLink} className="mt-4 inline-block text-sm text-brand-400 hover:text-brand-300">
          ← Volver al mapa
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <Link to={backLink} className="text-sm text-slate-400 hover:text-white">
        ← Volver al mapa
      </Link>

      <div className="mt-4 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
        <div className="flex flex-wrap items-center gap-2">
          <span className="rounded-full bg-brand-500 px-3 py-1 text-xs font-semibold text-[#fff]">
            {mode === 'demo' ? 'Modo Demo' : 'Modo Edición'}
          </span>
          <span className="text-xs text-slate-400">
            {mode === 'demo'
              ? 'Todos los pasos están desbloqueados para revisión — no afecta el progreso real.'
              : 'Edita el contenido de cada paso vía un prompt antes de aprobarlo.'}
          </span>
        </div>
      </div>

      {/* Fully clickable stepper — no locks, jump to any of the 5 steps. */}
      <div className="mt-4 flex items-center">
        {STEP_ORDER.map((stepType, index) => {
          const Icon = STEP_ICONS[stepType];
          const isActive = stepType === selectedStep;
          const hasContent = blueprint.Steps.some((s) => s.StepType === stepType);
          return (
            <div key={stepType} className="flex flex-1 items-center">
              <button
                onClick={() => hasContent && setSelectedStep(stepType)}
                disabled={!hasContent}
                className="flex flex-col items-center gap-1 disabled:cursor-not-allowed disabled:opacity-40"
              >
                <div
                  className={`flex h-9 w-9 items-center justify-center rounded-full border-2 ${
                    isActive
                      ? 'border-transparent bg-gradient-to-br from-brand-500 to-accent-500 text-[#fff]'
                      : 'border-white/10 bg-white/[0.03] text-slate-400 hover:border-brand-400/50'
                  }`}
                >
                  <Icon className="h-4 w-4" />
                </div>
                <span className={`text-[11px] font-medium ${isActive ? 'text-white' : 'text-slate-500'}`}>{STEP_LABELS[stepType]}</span>
              </button>
              {index < STEP_ORDER.length - 1 && <div className="mx-1 h-0.5 flex-1 bg-white/10" />}
            </div>
          );
        })}
      </div>

      <div className="mt-6 rounded-2xl border border-white/10 bg-white/[0.03] p-6">
        {currentStep ? (
          <>
            {selectedStep === 'Assessment' && (
              <p className="mb-3 rounded-lg border border-amber-400/20 bg-amber-500/[0.06] px-3 py-2 text-xs text-amber-200">
                Esto son los <strong>criterios internos de evaluación</strong> (la rúbrica que la IA usa para calificar),
                no una pregunta ni texto que el estudiante vea tal cual. En la sesión real, las preguntas de Evaluación se
                generan dinámicamente a partir de estos criterios (una distinta cada vez) — editar este texto cambia con
                qué se califica, no lo que se le muestra literalmente al estudiante.
              </p>
            )}
            <div
              className="prose prose-invert prose-sm max-w-none whitespace-pre-wrap text-slate-200"
              dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(currentStep.Content, { ALLOWED_TAGS: ['p', 'br', 'strong', 'em', 'ul', 'ol', 'li', 'a'] }) }}
            />

            {currentStep.Illustrations.map((illustration) => (
              <div key={illustration.IllustrationId} className="mt-4 overflow-hidden rounded-xl border border-white/10 bg-white/[0.02]">
                <img
                  src={apiImageUrl(`/illustrations/${illustration.IllustrationId}/image`)}
                  alt={illustration.Caption ?? ''}
                  className="h-auto w-full max-w-sm object-contain"
                />
              </div>
            ))}

            {mode === 'edit' && (
              <div className="mt-6 rounded-xl border border-brand-400/20 bg-brand-500/[0.05] p-4">
                <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-brand-300">
                  <Sparkles className="h-3.5 w-3.5" /> Editar este paso con IA
                </p>
                <textarea
                  value={instruction}
                  onChange={(e) => setInstruction(e.target.value)}
                  rows={3}
                  placeholder='Ej: "hazlo más simple", "cambia el ejemplo a animales", "agrega una ilustración"'
                  className="mt-2 w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white placeholder:text-slate-500 focus:border-brand-400 focus:outline-none"
                />
                {saveError && <p className="mt-2 text-sm text-red-300">{saveError}</p>}
                {savedNotice && !saveError && (
                  <p className="mt-2 flex items-center gap-1 text-sm text-emerald-300">
                    <Check className="h-4 w-4" /> Cambios aplicados.
                  </p>
                )}
                <button
                  onClick={handleApplyEdit}
                  disabled={saving || !instruction.trim()}
                  className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 disabled:opacity-50"
                >
                  {saving ? 'Aplicando...' : 'Aplicar cambio'}
                </button>
              </div>
            )}
          </>
        ) : (
          <p className="text-sm text-slate-400">Este nodo aún no tiene un blueprint generado.</p>
        )}
      </div>
    </div>
  );
}
