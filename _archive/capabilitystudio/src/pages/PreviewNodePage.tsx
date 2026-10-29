import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router-dom';
import DOMPurify from 'dompurify';
import { Lightbulb, BookOpen, RotateCcw, Hammer, ClipboardCheck, Check, Send, MessageCircle, Images, X } from 'lucide-react';
import { apiImageUrl } from '../lib/api/httpClient';
import {
  getCapabilityGraph,
  getActiveSession,
  startSession,
  submitStepResponse,
  advanceStep,
  askTutor,
  submitRecallAttempt,
  submitDeepDiveAttempt,
  evaluateProduction,
  completeNode,
  getActiveAssessmentRound,
  startAssessmentRound,
  submitAssessmentAnswer,
  getNodeBlueprint,
  getNodeSummary,
  getStepReview,
  forceAdvanceStep,
  PREVIEW_PERSON_ID,
  type ExperienceStepType,
  type BackendRuntimeStep,
  type BackendRuntimeSessionInfo,
  type ExampleCardDto,
  type TutorMode,
  type GraphNodeInfoDto,
  type AssessmentRoundStateDto,
  type SubmitAssessmentAnswerResponse,
  type EvaluateProductionResponseDto,
  type NodeBlueprintDto,
  type NodeSummaryDto,
  type StepReviewDto,
  type NodeAttemptSummaryDto,
} from '../lib/api/runtimeApi';
import LoadingSpinner from '../components/LoadingSpinner';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import { usePreviewMode, useAutoApprove, type PreviewMode } from '../lib/previewMode';
import { getGeneratedByLabel } from '../lib/illustrationLabel';
import PreviewNodeBlueprintView from './PreviewNodeBlueprintView';

DOMPurify.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A') {
    node.setAttribute('target', '_blank');
    node.setAttribute('rel', 'noopener noreferrer');
  }
});

const STEP_ORDER: ExperienceStepType[] = ['Hypothesis', 'Teaching', 'DeepDive', 'Recall', 'Production', 'Assessment'];
const RECALL_MAX_ATTEMPTS = 3;
const RECALL_ITEMS_REQUIRED = 3;

const STEP_ICONS: Record<ExperienceStepType, typeof Lightbulb> = {
  Hypothesis: Lightbulb,
  Teaching: BookOpen,
  DeepDive: Images,
  Recall: RotateCcw,
  Production: Hammer,
  Assessment: ClipboardCheck,
};

interface NormalizedStep {
  learningSessionStepId: string;
  stepType: ExperienceStepType;
  content: string;
  illustrations: { illustrationId: string; storagePath: string; caption?: string; imageModel: string }[];
  exampleCards?: ExampleCardDto[] | null;
  /** Only meaningful when stepType === 'DeepDive' — lets a page reload resume exactly where the student left off, card-for-card. */
  currentDeepDiveCardIndex?: number;
  deepDiveTotalCards?: number;
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
      imageModel: i.ImageModel,
    })),
    exampleCards: step.ExampleCards,
    currentDeepDiveCardIndex: step.CurrentDeepDiveCardIndex,
    deepDiveTotalCards: step.DeepDiveTotalCards,
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
  const { t } = useTranslation();
  const { capabilityId, nodeId } = useParams<{ capabilityId: string; nodeId: string }>();
  const navigate = useNavigate();
  const [autoApprove, setAutoApprove] = useAutoApprove();
  const [forceApproving, setForceApproving] = useState(false);

  const [phase, setPhase] = useState<'loading' | 'ready' | 'error' | 'summary'>('loading');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [nodeInfo, setNodeInfo] = useState<{ name: string; description?: string } | null>(null);
  // Read-only recap for an already-Mastered node (2026-07-30 — user
  // request: "index of the cards so the student can go back to older
  // lessons already answered for practice"). Ported from
  // frontend/human-os-web's NodeWorkflowPage/NodeSummaryView, only in
  // 'real' mode — 'demo' mode deliberately keeps its existing
  // no-locks/no-recap behavior for QA reviewers.
  const [nodeSummary, setNodeSummary] = useState<NodeSummaryDto | null>(null);
  const [practicingAgain, setPracticingAgain] = useState(false);

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

  // Deep Dive: sequential, mandatory-all-cards quiz loop (mirrors backend
  // Agentic/Runtime/DeepDiveLoopGate.cs) — unlike Recall, there's no
  // attempt cap and no distinct-N-of-pool logic, just "master this card,
  // then the next one, in order, until every card is done".
  const [deepDiveCardIndex, setDeepDiveCardIndex] = useState(0);
  const [deepDiveTotalCards, setDeepDiveTotalCards] = useState(0);
  const [deepDiveLastPrompt, setDeepDiveLastPrompt] = useState<string | undefined>(undefined);
  // BUG FIX (2026-07-30 — explicit user request: "when the answer is
  // correct let's not move to the next lesson automatically, let the
  // voice instruct the student to press the button when ready"): mastering
  // a card used to auto-advance straight to the next card's content with
  // no pause. Now mastering ANY card (not just the final one, which
  // already paused via `pendingAdvance`) pauses on a reward banner until
  // the student clicks Continue themselves.
  const [pendingCardAdvance, setPendingCardAdvance] = useState<{ nextCardIndex: number; totalCards: number } | null>(null);
  // Card index/nav (2026-07-30 — user request: "index of the cards so the
  // student can go back to older lessons already answered for practice").
  // Read-only: clicking an already-mastered card (index < deepDiveCardIndex)
  // opens this card's content in an overlay, purely for review — never
  // touches deepDiveCardIndex, deepDiveLastPrompt, or any grading state.
  const [reviewCardIndex, setReviewCardIndex] = useState<number | null>(null);

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
    setDeepDiveCardIndex(next.currentDeepDiveCardIndex ?? 0);
    setDeepDiveTotalCards(next.deepDiveTotalCards ?? next.exampleCards?.length ?? 0);
    setDeepDiveLastPrompt(undefined);
    setPendingCardAdvance(null);
    setReviewCardIndex(null);
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
      // Capability Graph first — in 'real' mode, a Mastered node gets a
      // read-only recap of its last completed attempt instead of silently
      // starting a brand-new one. If this fetch fails, fall through to the
      // normal session flow below rather than blocking the page.
      let isMastered = false;
      try {
        const graph = await getCapabilityGraph(capabilityId!, PREVIEW_PERSON_ID);
        if (cancelled) return;
        const match = graph.Nodes.find((n) => n.CapabilityGraphNodeId === nodeId);
        if (match) setNodeInfo({ name: match.Name, description: match.Description });
        isMastered = match?.State === 'Mastered';
      } catch {
        // Non-critical for the header.
      }

      if (isMastered && mode === 'real') {
        try {
          const summary = await getNodeSummary(PREVIEW_PERSON_ID, nodeId!);
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
          setErrorMessage(t('previewNode.experienceLoadErrorFallback'));
          setPhase('error');
        }
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [capabilityId, nodeId, mode, resetStepUiState]);

  const handlePracticeAgain = useCallback(async () => {
    if (!capabilityId || !nodeId) return;
    setPracticingAgain(true);
    setErrorMessage(null);
    try {
      const info = await startSession(PREVIEW_PERSON_ID, capabilityId, nodeId);
      setSessionNodeId(info.LearningSessionNodeId);
      resetStepUiState(normalizeStep(info.CurrentStep));
      setNodeSummary(null);
      setPhase('ready');
    } catch {
      setErrorMessage(t('previewNode.experienceLoadErrorFallback'));
    } finally {
      setPracticingAgain(false);
    }
  }, [capabilityId, nodeId, resetStepUiState, t]);

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
      setErrorMessage(t('previewNode.genericErrorFallback'));
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
      setErrorMessage(t('previewNode.genericErrorFallback'));
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
      setErrorMessage(t('previewNode.genericErrorFallback'));
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
      setTutorMessages((messages) => [...messages, { author: 'tutor', text: t('previewNode.tutorNoAnswerFallback') }]);
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
      setErrorMessage(t('previewNode.genericErrorFallback'));
    } finally {
      setSubmitting(false);
    }
  }, [step, responseText, recallLastPrompt]);

  // Deep Dive: score one attempt on the CURRENT card only (no attempt cap,
  // no regression path). Mastering the card advances NextCardIndex; once
  // every card is mastered in order, Advanced flips true and the same
  // "reward pause" pattern as Recall plays before moving to the next step.
  //
  // Accepts an optional `overrideText` (2026-07-30) so the Deep Dive voice
  // Agent's transcribed speech can be submitted directly — young students
  // (this feature targets 6-year-olds) can't reliably write out a full
  // sentence, so requiring them to TYPE their spoken explanation into the
  // textbox before it ever reaches real grading made the voice
  // conversation a dead end with no way to finish (reported as "an
  // infinite loop with no end"). See the onTranscript wiring below, which
  // calls this the same way "Responder"/submit does for Recall.
  const handleDeepDiveSubmit = useCallback(
    async (overrideText?: string) => {
      const text = (overrideText ?? responseText).trim();
      if (!step || !text || submitting) return;
      setSubmitting(true);
      setErrorMessage(null);
      try {
        const outcome = await submitDeepDiveAttempt(step.learningSessionStepId, text, deepDiveLastPrompt);
        setResponseText('');
        if (outcome.Advanced && outcome.NextStep) {
          setPendingAdvance({ nextStep: outcome.NextStep });
        } else if (outcome.Mastered) {
          // BUG FIX (2026-07-30 — explicit user request): mastering a card
          // used to jump straight to the next card's content automatically.
          // Now pause on a reward banner (same RewardBanner/Continue pattern
          // already used once the WHOLE step is done) until the student
          // clicks Continue themselves — see handlePendingCardAdvanceContinue,
          // which is what actually applies NextCardIndex/TotalCards and
          // clears deepDiveLastPrompt.
          setPendingCardAdvance({ nextCardIndex: outcome.NextCardIndex, totalCards: outcome.TotalCards });
        } else {
          setDeepDiveCardIndex(outcome.NextCardIndex);
          setDeepDiveTotalCards(outcome.TotalCards);
          setDeepDiveLastPrompt(outcome.TutorTurn.Message);
        }
      } catch {
        setErrorMessage(t('previewNode.genericErrorFallback'));
      } finally {
        setSubmitting(false);
      }
    },
    [step, responseText, deepDiveLastPrompt, submitting]
  );

  const handlePendingCardAdvanceContinue = useCallback(() => {
    if (!pendingCardAdvance) return;
    setDeepDiveCardIndex(pendingCardAdvance.nextCardIndex);
    setDeepDiveTotalCards(pendingCardAdvance.totalCards);
    // Same reset as the "just mastered a card" branch above — no stale
    // tutor message from the just-mastered card should show under the
    // freshly-revealed next card; the generic per-card opening prompt
    // takes over instead.
    setDeepDiveLastPrompt(undefined);
    setPendingCardAdvance(null);
  }, [pendingCardAdvance]);

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
        if (!cancelled) setErrorMessage(t('previewNode.genericErrorFallback'));
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [sessionNodeId, step, assessmentRound]);

  const handleAssessmentSubmitAnswer = useCallback(async () => {
    if (!assessmentRound?.CurrentQuestion || !assessmentAnswerText.trim() || assessmentSubmitting) return;
    setAssessmentSubmitting(true);
    setErrorMessage(null);
    try {
      const result = await submitAssessmentAnswer(assessmentRound.CurrentQuestion.AssessmentQuestionId, assessmentAnswerText.trim());
      setAssessmentLastResult(result);
      setAssessmentAnswerText('');
    } catch {
      setErrorMessage(t('previewNode.genericErrorFallback'));
    } finally {
      setAssessmentSubmitting(false);
    }
  }, [assessmentRound, assessmentAnswerText, assessmentSubmitting]);

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
      setNewlyUnlocked(result.NewlyUnlockedNodes);
    } catch {
      setErrorMessage(t('previewNode.genericErrorFallback'));
    } finally {
      setCompleting(false);
    }
  }, [sessionNodeId]);

  // Dev/demo-only "Aprobar (temporal)" (2026-08-02) — Capability Studio
  // ONLY, see previewMode.ts's useAutoApprove. Force-completes whatever
  // gated step is Active right now, WITHOUT requiring a real mastered
  // answer, via StudioForceAdvanceStepFunction — never touches
  // submitRecallAttempt/submitDeepDiveAttempt/evaluateProduction/
  // submitAssessmentAnswer, so the real grading logic students depend on
  // is completely untouched. For Assessment, the backend directly
  // completes the node (there is no "next step"); reuse the EXISTING
  // completeNode call afterward purely to fetch newlyUnlockedNodes for the
  // same completion screen a real Passed round would show — CompleteNodeAsync
  // is idempotent, so calling it twice is harmless.
  const handleForceApprove = useCallback(async () => {
    if (!sessionNodeId || forceApproving) return;
    setForceApproving(true);
    setErrorMessage(null);
    try {
      const result = await forceAdvanceStep(sessionNodeId);
      if (result.NodeCompleted) {
        const completion = await completeNode(sessionNodeId);
        setNewlyUnlocked(completion.NewlyUnlockedNodes);
      } else if (result.NextStep) {
        resetStepUiState(normalizeStep(result.NextStep));
      }
    } catch {
      setErrorMessage(t('previewNode.genericErrorFallback'));
    } finally {
      setForceApproving(false);
    }
  }, [sessionNodeId, forceApproving, resetStepUiState]);

  const backLink = `/capabilities/${capabilityId}/preview`;

  if (phase === 'loading') {
    return <LoadingSpinner label={t('previewNode.loading')} />;
  }

  if (phase === 'summary' && nodeSummary) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-10">
        <Link to={backLink} className="text-sm text-slate-400 hover:text-white">
          {t('previewNode.backToMap')}
        </Link>
        <NodeSummaryView
          nodeInfo={nodeInfo}
          summary={nodeSummary}
          onPracticeAgain={handlePracticeAgain}
          practicing={practicingAgain}
          t={t}
        />
      </div>
    );
  }

  if (phase === 'error' || !step) {
    return (
      <div className="mx-auto max-w-2xl px-6 py-16 text-center">
        <p className="text-red-300">{errorMessage ?? t('previewNode.loadError')}</p>
        <Link to={backLink} className="mt-4 inline-block text-sm text-brand-400 hover:text-brand-300">
          {t('previewNode.backToMap')}
        </Link>
      </div>
    );
  }

  if (newlyUnlocked !== null) {
    return (
      <div className="mx-auto max-w-xl px-6 py-16">
        <div className="rounded-2xl border border-emerald-400/30 bg-emerald-500/[0.06] p-8 text-center">
          <Check className="mx-auto h-10 w-10 text-emerald-300" />
          <h1 className="mt-3 text-xl font-semibold text-white">{t('previewNode.nodeCompleted')}</h1>
          {newlyUnlocked.length > 0 && (
            <div className="mt-4 text-left text-sm text-emerald-200">
              <p className="font-medium">{t('previewNode.newlyUnlocked')}</p>
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
            {t('previewNode.backToMapButton')}
          </button>
        </div>
      </div>
    );
  }

  const currentIndex = STEP_ORDER.indexOf(step.stepType);
  const showsTutorChat = step.stepType === 'Teaching' || step.stepType === 'Production';
  const isGatedStepType =
    step.stepType === 'DeepDive' || step.stepType === 'Recall' || step.stepType === 'Production' || step.stepType === 'Assessment';
  const showingRewardScreen = Boolean(
    pendingAdvance || recallReview || pendingCardAdvance || (step.stepType === 'Production' && productionEvaluation?.IsCorrect)
  );

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <Link to={backLink} className="text-sm text-slate-400 hover:text-white">
        {t('previewNode.backToMap')}
      </Link>

      {nodeInfo && (
        <div className="mt-4 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-semibold text-white">{nodeInfo.name}</h1>
            {mode !== 'real' && (
              <span className="rounded-full border border-amber-400/40 bg-amber-500/10 px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-wide text-amber-300">
                {t('previewNode.demoModeBadge')}
              </span>
            )}
          </div>
          {nodeInfo.description && <p className="mt-2 text-sm leading-relaxed text-slate-400">{nodeInfo.description}</p>}
          <label className="mt-3 flex w-fit cursor-pointer items-center gap-2 text-xs text-slate-400">
            <input
              type="checkbox"
              checked={autoApprove}
              onChange={(e) => setAutoApprove(e.target.checked)}
              className="h-3.5 w-3.5 rounded border-white/20 bg-white/[0.03] accent-amber-400"
            />
            {t('previewNode.autoApproveLabel')}
          </label>
        </div>
      )}

      <StepperBar currentIndex={currentIndex} mode={mode} viewingStepType={viewingStepType} onSelectStep={handleSelectStep} />

      {autoApprove && isGatedStepType && viewingStepType === null && !showingRewardScreen && (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-400/30 bg-amber-500/10 px-4 py-3">
          <p className="text-xs text-amber-200">{t('previewNode.autoApproveNotice')}</p>
          <button
            type="button"
            onClick={handleForceApprove}
            disabled={forceApproving}
            className="rounded-lg border border-amber-400/40 bg-amber-500/20 px-3 py-1.5 text-xs font-semibold text-amber-100 hover:bg-amber-500/30 disabled:opacity-50"
          >
            {forceApproving ? t('previewNode.autoApproving') : t('previewNode.autoApproveButton')}
          </button>
        </div>
      )}

      {viewingStepType === null && step.stepType !== 'Recall' && step.stepType !== 'DeepDive' && (
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
      ) : (<div className="mt-6 rounded-2xl border border-white/10 bg-white/[0.03] p-6">
        {errorMessage && <p className="mb-4 text-sm text-red-300">{errorMessage}</p>}

        {step.stepType !== 'Recall' && step.stepType !== 'DeepDive' && step.stepType !== 'Assessment' && (
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
                {illustration.imageModel && (
                  <p className="px-3 py-1.5 text-[10px] italic text-slate-500">{getGeneratedByLabel(illustration.imageModel, t)}</p>
                )}
              </figure>
            ))}
          </div>
        )}

        {showsTutorChat && (
          <div className="mt-6 rounded-xl border border-white/10 bg-white/[0.02] p-4">
            <p className="mb-3 flex items-center gap-1.5 text-xs text-slate-400">
              <MessageCircle className="h-3.5 w-3.5" /> {t('previewNode.tutorGuideNotice')}
            </p>
            <TutorChat messages={tutorMessages} loading={tutorLoading} />
            <div className="mt-3 flex gap-2">
              <input
                value={tutorInput}
                onChange={(e) => setTutorInput(e.target.value)}
                placeholder={t('previewNode.askTutorPlaceholder')}
                className="flex-1 rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white placeholder:text-slate-500 focus:border-brand-400 focus:outline-none"
                onKeyDown={(e) => e.key === 'Enter' && handleAskTutor()}
              />
              <button
                onClick={handleAskTutor}
                disabled={tutorLoading || !tutorInput.trim()}
                className="flex items-center gap-1 rounded-lg bg-white/10 px-3 py-2 text-sm text-white hover:bg-white/20 disabled:opacity-50"
              >
                <Send className="h-4 w-4" /> {t('previewNode.send')}
              </button>
            </div>
          </div>
        )}

        {step.stepType === 'Hypothesis' && (
          <div className="mt-6">
            <label className="mb-1 block text-sm font-medium text-slate-300">{t('previewNode.yourAnswer')}</label>
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
              {t('previewNode.continue')}
            </button>
          </div>
        )}

        {step.stepType === 'Production' &&
          (productionEvaluation?.IsCorrect ? (
            <RewardBanner title={t('previewNode.correctBanner')} onContinue={handleProductionContinue}>
              <p className="mt-2 text-sm text-emerald-200">{productionEvaluation.Feedback}</p>
            </RewardBanner>
          ) : (
            <div className="mt-6">
              <label className="mb-1 block text-sm font-medium text-slate-300">{t('previewNode.yourAnswer')}</label>
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
                {productionSubmitting ? t('previewNode.evaluating') : productionEvaluation ? t('previewNode.tryAgain') : t('previewNode.submit')}
              </button>
            </div>
          ))}

        {step.stepType === 'Teaching' && (
          <div className="mt-6">
            <label className="mb-1 block text-sm font-medium text-slate-300">{t('previewNode.notesLabel')}</label>
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
              {t('previewNode.continueWhenReady')}
            </button>
          </div>
        )}

        {step.stepType === 'DeepDive' &&
          (pendingAdvance ? (
            <RewardBanner title={t('previewNode.deepDiveRewardTitle')} onContinue={handlePendingAdvanceContinue} />
          ) : (
            // BUG FIX (2026-07-30 — "the voice gets interrupted"): mastering
            // a card used to swap this ENTIRE block out for a standalone
            // <RewardBanner>, which unmounted the card's <VoiceTutorAgent>
            // (and its live WebRTC connection) the instant the backend
            // confirmed mastery — cutting off the voice mid-conversation.
            // Now the same card (image/title/text + VoiceTutorAgent) stays
            // mounted the whole time; only the bottom "answer" area swaps
            // between the submit form and the reward message + Continue
            // button, so the voice session is never interrupted.
            step.exampleCards &&
            step.exampleCards.length > 0 && (
              <div className="mt-4">
                <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                  {t('previewNode.deepDiveCardLabel')} {Math.min(deepDiveCardIndex + 1, step.exampleCards.length)}{' '}
                  {t('previewNode.deepDiveOfLabel')} {deepDiveTotalCards || step.exampleCards.length}
                </p>
                {step.exampleCards.length > 1 && (
                  <div className="mt-2 flex flex-wrap gap-1.5">
                    {step.exampleCards.map((card, index) => {
                      const isMastered = index < deepDiveCardIndex;
                      const isCurrent = index === deepDiveCardIndex;
                      const isReviewable = isMastered || isCurrent;
                      return (
                        <button
                          key={index}
                          type="button"
                          disabled={!isReviewable}
                          onClick={() => isReviewable && setReviewCardIndex(index)}
                          title={card.Title}
                          className={`flex h-7 min-w-[1.75rem] items-center justify-center rounded-full border px-2 text-[11px] font-semibold transition ${
                            isCurrent
                              ? 'border-brand-400 bg-brand-500/20 text-brand-200'
                              : isMastered
                              ? 'border-emerald-400/40 bg-emerald-500/10 text-emerald-300 hover:bg-emerald-500/20 cursor-pointer'
                              : 'border-white/10 bg-white/[0.02] text-slate-600 cursor-not-allowed'
                          }`}
                        >
                          {isMastered ? <Check className="h-3 w-3" /> : index + 1}
                        </button>
                      );
                    })}
                  </div>
                )}
                {(() => {
                  const card = step.exampleCards![Math.min(deepDiveCardIndex, step.exampleCards!.length - 1)];
                  return (
                    <>
                      <div className="mt-2 rounded-xl border border-white/10 bg-white/[0.03] p-4">
                        {card.IllustrationId ? (
                          <img
                            src={apiImageUrl(`/illustrations/${card.IllustrationId}/image`)}
                            alt={card.Title}
                            className="mb-2 h-auto max-h-80 w-full rounded-lg object-contain bg-black/20"
                          />
                        ) : (
                          card.Emoji && <div className="mb-2 text-3xl leading-none">{card.Emoji}</div>
                        )}
                        <p className="text-sm font-semibold text-white">{card.Title}</p>
                        <div
                          className="prose prose-invert prose-sm mt-1 max-w-none text-slate-300"
                          dangerouslySetInnerHTML={{
                            __html: DOMPurify.sanitize(card.Text, { ALLOWED_TAGS: ['p', 'br', 'strong', 'em', 'ul', 'ol', 'li', 'a'] }),
                          }}
                        />
                        <VoiceTutorAgent
                          learningSessionStepId={step.learningSessionStepId}
                          exampleCardIndex={deepDiveCardIndex}
                          autoConnect={false}
                          compact
                          onExplanationAccepted={(explanation) => {
                            // Deliberately do NOT setResponseText(explanation)
                            // here (2026-07-30 fix) — that used to flash the
                            // spoken explanation into the visible "Your
                            // answer" textarea for an instant before
                            // handleDeepDiveSubmit's own success handler
                            // cleared it again, which read as "the system
                            // answering itself" to a student who never typed
                            // anything. handleDeepDiveSubmit already accepts
                            // an overrideText param specifically so the
                            // grading call can fire WITHOUT ever touching the
                            // visible textbox — the textbox stays empty and
                            // is purely for the manual typed-answer path.
                            void handleDeepDiveSubmit(explanation);
                          }}
                        />
                      </div>

                      {/* Before the student's first attempt on this card, there
                          is no tutor-generated question yet (deepDiveLastPrompt
                          is only set after a submission) — show a clear,
                          card-specific task instead of the generic Deep Dive
                          blueprint overview text. Once the tutor responds,
                          its own dynamic question/follow-up takes over. */}
                      {deepDiveLastPrompt ? (
                        <RichContent className="mt-4 text-slate-300" html={deepDiveLastPrompt} />
                      ) : (
                        <p className="mt-4 text-slate-300">{t('previewNode.deepDiveOpeningPrompt', { title: card.Title })}</p>
                      )}
                    </>
                  );
                })()}

                {pendingCardAdvance ? (
                  // Same card stays on screen (see fix note above) — just
                  // hide the submit form and show the reward message +
                  // Continue button in its place, instead of tearing down
                  // and replacing the whole block.
                  <div className="mt-6 rounded-xl border border-emerald-400/30 bg-emerald-500/[0.08] p-5 text-center">
                    <p className="text-sm font-semibold text-emerald-200">{t('previewNode.deepDiveCardMasteredTitle')}</p>
                    <button
                      onClick={handlePendingCardAdvanceContinue}
                      className="mt-4 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25"
                    >
                      {t('previewNode.continue')}
                    </button>
                  </div>
                ) : (
                  <>
                    <label className="mb-1 mt-4 block text-sm font-medium text-slate-300">{t('previewNode.yourAnswer')}</label>
                    <textarea
                      value={responseText}
                      onChange={(e) => setResponseText(e.target.value)}
                      rows={3}
                      className="w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white focus:border-brand-400 focus:outline-none"
                    />
                    <div className="mt-3 flex flex-wrap gap-3">
                      <button
                        onClick={() => handleDeepDiveSubmit()}
                        disabled={submitting || !responseText.trim()}
                        className="rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 disabled:opacity-50"
                      >
                        {t('previewNode.deepDiveSubmit')}
                      </button>
                      {/* Reviewer-only bypass (2026-07-31, revised 2026-08-02
                          per explicit user request: gate on the `autoApprove`
                          switch itself, not on `mode === 'demo'` — the button
                          must appear ONLY when "Auto-approve (temporary,
                          demos only)" is checked, regardless of Real/Demo/Edit
                          mode, matching the same switch that drives the
                          force-approve banner for other gated steps). Lets a
                          reviewer browse through every card's content without
                          having to satisfy the AI grader on each one. Purely
                          client-side — never calls submitDeepDiveAttempt,
                          never touches the real LearningSession/grading state.
                          Once the last card is reached, further navigation
                          happens via the already-unlocked step stepper above. */}
                      {autoApprove && deepDiveCardIndex < step.exampleCards.length - 1 && (
                        <button
                          type="button"
                          onClick={() => {
                            setDeepDiveCardIndex((i) => Math.min(i + 1, step.exampleCards!.length - 1));
                            setDeepDiveLastPrompt(undefined);
                            setResponseText('');
                          }}
                          className="rounded-xl border border-white/10 bg-white/[0.03] px-5 py-2.5 text-sm font-semibold text-slate-200 hover:bg-white/[0.08]"
                        >
                          {t('previewNode.deepDiveSkip')}
                        </button>
                      )}
                    </div>
                  </>
                )}
              </div>
            )
          ))}

        {step.stepType === 'DeepDive' && step.exampleCards && reviewCardIndex !== null && (
          <DeepDiveCardReviewModal
            card={step.exampleCards[reviewCardIndex]}
            index={reviewCardIndex}
            capabilityGraphNodeId={nodeId}
            onClose={() => setReviewCardIndex(null)}
            t={t}
          />
        )}

        {step.stepType === 'Recall' &&
          (pendingAdvance ? (
            <RewardBanner title={t('previewNode.masteredRecall')} onContinue={handlePendingAdvanceContinue} />
          ) : recallReview ? (
            <RewardBanner title={t('previewNode.reviewTeaching')} onContinue={handleRecallReviewContinue} tone="amber" />
          ) : (
            <div className="mt-6">
              <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {t('previewNode.masteredCount', {
                  mastered: recallItemsMastered,
                  required: RECALL_ITEMS_REQUIRED,
                  attempt: recallAttemptsUsed + 1,
                  max: RECALL_MAX_ATTEMPTS,
                })}
              </p>
              <RichContent className="mt-2 text-slate-300" html={recallLastPrompt ?? step.content} />

              <label className="mb-1 mt-4 block text-sm font-medium text-slate-300">{t('previewNode.yourAnswer')}</label>
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
                {t('previewNode.answer')}
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

/**
 * Read-only overlay for revisiting an already-mastered Deep Dive card
 * (2026-07-30 — user request: "index of the cards so the student can go
 * back to older lessons already answered for practice"). Purely display —
 * no VoiceTutorAgent, no submit button, no effect on deepDiveCardIndex or
 * any grading state whatsoever.
 */
function DeepDiveCardReviewModal({
  card,
  index,
  capabilityGraphNodeId,
  onClose,
  t,
}: {
  card: ExampleCardDto;
  index: number;
  /** Powers the read-only voice explain below (blueprint-only "peek" path
   * — no `learningSessionStepId`, no `onExplanationAccepted`, so it can
   * never grade or mutate progress). */
  capabilityGraphNodeId?: string;
  onClose: () => void;
  t: (key: string, opts?: Record<string, unknown>) => string;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-sm">
      <div className="max-h-[85vh] w-full max-w-md overflow-y-auto rounded-2xl border border-white/10 bg-slate-950 p-6 shadow-2xl shadow-black/40">
        <div className="mb-3 flex items-start justify-between gap-3">
          <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
            {t('previewNode.deepDiveCardLabel')} {index + 1}
          </p>
          <button
            type="button"
            onClick={onClose}
            className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        {card.IllustrationId ? (
          <img
            src={apiImageUrl(`/illustrations/${card.IllustrationId}/image`)}
            alt={card.Title}
            className="mb-2 h-auto max-h-72 w-full rounded-lg object-contain bg-black/20"
          />
        ) : (
          card.Emoji && <div className="mb-2 text-3xl leading-none">{card.Emoji}</div>
        )}
        <p className="text-sm font-semibold text-white">{card.Title}</p>
        <RichContent className="mt-1 text-slate-300" html={card.Text} />

        {/* Read-only voice explain (2026-07-30) — no learningSessionStepId
            and no onExplanationAccepted passed, so this can never grade or
            advance anything; purely conversational recap + Q&A. */}
        {capabilityGraphNodeId && (
          <VoiceTutorAgent
            capabilityGraphNodeId={capabilityGraphNodeId}
            stepType="DeepDive"
            exampleCardIndex={index}
            reviewOnly
            autoConnect={false}
            compact
            startLabel={t('previewNode.deepDiveReviewVoiceStart')}
          />
        )}
      </div>
    </div>
  );
}

/**
 * Shared body content for a step's read-only recap — the blueprint's
 * Content plus the student's past Evidence entries for that step. Ported
 * from frontend/human-os-web's NodeWorkflowPage.tsx (2026-07-30).
 */
function StepReviewBody({ data, t }: { data: StepReviewDto; t: (key: string, opts?: Record<string, unknown>) => string }) {
  return (
    <>
      <RichContent className="text-sm leading-relaxed text-slate-300" html={data.Content} />

      {data.ExampleCards && data.ExampleCards.length > 0 && (
        <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
          {data.ExampleCards.map((card, index) => (
            <div key={index} className="rounded-xl border border-white/10 bg-white/[0.02] p-3">
              {card.IllustrationId ? (
                <img
                  src={apiImageUrl(`/illustrations/${card.IllustrationId}/image`)}
                  alt={card.Title}
                  className="mb-1.5 h-24 w-full rounded-lg object-contain object-center"
                />
              ) : (
                card.Emoji && <div className="mb-1.5 text-2xl leading-none">{card.Emoji}</div>
              )}
              <p className="text-sm font-semibold text-white">{card.Title}</p>
              <RichContent className="mt-1 text-sm text-slate-300" html={card.Text} />
            </div>
          ))}
        </div>
      )}

      {data.Evidence.length > 0 ? (
        <div className="mt-5 space-y-3 border-t border-white/10 pt-4">
          <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{t('previewNode.stepReviewYourAnswers')}</p>
          {data.Evidence.map((entry, index) => (
            <div key={index} className="rounded-lg bg-white/[0.03] p-3">
              {entry.TutorPrompt && <p className="text-xs text-slate-500">{entry.TutorPrompt}</p>}
              <p className="mt-1 text-sm font-medium text-slate-200">{entry.StudentResponse}</p>
              <p className="mt-1 text-[11px] text-slate-500">
                {new Date(entry.CreatedDate).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })}
                {typeof entry.TutorScore === 'number' && ` · ${t('previewNode.stepReviewScoreLabel')} ${entry.TutorScore}`}
              </p>
            </div>
          ))}
        </div>
      ) : (
        <p className="mt-5 border-t border-white/10 pt-4 text-sm text-slate-500">{t('previewNode.stepReviewNoAnswers')}</p>
      )}
    </>
  );
}

function SummaryStat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-[11px] uppercase tracking-wide text-emerald-400">{label}</p>
      <p className="text-sm font-semibold text-emerald-100">{value}</p>
    </div>
  );
}

/**
 * Read-only recap shown when opening a node that is already Mastered on
 * the map — "what happened the last time I completed this" instead of
 * silently starting a brand-new attempt. "Practicar de nuevo" stays an
 * explicit, separate action that creates a new attempt, preserving this
 * one's history forever. Ported from frontend/human-os-web's
 * NodeWorkflowPage.tsx/NodeSummaryView (2026-07-30) — deliberately WITHOUT
 * the "Reactivar Engrama" voice feature, which doesn't exist in Capability
 * Studio yet.
 */
function NodeSummaryView({
  nodeInfo,
  summary,
  onPracticeAgain,
  practicing,
  t,
}: {
  nodeInfo: { name: string; description?: string } | null;
  summary: NodeSummaryDto;
  onPracticeAgain: () => void;
  practicing: boolean;
  t: (key: string, opts?: Record<string, unknown>) => string;
}) {
  const stepLabels: Record<ExperienceStepType, string> = {
    Hypothesis: t('previewNode.stepLabels.Hypothesis'),
    Teaching: t('previewNode.stepLabels.Teaching'),
    DeepDive: t('previewNode.stepLabels.DeepDive'),
    Recall: t('previewNode.stepLabels.Recall'),
    Production: t('previewNode.stepLabels.Production'),
    Assessment: t('previewNode.stepLabels.Assessment'),
  };

  // Which attempt's step-by-step detail is currently shown — defaults to
  // the most recent one (already included in `summary.Steps`, no fetch
  // needed). Picking an older row in PastAttempts fetches that attempt's
  // own steps on demand via getStepReview (read-only, never mutates).
  const [viewedAttemptId, setViewedAttemptId] = useState(summary.LearningSessionNodeId);
  const [viewedSteps, setViewedSteps] = useState<StepReviewDto[]>(summary.Steps);
  const [loadingAttempt, setLoadingAttempt] = useState(false);
  // "Reactivar Engrama" voice review panel (2026-07-30) — closed by default;
  // the student explicitly opts in rather than the Agent auto-talking the
  // instant the recap screen loads.
  const [voiceReviewOpen, setVoiceReviewOpen] = useState(false);

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
      <div className="rounded-2xl border border-emerald-400/20 bg-emerald-500/[0.08] p-6">
        <div className="flex items-center gap-2 text-emerald-300">
          <Check className="h-5 w-5" />
          <h1 className="text-lg font-semibold">{nodeInfo?.name ?? t('previewNode.nodeSummaryTitle')}</h1>
        </div>
        {nodeInfo?.description && <p className="mt-2 text-sm text-emerald-200">{nodeInfo.description}</p>}

        <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <SummaryStat label={t('previewNode.nodeSummaryAttempts')} value={String(summary.AttemptCount)} />
          <SummaryStat
            label={t('previewNode.nodeSummaryScore')}
            value={typeof summary.FinalScore === 'number' ? `${summary.FinalScore}/100` : '—'}
          />
          <SummaryStat
            label={t('previewNode.nodeSummaryFirstCompleted')}
            value={summary.FirstCompletedDate ? new Date(summary.FirstCompletedDate).toLocaleDateString(undefined, { dateStyle: 'medium' }) : '—'}
          />
          <SummaryStat
            label={t('previewNode.nodeSummaryLastCompleted')}
            value={summary.LastCompletedDate ? new Date(summary.LastCompletedDate).toLocaleDateString(undefined, { dateStyle: 'medium' }) : '—'}
          />
        </div>

        {summary.PastAttempts.length > 1 && (
          <div className="mt-5 border-t border-emerald-400/20 pt-4">
            <p className="text-xs font-medium uppercase tracking-wide text-emerald-400">{t('previewNode.nodeSummaryPastAttempts')}</p>
            <div className="mt-2 flex flex-wrap gap-2">
              {summary.PastAttempts.map((attempt, index) => {
                const isSelected = attempt.LearningSessionNodeId === viewedAttemptId;
                const label = attempt.CompletedDate
                  ? new Date(attempt.CompletedDate).toLocaleDateString(undefined, { dateStyle: 'medium' })
                  : `#${summary.PastAttempts.length - index}`;
                return (
                  <button
                    key={attempt.LearningSessionNodeId}
                    type="button"
                    onClick={() => handleSelectAttempt(attempt)}
                    className={`rounded-full border px-3 py-1.5 text-xs font-medium transition ${
                      isSelected
                        ? 'border-emerald-400 bg-emerald-500 text-white'
                        : 'border-emerald-400/30 bg-white/[0.03] text-emerald-200 hover:bg-emerald-500/10'
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

        <div className="mt-5 flex flex-wrap gap-3">
          <button
            onClick={onPracticeAgain}
            disabled={practicing}
            className="rounded-xl bg-emerald-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-emerald-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
          >
            {practicing ? t('previewNode.nodeSummaryPracticing') : t('previewNode.nodeSummaryPracticeAgain')}
          </button>
          <button
            type="button"
            onClick={() => setVoiceReviewOpen((open) => !open)}
            className="rounded-xl border border-accent-400/30 bg-accent-500/10 px-5 py-2.5 text-sm font-semibold text-accent-200 transition hover:bg-accent-500/20"
          >
            {voiceReviewOpen ? t('previewNode.nodeSummaryVoiceReviewClose') : t('previewNode.nodeSummaryVoiceReviewStart')}
          </button>
        </div>

        {/* "Reactivar Engrama" (2026-07-30) — a spoken, NEVER-graded
            walkthrough of the whole lesson: the Agent reads/explains it
            back and invites questions, purely conversational. Unmounting
            (closing this panel) tears down the WebRTC connection via
            VoiceTutorAgent's own unmount cleanup. */}
        {voiceReviewOpen && (
          <div className="mt-4">
            <VoiceTutorAgent
              reviewSession={{ personId: PREVIEW_PERSON_ID, capabilityGraphNodeId: summary.CapabilityGraphNodeId }}
              startLabel={t('previewNode.nodeSummaryVoiceReviewIdle')}
              autoConnect
            />
          </div>
        )}
      </div>

      <div className="space-y-4">
        {loadingAttempt && <p className="text-sm text-slate-400">{t('previewNode.stepReviewLoading')}</p>}
        {!loadingAttempt &&
          viewedSteps.map((stepData) => {
            const Icon = STEP_ICONS[stepData.StepType];
            return (
              <div key={stepData.StepType} className="rounded-2xl border border-white/10 bg-white/[0.03] p-5">
                <div className="mb-3 flex items-center gap-2">
                  <Icon className="h-4 w-4 text-slate-400" />
                  <h2 className="text-sm font-semibold text-slate-200">{stepLabels[stepData.StepType]}</h2>
                </div>
                <StepReviewBody data={stepData} t={t} />
              </div>
            );
          })}
      </div>
    </div>
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
  const { t } = useTranslation();
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
                {t(`previewNode.stepLabels.${stepType}`)}
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
  const { t } = useTranslation();
  const blueprintStep = blueprint?.Steps.find((s) => s.StepType === stepType);

  return (
    <div className="mt-6 rounded-2xl border border-amber-400/30 bg-amber-500/[0.04] p-6">
      <div className="mb-4 flex items-center justify-between">
        <p className="text-xs font-medium uppercase tracking-wide text-amber-300">
          {t('previewNode.readOnlyPreview', { step: t(`previewNode.stepLabels.${stepType}`) })}
        </p>
        <button
          onClick={onBack}
          className="rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-white/[0.08]"
        >
          {t('previewNode.backToLiveSession')}
        </button>
      </div>

      {(stepType === 'Hypothesis' || stepType === 'Teaching') && (
        <VoiceTutorAgent capabilityGraphNodeId={capabilityGraphNodeId} stepType={stepType} />
      )}

      {loading || !blueprintStep ? (
        <LoadingSpinner label={t('previewNode.loadingContent')} />
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
                  {illustration.ImageModel && (
                    <p className="px-3 py-1.5 text-[10px] italic text-slate-500">{getGeneratedByLabel(illustration.ImageModel, t)}</p>
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
  const { t } = useTranslation();
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
      {loading && <div className="max-w-[85%] rounded-lg bg-white/5 px-3 py-2 text-sm text-slate-400">{t('previewNode.typing')}</div>}
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
  const { t } = useTranslation();
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
        {t('previewNode.continue')}
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
  const { t } = useTranslation();
  if (!round) {
    return <p className="mt-6 text-sm text-slate-400">{t('previewNode.preparingAssessment')}</p>;
  }

  if (round.Status === 'Passed') {
    return (
      <div className="mt-6 rounded-xl border border-emerald-400/30 bg-emerald-500/[0.08] p-5 text-center">
        <p className="text-sm font-semibold text-emerald-200">
          {t('previewNode.assessmentPassed')} {typeof round.FinalScore === 'number' && t('previewNode.scoreOf100', { score: round.FinalScore })}
        </p>
        <button
          onClick={onCompleteNode}
          disabled={completing}
          className="mt-4 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 disabled:opacity-50"
        >
          {completing ? t('previewNode.saving') : t('previewNode.completeNode')}
        </button>
      </div>
    );
  }

  if (lastResult) {
    return (
      <div className="mt-6 rounded-xl border border-white/10 bg-white/[0.02] p-5">
        <p className={`text-sm font-medium ${lastResult.Grade.Correctness === 'Correct' ? 'text-emerald-300' : 'text-amber-300'}`}>
          {lastResult.Grade.Correctness === 'Correct'
            ? t('previewNode.correct')
            : lastResult.Grade.Correctness === 'PartiallyCorrect'
            ? t('previewNode.partiallyCorrect')
            : t('previewNode.incorrect')}
        </p>
        <p className="mt-2 text-sm text-slate-300">{lastResult.Grade.Feedback}</p>
        <button
          onClick={onContinue}
          className="mt-4 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/25"
        >
          {t('previewNode.next')}
        </button>
      </div>
    );
  }

  if (!round.CurrentQuestion) {
    return <p className="mt-6 text-sm text-slate-400">{t('previewNode.loadingNextQuestion')}</p>;
  }

  return (
    <div className="mt-6">
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
        {t('previewNode.questionCounter', {
          index: round.CurrentQuestion.QuestionIndex,
          total: round.TotalQuestions,
          round: round.RoundNumber,
        })}
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
        {submitting ? t('previewNode.sending') : t('previewNode.answer')}
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

