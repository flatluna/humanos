import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import DOMPurify from 'dompurify';
import { Lightbulb, BookOpen, RotateCcw, Hammer, ClipboardCheck, Check, Send, MessageCircle, Star, X, Sparkles, ChevronDown, ChevronUp, Images, ShieldCheck, SkipForward, SkipBack } from 'lucide-react';
import { useI18n } from '../i18n/useI18n';
import { useAuth } from '@/auth/AuthContext';
import { API_BASE_URL } from '../api/httpClient';
import { getCapabilityGraph } from '../api/capabilityGraphApi';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import EngramReviewAgent from '../components/EngramReviewAgent';
import {
  getActiveSession,
  startSession,
  submitStepResponse,
  advanceStep,
  forceAdvanceStep,
  skipDeepDiveStep,
  askTutor,
  submitRecallAttempt,
  submitDeepDiveAttempt,
  evaluateProduction,
  completeNode,
  getActiveAssessmentRound,
  startAssessmentRound,
  submitAssessmentAnswer,
  getStepReview,
  getNodeSummary,
  expandNodeKnowledge,
  type ExperienceStepType,
  type BackendRuntimeStep,
  type BackendRuntimeSessionInfo,
  type ExampleCardDto,
  type TutorMode,
  type GraphNodeInfoDto,
  type AssessmentRoundStateDto,
  type SubmitAssessmentAnswerResponse,
  type EvaluateProductionResponseDto,
  type StepReviewDto,
  type NodeSummaryDto,
  type NodeAttemptSummaryDto,
  type KnowledgeExpansionDto,
} from '../api/runtimeSessionApi';

// Force every sanitized anchor (e.g. web-grounding citation links baked
// into blueprint Content by ExperienceDesignerAgent) to open safely in a
// new tab — set once at module load, not per-render.
DOMPurify.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A') {
    node.setAttribute('target', '_blank');
    node.setAttribute('rel', 'noopener noreferrer');
  }
});

/**
 * Node interior — Paso 6. Drives a node's 5-step Memory Paradox sequence
 * (Hypothesis/Teaching/Recall/Production/Assessment) against the real
 * instructor-runtime API. The stepper is a permanent visible structure;
 * the tutor conversation lives INSIDE the active step, never replacing it
 * (see /memories/repo/student-graph-ui-redesign-final-design.md).
 */

const STEP_ORDER: ExperienceStepType[] = ['Hypothesis', 'Teaching', 'DeepDive', 'Recall', 'Production', 'Assessment'];

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
  exampleCards?: ExampleCardDto[];
  /** Only meaningful when stepType === 'DeepDive' — lets a page reload resume exactly where the student left off, card-for-card. */
  currentDeepDiveCardIndex?: number;
  deepDiveTotalCards?: number;
}

function normalizeStep(step: BackendRuntimeStep): NormalizedStep {
  return {
    learningSessionStepId: step.LearningSessionStepId,
    stepType: step.StepType,
    content: step.Content,
    // Backend doesn't always include Illustrations for every step (e.g. some
    // step types/responses omit it entirely) — default to [] instead of
    // crashing the whole page on `.map`/`.length` of undefined.
    illustrations: (step.Illustrations ?? []).map((i) => ({
      illustrationId: i.IllustrationId,
      storagePath: i.StoragePath,
      caption: i.Caption,
    })),
    exampleCards: step.ExampleCards,
    currentDeepDiveCardIndex: step.CurrentDeepDiveCardIndex,
    deepDiveTotalCards: step.DeepDiveTotalCards,
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
  const [searchParams] = useSearchParams();
  const programId = searchParams.get('programId');
  // Lets the caller override the "back" destination — e.g. the Brain page's
  // "Rehacer los 5 pasos" link passes ?from=brain, and /today's "Continuar
  // Aprendiendo" cards pass ?from=today, so the student returns to exactly
  // where they came from (not the capability graph map) after finishing/
  // leaving the node.
  const fromParam = searchParams.get('from');
  const backToMapHref =
    fromParam === 'brain'
      ? '/brain'
      : fromParam === 'today'
        ? '/today'
        : programId
          ? `/capabilities/${capabilityId}?programId=${programId}`
          : `/capabilities/${capabilityId}`;
  const { t, language } = useI18n();
  const navigate = useNavigate();
  const { user } = useAuth();
  const personId = user?.personId ?? '';

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
  // Guards handleContinue/handleProductionContinue against firing
  // advanceStep twice in a row (e.g. a double-click before the `submitting`
  // state re-render disables the button) — a second concurrent advance
  // call fails with a real, harmless 409 "AlreadyOnAssessment"/
  // "RecallRequiresGate" from the backend since the first call already
  // moved the node to the next step, but it used to still overwrite the UI
  // with a spurious error message. A ref is used (not state) because it
  // must be checked synchronously, before any re-render can happen.
  const advancingRef = useRef(false);

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

  // Deep Dive: sequential, mandatory-all-cards quiz loop (see
  // backend/HumanOS/Agentic/Runtime/DeepDiveLoopGate.cs) — unlike Recall,
  // there's no attempt cap and no distinct-N-of-pool logic, just "master
  // this card, then the next one, in order, until every card is done".
  const [deepDiveCardIndex, setDeepDiveCardIndex] = useState(0);
  const [deepDiveTotalCards, setDeepDiveTotalCards] = useState(0);
  const [deepDiveLastPrompt, setDeepDiveLastPrompt] = useState<string | undefined>(undefined);

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
  // "Aprobar (demo HR)" (ported from human-os-employee) — HRAdmin-only,
  // real backend-checked (see HrForceAdvanceStepFunction.cs), lets an
  // HRAdmin force-complete the active gated step for a live demo. Button
  // only renders when user?.orgRole === 'HRAdmin' below.
  const [forceApproving, setForceApproving] = useState(false);
  // Memory Paradox reward pause: after a correct Recall attempt or a
  // Production submission, hold the already-fetched next step here and show
  // a small celebratory banner instead of jumping straight to the next step
  // — this gives the dopamine-driven "neurons that fire together, wire
  // together" moment a beat to land before moving on.
  const [pendingAdvance, setPendingAdvance] = useState<{ nextStep: BackendRuntimeStep; title: string } | null>(null);

  // "Profundizar" (Knowledge Expansion, 2026-07-20) — learner-triggered
  // only, shown in the Teaching step. Never fetched automatically.
  const [knowledgeExpansion, setKnowledgeExpansion] = useState<KnowledgeExpansionDto | null>(null);
  const [expansionLoading, setExpansionLoading] = useState(false);
  const [expansionError, setExpansionError] = useState<string | null>(null);
  const [expansionPanelOpen, setExpansionPanelOpen] = useState(true);

  const resetStepUiState = useCallback((next: NormalizedStep) => {
    setStep(next);
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
    setProductionEvaluation(null);
    setAssessmentRound(null);
    setAssessmentAnswerText('');
    setAssessmentLastResult(null);
    setPendingAdvance(null);
    setKnowledgeExpansion(null);
    setExpansionLoading(false);
    setExpansionError(null);
    setExpansionPanelOpen(true);
  }, []);

  useEffect(() => {
    if (!capabilityId || !nodeId || !personId) return;
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
        const graph = await getCapabilityGraph(capabilityId!, personId);
        if (cancelled) return;
        const match = graph.nodes.find((n) => n.capabilityGraphNodeId === nodeId);
        if (match) {
          setNodeInfo({ name: match.name, description: match.description });
          isMastered = match.state === 'Mastered';
        }
      } catch {
        // Non-critical for the header — the workflow still works without a title.
      }

      if (isMastered) {
        try {
          const summary = await getNodeSummary(personId, nodeId!);
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
        const active = await getActiveSession(personId, capabilityId!, programId);
        const info =
          active && active.CapabilityGraphNodeId === nodeId
            ? active
            : await startSession(personId, capabilityId!, nodeId!, programId);
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
  }, [capabilityId, nodeId, personId, programId, resetStepUiState, t.nodeError]);

  const handlePracticeAgain = useCallback(async () => {
    if (!capabilityId || !nodeId) return;
    setPracticingAgain(true);
    setErrorMessage(null);
    try {
      const info = await startSession(personId, capabilityId, nodeId, programId);
      setSessionNodeId(info.LearningSessionNodeId);
      resetStepUiState(normalizeSessionInfo(info));
      setNodeSummary(null);
      setPhase('ready');
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setPracticingAgain(false);
    }
  }, [capabilityId, nodeId, personId, programId, resetStepUiState, t.nodeError]);

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
      setErrorMessage(t.nodeError);
    } finally {
      setSubmitting(false);
      advancingRef.current = false;
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

  // DeepDive is the one exception: revisiting it (2026-08-05 "quiero que se
  // active el comportamiento igual que como cuando ingresa por primera vez,
  // no un modal con los textos de cada cuadro") browses the same cards, one
  // at a time with Prev/Next, instead of a flat read-only text dump — pure
  // client-side free practice, never touches grading/backend progression.
  const [deepDivePractice, setDeepDivePractice] = useState<{
    data: StepReviewDto | null;
    cardIndex: number;
    loading: boolean;
    error: boolean;
  } | null>(null);

  const handleStepIconClick = useCallback(
    (stepType: ExperienceStepType) => {
      if (!sessionNodeId) return;
      if (stepType === 'DeepDive') {
        setDeepDivePractice({ data: null, cardIndex: 0, loading: true, error: false });
        getStepReview(sessionNodeId, stepType)
          .then((data) => setDeepDivePractice({ data, cardIndex: 0, loading: false, error: false }))
          .catch(() => setDeepDivePractice({ data: null, cardIndex: 0, loading: false, error: true }));
        return;
      }
      setStepReview({ stepType, data: null, loading: true, error: false });
      getStepReview(sessionNodeId, stepType)
        .then((data) => setStepReview({ stepType, data, loading: false, error: false }))
        .catch(() => setStepReview({ stepType, data: null, loading: false, error: true }));
    },
    [sessionNodeId]
  );

  const closeStepReview = useCallback(() => setStepReview(null), []);
  const closeDeepDivePractice = useCallback(() => setDeepDivePractice(null), []);
  const deepDivePracticePrevCard = useCallback(() => {
    setDeepDivePractice((prev) => (prev ? { ...prev, cardIndex: Math.max(prev.cardIndex - 1, 0) } : prev));
  }, []);
  const deepDivePracticeNextCard = useCallback(() => {
    setDeepDivePractice((prev) =>
      prev ? { ...prev, cardIndex: Math.min(prev.cardIndex + 1, (prev.data?.ExampleCards?.length ?? 1) - 1) } : prev
    );
  }, []);

  // Production ("Aplícalo") formative grading: submit for real AI
  // evaluation. No attempt cap — an incorrect verdict just clears the
  // evaluation and lets the student edit/resubmit their response; a
  // correct verdict shows a reward banner whose Continue button advances
  // the step via handleProductionContinue below.
  const handleProductionSubmit = useCallback(async () => {
    if (!step || !responseText.trim()) return;
    setProductionSubmitting(true);
    setErrorMessage(null);
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
    if (!sessionNodeId || advancingRef.current) return;
    advancingRef.current = true;
    setSubmitting(true);
    setErrorMessage(null);
    try {
      const nextStep = await advanceStep(sessionNodeId);
      resetStepUiState(normalizeStep(nextStep));
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setSubmitting(false);
      advancingRef.current = false;
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

  const handleExpandKnowledge = useCallback(async () => {
    if (!nodeId) return;
    setExpansionLoading(true);
    setExpansionError(null);
    try {
      const result = await expandNodeKnowledge(nodeId);
      setKnowledgeExpansion(result);
    } catch {
      setExpansionError(t.expandKnowledgeError);
    } finally {
      setExpansionLoading(false);
    }
  }, [nodeId, t.expandKnowledgeError]);

  const handleRecallSubmit = useCallback(async () => {
    if (!step || !responseText.trim()) return;
    setSubmitting(true);
    setErrorMessage(null);
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

  // Deep Dive: score one attempt on the CURRENT card only (no attempt cap,
  // no regression path). Mastering the card advances NextCardIndex; once
  // every card is mastered in order, Advanced flips true and the same
  // "reward pause" pattern as Recall plays before moving to the next step.
  const handleDeepDiveSubmit = useCallback(async (overrideText?: string) => {
    const text = (overrideText ?? responseText).trim();
    if (!step || !text) return;
    setSubmitting(true);
    setErrorMessage(null);
    try {
      const outcome = await submitDeepDiveAttempt(step.learningSessionStepId, text, deepDiveLastPrompt);
      setResponseText('');
      if (outcome.Advanced && outcome.NextStep) {
        setPendingAdvance({ nextStep: outcome.NextStep, title: t.deepDiveRewardTitle });
      } else {
        setDeepDiveCardIndex(outcome.NextCardIndex);
        setDeepDiveTotalCards(outcome.TotalCards);
        // BUG FIX (2026-07-30): when THIS attempt just mastered the current
        // card, TutorTurn.Message is feedback/follow-up about the card that
        // was JUST mastered, not the NEW card now being shown — showing it
        // here would carry the previous card's question/answer over onto
        // the new card (real bug reported live: "Letter F" card shown while
        // the text below still asked about "Letter E"). Clear it instead so
        // the generic per-card opening prompt shows for the new card (same
        // reset the backend already does to CurrentDeepDiveQuestion in this
        // exact case — see TutorService.SubmitDeepDiveAttemptAsync).
        setDeepDiveLastPrompt(outcome.Mastered ? undefined : outcome.TutorTurn.Message);
      }
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setSubmitting(false);
    }
  }, [step, responseText, deepDiveLastPrompt, t.nodeError, t.deepDiveRewardTitle]);

  // Manual card navigation (2026-08-05, explicit repeated user request: "no
  // quiero forzarlo a contestar perfecto... que decida cuándo dar next...
  // aunque no haya enviado respuesta... siguiente tarjeta siempre tiene que
  // estar disponible... también un back para ver tarjeta anterior, quiero
  // que el usuario siempre pueda regresar a ver sus tarjetas sin
  // restricción"). Previous is purely client-side (mirrors Capability
  // Studio's reviewer-only `autoApprove` skip button, but available to
  // every student here) — it only ever revisits already-seen cards, never
  // touches real grading/mastery state, so it's always unrestricted. Next
  // is the same for every card EXCEPT the last one — completing the whole
  // Deep Dive step is still tracked server-side, so on the last card it
  // calls RuntimeSkipDeepDiveStepFunction (self-service, DeepDive-only —
  // see skipDeepDiveStep) to advance to the next workflow step ungraded.
  const totalDeepDiveCards = deepDiveTotalCards || step?.exampleCards?.length || 0;
  const [skippingDeepDive, setSkippingDeepDive] = useState(false);

  const handleDeepDivePrevCard = useCallback(() => {
    setDeepDiveCardIndex((i) => Math.max(i - 1, 0));
    setDeepDiveLastPrompt(undefined);
    setResponseText('');
  }, []);

  const handleDeepDiveNextCard = useCallback(async () => {
    if (!step?.exampleCards) return;
    if (deepDiveCardIndex < totalDeepDiveCards - 1) {
      setDeepDiveCardIndex((i) => Math.min(i + 1, step.exampleCards!.length - 1));
      setDeepDiveLastPrompt(undefined);
      setResponseText('');
      return;
    }
    if (!sessionNodeId || skippingDeepDive) return;
    setSkippingDeepDive(true);
    setErrorMessage(null);
    try {
      const nextStep = await skipDeepDiveStep(sessionNodeId);
      resetStepUiState(normalizeStep(nextStep));
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setSkippingDeepDive(false);
    }
  }, [step, deepDiveCardIndex, totalDeepDiveCards, sessionNodeId, skippingDeepDive, resetStepUiState, t.nodeError]);

  // Adaptive Assessment: the first time this node's step becomes
  // Assessment, resume any in-progress round or start a brand-new one.
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
    setErrorMessage(null);
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
    setErrorMessage(null);
    try {
      const result = await completeNode(sessionNodeId);
      setNewlyUnlocked(result.newlyUnlockedNodes ?? []);
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setCompleting(false);
    }
  }, [sessionNodeId, t.nodeError]);

  // "Aprobar (demo HR)" — force-completes whatever gated step is Active
  // right now, without answering it, so an HRAdmin can advance/demo a
  // course live. The backend endpoint independently re-verifies the
  // caller is HRAdmin (see HrForceAdvanceStepFunction.cs) — this button
  // is a convenience, not the security boundary.
  const handleForceApprove = useCallback(async () => {
    if (!sessionNodeId || advancingRef.current) return;
    advancingRef.current = true;
    setForceApproving(true);
    setErrorMessage(null);
    try {
      const result = await forceAdvanceStep(sessionNodeId);
      if (result.NodeCompleted) {
        setNewlyUnlocked([]);
      } else if (result.NextStep) {
        resetStepUiState(normalizeStep(result.NextStep));
      }
    } catch {
      setErrorMessage(t.nodeError);
    } finally {
      setForceApproving(false);
      advancingRef.current = false;
    }
  }, [sessionNodeId, resetStepUiState, t.nodeError]);

  if (phase === 'loading') {
    return (
      <div className="mx-auto max-w-3xl p-8">
        <Link to={backToMapHref} className="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:underline">
          ← {t.backToMap}
        </Link>
        <p className="mt-6 text-slate-500 dark:text-slate-400">{t.nodeLoading}</p>
      </div>
    );
  }

  if (phase === 'summary' && nodeSummary) {
    return (
      <div className="mx-auto max-w-3xl p-4 sm:p-8">
        <Link to={backToMapHref} className="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:underline">
          ← {t.backToMap}
        </Link>
        <NodeSummaryView
          nodeInfo={nodeInfo}
          summary={nodeSummary}
          onPracticeAgain={handlePracticeAgain}
          practicing={practicingAgain}
          personId={personId}
          t={t}
          language={language}
        />
      </div>
    );
  }

  if (phase === 'error' || !step) {
    return (
      <div className="mx-auto max-w-3xl p-8">
        <Link to={backToMapHref} className="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:underline">
          ← {t.backToMap}
        </Link>
        <p className="mt-6 text-red-600 dark:text-red-400">{errorMessage ?? t.nodeError}</p>
      </div>
    );
  }

  // Revisiting Deep Dive from the stepper: same one-card-at-a-time browsing
  // as first entry (Prev/Next), not the flat all-cards-text modal — purely
  // client-side free practice, no grading/backend calls at all.
  if (deepDivePractice) {
    const cards = deepDivePractice.data?.ExampleCards ?? [];
    const cardIndex = Math.min(deepDivePractice.cardIndex, Math.max(cards.length - 1, 0));
    const card = cards[cardIndex];
    return (
      <div className="mx-auto max-w-3xl p-4 sm:p-8">
        <button
          type="button"
          onClick={closeDeepDivePractice}
          className="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:underline"
        >
          ← {t.deepDivePracticeClose}
        </button>

        {nodeInfo && (
          <div className="mt-4 rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-5">
            <h1 className="text-xl font-semibold tracking-tight text-slate-900 dark:text-white">{nodeInfo.name}</h1>
          </div>
        )}

        <div className="mt-6 rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-6">
          <h2 className="mb-4 flex items-center gap-2 text-lg font-semibold text-slate-900 dark:text-white">
            <BookOpen className="h-5 w-5 text-brand-500" /> {t.stepDeepDive}
          </h2>

          {deepDivePractice.loading && <p className="text-sm text-slate-500 dark:text-slate-400">{t.stepReviewLoading}</p>}
          {deepDivePractice.error && <p className="text-sm text-red-600 dark:text-red-400">{t.nodeError}</p>}

          {card && (
            <>
              <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {t.deepDiveCardLabel} {cardIndex + 1} {t.assessmentOfLabel} {cards.length}
              </p>
              <div className="mt-2 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-4">
                {card.IllustrationId ? (
                  <img
                    src={`${API_BASE_URL}/illustrations/${card.IllustrationId}/image`}
                    alt={card.Title}
                    className="mb-2 h-auto max-h-80 w-full rounded-lg object-contain bg-black/20"
                  />
                ) : (
                  card.Emoji && <div className="mb-2 text-3xl leading-none">{card.Emoji}</div>
                )}
                <p className="text-sm font-semibold text-slate-900 dark:text-white">{card.Title}</p>
                <RichContent className="mt-1 text-sm text-slate-600 dark:text-slate-300" html={card.Text} />
                {/* Same per-card "habla con tu maestro" voice Agent as first
                    entry — blueprint-only path (no live LearningSessionStep
                    exists for a step the student already passed), so it just
                    talks about the card, never grades anything here. */}
                {nodeId && (
                  <VoiceTutorAgent
                    capabilityGraphNodeId={nodeId}
                    stepType="DeepDive"
                    exampleCardIndex={cardIndex}
                    isReviewOnly
                    autoConnect={false}
                    compact
                  />
                )}
              </div>
              <div className="mt-3 flex flex-wrap gap-3">
                <button
                  type="button"
                  onClick={deepDivePracticePrevCard}
                  disabled={cardIndex <= 0}
                  className="flex items-center gap-1.5 rounded-xl border border-slate-300 bg-white px-5 py-2.5 text-sm font-semibold text-slate-600 transition hover:bg-slate-50 disabled:opacity-50 disabled:hover:bg-white dark:border-slate-600 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700 dark:disabled:hover:bg-slate-800"
                >
                  <SkipBack className="h-4 w-4" /> {t.deepDivePrevCard}
                </button>
                <button
                  type="button"
                  onClick={deepDivePracticeNextCard}
                  disabled={cardIndex >= cards.length - 1}
                  className="flex items-center gap-1.5 rounded-xl border border-emerald-300 bg-emerald-50 px-5 py-2.5 text-sm font-semibold text-emerald-700 transition hover:bg-emerald-100 disabled:opacity-50 disabled:hover:bg-emerald-50 dark:border-emerald-400/30 dark:bg-emerald-500/10 dark:text-emerald-300 dark:hover:bg-emerald-500/20 dark:disabled:hover:bg-emerald-500/10"
                >
                  <SkipForward className="h-4 w-4" /> {t.deepDiveNextCard}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    );
  }

  if (newlyUnlocked !== null) {
    return (
      <div className="mx-auto max-w-2xl p-8">
        <div className="rounded-2xl border border-emerald-400/20 bg-emerald-500/10 p-8 text-center">
          <Check className="mx-auto h-10 w-10 text-emerald-600 dark:text-emerald-400" />
          <h1 className="mt-3 text-xl font-semibold text-emerald-700 dark:text-emerald-300">{t.completedBannerTitle}</h1>
          {(newlyUnlocked ?? []).length > 0 && (
            <div className="mt-4 text-sm text-emerald-700 dark:text-emerald-200">
              <p className="font-medium">{t.newlyUnlockedTitle}</p>
              <ul className="mt-1 space-y-1">
                {(newlyUnlocked ?? []).map((n) => (
                  <li key={n.CapabilityGraphNodeId}>{n.Name}</li>
                ))}
              </ul>
            </div>
          )}
          <button
            onClick={() => navigate(backToMapHref)}
            className="mt-6 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
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
      <Link to={backToMapHref} className="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:underline">
        ← {t.backToMap}
      </Link>

      {nodeInfo && (
        <div className="mt-4 rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-5">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <h1 className="text-xl font-semibold tracking-tight text-slate-900 dark:text-white">{nodeInfo.name}</h1>
            <span className="whitespace-nowrap text-xs text-slate-500">
              {t.nodeStartTimeLabel}: {startTime.toLocaleTimeString(language, { hour: '2-digit', minute: '2-digit' })}
            </span>
          </div>
          {nodeInfo.description && (
            <p className="mt-2 text-sm leading-relaxed text-slate-500 dark:text-slate-400">
              <span className="font-medium text-slate-500">{t.nodeObjectiveLabel}: </span>
              {nodeInfo.description}
            </p>
          )}
        </div>
      )}

      <StepperBar currentIndex={currentIndex} t={t} onStepClick={handleStepIconClick} />

      {/* "Aprobar (demo HR)" — HRAdmin-only, only shown on a gated step
          (Hypothesis/Teaching never need approval, they just Continue).
          Backend independently re-verifies OrgRole=HRAdmin on every call
          (see HrForceAdvanceStepFunction.cs) — hiding this button is only
          a convenience for everyone else, not the actual security check. */}
      {user?.orgRole === 'HRAdmin' && step.stepType !== 'Hypothesis' && step.stepType !== 'Teaching' && (
        <div className="mt-4 flex items-center justify-end gap-2 rounded-xl border border-amber-400/30 bg-amber-500/10 px-4 py-2.5">
          <ShieldCheck className="h-4 w-4 shrink-0 text-amber-600 dark:text-amber-400" />
          <span className="text-xs text-amber-700 dark:text-amber-300">Modo demo HR: puedes aprobar este paso sin responderlo.</span>
          <button
            type="button"
            onClick={handleForceApprove}
            disabled={forceApproving}
            className="whitespace-nowrap rounded-lg bg-amber-500 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-amber-600 disabled:opacity-60"
          >
            {forceApproving ? '...' : 'Aprobar (temporal)'}
          </button>
        </div>
      )}

      {stepReview && (
        <StepReviewModal review={stepReview} onClose={closeStepReview} t={t} language={language} />
      )}

      {expansionLoading && <KnowledgeExpansionLoadingModal t={t} />}

      {/* "Agente de voz" — real-time WebRTC voice tutor (2026-07-22, ported
          from Capability Studio, positioned here — above the step content —
          to match Studio's own layout). Only on Hypothesis/Teaching — never
          on Recall for a real student (would let them fish for hints/
          answers mid-recall, defeating unaided retrieval), and never on
          Production/Assessment. */}
      {(step.stepType === 'Hypothesis' || step.stepType === 'Teaching') && (
        <VoiceTutorAgent learningSessionStepId={step.learningSessionStepId} />
      )}

      <div className="mt-6 rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-6">
        {errorMessage && <p className="mb-4 text-sm text-red-600 dark:text-red-400">{errorMessage}</p>}

        {/* Recall's content is shown via recallLastPrompt below, Deep
            Dive's via deepDiveLastPrompt below, and Assessment's content is
            an internal grading rubric ("Criterios observables de
            dominio...") never meant for the student — the actual
            questions come from AssessmentPanel's round state. */}
        {step.stepType !== 'Recall' && step.stepType !== 'DeepDive' && step.stepType !== 'Assessment' && (
          <RichContent className="text-slate-600 dark:text-slate-300" html={step.content} />
        )}

        {(step.illustrations?.length ?? 0) > 0 && (
          <div className="mt-4 flex flex-col gap-4">
            {step.illustrations.map((illustration) => (
              <figure
                key={illustration.illustrationId}
                className="overflow-hidden rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02]"
              >
                <img
                  src={`${API_BASE_URL}/illustrations/${illustration.illustrationId}/image`}
                  alt={illustration.caption ?? ''}
                  className="h-auto w-full object-contain"
                />
                {illustration.caption && (
                  <figcaption className="border-t border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] px-3 py-1.5 text-xs text-slate-500 dark:text-slate-400">
                    {illustration.caption}
                  </figcaption>
                )}
              </figure>
            ))}
          </div>
        )}

        {/* Deep Dive — sequential, mandatory-all-cards quiz (see
            backend/HumanOS/Agentic/Runtime/DeepDiveLoopGate.cs and
            /memories/repo — every card must be individually mastered, in
            order, unlike Recall's distinct-N-of-pool rule. Only the
            CURRENT card is shown; there is no attempt cap, the student
            just keeps retrying the same card until mastered. A big reward
            star plays once every card is done, via the same RewardBanner
            used by Recall. */}
        {step.stepType === 'DeepDive' && !pendingAdvance && step.exampleCards && step.exampleCards.length > 0 && (
          <div className="mt-4">
            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
              {t.deepDiveCardLabel} {Math.min(deepDiveCardIndex + 1, step.exampleCards.length)} {t.assessmentOfLabel}{' '}
              {deepDiveTotalCards || step.exampleCards.length}
            </p>
            {(() => {
              const card = step.exampleCards![Math.min(deepDiveCardIndex, step.exampleCards!.length - 1)];
              return (
                <div className="mt-2 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-4">
                  {card.IllustrationId ? (
                    <img
                      src={`${API_BASE_URL}/illustrations/${card.IllustrationId}/image`}
                      alt={card.Title}
                      className="mb-2 h-auto max-h-80 w-full rounded-lg object-contain bg-black/20"
                    />
                  ) : (
                    card.Emoji && <div className="mb-2 text-3xl leading-none">{card.Emoji}</div>
                  )}
                  <p className="text-sm font-semibold text-slate-900 dark:text-white">{card.Title}</p>
                  <RichContent className="mt-1 text-sm text-slate-600 dark:text-slate-300" html={card.Text} />
                  {/* "Habla con tu maestro" — same compact, per-card voice
                      Agent as Capability Studio's PreviewNodePage, ported
                      here (2026-08-05, restores what was missing). Doesn't
                      auto-connect (student must press it); when the Agent
                      itself decides the spoken explanation is good enough
                      (mark_explanation_understood), it's graded exactly
                      like a typed answer via handleDeepDiveSubmit's
                      overrideText, without ever touching the visible
                      textarea below. */}
                  <VoiceTutorAgent
                    learningSessionStepId={step.learningSessionStepId}
                    exampleCardIndex={deepDiveCardIndex}
                    autoConnect={false}
                    compact
                    onExplanationAccepted={(explanation) => void handleDeepDiveSubmit(explanation)}
                  />
                </div>
              );
            })()}
          </div>
        )}

        {showsTutorChat && (
          <div className="mt-6 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-4">
            <p className="mb-3 flex items-center gap-1.5 text-xs text-slate-500">
              <MessageCircle className="h-3.5 w-3.5" /> {t.tutorGuidesNotGrades}
            </p>
            <TutorChat messages={tutorMessages} loading={tutorLoading} />
            <div className="mt-3 flex gap-2">
              <input
                value={tutorInput}
                onChange={(e) => setTutorInput(e.target.value)}
                placeholder={t.askTutorPlaceholder}
                className="input flex-1"
                onKeyDown={(e) => e.key === 'Enter' && handleAskTutor()}
              />
              <button
                onClick={handleAskTutor}
                disabled={tutorLoading || !tutorInput.trim()}
                className="flex items-center gap-1 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-3.5 py-2 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.03] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
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
            <label className="mb-1 block text-sm font-medium text-slate-600 dark:text-slate-300">{t.yourResponseLabel}</label>
            <textarea
              value={responseText}
              onChange={(e) => setResponseText(e.target.value)}
              rows={4}
              className="input"
            />
            <button
              onClick={handleContinue}
              disabled={submitting || !responseText.trim()}
              className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
            >
              {t.continueButton}
            </button>
          </div>
        )}

        {/* Deep Dive — same reward-pause pattern as Recall once every
            card in order has been mastered; otherwise, a fresh question on
            the CURRENT card each attempt (never advancing card index until
            mastered), scored via TutorSubmitDeepDiveAttempt. */}
        {step.stepType === 'DeepDive' && (
          pendingAdvance ? (
            <RewardBanner title={pendingAdvance.title} onContinue={handlePendingAdvanceContinue} t={t} />
          ) : (
            <div className="mt-6">
              {/* Before the student's first attempt on this card, there is
                  no tutor-generated question yet (deepDiveLastPrompt is only
                  set after a submission) — show a clear, card-specific task
                  instead of the generic Deep Dive blueprint overview text.
                  Once the tutor responds, its own dynamic question/follow-up
                  takes over. */}
              {deepDiveLastPrompt ? (
                <RichContent className="text-slate-600 dark:text-slate-300" html={deepDiveLastPrompt} />
              ) : (
                <p className="text-slate-600 dark:text-slate-300">
                  {t.deepDiveOpeningPromptBefore} “
                  {step.exampleCards?.[Math.min(deepDiveCardIndex, (step.exampleCards?.length ?? 1) - 1)]?.Title ?? ''}”
                  {t.deepDiveOpeningPromptAfter}
                </p>
              )}

              <label className="mb-1 mt-4 block text-sm font-medium text-slate-600 dark:text-slate-300">{t.yourResponseLabel}</label>
              <textarea
                value={responseText}
                onChange={(e) => setResponseText(e.target.value)}
                rows={3}
                className="input"
              />
              <div className="mt-3 flex flex-wrap gap-3">
                <button
                  onClick={() => void handleDeepDiveSubmit()}
                  disabled={submitting || !responseText.trim()}
                  className="rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
                >
                  {t.deepDiveSubmit}
                </button>
                {/* "Anterior" (2026-08-05: "queiro que el usuario siempre
                    pueda regresar a ver sus tarjetas sin restricción") —
                    purely local card navigation, never touches grading, so
                    it's only ever disabled on card 1 (nothing before it). */}
                <button
                  type="button"
                  onClick={handleDeepDivePrevCard}
                  disabled={deepDiveCardIndex <= 0}
                  className="flex items-center gap-1.5 rounded-xl border border-slate-300 bg-white px-5 py-2.5 text-sm font-semibold text-slate-600 transition hover:bg-slate-50 disabled:opacity-50 disabled:hover:bg-white dark:border-slate-600 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700 dark:disabled:hover:bg-slate-800"
                >
                  <SkipBack className="h-4 w-4" /> {t.deepDivePrevCard}
                </button>
                {/* Shown on every card, ALWAYS enabled (2026-08-05: "en cada
                    tarjeta pon siguiente tarjeta" then "siguiente tarjeta
                    siempre tiene que estar disponible") — on the last card
                    this calls skipDeepDiveStep (RuntimeSkipDeepDiveStepFunction)
                    to advance past Deep Dive ungraded instead of just
                    moving the local card index. */}
                <button
                  type="button"
                  onClick={() => void handleDeepDiveNextCard()}
                  disabled={skippingDeepDive}
                  className="flex items-center gap-1.5 rounded-xl border border-emerald-300 bg-emerald-50 px-5 py-2.5 text-sm font-semibold text-emerald-700 transition hover:bg-emerald-100 disabled:opacity-50 disabled:hover:bg-emerald-50 dark:border-emerald-400/30 dark:bg-emerald-500/10 dark:text-emerald-300 dark:hover:bg-emerald-500/20 dark:disabled:hover:bg-emerald-500/10"
                >
                  <SkipForward className="h-4 w-4" /> {t.deepDiveNextCard}
                </button>
              </div>
            </div>
          )
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
              <p className="mt-2 text-sm text-amber-700 dark:text-amber-200">{productionEvaluation.Feedback}</p>
            </RewardBanner>
          ) : (
            <div className="mt-6">
              <label className="mb-1 block text-sm font-medium text-slate-600 dark:text-slate-300">{t.yourResponseLabel}</label>
              <textarea
                value={responseText}
                onChange={(e) => setResponseText(e.target.value)}
                rows={4}
                className="input"
              />
              {productionEvaluation && !productionEvaluation.IsCorrect && (
                <div className="mt-3 rounded-lg border border-red-400/20 bg-red-500/10 p-3 text-sm text-red-700 dark:text-red-300">
                  {productionEvaluation.Feedback}
                </div>
              )}
              <button
                onClick={productionEvaluation ? handleProductionRetry : handleProductionSubmit}
                disabled={productionSubmitting || (!productionEvaluation && !responseText.trim())}
                className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
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
            {/* "Profundizar" (Knowledge Expansion) — explicit, deliberate
                learner action only, never auto-surfaced (see
                /memories/repo/adaptive-learning-engine-design.md). Combines
                the LLM's own knowledge with a live Bing Grounding search. */}
            <div className="mb-4 rounded-xl border border-accent-400/20 bg-accent-500/5 p-4">
              {!knowledgeExpansion && (
                <button
                  onClick={handleExpandKnowledge}
                  disabled={expansionLoading}
                  className="flex items-center gap-1.5 rounded-lg border border-accent-400/30 bg-slate-100 dark:bg-white/[0.04] px-3.5 py-2 text-sm font-medium text-accent-600 dark:text-accent-300 hover:bg-accent-500/10 disabled:opacity-50"
                >
                  <Sparkles className="h-4 w-4" />
                  {expansionLoading ? t.expandKnowledgeLoading : t.expandKnowledgeButton}
                </button>
              )}
              {expansionError && <p className="mt-2 text-sm text-red-600 dark:text-red-400">{expansionError}</p>}
              {knowledgeExpansion && (
                <div>
                  <button
                    type="button"
                    onClick={() => setExpansionPanelOpen((open) => !open)}
                    className="flex w-full items-center justify-between gap-1.5 text-xs font-medium uppercase tracking-wide text-accent-600 dark:text-accent-300"
                  >
                    <span className="flex items-center gap-1.5">
                      <Sparkles className="h-3.5 w-3.5" /> {t.expandKnowledgeTitle}
                    </span>
                    {expansionPanelOpen ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
                  </button>
                  {expansionPanelOpen && (
                    <div className="mt-2">
                      <RichContent className="text-slate-600 dark:text-slate-300" html={knowledgeExpansion.Content} />
                      {knowledgeExpansion.DiagramIllustrationId && (
                        <figure className="mt-3 overflow-hidden rounded-lg border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02]">
                          <img
                            src={`${API_BASE_URL}/illustrations/${knowledgeExpansion.DiagramIllustrationId}/image`}
                            alt=""
                            className="h-auto w-full object-contain"
                          />
                        </figure>
                      )}
                    </div>
                  )}
                </div>
              )}
            </div>

            <label className="mb-1 block text-sm font-medium text-slate-600 dark:text-slate-300">{t.teachingNotesLabel}</label>
            <textarea
              value={responseText}
              onChange={(e) => setResponseText(e.target.value)}
              rows={3}
              className="input"
            />
            <button
              onClick={handleContinue}
              disabled={submitting}
              className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
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
              <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {t.recallItemsMasteredLabel} {recallItemsMastered}/{RECALL_ITEMS_REQUIRED} ·{' '}
                {t.recallAttemptLabel} {recallAttemptsUsed + 1} {t.assessmentOfLabel} {RECALL_MAX_ATTEMPTS}
              </p>
              <RichContent className="mt-2 text-slate-600 dark:text-slate-300" html={recallLastPrompt ?? step.content} />

              <label className="mb-1 mt-4 block text-sm font-medium text-slate-600 dark:text-slate-300">{t.yourResponseLabel}</label>
              <textarea
                value={responseText}
                onChange={(e) => setResponseText(e.target.value)}
                rows={3}
                className="input"
              />
              <div className="mt-3 flex flex-wrap items-center gap-3">
                <button
                  onClick={handleRecallSubmit}
                  disabled={submitting || !responseText.trim()}
                  className="rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
                >
                  {t.recallSubmit}
                </button>
                <button
                  type="button"
                  onClick={() => handleStepIconClick('Teaching')}
                  className="text-sm text-slate-500 underline decoration-dotted underline-offset-2 hover:text-slate-700 dark:hover:text-slate-300"
                >
                  {t.recallReviewTeachingLink}
                </button>
              </div>
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

/**
 * Renders step/blueprint Content as sanitized HTML instead of a plain
 * string. ExperienceDesignerAgent is instructed to use a small set of
 * semantic tags (p/strong/em/ul/ol/li/a/...) to structure lesson content
 * (bold key terms, bullet lists, citation links from web-grounded
 * findings) — DOMPurify strips anything else (scripts, event handlers,
 * javascript: URLs) before it ever reaches the DOM. Plain text with no
 * HTML tags (e.g. still-plain-text Recall/Assessment prompts from other
 * agents) renders identically to before, since 'white-space: pre-wrap'
 * still preserves their newlines.
 */
function RichContent({ html, className }: { html: string; className?: string }) {
  const sanitized = useMemo(
    () =>
      DOMPurify.sanitize(html, {
        ALLOWED_TAGS: ['p', 'br', 'strong', 'b', 'em', 'i', 'ul', 'ol', 'li', 'h3', 'h4', 'blockquote', 'code', 'a', 'span'],
        ALLOWED_ATTR: ['href'],
      }),
    [html],
  );

  return (
    <div
      className={`${className ?? ''} [&_a]:text-brand-700 dark:[&_a]:text-brand-300 [&_a]:underline [&_ul]:list-disc [&_ul]:pl-5 [&_ol]:list-decimal [&_ol]:pl-5`}
      style={{ whiteSpace: 'pre-wrap' }}
      dangerouslySetInnerHTML={{ __html: sanitized }}
    />
  );
}

const STEP_ICONS: Record<ExperienceStepType, typeof Lightbulb> = {
  Hypothesis: Lightbulb,
  Teaching: BookOpen,
  DeepDive: Images,
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
    DeepDive: t.stepDeepDive,
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
                  className="flex h-9 w-9 items-center justify-center rounded-full border-2 border-emerald-400 bg-emerald-500 text-[#fff] shadow-lg shadow-emerald-500/20 transition hover:brightness-110"
                >
                  <Check className="h-4 w-4" />
                </button>
              ) : (
                <div
                  className={`flex h-9 w-9 items-center justify-center rounded-full border-2 ${
                    isActive
                      ? 'border-brand-400 bg-brand-500/10 text-brand-700 dark:text-brand-300'
                      : 'border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] text-slate-500'
                  }`}
                >
                  <Icon className="h-4 w-4" />
                </div>
              )}
              <span
                className={`text-[11px] font-medium ${isActive ? 'text-brand-700 dark:text-brand-300' : isDone ? 'text-emerald-700 dark:text-emerald-300' : 'text-slate-500'}`}
              >
                {labels[stepType]}
              </span>
            </div>
            {index < STEP_ORDER.length - 1 && (
              <div className={`mx-1 h-0.5 flex-1 ${index < currentIndex ? 'bg-emerald-400/50' : 'bg-slate-200 dark:bg-white/10'}`} />
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
      <RichContent className="text-sm leading-relaxed text-slate-600 dark:text-slate-300" html={data.Content} />

      {data.ExampleCards && data.ExampleCards.length > 0 && (
        <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
          {data.ExampleCards.map((card, index) => (
            <div key={index} className="rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-3">
              {card.IllustrationId ? (
                <img
                  src={`${API_BASE_URL}/illustrations/${card.IllustrationId}/image`}
                  alt={card.Title}
                  className="mb-1.5 h-24 w-full rounded-lg object-cover"
                />
              ) : (
                card.Emoji && <div className="mb-1.5 text-2xl leading-none">{card.Emoji}</div>
              )}
              <p className="text-sm font-semibold text-slate-900 dark:text-white">{card.Title}</p>
              <RichContent className="mt-1 text-sm text-slate-600 dark:text-slate-300" html={card.Text} />
            </div>
          ))}
        </div>
      )}

      {data.Evidence.length > 0 ? (
        <div className="mt-5 space-y-3 border-t border-slate-200 dark:border-white/10 pt-4">
          <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{t.stepReviewYourAnswers}</p>
          {data.Evidence.map((entry, index) => (
            <div key={index} className="rounded-lg bg-white dark:bg-white/[0.03] p-3">
              {entry.TutorPrompt && <p className="text-xs text-slate-500">{entry.TutorPrompt}</p>}
              <p className="mt-1 text-sm font-medium text-slate-700 dark:text-slate-200">{entry.StudentResponse}</p>
              <p className="mt-1 text-[11px] text-slate-500">
                {new Date(entry.CreatedDate).toLocaleString(language, { dateStyle: 'short', timeStyle: 'short' })}
                {typeof entry.TutorScore === 'number' && ` · ${t.stepReviewScoreLabel} ${entry.TutorScore}`}
              </p>
            </div>
          ))}
        </div>
      ) : (
        <p className="mt-5 border-t border-slate-200 dark:border-white/10 pt-4 text-sm text-slate-500">{t.stepReviewNoAnswers}</p>
      )}
    </>
  );
}

/**
 * Full-viewport overlay shown while the "Profundizar" (Knowledge
 * Expansion) request is in flight. This call can take up to ~30-60s (live
 * LLM call + Bing Grounding search + optional diagram generation), so a
 * silent spinner would feel broken — this shows a small rotating list of
 * "what's happening" steps to make the wait feel purposeful. Rendered via
 * a portal to document.body, same pattern as StepReviewModal. Purely
 * decorative/informational — has no onClose, it disappears automatically
 * once expansionLoading flips back to false in the parent.
 */
function KnowledgeExpansionLoadingModal({ t }: { t: Record<string, string> }) {
  const steps = useMemo(
    () => [t.expandKnowledgeModalStep1, t.expandKnowledgeModalStep2, t.expandKnowledgeModalStep3, t.expandKnowledgeModalStep4],
    [t]
  );
  const [activeStep, setActiveStep] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setActiveStep((current) => (current + 1) % steps.length);
    }, 2200);
    return () => clearInterval(interval);
  }, [steps.length]);

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-950 p-7 text-center shadow-2xl shadow-black/40">
        <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-accent-500/10">
          <Sparkles className="h-7 w-7 animate-pulse text-accent-600 dark:text-accent-400" />
        </div>
        <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{t.expandKnowledgeModalTitle}</h2>
        <p className="mt-1.5 text-sm text-slate-500 dark:text-slate-400">{t.expandKnowledgeModalSubtitle}</p>

        <ul className="mt-6 space-y-3 text-left">
          {steps.map((label, index) => {
            const isDone = index < activeStep;
            const isActive = index === activeStep;
            return (
              <li key={label} className="flex items-center gap-3">
                <span
                  className={
                    'flex h-6 w-6 flex-none items-center justify-center rounded-full transition-colors ' +
                    (isDone
                      ? 'bg-accent-500 text-[#fff]'
                      : isActive
                        ? 'border-2 border-accent-400'
                        : 'border-2 border-slate-200 dark:border-white/10')
                  }
                >
                  {isDone ? (
                    <Check className="h-3.5 w-3.5" />
                  ) : isActive ? (
                    <span className="h-2.5 w-2.5 animate-pulse rounded-full bg-accent-400" />
                  ) : null}
                </span>
                <span className={'text-sm ' + (isActive ? 'font-medium text-slate-900 dark:text-white' : isDone ? 'text-slate-500 line-through' : 'text-slate-500')}>
                  {label}
                </span>
              </li>
            );
          })}
        </ul>

        <div className="mt-6 h-1.5 w-full overflow-hidden rounded-full bg-slate-200 dark:bg-white/10">
          <div className="h-full w-1/3 animate-loadingBar rounded-full bg-accent-500" />
        </div>
      </div>
    </div>,
    document.body
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
    DeepDive: t.stepDeepDive,
    Recall: t.stepRecall,
    Production: t.stepProduction,
    Assessment: t.stepAssessment,
  };

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-sm">
      <div className="max-h-[85vh] w-full max-w-lg overflow-y-auto rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-950 p-6 shadow-2xl shadow-black/40">
        <div className="mb-4 flex items-start justify-between gap-3">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{stepLabels[review.stepType]}</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded-full p-1 text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10 hover:text-slate-900 dark:hover:text-white"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {review.loading && <p className="text-sm text-slate-500 dark:text-slate-400">{t.stepReviewLoading}</p>}
        {review.error && <p className="text-sm text-red-600 dark:text-red-400">{t.nodeError}</p>}

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
  personId,
  t,
  language,
}: {
  nodeInfo: { name: string; description?: string } | null;
  summary: NodeSummaryDto;
  onPracticeAgain: () => void;
  practicing: boolean;
  personId: string;
  t: Record<string, string>;
  language: string;
}) {
  // "Reactivar Engrama" (2026-07-24) — a second, distinct action from
  // "Practicar de nuevo": a full-lesson active-recall STUDY conversation,
  // never an exam, never graded. Rendered as a dedicated modal overlay so
  // it can own the entire screen while the voice conversation is live.
  const [showEngramReview, setShowEngramReview] = useState(false);
  const stepLabels: Record<ExperienceStepType, string> = {
    Hypothesis: t.stepHypothesis,
    Teaching: t.stepTeaching,
    DeepDive: t.stepDeepDive,
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
      <div className="rounded-2xl border border-emerald-400/20 bg-emerald-500/10 p-6">
        <div className="flex items-center gap-2 text-emerald-700 dark:text-emerald-300">
          <Check className="h-5 w-5" />
          <h1 className="text-lg font-semibold">{nodeInfo?.name ?? t.nodeSummaryTitle}</h1>
        </div>
        {nodeInfo?.description && <p className="mt-2 text-sm text-emerald-700 dark:text-emerald-200">{nodeInfo.description}</p>}

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
          <div className="mt-5 border-t border-emerald-400/20 pt-4">
            <p className="text-xs font-medium uppercase tracking-wide text-emerald-600 dark:text-emerald-400">{t.nodeSummaryPastAttempts}</p>
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
                        ? 'border-emerald-400 bg-emerald-500 text-[#fff]'
                        : 'border-emerald-400/30 bg-white dark:bg-white/[0.03] text-emerald-700 dark:text-emerald-200 hover:bg-emerald-500/10'
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

        <div className="mt-5 flex flex-wrap items-start gap-4">
          <button
            onClick={onPracticeAgain}
            disabled={practicing}
            className="rounded-xl bg-emerald-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-emerald-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
          >
            {practicing ? t.nodeSummaryPracticing : t.nodeSummaryPracticeAgain}
          </button>
          <div className="max-w-xs">
            <button
              onClick={() => setShowEngramReview(true)}
              className="flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-accent-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
            >
              <Sparkles className="h-4 w-4" />
              {t.reactivateEngramButton}
            </button>
            <p className="mt-1.5 text-xs text-emerald-700/80 dark:text-emerald-200/70">{t.reactivateEngramExplanation}</p>
          </div>
        </div>
      </div>

      {showEngramReview && (
        <EngramReviewAgent
          personId={personId}
          capabilityGraphNodeId={summary.CapabilityGraphNodeId}
          onExit={() => setShowEngramReview(false)}
        />
      )}

      <div className="space-y-4">
        {loadingAttempt && <p className="text-sm text-slate-500">{t.stepReviewLoading}</p>}
        {!loadingAttempt &&
          viewedSteps.map((stepData) => {
            const Icon = STEP_ICONS[stepData.StepType];
            return (
              <div key={stepData.StepType} className="rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-5">
                <div className="mb-3 flex items-center gap-2">
                  <Icon className="h-4 w-4 text-slate-500 dark:text-slate-400" />
                  <h2 className="text-sm font-semibold text-slate-700 dark:text-slate-200">{stepLabels[stepData.StepType]}</h2>
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
      <p className="text-[11px] uppercase tracking-wide text-emerald-600 dark:text-emerald-400">{label}</p>
      <p className="text-sm font-semibold text-emerald-700 dark:text-emerald-100">{value}</p>
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
    <div className="mt-6 rounded-xl border border-amber-400/20 bg-amber-500/10 p-6 text-center">
      <Star className="mx-auto h-9 w-9 text-amber-600 dark:text-amber-400" fill="currentColor" />
      <p className="mt-2 text-base font-semibold text-amber-700 dark:text-amber-200">{title}</p>
      {children}
      <button
        onClick={onContinue}
        className="mt-4 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
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
    <div className="mt-6 rounded-xl border border-brand-400/20 bg-brand-500/10 p-6 text-center">
      <BookOpen className="mx-auto h-9 w-9 text-brand-700 dark:text-brand-300" />
      <p className="mt-2 text-base font-semibold text-brand-700 dark:text-brand-200">{title}</p>
      <button
        onClick={onContinue}
        className="mt-4 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
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
            message.author === 'student' ? 'ml-8 bg-brand-500/20 text-brand-700 dark:text-brand-100' : 'mr-8 bg-slate-100 dark:bg-white/[0.05] text-slate-600 dark:text-slate-300 shadow-sm'
          }`}
        >
          {message.text}
        </div>
      ))}
      {loading && <p className="mr-8 rounded-lg bg-slate-100 dark:bg-white/[0.05] px-3 py-2 text-sm italic text-slate-500 shadow-sm">…</p>}
    </div>
  );
}

const CORRECTNESS_STYLES: Record<string, string> = {
  Correct: 'border-emerald-400/20 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300',
  PartiallyCorrect: 'border-amber-400/20 bg-amber-500/10 text-amber-600 dark:text-amber-300',
  Incorrect: 'border-red-400/20 bg-red-500/10 text-red-700 dark:text-red-300',
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
          <div className={`rounded-xl border p-4 ${passed ? 'border-emerald-400/20 bg-emerald-500/10' : 'border-amber-400/20 bg-amber-500/10'}`}>
            <p className={`text-sm font-medium ${passed ? 'text-emerald-700 dark:text-emerald-300' : 'text-amber-600 dark:text-amber-300'}`}>
              {passed ? t.assessmentPassed : t.assessmentFailed}
            </p>
            <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
              {t.assessmentFinalScoreLabel}: {lastResult.FinalScore}/100
            </p>
            <p className="mt-2 text-xs leading-relaxed text-slate-500">{t.assessmentScoreExplanation}</p>
            {!passed && <p className="mt-2 text-sm text-amber-600 dark:text-amber-300">{t.assessmentRoundFailedMessage}</p>}
          </div>

          {passed ? (
            <button
              onClick={onCompleteNode}
              disabled={completing}
              className="mt-4 rounded-xl bg-emerald-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-emerald-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
            >
              {t.completeNodeButton}
            </button>
          ) : (
            <button
              onClick={onContinue}
              className="mt-4 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
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
            {lastResult.Grade.Correctness === 'Correct' && <Star className="h-4 w-4 text-amber-600 dark:text-amber-400" fill="currentColor" />}
            {correctnessLabel(lastResult.Grade.Correctness, t)}
          </p>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{lastResult.Grade.Feedback}</p>
        </div>
        <button
          onClick={onContinue}
          className="mt-4 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
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
        <div className={`rounded-xl border p-4 ${passed ? 'border-emerald-400/20 bg-emerald-500/10' : 'border-amber-400/20 bg-amber-500/10'}`}>
          <p className={`text-sm font-medium ${passed ? 'text-emerald-700 dark:text-emerald-300' : 'text-amber-600 dark:text-amber-300'}`}>
            {passed ? t.assessmentRoundPassedMessage : t.assessmentFailed}
          </p>
          {round.FinalScore !== null && round.FinalScore !== undefined && (
            <>
              <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
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
            className="mt-4 rounded-xl bg-emerald-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-emerald-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
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
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
        {t.assessmentRoundNumberLabel} {round.RoundNumber} · {t.assessmentQuestionLabel} {question.QuestionIndex} {t.assessmentOfLabel}{' '}
        {round.TotalQuestions}
      </p>
      <RichContent className="mt-2 text-slate-600 dark:text-slate-300" html={question.QuestionText} />
      {question.IllustrationId && (
        <figure className="mt-3 overflow-hidden rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02]">
          <img
            src={`${API_BASE_URL}/illustrations/${question.IllustrationId}/image`}
            alt=""
            className="h-auto w-full object-contain"
          />
        </figure>
      )}
      <label className="mb-1 mt-4 block text-sm font-medium text-slate-600 dark:text-slate-300">{t.yourResponseLabel}</label>
      <textarea
        value={answerText}
        onChange={(e) => onAnswerTextChange(e.target.value)}
        rows={4}
        className="input"
      />
      <button
        onClick={onSubmitAnswer}
        disabled={submitting || !answerText.trim()}
        className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:hover:scale-100"
      >
        {t.assessmentSubmitAnswer}
      </button>
    </div>
  );
}
