import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import DOMPurify from 'dompurify';
import { Lightbulb, BookOpen, RotateCcw, Hammer, ClipboardCheck, Check, Send, MessageCircle } from 'lucide-react';
import { apiImageUrl } from '../lib/api/httpClient';
import {
  getCapabilityGraph,
  getActiveSession,
  startSession,
  submitStepResponse,
  advanceStep,
  askTutor,
  submitRecallAttempt,
  evaluateProduction,
  completeNode,
  getActiveAssessmentRound,
  startAssessmentRound,
  submitAssessmentAnswer,
  getNodeBlueprint,
  PREVIEW_PERSON_ID,
  type ExperienceStepType,
  type BackendRuntimeStep,
  type BackendRuntimeSessionInfo,
  type TutorMode,
  type GraphNodeInfoDto,
  type AssessmentRoundStateDto,
  type SubmitAssessmentAnswerResponse,
  type EvaluateProductionResponseDto,
  type NodeBlueprintDto,
} from '../lib/api/runtimeApi';
import LoadingSpinner from '../components/LoadingSpinner';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import { usePreviewMode, type PreviewMode } from '../lib/previewMode';
import PreviewNodeBlueprintView from './PreviewNodeBlueprintView';

DOMPurify.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A') {
    node.setAttribute('target', '_blank');
    node.setAttribute('rel', 'noopener noreferrer');
  }
});

const STEP_ORDER: ExperienceStepType[] = ['Hypothesis', 'Teaching', 'Recall', 'Production', 'Assessment'];
const RECALL_MAX_ATTEMPTS = 5;
const RECALL_ITEMS_REQUIRED = 3;

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

interface NormalizedStep {
  learningSessionStepId: string;
  stepType: ExperienceStepType;
  content: string;
  illustrations: { illustrationId: string; storagePath: string; caption?: string }[];
}

function normalizeStep(step: BackendRuntimeStep): NormalizedStep {
  return {
    learningSessionStepId: step.LearningSessionStepId,
    stepType: step.StepType,
    content: step.Content,
    illustrations: step.Illustrations.map((i) => ({
      illustrationId: i.IllustrationId,
      storagePath: i.StoragePath,
      caption: i.Caption,
    })),
  };
}

interface TutorChatMessage {
  author: 'student' | 'tutor';
  text: string;
}

/**
 * "Probar como estudiante" — Memory Paradox node experience
 * (Hypothesis/Teaching/Recall/Production/Assessment) rendered with
 * Capability Studio's own dark UI, ported from
 * humanlearn/src/pages/NodeWorkflowPage.tsx so the team can preview the
 * real student runtime without leaving Studio. Talks to the SAME
 * instructor-runtime API as the student app, using a fixed seeded test
 * Person (PREVIEW_PERSON_ID) since there is no student auth here.
 * Deliberately trimmed vs. the full student app: no "Profundizar"
 * knowledge expansion, no per-step read-only review modal, no
 * mastered-node past-attempts recap — just the core 5-step flow.
 *
 * Mode-aware (2026-07-21, revised 2026-07-22): "Real" and "Demo" modes
 * BOTH render this same interactive experience (same LearningSession-
 * driven runtime, same seeded PREVIEW_PERSON_ID — never a real student's
 * data either way) so a reviewer in Demo mode sees exactly what a real
 * student would, including the Agente de Voz on Recall. Only "Edición"
 * mode is a completely separate, session-independent rendering path
 * (PreviewNodeBlueprintView) — see the thin wrapper default export below.
 */
function RealNodeExperience({ mode }: { mode: PreviewMode }) {
  const { capabilityId, nodeId } = useParams<{ capabilityId: string; nodeId: string }>();
  const navigate = useNavigate();

  const [phase, setPhase] = useState<'loading' | 'ready' | 'error'>('loading');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [nodeInfo, setNodeInfo] = useState<{ name: string; description?: string } | null>(null);

  const [sessionNodeId, setSessionNodeId] = useState<string | null>(null);
  const [step, setStep] = useState<NormalizedStep | null>(null);
  const [responseText, setResponseText] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const advancingRef = useRef(false);

  const [tutorMessages, setTutorMessages] = useState<TutorChatMessage[]>([]);
  const [tutorInput, setTutorInput] = useState('');
  const [tutorLoading, setTutorLoading] = useState(false);

  const [recallAttemptsUsed, setRecallAttemptsUsed] = useState(0);
  const [recallItemsMastered, setRecallItemsMastered] = useState(0);
  const [recallLastPrompt, setRecallLastPrompt] = useState<string | undefined>(undefined);
  const [recallReview, setRecallReview] = useState<{ nextStep: BackendRuntimeStep } | null>(null);

  const [productionEvaluation, setProductionEvaluation] = useState<EvaluateProductionResponseDto | null>(null);
  const [productionSubmitting, setProductionSubmitting] = useState(false);

  const [assessmentRound, setAssessmentRound] = useState<AssessmentRoundStateDto | null>(null);
  const [assessmentAnswerText, setAssessmentAnswerText] = useState('');
  const [assessmentSubmitting, setAssessmentSubmitting] = useState(false);
  const [assessmentLastResult, setAssessmentLastResult] = useState<SubmitAssessmentAnswerResponse | null>(null);
  const [completing, setCompleting] = useState(false);
  const [newlyUnlocked, setNewlyUnlocked] = useState<GraphNodeInfoDto[] | null>(null);
  const [pendingAdvance, setPendingAdvance] = useState<{ nextStep: BackendRuntimeStep } | null>(null);

  // Demo-mode-only "peek back" at another step's static blueprint content
  // (2026-07-22) — lets a reviewer jump to e.g. Hipótesis while sitting at
  // Recordar in a live session, without touching the actual LearningSession
  // progression (which only ever moves forward). Read-only: reuses the same
  // GetNodeBlueprintFunction data as Studio's "Edición" view, never a
  // learningSessionStepId, so no VoiceTutorAgent/grading on a peeked step.
  const [blueprint, setBlueprint] = useState<NodeBlueprintDto | null>(null);
  const [blueprintLoading, setBlueprintLoading] = useState(false);
  const [viewingStepType, setViewingStepType] = useState<ExperienceStepType | null>(null);

  const handleSelectStep = useCallback(
    async (stepType: ExperienceStepType) => {
      if (mode === 'real' || !nodeId || !step) return;
      // No locks in demo mode (2026-07-22): a reviewer can freely jump to
      // ANY of the 5 steps, forward or backward, regardless of where the
      // live session actually is. A non-current step is always shown via
      // the read-only BlueprintStepPeek (static blueprint content, no
      // learningSessionStepId/VoiceTutorAgent/grading either way), so
      // peeking ahead is just as safe as peeking back. Real forward
      // progress still only happens through Continuar/Responder.
      if (stepType === step.stepType) {
        setViewingStepType(null);
        return;
      }
      setViewingStepType(stepType);
      if (!blueprint) {
        setBlueprintLoading(true);
        try {
          setBlueprint(await getNodeBlueprint(nodeId));
        } catch {
          setViewingStepType(null);
        } finally {
          setBlueprintLoading(false);
        }
      }
    },
    [mode, nodeId, step, blueprint]
  );

  const resetStepUiState = useCallback((next: NormalizedStep) => {
    setStep(next);
    setViewingStepType(null);
    setResponseText('');
    setTutorMessages([]);
    setTutorInput('');
    setRecallAttemptsUsed(0);
    setRecallItemsMastered(0);
    setRecallLastPrompt(undefined);
    setRecallReview(null);
    setProductionEvaluation(null);
    setAssessmentRound(null);
    setAssessmentAnswerText('');
    setAssessmentLastResult(null);
    setPendingAdvance(null);
  }, []);

  useEffect(() => {
    if (!capabilityId || !nodeId) return;
    let cancelled = false;

    async function load() {
      setPhase('loading');
      setErrorMessage(null);

      try {
        const graph = await getCapabilityGraph(capabilityId!, PREVIEW_PERSON_ID);
        if (cancelled) return;
        const match = graph.Nodes.find((n) => n.CapabilityGraphNodeId === nodeId);
        if (match) setNodeInfo({ name: match.Name, description: match.Description });
      } catch {
        // Non-critical for the header.
      }

      try {
        const active = await getActiveSession(PREVIEW_PERSON_ID, capabilityId!);
        const info: BackendRuntimeSessionInfo =
          active && active.CapabilityGraphNodeId === nodeId
            ? active
            : await startSession(PREVIEW_PERSON_ID, capabilityId!, nodeId!);
        if (cancelled) return;
        setSessionNodeId(info.LearningSessionNodeId);
        resetStepUiState(normalizeStep(info.CurrentStep));
        setPhase('ready');
      } catch {
        if (!cancelled) {
          setErrorMessage('No se pudo cargar la experiencia de este nodo.');
          setPhase('error');
        }
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [capabilityId, nodeId, resetStepUiState]);

  const handleContinue = useCallback(async () => {
    if (!step || !sessionNodeId || advancingRef.current) return;
    advancingRef.current = true;
    setSubmitting(true);
    setErrorMessage(null);
    try {
      if (responseText.trim()) {
        await submitStepResponse(step.learningSessionStepId, responseText.trim());
      }
      const nextStep = await advanceStep(sessionNodeId);
      resetStepUiState(normalizeStep(nextStep));
    } catch {
      setErrorMessage('Ocurrió un error. Intenta de nuevo.');
    } finally {
      setSubmitting(false);
      advancingRef.current = false;
    }
  }, [step, sessionNodeId, responseText, resetStepUiState]);

  const handlePendingAdvanceContinue = useCallback(() => {
    if (!pendingAdvance) return;
    resetStepUiState(normalizeStep(pendingAdvance.nextStep));
  }, [pendingAdvance, resetStepUiState]);

  const handleRecallReviewContinue = useCallback(() => {
    if (!recallReview) return;
    resetStepUiState(normalizeStep(recallReview.nextStep));
  }, [recallReview, resetStepUiState]);

  const handleProductionSubmit = useCallback(async () => {
    if (!step || !responseText.trim()) return;
    setProductionSubmitting(true);
    setErrorMessage(null);
    try {
      const outcome = await evaluateProduction(step.learningSessionStepId, responseText.trim());
      setProductionEvaluation(outcome);
    } catch {
      setErrorMessage('Ocurrió un error. Intenta de nuevo.');
    } finally {
      setProductionSubmitting(false);
    }
  }, [step, responseText]);

  const handleProductionRetry = useCallback(() => {
    setProductionEvaluation(null);
    setResponseText('');
  }, []);

  const handleProductionContinue = useCallback(async () => {
    if (!sessionNodeId || advancingRef.current) return;
    advancingRef.current = true;
    setSubmitting(true);
    setErrorMessage(null);
    try {
      const nextStep = await advanceStep(sessionNodeId);
      resetStepUiState(normalizeStep(nextStep));
    } catch {
      setErrorMessage('Ocurrió un error. Intenta de nuevo.');
    } finally {
      setSubmitting(false);
      advancingRef.current = false;
    }
  }, [sessionNodeId, resetStepUiState]);

  const handleAskTutor = useCallback(async () => {
    if (!step || !tutorInput.trim()) return;
    const mode = step.stepType as TutorMode;
    const studentMessage = tutorInput.trim();
    setTutorMessages((messages) => [...messages, { author: 'student', text: studentMessage }]);
    setTutorInput('');
    setTutorLoading(true);
    try {
      const turn = await askTutor(step.learningSessionStepId, mode, studentMessage);
      setTutorMessages((messages) => [...messages, { author: 'tutor', text: turn.Message }]);
    } catch {
      setTutorMessages((messages) => [...messages, { author: 'tutor', text: 'No pude responder. Intenta de nuevo.' }]);
    } finally {
      setTutorLoading(false);
    }
  }, [step, tutorInput]);

  const handleRecallSubmit = useCallback(async () => {
    if (!step || !responseText.trim()) return;
    setSubmitting(true);
    setErrorMessage(null);
    try {
      const outcome = await submitRecallAttempt(step.learningSessionStepId, responseText.trim(), recallLastPrompt);
      setResponseText('');
      if (outcome.Advanced && outcome.NextStep) {
        setPendingAdvance({ nextStep: outcome.NextStep });
      } else if (outcome.RegressedToTeaching && outcome.NextStep) {
        setRecallReview({ nextStep: outcome.NextStep });
      } else {
        setRecallAttemptsUsed(outcome.AttemptsUsedForItem);
        setRecallItemsMastered(outcome.ItemsMastered);
        setRecallLastPrompt(outcome.TutorTurn.Message);
      }
    } catch {
      setErrorMessage('Ocurrió un error. Intenta de nuevo.');
    } finally {
      setSubmitting(false);
    }
  }, [step, responseText, recallLastPrompt]);

  useEffect(() => {
    if (!sessionNodeId || step?.stepType !== 'Assessment' || assessmentRound !== null) return;
    let cancelled = false;

    (async () => {
      setErrorMessage(null);
      try {
        const active = await getActiveAssessmentRound(sessionNodeId);
        const round = active ?? (await startAssessmentRound(sessionNodeId));
        if (!cancelled) setAssessmentRound(round);
      } catch {
        if (!cancelled) setErrorMessage('Ocurrió un error. Intenta de nuevo.');
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [sessionNodeId, step, assessmentRound]);

  const handleAssessmentSubmitAnswer = useCallback(async () => {
    if (!assessmentRound?.CurrentQuestion || !assessmentAnswerText.trim()) return;
    setAssessmentSubmitting(true);
    setErrorMessage(null);
    try {
      const result = await submitAssessmentAnswer(assessmentRound.CurrentQuestion.AssessmentQuestionId, assessmentAnswerText.trim());
      setAssessmentLastResult(result);
      setAssessmentAnswerText('');
    } catch {
      setErrorMessage('Ocurrió un error. Intenta de nuevo.');
    } finally {
      setAssessmentSubmitting(false);
    }
  }, [assessmentRound, assessmentAnswerText]);

  const handleAssessmentContinue = useCallback(() => {
    if (!assessmentLastResult || !assessmentRound) return;

    if (!assessmentLastResult.RoundComplete) {
      setAssessmentRound({ ...assessmentRound, CurrentQuestion: assessmentLastResult.NextQuestion ?? null });
    } else if (!assessmentLastResult.Passed && assessmentLastResult.NewAssessmentRoundId) {
      setAssessmentRound({
        AssessmentRoundId: assessmentLastResult.NewAssessmentRoundId,
        RoundNumber: assessmentLastResult.NewRoundNumber ?? assessmentRound.RoundNumber + 1,
        TotalQuestions: assessmentRound.TotalQuestions,
        Status: 'InProgress',
        FinalScore: null,
        CurrentQuestion: assessmentLastResult.NextQuestion ?? null,
      });
    }
    setAssessmentLastResult(null);
  }, [assessmentLastResult, assessmentRound]);

  const handleCompleteNode = useCallback(async () => {
    if (!sessionNodeId) return;
    setCompleting(true);
    setErrorMessage(null);
    try {
      const result = await completeNode(sessionNodeId);
      setNewlyUnlocked(result.newlyUnlockedNodes);
    } catch {
      setErrorMessage('Ocurrió un error. Intenta de nuevo.');
    } finally {
      setCompleting(false);
    }
  }, [sessionNodeId]);

  const backLink = `/capabilities/${capabilityId}/preview`;

  if (phase === 'loading') {
    return <LoadingSpinner label="Cargando experiencia..." />;
  }

  if (phase === 'error' || !step) {
    return (
      <div className="mx-auto max-w-2xl px-6 py-16 text-center">
        <p className="text-red-300">{errorMessage ?? 'No se pudo cargar el nodo.'}</p>
        <Link to={backLink} className="mt-4 inline-block text-sm text-brand-400 hover:text-brand-300">
          ← Volver al mapa
        </Link>
      </div>
    );
  }

  if (newlyUnlocked !== null) {
    return (
      <div className="mx-auto max-w-xl px-6 py-16">
        <div className="rounded-2xl border border-emerald-400/30 bg-emerald-500/[0.06] p-8 text-center">
          <Check className="mx-auto h-10 w-10 text-emerald-300" />
          <h1 className="mt-3 text-xl font-semibold text-white">¡Nodo completado!</h1>
          {newlyUnlocked.length > 0 && (
            <div className="mt-4 text-left text-sm text-emerald-200">
              <p className="font-medium">Nodos recién desbloqueados:</p>
              <ul className="mt-1 list-disc space-y-1 pl-5">
                {newlyUnlocked.map((n) => (
                  <li key={n.CapabilityGraphNodeId}>{n.Name}</li>
                ))}
              </ul>
            </div>
          )}
          <button
            onClick={() => navigate(backLink)}
            className="mt-6 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25"
          >
            Volver al mapa
          </button>
        </div>
      </div>
    );
  }

  const currentIndex = STEP_ORDER.indexOf(step.stepType);
  const showsTutorChat = step.stepType === 'Teaching' || step.stepType === 'Production';

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <Link to={backLink} className="text-sm text-slate-400 hover:text-white">
        ← Volver al mapa
      </Link>

      {nodeInfo && (
        <div className="mt-4 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-semibold text-white">{nodeInfo.name}</h1>
            {mode !== 'real' && (
              <span className="rounded-full border border-amber-400/40 bg-amber-500/10 px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-wide text-amber-300">
                Modo demo · sin candados
              </span>
            )}
          </div>
          {nodeInfo.description && <p className="mt-2 text-sm leading-relaxed text-slate-400">{nodeInfo.description}</p>}
        </div>
      )}

      <StepperBar currentIndex={currentIndex} mode={mode} viewingStepType={viewingStepType} onSelectStep={handleSelectStep} />

      {viewingStepType === null && step.stepType !== 'Recall' && (
        <VoiceTutorAgent learningSessionStepId={step.learningSessionStepId} />
      )}

      {viewingStepType !== null ? (
        <BlueprintStepPeek
          stepType={viewingStepType}
          blueprint={blueprint}
          loading={blueprintLoading}
          capabilityGraphNodeId={nodeId!}
          onBack={() => setViewingStepType(null)}
        />
      ) : (
      <div className="mt-6 rounded-2xl border border-white/10 bg-white/[0.03] p-6">
        {errorMessage && <p className="mb-4 text-sm text-red-300">{errorMessage}</p>}

        {step.stepType !== 'Recall' && step.stepType !== 'Assessment' && (
          <RichContent className="text-slate-300" html={step.content} />
        )}

        {step.illustrations.length > 0 && (
          <div className="mt-4 flex flex-col gap-4">
            {step.illustrations.map((illustration) => (
              <figure key={illustration.illustrationId} className="overflow-hidden rounded-xl border border-white/10 bg-white/[0.02]">
                <img
                  src={apiImageUrl(`/illustrations/${illustration.illustrationId}/image`)}
                  alt={illustration.caption ?? ''}
                  className="h-auto w-full object-contain"
                />
                {illustration.caption && (
                  <figcaption className="border-t border-white/10 px-3 py-1.5 text-xs text-slate-400">
                    {illustration.caption}
                  </figcaption>
                )}
              </figure>
            ))}
          </div>
        )}

        {showsTutorChat && (
          <div className="mt-6 rounded-xl border border-white/10 bg-white/[0.02] p-4">
            <p className="mb-3 flex items-center gap-1.5 text-xs text-slate-400">
              <MessageCircle className="h-3.5 w-3.5" /> El tutor te guía, no te califica aquí.
            </p>
            <TutorChat messages={tutorMessages} loading={tutorLoading} />
            <div className="mt-3 flex gap-2">
              <input
                value={tutorInput}
                onChange={(e) => setTutorInput(e.target.value)}
                placeholder="Pregúntale al tutor..."
                className="flex-1 rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white placeholder:text-slate-500 focus:border-brand-400 focus:outline-none"
                onKeyDown={(e) => e.key === 'Enter' && handleAskTutor()}
              />
              <button
                onClick={handleAskTutor}
                disabled={tutorLoading || !tutorInput.trim()}
                className="flex items-center gap-1 rounded-lg bg-white/10 px-3 py-2 text-sm text-white hover:bg-white/20 disabled:opacity-50"
              >
                <Send className="h-4 w-4" /> Enviar
              </button>
            </div>
          </div>
        )}

        {step.stepType === 'Hypothesis' && (
          <div className="mt-6">
            <label className="mb-1 block text-sm font-medium text-slate-300">Tu respuesta</label>
            <textarea
              value={responseText}
              onChange={(e) => setResponseText(e.target.value)}
              rows={4}
              className="w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white focus:border-brand-400 focus:outline-none"
            />
            <button
              onClick={handleContinue}
              disabled={submitting || (mode === 'real' && !responseText.trim())}
              className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 disabled:opacity-50"
            >
              Continuar
            </button>
          </div>
        )}

        {step.stepType === 'Production' &&
          (productionEvaluation?.IsCorrect ? (
            <RewardBanner title="¡Correcto!" onContinue={handleProductionContinue}>
              <p className="mt-2 text-sm text-emerald-200">{productionEvaluation.Feedback}</p>
            </RewardBanner>
          ) : (
            <div className="mt-6">
              <label className="mb-1 block text-sm font-medium text-slate-300">Tu respuesta</label>
              <textarea
                value={responseText}
                onChange={(e) => setResponseText(e.target.value)}
                rows={4}
                className="w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white focus:border-brand-400 focus:outline-none"
              />
              {productionEvaluation && !productionEvaluation.IsCorrect && (
                <div className="mt-3 rounded-lg border border-red-400/30 bg-red-500/10 p-3 text-sm text-red-200">
                  {productionEvaluation.Feedback}
                </div>
              )}
              <button
                onClick={productionEvaluation ? handleProductionRetry : handleProductionSubmit}
                disabled={productionSubmitting || (!productionEvaluation && !responseText.trim())}
                className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 disabled:opacity-50"
              >
                {productionSubmitting ? 'Evaluando...' : productionEvaluation ? 'Intentar de nuevo' : 'Enviar'}
              </button>
            </div>
          ))}

        {step.stepType === 'Teaching' && (
          <div className="mt-6">
            <label className="mb-1 block text-sm font-medium text-slate-300">Notas (opcional)</label>
            <textarea
              value={responseText}
              onChange={(e) => setResponseText(e.target.value)}
              rows={3}
              className="w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white focus:border-brand-400 focus:outline-none"
            />
            <button
              onClick={handleContinue}
              disabled={submitting}
              className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 disabled:opacity-50"
            >
              Continuar cuando estés listo
            </button>
          </div>
        )}

        {step.stepType === 'Recall' &&
          (pendingAdvance ? (
            <RewardBanner title="¡Excelente! Dominaste este recall." onContinue={handlePendingAdvanceContinue} />
          ) : recallReview ? (
            <RewardBanner title="Repasemos la enseñanza antes de seguir." onContinue={handleRecallReviewContinue} tone="amber" />
          ) : (
            <div className="mt-6">
              <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                Dominados {recallItemsMastered}/{RECALL_ITEMS_REQUIRED} · Intento {recallAttemptsUsed + 1} de {RECALL_MAX_ATTEMPTS}
              </p>
              <RichContent className="mt-2 text-slate-300" html={recallLastPrompt ?? step.content} />

              <label className="mb-1 mt-4 block text-sm font-medium text-slate-300">Tu respuesta</label>
              <textarea
                value={responseText}
                onChange={(e) => setResponseText(e.target.value)}
                rows={3}
                className="w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white focus:border-brand-400 focus:outline-none"
              />
              <button
                onClick={handleRecallSubmit}
                disabled={submitting || !responseText.trim()}
                className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 disabled:opacity-50"
              >
                Responder
              </button>
            </div>
          ))}

        {step.stepType === 'Assessment' && (
          <AssessmentPanel
            round={assessmentRound}
            answerText={assessmentAnswerText}
            onAnswerTextChange={setAssessmentAnswerText}
            submitting={assessmentSubmitting}
            lastResult={assessmentLastResult}
            onSubmitAnswer={handleAssessmentSubmitAnswer}
            onContinue={handleAssessmentContinue}
            onCompleteNode={handleCompleteNode}
            completing={completing}
          />
        )}
      </div>
      )}
    </div>
  );
}

function RichContent({ html, className }: { html: string; className?: string }) {
  const sanitized = useMemo(
    () =>
      DOMPurify.sanitize(html, {
        ALLOWED_TAGS: ['p', 'br', 'strong', 'b', 'em', 'i', 'ul', 'ol', 'li', 'h3', 'h4', 'blockquote', 'code', 'a', 'span'],
        ALLOWED_ATTR: ['href'],
      }),
    [html]
  );

  return (
    <div
      className={`${className ?? ''} [&_a]:text-brand-400 [&_a]:underline [&_ul]:list-disc [&_ul]:pl-5 [&_ol]:list-decimal [&_ol]:pl-5`}
      style={{ whiteSpace: 'pre-wrap' }}
      dangerouslySetInnerHTML={{ __html: sanitized }}
    />
  );
}

function StepperBar({
  currentIndex,
  mode,
  viewingStepType,
  onSelectStep,
}: {
  currentIndex: number;
  mode: PreviewMode;
  viewingStepType: ExperienceStepType | null;
  onSelectStep: (stepType: ExperienceStepType) => void;
}) {
  const clickable = mode !== 'real';
  return (
    <div className="mt-4 flex items-center">
      {STEP_ORDER.map((stepType, index) => {
        const Icon = STEP_ICONS[stepType];
        const isDone = index < currentIndex;
        const isActive = index === currentIndex && viewingStepType === null;
        const isPeeking = viewingStepType === stepType;
        const isClickableStep = clickable;
        return (
          <div key={stepType} className="flex flex-1 items-center">
            <button
              type="button"
              disabled={!isClickableStep}
              onClick={() => onSelectStep(stepType)}
              className={`flex flex-col items-center gap-1 ${isClickableStep ? 'cursor-pointer' : 'cursor-default'}`}
            >
              <div
                className={`flex h-9 w-9 items-center justify-center rounded-full border-2 ${
                  isPeeking
                    ? 'border-amber-400 bg-amber-500/10 text-amber-300'
                    : isDone
                    ? 'border-transparent bg-gradient-to-br from-brand-500 to-accent-500 text-white'
                    : isActive
                    ? 'border-brand-400 bg-brand-500/10 text-brand-300'
                    : 'border-white/10 bg-white/[0.03] text-slate-500'
                }`}
              >
                {isDone && !isPeeking ? <Check className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
              </div>
              <span
                className={`text-[11px] font-medium ${
                  isPeeking ? 'text-amber-300' : isActive ? 'text-white' : isDone ? 'text-brand-300' : 'text-slate-500'
                }`}
              >
                {STEP_LABELS[stepType]}
              </span>
            </button>
            {index < STEP_ORDER.length - 1 && (
              <div className={`mx-1 h-0.5 flex-1 ${index < currentIndex ? 'bg-brand-500' : 'bg-white/10'}`} />
            )}
          </div>
        );
      })}
    </div>
  );
}

/**
 * Read-only "peek" at another step's static blueprint content while a
 * demo-mode reviewer is mid-session on a different (live) step — e.g.
 * jumping back to Hipótesis while actually sitting at Recordar. Reuses
 * the same blueprint data as Studio's Edición view; has no textarea/
 * submit since there's no learningSessionStepId for a step you're not
 * actually on. Hypothesis/Teaching DO still show the Voice Tutor Agent
 * here (2026-07-22) via its blueprint-only path (CapabilityGraphNodeId +
 * StepType, no LearningSessionStepId needed) — the Agent should never
 * disappear just because the student peeked at a step early.
 */
function BlueprintStepPeek({
  stepType,
  blueprint,
  loading,
  capabilityGraphNodeId,
  onBack,
}: {
  stepType: ExperienceStepType;
  blueprint: NodeBlueprintDto | null;
  loading: boolean;
  capabilityGraphNodeId: string;
  onBack: () => void;
}) {
  const blueprintStep = blueprint?.Steps.find((s) => s.StepType === stepType);

  return (
    <div className="mt-6 rounded-2xl border border-amber-400/30 bg-amber-500/[0.04] p-6">
      <div className="mb-4 flex items-center justify-between">
        <p className="text-xs font-medium uppercase tracking-wide text-amber-300">
          Vista previa de solo lectura · {STEP_LABELS[stepType]}
        </p>
        <button
          onClick={onBack}
          className="rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-white/[0.08]"
        >
          ← Volver a mi sesión en vivo
        </button>
      </div>

      {(stepType === 'Hypothesis' || stepType === 'Teaching') && (
        <VoiceTutorAgent capabilityGraphNodeId={capabilityGraphNodeId} stepType={stepType} />
      )}

      {loading || !blueprintStep ? (
        <LoadingSpinner label="Cargando contenido..." />
      ) : (
        <>
          <RichContent className="text-slate-300" html={blueprintStep.Content} />
          {blueprintStep.Illustrations.length > 0 && (
            <div className="mt-4 flex flex-col gap-4">
              {blueprintStep.Illustrations.map((illustration) => (
                <figure
                  key={illustration.IllustrationId}
                  className="overflow-hidden rounded-xl border border-white/10 bg-white/[0.02]"
                >
                  <img
                    src={apiImageUrl(`/illustrations/${illustration.IllustrationId}/image`)}
                    alt={illustration.Caption ?? ''}
                    className="h-auto w-full object-contain"
                  />
                  {illustration.Caption && (
                    <figcaption className="border-t border-white/10 px-3 py-1.5 text-xs text-slate-400">
                      {illustration.Caption}
                    </figcaption>
                  )}
                </figure>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}

function TutorChat({ messages, loading }: { messages: TutorChatMessage[]; loading: boolean }) {
  if (messages.length === 0 && !loading) return null;
  return (
    <div className="max-h-64 space-y-2 overflow-y-auto">
      {messages.map((message, index) => (
        <div
          key={index}
          className={`max-w-[85%] rounded-lg px-3 py-2 text-sm ${
            message.author === 'student' ? 'ml-auto bg-brand-500/20 text-white' : 'bg-white/5 text-slate-300'
          }`}
        >
          {message.text}
        </div>
      ))}
      {loading && <div className="max-w-[85%] rounded-lg bg-white/5 px-3 py-2 text-sm text-slate-400">Escribiendo...</div>}
    </div>
  );
}

function RewardBanner({
  title,
  children,
  onContinue,
  tone = 'emerald',
}: {
  title: string;
  children?: React.ReactNode;
  onContinue: () => void;
  tone?: 'emerald' | 'amber';
}) {
  const colors =
    tone === 'emerald'
      ? { border: 'border-emerald-400/30', bg: 'bg-emerald-500/[0.08]', text: 'text-emerald-200' }
      : { border: 'border-amber-400/30', bg: 'bg-amber-500/[0.08]', text: 'text-amber-200' };
  return (
    <div className={`mt-6 rounded-xl border ${colors.border} ${colors.bg} p-5 text-center`}>
      <p className={`text-sm font-semibold ${colors.text}`}>{title}</p>
      {children}
      <button
        onClick={onContinue}
        className="mt-4 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25"
      >
        Continuar
      </button>
    </div>
  );
}

function AssessmentPanel({
  round,
  answerText,
  onAnswerTextChange,
  submitting,
  lastResult,
  onSubmitAnswer,
  onContinue,
  onCompleteNode,
  completing,
}: {
  round: AssessmentRoundStateDto | null;
  answerText: string;
  onAnswerTextChange: (value: string) => void;
  submitting: boolean;
  lastResult: SubmitAssessmentAnswerResponse | null;
  onSubmitAnswer: () => void;
  onContinue: () => void;
  onCompleteNode: () => void;
  completing: boolean;
}) {
  if (!round) {
    return <p className="mt-6 text-sm text-slate-400">Preparando la evaluación...</p>;
  }

  if (round.Status === 'Passed') {
    return (
      <div className="mt-6 rounded-xl border border-emerald-400/30 bg-emerald-500/[0.08] p-5 text-center">
        <p className="text-sm font-semibold text-emerald-200">
          ¡Evaluación superada! {typeof round.FinalScore === 'number' && `(${round.FinalScore}/100)`}
        </p>
        <button
          onClick={onCompleteNode}
          disabled={completing}
          className="mt-4 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 disabled:opacity-50"
        >
          {completing ? 'Guardando...' : 'Completar nodo'}
        </button>
      </div>
    );
  }

  if (lastResult) {
    return (
      <div className="mt-6 rounded-xl border border-white/10 bg-white/[0.02] p-5">
        <p className={`text-sm font-medium ${lastResult.Grade.Correctness === 'Correct' ? 'text-emerald-300' : 'text-amber-300'}`}>
          {lastResult.Grade.Correctness === 'Correct'
            ? 'Correcto'
            : lastResult.Grade.Correctness === 'PartiallyCorrect'
            ? 'Parcialmente correcto'
            : 'Incorrecto'}
        </p>
        <p className="mt-2 text-sm text-slate-300">{lastResult.Grade.Feedback}</p>
        <button
          onClick={onContinue}
          className="mt-4 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25"
        >
          Siguiente
        </button>
      </div>
    );
  }

  if (!round.CurrentQuestion) {
    return <p className="mt-6 text-sm text-slate-400">Cargando siguiente pregunta...</p>;
  }

  return (
    <div className="mt-6">
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
        Pregunta {round.CurrentQuestion.QuestionIndex} de {round.TotalQuestions} · Ronda {round.RoundNumber}
      </p>
      <p className="mt-2 text-sm text-slate-200">{round.CurrentQuestion.QuestionText}</p>
      {round.CurrentQuestion.IllustrationId && (
        <div className="mt-3 overflow-hidden rounded-xl border border-white/10 bg-white/[0.02]">
          <img
            src={apiImageUrl(`/illustrations/${round.CurrentQuestion.IllustrationId}/image`)}
            alt=""
            className="h-auto w-full max-w-sm object-contain"
          />
        </div>
      )}
      <textarea
        value={answerText}
        onChange={(e) => onAnswerTextChange(e.target.value)}
        rows={3}
        className="mt-3 w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white focus:border-brand-400 focus:outline-none"
      />
      <button
        onClick={onSubmitAnswer}
        disabled={submitting || !answerText.trim()}
        className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 disabled:opacity-50"
      >
        {submitting ? 'Enviando...' : 'Responder'}
      </button>
    </div>
  );
}

/**
 * Mode-aware entry point (2026-07-21, revised 2026-07-22): "Real" AND
 * "Demo" modes both render the actual LearningSession-driven runtime
 * experience (RealNodeExperience, above) — Demo just skips the Recall
 * voice-agent restriction so a reviewer can fully simulate the student
 * experience. "Edición" mode renders a completely separate,
 * session-independent view (PreviewNodeBlueprintView) that reads/edits the
 * node's blueprint directly, with no progression gating. Kept as two
 * fully separate components (rather than branching inside one component's
 * body) so neither one's hooks are ever conditionally skipped.
 */
export default function PreviewNodePage() {
  const [mode] = usePreviewMode();

  if (mode === 'edit') {
    return <PreviewNodeBlueprintView mode={mode} />;
  }

  return <RealNodeExperience mode={mode} />;
}

