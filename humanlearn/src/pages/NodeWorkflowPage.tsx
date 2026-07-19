import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Lightbulb, BookOpen, RotateCcw, Hammer, ClipboardCheck, Check, Send, MessageCircle, Star, X } from 'lucide-react';
import { useI18n } from '../i18n';
import { MOCK_USER } from '../components/layout/AppShell';
import { API_BASE_URL } from '../lib/api/httpClient';
import { getCapabilityGraph } from '../lib/api/capabilityGraphApi';
import {
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
  getStepReview,
  getNodeSummary,
  type ExperienceStepType,
  type BackendRuntimeStep,
  type BackendRuntimeSessionInfo,
  type TutorMode,
  type GraphNodeInfoDto,
  type AssessmentRoundStateDto,
  type SubmitAssessmentAnswerResponse,
  type EvaluateProductionResponseDto,
  type StepReviewDto,
  type NodeSummaryDto,
  type NodeAttemptSummaryDto,
} from '../lib/api/runtimeSessionApi';

/**
 * Node interior — Paso 6. Drives a node's 5-step Memory Paradox sequence
 * (Hypothesis/Teaching/Recall/Production/Assessment) against the real
 * instructor-runtime API. The stepper is a permanent visible structure;
 * the tutor conversation lives INSIDE the active step, never replacing it
 * (see /memories/repo/student-graph-ui-redesign-final-design.md).
 */

const STEP_ORDER: ExperienceStepType[] = ['Hypothesis', 'Teaching', 'Recall', 'Production', 'Assessment'];

// Mirrors backend RecallLoopGate (Agentic/Runtime/RecallLoopGate.cs): each
// Recall item gets up to this many attempts, and the student must master
// this many DISTINCT items before the step advances to Production.
const RECALL_MAX_ATTEMPTS = 5;
const RECALL_ITEMS_REQUIRED = 3;

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

function normalizeSessionInfo(info: BackendRuntimeSessionInfo): NormalizedStep {
  return normalizeStep(info.CurrentStep);
}

interface TutorChatMessage {
  author: 'student' | 'tutor';
  text: string;
}

export default function NodeWorkflowPage() {
  const { capabilityId, nodeId } = useParams();
  const { t, language } = useI18n();
  const navigate = useNavigate();

  const [phase, setPhase] = useState<'loading' | 'ready' | 'error' | 'summary'>('loading');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Set only when this node is already Mastered — the read-only recap of
  // the last completed attempt is shown instead of silently starting a new
  // one (product decision, 2026-07-19). "Practicar de nuevo" below is the
  // only way to actually start a new attempt on a Mastered node.
  const [nodeSummary, setNodeSummary] = useState<NodeSummaryDto | null>(null);
  const [practicingAgain, setPracticingAgain] = useState(false);

  // Node header: title/objective (from the Capability Graph's own Name/
  // Description — no new endpoint needed) + a purely client-side "started
  // at" timestamp, captured once when this page first mounts for this node.
  const [nodeInfo, setNodeInfo] = useState<{ name: string; description?: string } | null>(null);
  const [startTime] = useState(() => new Date());

  const [sessionNodeId, setSessionNodeId] = useState<string | null>(null);
  const [step, setStep] = useState<NormalizedStep | null>(null);
  const [responseText, setResponseText] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const [tutorMessages, setTutorMessages] = useState<TutorChatMessage[]>([]);
  const [tutorInput, setTutorInput] = useState('');
  const [tutorLoading, setTutorLoading] = useState(false);

  const [recallAttemptsUsed, setRecallAttemptsUsed] = useState(0);
  const [recallItemsMastered, setRecallItemsMastered] = useState(0);
  const [recallLastPrompt, setRecallLastPrompt] = useState<string | undefined>(undefined);
  // Set only when the student exhausted their attempts on a Recall item
  // without mastering it — the node regressed back to Teaching, and this
  // holds the (already-fetched) Teaching step plus a friendly review
  // message until the student clicks through.
  const [recallReview, setRecallReview] = useState<{ nextStep: BackendRuntimeStep; title: string } | null>(null);

  // Production ("Aplícalo") formative grading: purely formative feedback,
  // no attempt cap — the student may resubmit as many times as they want
  // until IsCorrect=true, since the goal is genuine learning, not
  // throughput (product decision, 2026-07-18).
  const [productionEvaluation, setProductionEvaluation] = useState<EvaluateProductionResponseDto | null>(null);
  const [productionSubmitting, setProductionSubmitting] = useState(false);

  const [assessmentRound, setAssessmentRound] = useState<AssessmentRoundStateDto | null>(null);
  const [assessmentAnswerText, setAssessmentAnswerText] = useState('');
  const [assessmentSubmitting, setAssessmentSubmitting] = useState(false);
  const [assessmentLastResult, setAssessmentLastResult] = useState<SubmitAssessmentAnswerResponse | null>(null);
  const [completing, setCompleting] = useState(false);
  const [newlyUnlocked, setNewlyUnlocked] = useState<GraphNodeInfoDto[] | null>(null);
  // Memory Paradox reward pause: after a correct Recall attempt or a
  // Production submission, hold the already-fetched next step here and show
  // a small celebratory banner instead of jumping straight to the next step
  // — this gives the dopamine-driven "neurons that fire together, wire
  // together" moment a beat to land before moving on.
  const [pendingAdvance, setPendingAdvance] = useState<{ nextStep: BackendRuntimeStep; title: string } | null>(null);

  const resetStepUiState = useCallback((next: NormalizedStep) => {
    setStep(next);
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
      setNodeSummary(null);

      // Resolve this node's own state (Locked/Available/Mastered) from the
      // Capability Graph first — a Mastered node gets a read-only recap of
      // its last completed attempt instead of silently starting a brand-new
      // one. If this fetch fails, fall through to the normal session flow
      // below rather than blocking the page (matches this fetch's prior
      // tolerant behavior, when it only affected the header title).
      let isMastered = false;
      try {
        const graph = await getCapabilityGraph(capabilityId!, MOCK_USER.oid);
        if (cancelled) return;
        const match = graph.Nodes.find((n) => n.CapabilityGraphNodeId === nodeId);
        if (match) {
          setNodeInfo({ name: match.Name, description: match.Description });
          isMastered = match.State === 'Mastered';
        }
      } catch {
        // Non-critical for the header — the workflow still works without a title.
      }

      if (isMastered) {
        try {
          const summary = await getNodeSummary(MOCK_USER.oid, nodeId!);
          if (cancelled) return;
          setNodeSummary(summary);
          setPhase('summary');
          return;
        } catch {
          // Fall through to the normal workflow below — better to let the
          // student practice than to hard-block them on a summary bug.
        }
      }

      try {
        // A person can have an active in-progress session for a DIFFERENT
        // node of this same capability (e.g. left mid-Teaching on node A,
        // then clicked into node B from the map). getActiveSession is only
        // scoped by person+capability, so it must NOT be reused unless it
        // actually belongs to the node the user is opening right now —
        // otherwise this page silently renders the wrong node's content
        // while the URL still shows the newly-clicked node's id.
        const active = await getActiveSession(MOCK_USER.oid, capabilityId!);
        const info =
          active && active.CapabilityGraphNodeId === nodeId
            ? active
            : await startSession(MOCK_USER.oid, capabilityId!, nodeId!);
        if (cancelled) return;
        setSessionNodeId(info.LearningSessionNodeId);
        resetStepUiState(normalizeSessionInfo(info));
        setPhase('ready');
      } catch {
        if (!cancelled) {
          setErrorMessage(t.nodeError);
          setPhase('error');
        }
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [capabilityId, nodeId, resetStepUiState, t.nodeError]);

  const handlePracticeAgain = useCallback(async () => {
    if (!capabilityId || !nodeId) return;
    setPracticingAgain(true);
    setErrorMessage(null);
    try {
      const info = await startSession(MOCK_USER.oid, capabilityId, nodeId);
      setSessionNodeId(info.LearningSessionNodeId);
      resetStepUiState(normalizeSessionInfo(info));
      setNodeSummary(null);
      setPhase('ready');
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setPracticingAgain(false);
    }
  }, [capabilityId, nodeId, resetStepUiState, t.nodeError]);

  const handleContinue = useCallback(async () => {
    if (!step || !sessionNodeId) return;
    setSubmitting(true);
    try {
      if (responseText.trim()) {
        await submitStepResponse(step.learningSessionStepId, responseText.trim());
      }
      const nextStep = await advanceStep(sessionNodeId);
      resetStepUiState(normalizeStep(nextStep));
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setSubmitting(false);
    }
  }, [step, sessionNodeId, responseText, resetStepUiState, t.nodeError]);

  const handlePendingAdvanceContinue = useCallback(() => {
    if (!pendingAdvance) return;
    resetStepUiState(normalizeStep(pendingAdvance.nextStep));
  }, [pendingAdvance, resetStepUiState]);

  const handleRecallReviewContinue = useCallback(() => {
    if (!recallReview) return;
    resetStepUiState(normalizeStep(recallReview.nextStep));
  }, [recallReview, resetStepUiState]);

  // Clicking a completed (green) step in the stepper opens a READ-ONLY
  // recap of what the student saw/answered — never restarts or reactivates
  // anything (see InstructorRuntimeOrchestrator.GetStepReviewAsync).
  const [stepReview, setStepReview] = useState<{
    stepType: ExperienceStepType;
    data: StepReviewDto | null;
    loading: boolean;
    error: boolean;
  } | null>(null);

  const handleStepIconClick = useCallback(
    (stepType: ExperienceStepType) => {
      if (!sessionNodeId) return;
      setStepReview({ stepType, data: null, loading: true, error: false });
      getStepReview(sessionNodeId, stepType)
        .then((data) => setStepReview({ stepType, data, loading: false, error: false }))
        .catch(() => setStepReview({ stepType, data: null, loading: false, error: true }));
    },
    [sessionNodeId]
  );

  const closeStepReview = useCallback(() => setStepReview(null), []);

  // Production ("Aplícalo") formative grading: submit for real AI
  // evaluation. No attempt cap — an incorrect verdict just clears the
  // evaluation and lets the student edit/resubmit their response; a
  // correct verdict shows a reward banner whose Continue button advances
  // the step via handleProductionContinue below.
  const handleProductionSubmit = useCallback(async () => {
    if (!step || !responseText.trim()) return;
    setProductionSubmitting(true);
    try {
      const outcome = await evaluateProduction(step.learningSessionStepId, responseText.trim());
      setProductionEvaluation(outcome);
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setProductionSubmitting(false);
    }
  }, [step, responseText, t.nodeError]);

  const handleProductionRetry = useCallback(() => {
    setProductionEvaluation(null);
    setResponseText('');
  }, []);

  const handleProductionContinue = useCallback(async () => {
    if (!sessionNodeId) return;
    setSubmitting(true);
    try {
      const nextStep = await advanceStep(sessionNodeId);
      resetStepUiState(normalizeStep(nextStep));
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setSubmitting(false);
    }
  }, [sessionNodeId, resetStepUiState, t.nodeError]);

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
      setTutorMessages((messages) => [...messages, { author: 'tutor', text: t.tutorError }]);
    } finally {
      setTutorLoading(false);
    }
  }, [step, tutorInput, t.tutorError]);

  const handleRecallSubmit = useCallback(async () => {
    if (!step || !responseText.trim()) return;
    setSubmitting(true);
    try {
      const outcome = await submitRecallAttempt(step.learningSessionStepId, responseText.trim(), recallLastPrompt);
      setResponseText('');
      if (outcome.Advanced && outcome.NextStep) {
        // All 3 items mastered — reward pause instead of an instant jump
        // to the next step.
        setPendingAdvance({ nextStep: outcome.NextStep, title: t.recallRewardTitle });
      } else if (outcome.RegressedToTeaching && outcome.NextStep) {
        // Exhausted the attempt budget on this item without mastering it —
        // the node regressed back to Teaching. Never show the "reward"
        // message here (that was the bug: attempt 5 could be wrong yet
        // still say "¡Genial!").
        setRecallReview({ nextStep: outcome.NextStep, title: t.recallReviewTitle });
      } else {
        setRecallAttemptsUsed(outcome.AttemptsUsedForItem);
        setRecallItemsMastered(outcome.ItemsMastered);
        setRecallLastPrompt(outcome.TutorTurn.Message);
      }
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setSubmitting(false);
    }
  }, [step, responseText, recallLastPrompt, resetStepUiState, t.nodeError, t.recallRewardTitle, t.recallReviewTitle]);

  // Adaptive Assessment: the first time this node's step becomes
  // Assessment, resume any in-progress round or start a brand-new one.
  useEffect(() => {
    if (!sessionNodeId || step?.stepType !== 'Assessment' || assessmentRound !== null) return;
    let cancelled = false;

    (async () => {
      try {
        const active = await getActiveAssessmentRound(sessionNodeId);
        const round = active ?? (await startAssessmentRound(sessionNodeId));
        if (!cancelled) setAssessmentRound(round);
      } catch {
        if (!cancelled) setErrorMessage(t.nodeError);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [sessionNodeId, step, assessmentRound, t.nodeError]);

  const handleAssessmentSubmitAnswer = useCallback(async () => {
    if (!assessmentRound?.CurrentQuestion || !assessmentAnswerText.trim()) return;
    setAssessmentSubmitting(true);
    try {
      const result = await submitAssessmentAnswer(assessmentRound.CurrentQuestion.AssessmentQuestionId, assessmentAnswerText.trim());
      setAssessmentLastResult(result);
      setAssessmentAnswerText('');
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setAssessmentSubmitting(false);
    }
  }, [assessmentRound, assessmentAnswerText, t.nodeError]);

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
    try {
      const result = await completeNode(sessionNodeId);
      setNewlyUnlocked(result.newlyUnlockedNodes);
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setCompleting(false);
    }
  }, [sessionNodeId, t.nodeError]);

  if (phase === 'loading') {
    return (
      <div className="mx-auto max-w-3xl p-8">
        <Link to={`/capabilities/${capabilityId}`} className="text-sm text-slate-500 hover:underline">
          ← {t.backToMap}
        </Link>
        <p className="mt-6 text-slate-500">{t.nodeLoading}</p>
      </div>
    );
  }

  if (phase === 'summary' && nodeSummary) {
    return (
      <div className="mx-auto max-w-3xl p-4 sm:p-8">
        <Link to={`/capabilities/${capabilityId}`} className="text-sm text-slate-500 hover:underline">
          ← {t.backToMap}
        </Link>
        <NodeSummaryView
          nodeInfo={nodeInfo}
          summary={nodeSummary}
          onPracticeAgain={handlePracticeAgain}
          practicing={practicingAgain}
          t={t}
          language={language}
        />
      </div>
    );
  }

  if (phase === 'error' || !step) {
    return (
      <div className="mx-auto max-w-3xl p-8">
        <Link to={`/capabilities/${capabilityId}`} className="text-sm text-slate-500 hover:underline">
          ← {t.backToMap}
        </Link>
        <p className="mt-6 text-red-600">{errorMessage ?? t.nodeError}</p>
      </div>
    );
  }

  if (newlyUnlocked !== null) {
    return (
      <div className="mx-auto max-w-2xl p-8">
        <div className="rounded-2xl border border-green-200 bg-green-50 p-8 text-center">
          <Check className="mx-auto h-10 w-10 text-green-600" />
          <h1 className="mt-3 text-xl font-semibold text-green-800">{t.completedBannerTitle}</h1>
          {newlyUnlocked.length > 0 && (
            <div className="mt-4 text-sm text-green-700">
              <p className="font-medium">{t.newlyUnlockedTitle}</p>
              <ul className="mt-1 space-y-1">
                {newlyUnlocked.map((n) => (
                  <li key={n.CapabilityGraphNodeId}>{n.Name}</li>
                ))}
              </ul>
            </div>
          )}
          <button
            onClick={() => navigate(`/capabilities/${capabilityId}`)}
            className="mt-6 rounded-lg bg-green-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-green-700"
          >
            {t.backToMapButton}
          </button>
        </div>
      </div>
    );
  }

  const currentIndex = STEP_ORDER.indexOf(step.stepType);
  const showsTutorChat = step.stepType === 'Teaching' || step.stepType === 'Production';

  return (
    <div className="mx-auto max-w-3xl p-4 sm:p-8">
      <Link to={`/capabilities/${capabilityId}`} className="text-sm text-slate-500 hover:underline">
        ← {t.backToMap}
      </Link>

      {nodeInfo && (
        <div className="mt-4 rounded-2xl border border-slate-200 bg-white p-5">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <h1 className="text-xl font-semibold text-slate-900">{nodeInfo.name}</h1>
            <span className="whitespace-nowrap text-xs text-slate-400">
              {t.nodeStartTimeLabel}: {startTime.toLocaleTimeString(language, { hour: '2-digit', minute: '2-digit' })}
            </span>
          </div>
          {nodeInfo.description && (
            <p className="mt-2 text-sm leading-relaxed text-slate-600">
              <span className="font-medium text-slate-500">{t.nodeObjectiveLabel}: </span>
              {nodeInfo.description}
            </p>
          )}
        </div>
      )}

      <StepperBar currentIndex={currentIndex} t={t} onStepClick={handleStepIconClick} />

      {stepReview && (
        <StepReviewModal review={stepReview} onClose={closeStepReview} t={t} language={language} />
      )}

      <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-6">
        {errorMessage && <p className="mb-4 text-sm text-red-600">{errorMessage}</p>}

        {/* Recall's content is shown via recallLastPrompt below, and
            Assessment's content is an internal grading rubric ("Criterios
            observables de dominio...") never meant for the student — the
            actual questions come from AssessmentPanel's round state. */}
        {step.stepType !== 'Recall' && step.stepType !== 'Assessment' && (
          <p className="whitespace-pre-wrap text-slate-700">{step.content}</p>
        )}

        {step.illustrations.length > 0 && (
          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
            {step.illustrations.map((illustration) => (
              <figure
                key={illustration.illustrationId}
                className="overflow-hidden rounded-lg border border-slate-200 bg-slate-50"
              >
                <img
                  src={`${API_BASE_URL}/illustrations/${illustration.illustrationId}/image`}
                  alt={illustration.caption ?? ''}
                  className="h-64 w-full object-contain sm:h-80"
                />
                {illustration.caption && (
                  <figcaption className="border-t border-slate-200 bg-white px-3 py-1.5 text-xs text-slate-500">
                    {illustration.caption}
                  </figcaption>
                )}
              </figure>
            ))}
          </div>
        )}

        {showsTutorChat && (
          <div className="mt-6 rounded-xl border border-slate-200 bg-slate-50 p-4">
            <p className="mb-3 flex items-center gap-1.5 text-xs text-slate-500">
              <MessageCircle className="h-3.5 w-3.5" /> {t.tutorGuidesNotGrades}
            </p>
            <TutorChat messages={tutorMessages} loading={tutorLoading} />
            <div className="mt-3 flex gap-2">
              <input
                value={tutorInput}
                onChange={(e) => setTutorInput(e.target.value)}
                placeholder={t.askTutorPlaceholder}
                className="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-400 focus:outline-none"
                onKeyDown={(e) => e.key === 'Enter' && handleAskTutor()}
              />
              <button
                onClick={handleAskTutor}
                disabled={tutorLoading || !tutorInput.trim()}
                className="flex items-center gap-1 rounded-lg bg-slate-700 px-3 py-2 text-sm text-white hover:bg-slate-800 disabled:opacity-50"
              >
                <Send className="h-4 w-4" /> {t.askTutorButton}
              </button>
            </div>
          </div>
        )}

        {/* Hypothesis — a real question is implied (predict the result).
            NOT graded by the backend (SubmitResponseAsync only persists
            evidence, never evaluates it) — just a plain advance. */}
        {step.stepType === 'Hypothesis' && (
          <div className="mt-6">
            <label className="mb-1 block text-sm font-medium text-slate-700">{t.yourResponseLabel}</label>
            <textarea
              value={responseText}
              onChange={(e) => setResponseText(e.target.value)}
              rows={4}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-400 focus:outline-none"
            />
            <button
              onClick={handleContinue}
              disabled={submitting || !responseText.trim()}
              className="mt-3 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {t.continueButton}
            </button>
          </div>
        )}

        {/* Production ("Aplícalo") — real, formative AI grading via
            ProductionEvaluatorAgent (never affects node mastery/unlocking,
            that stays Assessment's job). No attempt cap: an incorrect
            verdict just shows WHY and lets the student edit/retry as many
            times as they want; a correct verdict shows a reward banner
            whose Continue advances to Assessment. */}
        {step.stepType === 'Production' && (
          productionEvaluation?.IsCorrect ? (
            <RewardBanner title={t.productionCorrectTitle} onContinue={handleProductionContinue} t={t}>
              <p className="mt-2 text-sm text-amber-700">{productionEvaluation.Feedback}</p>
            </RewardBanner>
          ) : (
            <div className="mt-6">
              <label className="mb-1 block text-sm font-medium text-slate-700">{t.yourResponseLabel}</label>
              <textarea
                value={responseText}
                onChange={(e) => setResponseText(e.target.value)}
                rows={4}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-400 focus:outline-none"
              />
              {productionEvaluation && !productionEvaluation.IsCorrect && (
                <div className="mt-3 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
                  {productionEvaluation.Feedback}
                </div>
              )}
              <button
                onClick={productionEvaluation ? handleProductionRetry : handleProductionSubmit}
                disabled={productionSubmitting || (!productionEvaluation && !responseText.trim())}
                className="mt-3 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {productionSubmitting
                  ? t.productionEvaluating
                  : productionEvaluation
                    ? t.productionRetryButton
                    : t.productionSubmitButton}
              </button>
            </div>
          )
        )}

        {/* Teaching — purely instructional, there is no question to answer.
            A note is optional (e.g. for the student's own reference); the
            student can move on as soon as they feel ready, without being
            forced to type a throwaway answer just to unlock the button. */}
        {step.stepType === 'Teaching' && (
          <div className="mt-6">
            <label className="mb-1 block text-sm font-medium text-slate-700">{t.teachingNotesLabel}</label>
            <textarea
              value={responseText}
              onChange={(e) => setResponseText(e.target.value)}
              rows={3}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-400 focus:outline-none"
            />
            <button
              onClick={handleContinue}
              disabled={submitting}
              className="mt-3 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {t.continueWhenReadyButton}
            </button>
          </div>
        )}

        {/* Recall — a fresh, concretely different retrieval question each
            attempt (never the same one twice), scored via
            TutorSubmitRecallAttempt. The student must master
            RECALL_ITEMS_REQUIRED distinct items (each with its own
            up-to-RECALL_MAX_ATTEMPTS budget) before the step advances. A
            reward pause plays once all items are mastered; a review
            banner sends the student back to Teaching if they exhaust an
            item's attempts without mastering it. */}
        {step.stepType === 'Recall' && (
          pendingAdvance ? (
            <RewardBanner title={pendingAdvance.title} onContinue={handlePendingAdvanceContinue} t={t} />
          ) : recallReview ? (
            <ReviewBanner title={recallReview.title} onContinue={handleRecallReviewContinue} t={t} />
          ) : (
            <div className="mt-6">
              <p className="text-xs font-medium uppercase tracking-wide text-slate-400">
                {t.recallItemsMasteredLabel} {recallItemsMastered}/{RECALL_ITEMS_REQUIRED} ·{' '}
                {t.recallAttemptLabel} {recallAttemptsUsed + 1} {t.assessmentOfLabel} {RECALL_MAX_ATTEMPTS}
              </p>
              <p className="mt-2 whitespace-pre-wrap text-slate-700">{recallLastPrompt ?? step.content}</p>

              <label className="mb-1 mt-4 block text-sm font-medium text-slate-700">{t.yourResponseLabel}</label>
              <textarea
                value={responseText}
                onChange={(e) => setResponseText(e.target.value)}
                rows={3}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-400 focus:outline-none"
              />
              <button
                onClick={handleRecallSubmit}
                disabled={submitting || !responseText.trim()}
                className="mt-3 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {t.recallSubmit}
              </button>
            </div>
          )
        )}

        {/* Assessment — dynamic, one question at a time (5 per round). A
            Failed round auto-starts a brand-new round with 5 new questions. */}
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
            t={t}
          />
        )}
      </div>
    </div>
  );
}

const STEP_ICONS: Record<ExperienceStepType, typeof Lightbulb> = {
  Hypothesis: Lightbulb,
  Teaching: BookOpen,
  Recall: RotateCcw,
  Production: Hammer,
  Assessment: ClipboardCheck,
};

function StepperBar({
  currentIndex,
  t,
  onStepClick,
}: {
  currentIndex: number;
  t: Record<string, string>;
  onStepClick: (stepType: ExperienceStepType) => void;
}) {
  const labels: Record<ExperienceStepType, string> = {
    Hypothesis: t.stepHypothesis,
    Teaching: t.stepTeaching,
    Recall: t.stepRecall,
    Production: t.stepProduction,
    Assessment: t.stepAssessment,
  };

  return (
    <div className="mt-4 flex items-center">
      {STEP_ORDER.map((stepType, index) => {
        const Icon = STEP_ICONS[stepType];
        const isDone = index < currentIndex;
        const isActive = index === currentIndex;
        return (
          <div key={stepType} className="flex flex-1 items-center">
            <div className="flex flex-col items-center gap-1">
              {isDone ? (
                <button
                  type="button"
                  onClick={() => onStepClick(stepType)}
                  title={t.stepReviewHint}
                  className="flex h-9 w-9 items-center justify-center rounded-full border-2 border-green-500 bg-green-500 text-white transition hover:brightness-110"
                >
                  <Check className="h-4 w-4" />
                </button>
              ) : (
                <div
                  className={`flex h-9 w-9 items-center justify-center rounded-full border-2 ${
                    isActive
                      ? 'border-blue-500 bg-blue-50 text-blue-600'
                      : 'border-slate-300 bg-white text-slate-400'
                  }`}
                >
                  <Icon className="h-4 w-4" />
                </div>
              )}
              <span
                className={`text-[11px] font-medium ${isActive ? 'text-blue-700' : isDone ? 'text-green-700' : 'text-slate-400'}`}
              >
                {labels[stepType]}
              </span>
            </div>
            {index < STEP_ORDER.length - 1 && (
              <div className={`mx-1 h-0.5 flex-1 ${index < currentIndex ? 'bg-green-400' : 'bg-slate-200'}`} />
            )}
          </div>
        );
      })}
    </div>
  );
}

/**
 * Shared body content for a step's read-only recap — the blueprint's
 * Content plus the student's past Evidence entries for that step. Used by
 * both StepReviewModal (one step at a time, clicked from the stepper) and
 * NodeSummaryView (all 5 steps at once, for a Mastered node's recap).
 */
function StepReviewBody({ data, t, language }: { data: StepReviewDto; t: Record<string, string>; language: string }) {
  return (
    <>
      <p className="whitespace-pre-wrap text-sm leading-relaxed text-slate-700">{data.Content}</p>

      {data.Evidence.length > 0 ? (
        <div className="mt-5 space-y-3 border-t border-slate-100 pt-4">
          <p className="text-xs font-medium uppercase tracking-wide text-slate-400">{t.stepReviewYourAnswers}</p>
          {data.Evidence.map((entry, index) => (
            <div key={index} className="rounded-lg bg-slate-50 p-3">
              {entry.TutorPrompt && <p className="text-xs text-slate-500">{entry.TutorPrompt}</p>}
              <p className="mt-1 text-sm font-medium text-slate-800">{entry.StudentResponse}</p>
              <p className="mt-1 text-[11px] text-slate-400">
                {new Date(entry.CreatedDate).toLocaleString(language, { dateStyle: 'short', timeStyle: 'short' })}
                {typeof entry.TutorScore === 'number' && ` · ${t.stepReviewScoreLabel} ${entry.TutorScore}`}
              </p>
            </div>
          ))}
        </div>
      ) : (
        <p className="mt-5 border-t border-slate-100 pt-4 text-sm text-slate-400">{t.stepReviewNoAnswers}</p>
      )}
    </>
  );
}

/**
 * Read-only recap shown when the student clicks a completed (green) step
 * in the stepper — "what did I see / what did I answer here". Rendered via
 * a portal to document.body so it always overlays the full viewport
 * regardless of any scrollable/sticky ancestor (see
 * /memories/repo/chapter-review-modal-feature.md). Never touches session
 * state — purely a GET, no reactivation, no restart.
 */
function StepReviewModal({
  review,
  onClose,
  t,
  language,
}: {
  review: { stepType: ExperienceStepType; data: StepReviewDto | null; loading: boolean; error: boolean };
  onClose: () => void;
  t: Record<string, string>;
  language: string;
}) {
  const stepLabels: Record<ExperienceStepType, string> = {
    Hypothesis: t.stepHypothesis,
    Teaching: t.stepTeaching,
    Recall: t.stepRecall,
    Production: t.stepProduction,
    Assessment: t.stepAssessment,
  };

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
      <div className="max-h-[85vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-6 shadow-xl">
        <div className="mb-4 flex items-start justify-between gap-3">
          <h2 className="text-lg font-semibold text-slate-900">{stepLabels[review.stepType]}</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded-full p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {review.loading && <p className="text-sm text-slate-500">{t.stepReviewLoading}</p>}
        {review.error && <p className="text-sm text-red-600">{t.nodeError}</p>}

        {review.data && <StepReviewBody data={review.data} t={t} language={language} />}
      </div>
    </div>,
    document.body
  );
}

/**
 * Read-only recap shown when opening a node that is already Mastered on
 * the map — "what happened the last time I completed this" instead of
 * silently starting a brand-new attempt (product decision, 2026-07-19).
 * "Practicar de nuevo" stays an explicit, separate action that creates a
 * new attempt, preserving this one's history forever.
 */
function NodeSummaryView({
  nodeInfo,
  summary,
  onPracticeAgain,
  practicing,
  t,
  language,
}: {
  nodeInfo: { name: string; description?: string } | null;
  summary: NodeSummaryDto;
  onPracticeAgain: () => void;
  practicing: boolean;
  t: Record<string, string>;
  language: string;
}) {
  const stepLabels: Record<ExperienceStepType, string> = {
    Hypothesis: t.stepHypothesis,
    Teaching: t.stepTeaching,
    Recall: t.stepRecall,
    Production: t.stepProduction,
    Assessment: t.stepAssessment,
  };

  // Which attempt's 5-step detail is currently shown below — defaults to
  // the most recent one (already included in `summary.Steps`, no fetch
  // needed). Picking an older row in PastAttempts fetches that attempt's
  // own steps on demand via getStepReview (same read-only endpoint used by
  // the stepper's per-step review modal).
  const [viewedAttemptId, setViewedAttemptId] = useState(summary.LearningSessionNodeId);
  const [viewedSteps, setViewedSteps] = useState<StepReviewDto[]>(summary.Steps);
  const [loadingAttempt, setLoadingAttempt] = useState(false);

  useEffect(() => {
    setViewedAttemptId(summary.LearningSessionNodeId);
    setViewedSteps(summary.Steps);
  }, [summary]);

  const handleSelectAttempt = useCallback(
    async (attempt: NodeAttemptSummaryDto) => {
      if (attempt.LearningSessionNodeId === viewedAttemptId) return;
      setViewedAttemptId(attempt.LearningSessionNodeId);
      setLoadingAttempt(true);
      try {
        const steps = await Promise.all(
          STEP_ORDER.map((stepType) => getStepReview(attempt.LearningSessionNodeId, stepType).catch(() => null))
        );
        setViewedSteps(steps.filter((s): s is StepReviewDto => s !== null));
      } finally {
        setLoadingAttempt(false);
      }
    },
    [viewedAttemptId]
  );

  return (
    <div className="mt-4 space-y-6">
      <div className="rounded-2xl border border-green-200 bg-green-50 p-6">
        <div className="flex items-center gap-2 text-green-700">
          <Check className="h-5 w-5" />
          <h1 className="text-lg font-semibold">{nodeInfo?.name ?? t.nodeSummaryTitle}</h1>
        </div>
        {nodeInfo?.description && <p className="mt-2 text-sm text-green-800">{nodeInfo.description}</p>}

        <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <SummaryStat label={t.nodeSummaryAttempts} value={String(summary.AttemptCount)} />
          <SummaryStat
            label={t.nodeSummaryScore}
            value={typeof summary.FinalScore === 'number' ? `${summary.FinalScore}/100` : '—'}
          />
          <SummaryStat
            label={t.nodeSummaryFirstCompleted}
            value={
              summary.FirstCompletedDate
                ? new Date(summary.FirstCompletedDate).toLocaleDateString(language, { dateStyle: 'medium' })
                : '—'
            }
          />
          <SummaryStat
            label={t.nodeSummaryLastCompleted}
            value={
              summary.LastCompletedDate
                ? new Date(summary.LastCompletedDate).toLocaleDateString(language, { dateStyle: 'medium' })
                : '—'
            }
          />
        </div>

        {summary.PastAttempts.length > 1 && (
          <div className="mt-5 border-t border-green-100 pt-4">
            <p className="text-xs font-medium uppercase tracking-wide text-green-600">{t.nodeSummaryPastAttempts}</p>
            <div className="mt-2 flex flex-wrap gap-2">
              {summary.PastAttempts.map((attempt, index) => {
                const isSelected = attempt.LearningSessionNodeId === viewedAttemptId;
                const label = attempt.CompletedDate
                  ? new Date(attempt.CompletedDate).toLocaleDateString(language, { dateStyle: 'medium' })
                  : `#${summary.PastAttempts.length - index}`;
                return (
                  <button
                    key={attempt.LearningSessionNodeId}
                    type="button"
                    onClick={() => handleSelectAttempt(attempt)}
                    className={`rounded-full border px-3 py-1.5 text-xs font-medium transition ${
                      isSelected
                        ? 'border-green-600 bg-green-600 text-white'
                        : 'border-green-300 bg-white text-green-700 hover:bg-green-100'
                    }`}
                  >
                    {label}
                    {typeof attempt.FinalScore === 'number' && ` · ${attempt.FinalScore}/100`}
                  </button>
                );
              })}
            </div>
          </div>
        )}

        <button
          onClick={onPracticeAgain}
          disabled={practicing}
          className="mt-5 rounded-lg bg-green-700 px-5 py-2.5 text-sm font-medium text-white hover:bg-green-800 disabled:opacity-50"
        >
          {practicing ? t.nodeSummaryPracticing : t.nodeSummaryPracticeAgain}
        </button>
      </div>

      <div className="space-y-4">
        {loadingAttempt && <p className="text-sm text-slate-400">{t.stepReviewLoading}</p>}
        {!loadingAttempt &&
          viewedSteps.map((stepData) => {
            const Icon = STEP_ICONS[stepData.StepType];
            return (
              <div key={stepData.StepType} className="rounded-2xl border border-slate-200 bg-white p-5">
                <div className="mb-3 flex items-center gap-2">
                  <Icon className="h-4 w-4 text-slate-500" />
                  <h2 className="text-sm font-semibold text-slate-800">{stepLabels[stepData.StepType]}</h2>
                </div>
                <StepReviewBody data={stepData} t={t} language={language} />
              </div>
            );
          })}
      </div>
    </div>
  );
}

function SummaryStat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-[11px] uppercase tracking-wide text-green-600">{label}</p>
      <p className="text-sm font-semibold text-green-900">{value}</p>
    </div>
  );
}

/**
 * Memory Paradox reward pause: a brief celebratory moment (star + message)
 * shown on a correct Recall attempt or a Production submission, BEFORE
 * actually moving on to the next step. Gives the dopamine hit a beat to
 * land instead of an instant jump straight to the next step's content.
 */
function RewardBanner({
  title,
  onContinue,
  t,
  children,
}: {
  title: string;
  onContinue: () => void;
  t: Record<string, string>;
  children?: ReactNode;
}) {
  return (
    <div className="mt-6 rounded-xl border border-amber-200 bg-amber-50 p-6 text-center">
      <Star className="mx-auto h-9 w-9 text-amber-500" fill="currentColor" />
      <p className="mt-2 text-base font-semibold text-amber-800">{title}</p>
      {children}
      <button
        onClick={onContinue}
        className="mt-4 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700"
      >
        {t.continueButton}
      </button>
    </div>
  );
}

/**
 * Recall regression banner: shown when a student exhausts their attempts
 * on one Recall item without mastering it. Distinct from RewardBanner
 * (BookOpen instead of Star, calm blue instead of amber) — this is a
 * friendly "let's review together" moment, not a celebration, and the
 * Continue button sends the student back into the Teaching step instead
 * of forward.
 */
function ReviewBanner({
  title,
  onContinue,
  t,
}: {
  title: string;
  onContinue: () => void;
  t: Record<string, string>;
}) {
  return (
    <div className="mt-6 rounded-xl border border-blue-200 bg-blue-50 p-6 text-center">
      <BookOpen className="mx-auto h-9 w-9 text-blue-500" />
      <p className="mt-2 text-base font-semibold text-blue-800">{title}</p>
      <button
        onClick={onContinue}
        className="mt-4 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700"
      >
        {t.continueButton}
      </button>
    </div>
  );
}

function TutorChat({ messages, loading }: { messages: TutorChatMessage[]; loading: boolean }) {
  if (messages.length === 0 && !loading) return null;
  return (
    <div className="max-h-56 space-y-2 overflow-y-auto">
      {messages.map((message, index) => (
        <div
          key={index}
          className={`rounded-lg px-3 py-2 text-sm ${
            message.author === 'student' ? 'ml-8 bg-blue-100 text-blue-900' : 'mr-8 bg-white text-slate-700 shadow-sm'
          }`}
        >
          {message.text}
        </div>
      ))}
      {loading && <p className="mr-8 rounded-lg bg-white px-3 py-2 text-sm italic text-slate-400 shadow-sm">…</p>}
    </div>
  );
}

const CORRECTNESS_STYLES: Record<string, string> = {
  Correct: 'border-green-200 bg-green-50 text-green-800',
  PartiallyCorrect: 'border-amber-200 bg-amber-50 text-amber-800',
  Incorrect: 'border-red-200 bg-red-50 text-red-800',
};

function correctnessLabel(correctness: string, t: Record<string, string>): string {
  if (correctness === 'Correct') return t.assessmentCorrect;
  if (correctness === 'PartiallyCorrect') return t.assessmentPartiallyCorrect;
  return t.assessmentIncorrect;
}

interface AssessmentPanelProps {
  round: AssessmentRoundStateDto | null;
  answerText: string;
  onAnswerTextChange: (value: string) => void;
  submitting: boolean;
  lastResult: SubmitAssessmentAnswerResponse | null;
  onSubmitAnswer: () => void;
  onContinue: () => void;
  onCompleteNode: () => void;
  completing: boolean;
  t: Record<string, string>;
}

/**
 * Dynamic Assessment UI: exactly 5 AI-generated questions per round, asked
 * ONE AT A TIME. A Failed round (final score &lt; 80) auto-starts a
 * brand-new round with 5 new questions targeting the errors observed —
 * never the same questions again.
 */
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
  t,
}: AssessmentPanelProps) {
  if (!round) {
    return <p className="mt-6 text-sm text-slate-500">{t.assessmentLoadingRound}</p>;
  }

  // A question was just graded — show the grade, then either the next
  // question, the new round's first question, or the final pass/fail state.
  if (lastResult) {
    const style = CORRECTNESS_STYLES[lastResult.Grade.Correctness] ?? CORRECTNESS_STYLES.Incorrect;

    if (lastResult.RoundComplete) {
      const passed = lastResult.Passed ?? false;
      return (
        <div className="mt-6">
          <div className={`rounded-xl border p-4 ${passed ? 'border-green-200 bg-green-50' : 'border-amber-200 bg-amber-50'}`}>
            <p className={`text-sm font-medium ${passed ? 'text-green-800' : 'text-amber-800'}`}>
              {passed ? t.assessmentPassed : t.assessmentFailed}
            </p>
            <p className="mt-1 text-sm text-slate-600">
              {t.assessmentFinalScoreLabel}: {lastResult.FinalScore}/100
            </p>
            <p className="mt-2 text-xs leading-relaxed text-slate-500">{t.assessmentScoreExplanation}</p>
            {!passed && <p className="mt-2 text-sm text-amber-700">{t.assessmentRoundFailedMessage}</p>}
          </div>

          {passed ? (
            <button
              onClick={onCompleteNode}
              disabled={completing}
              className="mt-4 rounded-lg bg-green-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
            >
              {t.completeNodeButton}
            </button>
          ) : (
            <button
              onClick={onContinue}
              className="mt-4 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700"
            >
              {t.assessmentStartNewRound}
            </button>
          )}
        </div>
      );
    }

    return (
      <div className="mt-6">
        <div className={`rounded-xl border p-4 ${style}`}>
          <p className="flex items-center gap-1.5 text-sm font-medium">
            {lastResult.Grade.Correctness === 'Correct' && <Star className="h-4 w-4 text-amber-500" fill="currentColor" />}
            {correctnessLabel(lastResult.Grade.Correctness, t)}
          </p>
          <p className="mt-1 text-sm text-slate-600">{lastResult.Grade.Feedback}</p>
        </div>
        <button
          onClick={onContinue}
          className="mt-4 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700"
        >
          {t.assessmentNextQuestion}
        </button>
      </div>
    );
  }

  // Resumed into an already-resolved round with nothing left to answer
  // (e.g. reloaded right after passing, before completing the node).
  if (!round.CurrentQuestion) {
    const passed = round.Status === 'Passed';
    return (
      <div className="mt-6">
        <div className={`rounded-xl border p-4 ${passed ? 'border-green-200 bg-green-50' : 'border-amber-200 bg-amber-50'}`}>
          <p className={`text-sm font-medium ${passed ? 'text-green-800' : 'text-amber-800'}`}>
            {passed ? t.assessmentRoundPassedMessage : t.assessmentFailed}
          </p>
          {round.FinalScore !== null && round.FinalScore !== undefined && (
            <>
              <p className="mt-1 text-sm text-slate-600">
                {t.assessmentFinalScoreLabel}: {round.FinalScore}/100
              </p>
              <p className="mt-2 text-xs leading-relaxed text-slate-500">{t.assessmentScoreExplanation}</p>
            </>
          )}
        </div>
        {passed && (
          <button
            onClick={onCompleteNode}
            disabled={completing}
            className="mt-4 rounded-lg bg-green-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
          >
            {t.completeNodeButton}
          </button>
        )}
      </div>
    );
  }

  const question = round.CurrentQuestion;
  return (
    <div className="mt-6">
      <p className="text-xs font-medium uppercase tracking-wide text-slate-400">
        {t.assessmentRoundNumberLabel} {round.RoundNumber} · {t.assessmentQuestionLabel} {question.QuestionIndex} {t.assessmentOfLabel}{' '}
        {round.TotalQuestions}
      </p>
      <p className="mt-2 whitespace-pre-wrap text-slate-800">{question.QuestionText}</p>
      <label className="mb-1 mt-4 block text-sm font-medium text-slate-700">{t.yourResponseLabel}</label>
      <textarea
        value={answerText}
        onChange={(e) => onAnswerTextChange(e.target.value)}
        rows={4}
        className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-400 focus:outline-none"
      />
      <button
        onClick={onSubmitAnswer}
        disabled={submitting || !answerText.trim()}
        className="mt-3 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
      >
        {t.assessmentSubmitAnswer}
      </button>
    </div>
  );
}
