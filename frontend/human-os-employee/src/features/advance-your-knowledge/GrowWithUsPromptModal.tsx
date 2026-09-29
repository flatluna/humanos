import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { Sparkles, X, Loader2, Search, FileText, Wand2 } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { useGrowWithUsPlanStore } from '@/store/useGrowWithUsPlanStore';
import { generateGrowWithUsPlan } from '@/features/advance-your-knowledge/api/growWithUsPlanApi';

/** Rotating progress steps shown while the backend works (web search +
 * job/résumé context gathering + LLM report writing) — this call can take
 * up to a minute, so a static spinner alone reads as "stuck". Cycles on a
 * timer purely for perceived-progress feedback; not tied to real backend
 * milestones (the HTTP call is a single request/response). */
const PROGRESS_STEPS_EN = [
  { icon: Search, text: 'Searching the web for current courses and news...' },
  { icon: FileText, text: 'Reading your role, résumé, and completed courses...' },
  { icon: Wand2, text: 'Writing your personalized growth plan...' },
];
const PROGRESS_STEPS_ES = [
  { icon: Search, text: 'Buscando en internet cursos y noticias actuales...' },
  { icon: FileText, text: 'Revisando tu puesto, currículum y cursos completados...' },
  { icon: Wand2, text: 'Escribiendo tu plan de crecimiento personalizado...' },
];

/**
 * "Grow With Us" prompt modal (2026-09-27) — opened from the main side
 * menu's "Grow With Us" item. Asks the learner what they want to focus on,
 * then calls GrowWithUsPlanAgent (via GenerateGrowWithUsPlan) and navigates
 * to /advance-your-knowledge to show the resulting plan.
 */
export function GrowWithUsPromptModal() {
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === 'en';
  const navigate = useNavigate();
  const { isModalOpen, closeModal, setGenerating, isGenerating, lastError, setPlan, setError, loadSavedSearches } = useGrowWithUsPlanStore();
  const [prompt, setPrompt] = useState('');
  const [stepIndex, setStepIndex] = useState(0);
  const steps = isEn ? PROGRESS_STEPS_EN : PROGRESS_STEPS_ES;

  useEffect(() => {
    if (!isGenerating) {
      setStepIndex(0);
      return;
    }
    const interval = setInterval(() => setStepIndex((current) => (current + 1) % steps.length), 3500);
    return () => clearInterval(interval);
  }, [isGenerating, steps.length]);

  if (!isModalOpen) return null;

  async function handleSubmit() {
    const personId = user?.personId;
    if (!personId || !prompt.trim() || isGenerating) return;

    setGenerating(true);
    setError(null);
    try {
      const plan = await generateGrowWithUsPlan(personId, prompt.trim(), isEn ? 'en' : 'es');
      setPlan(plan);
      void loadSavedSearches(personId);
      closeModal();
      setPrompt('');
      navigate('/advance-your-knowledge');
    } catch {
      setError(isEn ? 'We could not generate your plan right now. Please try again.' : 'No pudimos generar tu plan en este momento. Intenta de nuevo.');
    } finally {
      setGenerating(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="grow-with-us-modal-title"
      onClick={() => !isGenerating && closeModal()}
    >
      <div
        className="w-full max-w-lg rounded-3xl border border-amber-400/20 bg-white p-6 shadow-2xl dark:bg-slate-900 sm:p-8"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-2.5">
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-amber-500 to-rose-600 text-white shadow-sm">
              <Sparkles className="h-5 w-5" aria-hidden="true" />
            </span>
            <div>
              <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-amber-600 dark:text-amber-300">
                {isEn ? 'Grow With Us' : 'Crece con Nosotros'}
              </p>
              <h3 id="grow-with-us-modal-title" className="text-lg font-bold text-slate-950 dark:text-white">
                {isEn ? 'What do you want to grow in?' : '¿En qué quieres crecer?'}
              </h3>
            </div>
          </div>
          <button
            type="button"
            onClick={closeModal}
            disabled={isGenerating}
            aria-label={isEn ? 'Close' : 'Cerrar'}
            className="shrink-0 rounded-full p-1 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600 disabled:opacity-40 dark:hover:bg-white/10 dark:hover:text-white"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        <p className="mt-4 text-sm leading-6 text-slate-600 dark:text-white/70">
          {isEn
            ? 'Tell us what you want to focus on — a technology, a skill, a career direction. We will search the web and build you a full plan: suggested courses, papers to read, and relevant news for your role.'
            : 'Cuéntanos en qué quieres enfocarte — una tecnología, una habilidad, una dirección de carrera. Buscaremos en internet y te armaremos un plan completo: cursos sugeridos, papers para leer, y noticias relevantes para tu puesto.'}
        </p>

        <textarea
          value={prompt}
          onChange={(event) => setPrompt(event.target.value)}
          disabled={isGenerating}
          rows={4}
          placeholder={isEn ? 'e.g. "I want to get better at building AI agents with tools and skills"' : 'ej. "Quiero mejorar construyendo agentes de IA con skills y tools"'}
          className="mt-5 w-full resize-none rounded-2xl border border-slate-200 bg-white p-4 text-sm text-slate-900 outline-none transition focus:border-amber-400 disabled:opacity-60 dark:border-white/10 dark:bg-white/[0.04] dark:text-white"
        />

        {isGenerating && (
          <div className="mt-4 flex items-center gap-3 rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 dark:border-amber-400/25 dark:bg-amber-400/10">
            <Loader2 className="h-5 w-5 shrink-0 animate-spin text-amber-600 dark:text-amber-300" aria-hidden="true" />
            <div className="min-w-0 flex-1">
              <p className="flex items-center gap-1.5 text-sm font-semibold text-amber-800 dark:text-amber-200">
                {(() => {
                  const StepIcon = steps[stepIndex].icon;
                  return <StepIcon className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />;
                })()}
                {steps[stepIndex].text}
              </p>
              <p className="mt-0.5 text-xs font-medium text-amber-700/80 dark:text-amber-200/70">
                {isEn ? 'This can take up to a minute — please keep this window open.' : 'Esto puede tardar hasta un minuto — por favor no cierres esta ventana.'}
              </p>
            </div>
          </div>
        )}

        {!isGenerating && lastError && (
          <p className="mt-4 rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700 dark:border-rose-400/25 dark:bg-rose-400/10 dark:text-rose-300">
            {lastError}
          </p>
        )}

        <button
          type="button"
          onClick={() => void handleSubmit()}
          disabled={!prompt.trim() || isGenerating}
          className="mt-5 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-500 to-rose-600 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-amber-600/20 transition hover:from-amber-600 hover:to-rose-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {isGenerating ? (
            <>
              <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
              {isEn ? 'Building your plan...' : 'Construyendo tu plan...'}
            </>
          ) : (
            <>
              <Sparkles className="h-4 w-4" aria-hidden="true" />
              {isEn ? 'Generate my plan' : 'Generar mi plan'}
            </>
          )}
        </button>
      </div>
    </div>
  );
}
