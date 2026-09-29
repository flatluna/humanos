import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import DOMPurify from 'dompurify';
import { ArrowLeft, Check, Lightbulb, BookOpenText, Layers, Brain, PenLine, ClipboardCheck, CheckCircle2, Loader2, Send } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { API_BASE_URL } from '../api/httpClient';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import {
  getStructuredLearningDocument,
  getStructuredLearningHypothesisResponses,
  submitStructuredLearningHypothesisResponses,
  submitStructuredLearningRecallAttempt,
  getStructuredLearningRecallState,
  startStructuredLearningAssessmentRound,
  getActiveStructuredLearningAssessmentRound,
  submitStructuredLearningAssessmentAnswer,
  getStructuredLearningDeepDiveState,
  submitStructuredLearningDeepDiveAttempt,
  type StructuredLearningDocumentDetail,
  type StructuredLearningStepDetail,
  type StructuredLearningAssessmentRoundState,
} from '../api/structuredLearningRuntimeApi';

DOMPurify.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A') {
    node.setAttribute('target', '_blank');
    node.setAttribute('rel', 'noopener noreferrer');
  }
});

const STEP_ORDER = ['Hypothesis', 'Teaching', 'DeepDive', 'Recall', 'Production', 'Assessment'];
const GATED_STEP_TYPES = new Set(['DeepDive', 'Recall', 'Assessment']);
const RECALL_MAX_ATTEMPTS = 5;

const STEP_META: Record<string, { label: string; Icon: typeof Lightbulb }> = {
  Hypothesis: { label: 'Before we start', Icon: Lightbulb },
  Teaching: { label: 'Teaching', Icon: BookOpenText },
  DeepDive: { label: 'Deep Dive', Icon: Layers },
  Recall: { label: 'Recall', Icon: Brain },
  Production: { label: 'Apply it', Icon: PenLine },
  Assessment: { label: 'Assessment', Icon: ClipboardCheck },
};

function imageUrl(illustrationId: string): string {
  return `${API_BASE_URL}/illustrations/structured-learning/${illustrationId}/image`;
}

type HypothesisQuestion = { question: string; options: string[]; correctIndex: number | null };

function parseHypothesisQuestions(step: StructuredLearningStepDetail): HypothesisQuestion[] | null {
  if (!step?.StepDataJson) return null;
  try {
    const parsed = JSON.parse(step.StepDataJson) as unknown;
    const questionList = Array.isArray(parsed)
      ? parsed
      : Array.isArray((parsed as { questions?: unknown })?.questions)
        ? (parsed as { questions: unknown[] }).questions
        : null;
    if (!questionList) return null;
    return questionList
      .map((item) => {
        const q = item as Record<string, unknown>;
        const question = typeof q.question === 'string' ? q.question : typeof q.Question === 'string' ? q.Question : '';
        const optionList = Array.isArray(q.options) ? q.options : Array.isArray(q.Options) ? q.Options : [];
        const options = optionList.map((o) => (typeof o === 'string' ? o : String(o ?? '').trim())).filter((o) => o.length > 0);
        const correctIndex =
          typeof q.correctIndex === 'number' ? q.correctIndex : typeof q.CorrectIndex === 'number' ? q.CorrectIndex : null;
        if (!question.trim() || options.length === 0) return null;
        return { question: question.trim(), options, correctIndex: correctIndex != null && correctIndex >= 0 && correctIndex < options.length ? correctIndex : null };
      })
      .filter((q): q is HypothesisQuestion => q !== null);
  } catch {
    return null;
  }
}

/** Real per-student persistence (2026-08-13/14) — answers are saved
 * server-side under the real signed-in student's PersonId, so they
 * survive reloads/devices and are visible to an instructor. */
function HypothesisQuestionCard({ sectionId, personId, step }: { sectionId: string; personId: string; step: StructuredLearningStepDetail }) {
  const questions = parseHypothesisQuestions(step);
  const [selectedAnswers, setSelectedAnswers] = useState<Record<number, number>>({});
  const [submitted, setSubmitted] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!step.StructuredLearningSectionStepId || !personId) return;
    getStructuredLearningHypothesisResponses(sectionId, personId)
      .then((saved) => {
        if (saved.length === 0) return;
        const restored = saved.reduce<Record<number, number>>((acc, r) => {
          acc[r.QuestionIndex] = r.SelectedOptionIndex;
          return acc;
        }, {});
        setSelectedAnswers(restored);
        setSubmitted(true);
      })
      .catch(() => undefined);
  }, [sectionId, personId, step.StructuredLearningSectionStepId]);

  if (!questions || questions.length === 0) return null;

  const answeredCount = Object.keys(selectedAnswers).length;
  const isComplete = questions.length > 0 && answeredCount === questions.length && questions.every((_, index) => selectedAnswers[index] !== undefined);

  async function handleSave() {
    setSaving(true);
    setError(null);
    try {
      await submitStructuredLearningHypothesisResponses(
        sectionId,
        personId,
        Object.entries(selectedAnswers).map(([questionIndex, selectedOptionIndex]) => ({ questionIndex: Number(questionIndex), selectedOptionIndex }))
      );
      setSubmitted(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="mt-4 space-y-4 rounded-2xl border border-brand-200 dark:border-brand-500/20 bg-brand-50 dark:bg-brand-500/[0.04] p-4">
      <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-brand-600 dark:text-brand-300">Before we start</p>
      {submitted ? (
        <div className="space-y-3">
          <div className="rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-100">
            Thank you for your responses. We now will start learning.
          </div>
          {questions.map((question, index) => {
            const selectedIndex = selectedAnswers[index];
            const selectedOption = selectedIndex !== undefined ? question.options[selectedIndex] : null;
            return (
              <div key={`${question.question}-answer-${index}`} className="rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-950/20 p-3">
                <p className="text-sm font-medium text-slate-900 dark:text-white">{index + 1}. {question.question}</p>
                <p className="mt-2 flex items-center gap-2 text-sm text-brand-700 dark:text-brand-300">
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-brand-200 dark:bg-brand-500/20 text-xs font-bold text-brand-800 dark:text-brand-100">
                    {selectedIndex !== undefined ? String.fromCharCode(65 + selectedIndex) : '?'}
                  </span>
                  <span>{selectedOption ?? 'No answer recorded'}</span>
                </p>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="space-y-4">
          {error && <p className="text-sm text-red-600 dark:text-red-300">{error}</p>}
          {questions.map((question, index) => {
            const selectedIndex = selectedAnswers[index];
            return (
              <div key={`${question.question}-${index}`} className="rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-950/20 p-3">
                <p className="text-sm font-medium text-slate-900 dark:text-white">{index + 1}. {question.question}</p>
                <div className="mt-3 space-y-2">
                  {question.options.map((option, optionIndex) => {
                    const isSelected = selectedIndex === optionIndex;
                    return (
                      <button
                        key={`${question.question}-${optionIndex}`}
                        type="button"
                        onClick={() => setSelectedAnswers((previous) => ({ ...previous, [index]: optionIndex }))}
                        className={`flex w-full items-center gap-3 rounded-lg border px-3 py-2 text-left text-sm transition-colors ${
                          isSelected
                            ? 'border-brand-400 bg-brand-100 dark:bg-brand-500/10 text-slate-900 dark:text-white'
                            : 'border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.02] text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-white/[0.04]'
                        }`}
                      >
                        <span className={`flex h-6 w-6 items-center justify-center rounded-full text-[10px] font-bold ${isSelected ? 'bg-brand-200 dark:bg-brand-500/20 text-brand-800 dark:text-brand-100' : 'bg-slate-100 dark:bg-white/5 text-slate-600 dark:text-slate-300'}`}>
                          {String.fromCharCode(65 + optionIndex)}
                        </span>
                        <span className="flex-1">{option}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            );
          })}
          <div className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.02] p-3">
            <p className="text-xs text-slate-500 dark:text-slate-300">{answeredCount}/{questions.length} answered</p>
            <button
              type="button"
              disabled={!isComplete || saving}
              onClick={handleSave}
              className="inline-flex items-center justify-center rounded-lg bg-gradient-to-r from-brand-500 to-accent-500 px-3 py-2 text-xs font-semibold text-white shadow-lg shadow-brand-500/20 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {saving ? 'Saving…' : 'Save responses'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function StepIllustration({ stepDataJson }: { stepDataJson: string | null }) {
  if (!stepDataJson) return null;
  let illustrationId: string | null = null;
  try {
    illustrationId = (JSON.parse(stepDataJson) as { IllustrationId?: string | null }).IllustrationId ?? null;
  } catch {
    return null;
  }
  if (!illustrationId) return null;
  return <img src={imageUrl(illustrationId)} alt="" className="h-72 w-full rounded-xl bg-slate-100 dark:bg-slate-950/50 object-contain object-center" />;
}

interface DeepDiveCard {
  Title: string;
  Text: string;
  Emoji?: string | null;
  IllustrationId?: string | null;
  /** Ordered step-by-step illustrations (e.g. a math derivation) — when
   * present, shown instead of the single IllustrationId as a sequence. */
  IllustrationIds?: string[] | null;
}

/** Deep Dive — a TEACHING/REVIEW walkthrough of example cards, NOT a test
 * (2026-08-14 redesign, explicit user feedback: this used to feel like a
 * quiz forcing a typed explanation to advance, and could loop stuck on
 * one card). Same professor voice orb as the Teaching step (auto-connects,
 * full size), the agent explains each card then asks if the student has
 * questions, and its spoken words are shown as live text underneath.
 * Navigation is now fully FREE — Previous/Next always work, the student
 * can jump back to review any earlier card whenever they want, and
 * reaching past the last card marks the step complete. The backend
 * mark_explanation_understood call still fires in the background to
 * record progress, but never blocks or gates the UI anymore. */
function DeepDiveQuiz({ sectionId, stepId, stepDataJson, onComplete }: { sectionId: string; stepId: string; stepDataJson: string; onComplete: () => void }) {
  let cards: DeepDiveCard[] = [];
  try {
    cards = JSON.parse(stepDataJson) as DeepDiveCard[];
  } catch {
    cards = [];
  }

  const startedRef = useRef(false);
  const completedRef = useRef(false);
  const [cardIndex, setCardIndex] = useState(0);
  const [furthestIndex, setFurthestIndex] = useState(0);
  const [currentPrompt, setCurrentPrompt] = useState<string | undefined>(undefined);
  const [transcript, setTranscript] = useState<string[]>([]);
  const [complete, setComplete] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const markCompleteOnce = () => {
    setComplete(true);
    if (!completedRef.current) {
      completedRef.current = true;
      onComplete();
    }
  };

  useEffect(() => {
    if (startedRef.current) return;
    startedRef.current = true;
    getStructuredLearningDeepDiveState(sectionId)
      .then((state) => {
        setCardIndex(state.CardIndex);
        setFurthestIndex(state.CardIndex);
        setCurrentPrompt(state.NextQuestion || undefined);
        if (state.Complete) markCompleteOnce();
      })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sectionId]);

  // Resets the on-screen transcript whenever a different card is viewed.
  useEffect(() => {
    setTranscript([]);
  }, [cardIndex]);

  /** Fire-and-forget record of progress — never blocks navigation. */
  function recordProgressInBackground(text: string) {
    void submitStructuredLearningDeepDiveAttempt(sectionId, text, currentPrompt).catch(() => undefined);
  }

  function goToCard(index: number) {
    if (index < 0) return;
    if (index >= cards.length) {
      recordProgressInBackground('No tengo preguntas, entendido, sigamos.');
      markCompleteOnce();
      return;
    }
    setFurthestIndex((f) => Math.max(f, index));
    setCardIndex(index);
    setCurrentPrompt(undefined);
  }

  if (error) return <p className="mt-3 text-sm text-red-600 dark:text-red-300">{error}</p>;
  if (cards.length === 0) return null;

  // Once complete, the student can still freely browse every card again —
  // completion just shows a badge, it no longer replaces the card browser
  // (2026-08-15 fix: returning here early hid the 3 cards entirely on revisit).
  const card = cards[Math.min(cardIndex, cards.length - 1)];

  return (
    <div className="mt-3 space-y-3">
      {complete && (
        <div className="flex items-center gap-2 rounded-xl border border-emerald-200 dark:border-emerald-400/30 bg-emerald-50 dark:bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-200">
          <CheckCircle2 className="h-4 w-4" /> Deep Dive complete — every example reviewed. You can still browse the cards below anytime.
        </div>
      )}
      {cards.length > 1 && (
        <div className="flex flex-wrap gap-1.5">
          {cards.map((c, index) => {
            const isViewed = index <= furthestIndex;
            const isCurrent = index === cardIndex;
            return (
              <button
                key={index}
                type="button"
                title={c.Title}
                onClick={() => goToCard(index)}
                className={`flex h-7 min-w-[1.75rem] cursor-pointer items-center justify-center rounded-full border px-2 text-[11px] font-semibold transition ${
                  isCurrent
                    ? 'border-brand-400 bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-200'
                    : isViewed
                      ? 'border-emerald-300 dark:border-emerald-400/40 bg-emerald-50 dark:bg-emerald-500/10 text-emerald-600 dark:text-emerald-300 hover:bg-emerald-100 dark:hover:bg-emerald-500/20'
                      : 'border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.02] text-slate-400 dark:text-slate-600 hover:border-slate-300 dark:hover:border-white/20'
                }`}
              >
                {isViewed && !isCurrent ? <Check className="h-3 w-3" /> : index + 1}
              </button>
            );
          })}
        </div>
      )}

      <div className="rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-4">
        {card.IllustrationIds && card.IllustrationIds.length > 0 ? (
          <div className="mb-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
            {card.IllustrationIds.map((id, i) => (
              <div key={id} className="space-y-1">
                <img src={imageUrl(id)} alt={`${card.Title} — step ${i + 1}`} className="h-auto max-h-56 w-full rounded-lg object-contain bg-black/5 dark:bg-black/20" />
                <p className="text-center text-[11px] font-medium text-slate-500 dark:text-slate-400">Step {i + 1}</p>
              </div>
            ))}
          </div>
        ) : card.IllustrationId ? (
          <img src={imageUrl(card.IllustrationId)} alt={card.Title} className="mb-2 h-auto max-h-72 w-full rounded-lg object-contain bg-black/5 dark:bg-black/20" />
        ) : (
          card.Emoji && <div className="mb-2 text-3xl leading-none">{card.Emoji}</div>
        )}
        <p className="text-sm font-semibold text-slate-900 dark:text-white">{card.Title}</p>
        <p className="mt-1 whitespace-pre-wrap text-sm text-slate-600 dark:text-slate-300">{card.Text}</p>

        {/* Same professor orb as the Teaching step, auto-connecting —
            explains this example warmly, then asks if the student has
            questions. Its own words are echoed as text below via
            onTranscript so students who prefer reading can follow along. */}
        <VoiceTutorAgent
          key={cardIndex}
          structuredLearningSectionStepId={stepId}
          exampleCardIndex={cardIndex}
          onTranscript={(text) => setTranscript((prev) => [...prev, text])}
          onExplanationAccepted={(explanation) => recordProgressInBackground(explanation)}
        />
      </div>

      {transcript.length > 0 && (
        <div className="rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-3">
          <p className="mb-1.5 text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">What you said</p>
          <div className="space-y-1">
            {transcript.map((line, i) => (
              <p key={i} className="text-sm text-slate-700 dark:text-slate-300">{line}</p>
            ))}
          </div>
        </div>
      )}

      {currentPrompt && <p className="text-sm text-slate-600 dark:text-slate-300">{currentPrompt}</p>}

      <div className="flex items-center justify-between gap-3">
        <button
          type="button"
          disabled={cardIndex === 0}
          onClick={() => goToCard(cardIndex - 1)}
          className="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] px-4 py-2 text-sm font-medium text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-30"
        >
          Previous
        </button>
        <button
          type="button"
          onClick={() => goToCard(cardIndex + 1)}
          className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2 text-sm font-semibold text-white shadow-lg shadow-brand-500/20"
        >
          {cardIndex === cards.length - 1 ? 'Finish' : 'Next'}
        </button>
      </div>
    </div>
  );
}

/** Interactive Recall practice — every turn grades the previous answer AND
 * generates a genuinely new question, never a static bank. */
function RecallPractice({ sectionId, personId, onComplete }: { sectionId: string; personId: string; onComplete: () => void }) {
  const startedRef = useRef(false);
  const completedRef = useRef(false);
  const [question, setQuestion] = useState<string | null>(null);
  const [response, setResponse] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [attemptsUsed, setAttemptsUsed] = useState(0);
  const [itemsMastered, setItemsMastered] = useState(0);
  const [itemsRequired, setItemsRequired] = useState(2);
  const [complete, setComplete] = useState(false);
  const [lastMastered, setLastMastered] = useState<boolean | null>(null);
  const [error, setError] = useState<string | null>(null);

  const markCompleteOnce = () => {
    setComplete(true);
    if (!completedRef.current) {
      completedRef.current = true;
      onComplete();
    }
  };

  useEffect(() => {
    if (startedRef.current || !personId) return;
    startedRef.current = true;
    getStructuredLearningRecallState(sectionId, personId)
      .then((result) => {
        setQuestion(result.NextQuestion);
        setItemsRequired(result.ItemsRequired);
        setItemsMastered(result.ItemsMastered);
        setAttemptsUsed(result.AttemptsUsedForItem);
        if (result.Complete) markCompleteOnce();
      })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sectionId, personId]);

  async function handleSubmit() {
    if (!response.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      const result = await submitStructuredLearningRecallAttempt(sectionId, personId, response);
      setQuestion(result.NextQuestion);
      setItemsMastered(result.ItemsMastered);
      setItemsRequired(result.ItemsRequired);
      setAttemptsUsed(result.AttemptsUsedForItem);
      setLastMastered(result.MasteredThisAttempt);
      setResponse('');
      if (result.Complete) markCompleteOnce();
      else setComplete(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  if (error) return <p className="mt-3 text-sm text-red-600 dark:text-red-300">{error}</p>;
  if (complete) {
    return (
      <div className="mt-3 flex items-center gap-2 rounded-xl border border-emerald-200 dark:border-emerald-400/30 bg-emerald-50 dark:bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-200">
        <CheckCircle2 className="h-4 w-4" /> Recall complete.
      </div>
    );
  }
  if (!question) return <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">Loading...</p>;

  return (
    <div className="mt-3 space-y-3">
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">Attempt {attemptsUsed + 1} of {RECALL_MAX_ATTEMPTS}</p>
      <div className="flex items-center gap-2">
        <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-slate-200 dark:bg-white/10">
          <div className="h-full rounded-full bg-fuchsia-400 transition-all" style={{ width: `${Math.min(100, (itemsMastered / itemsRequired) * 100)}%` }} />
        </div>
        <span className="text-xs text-slate-500 dark:text-slate-400">{itemsMastered}/{itemsRequired} mastered</span>
      </div>
      {lastMastered === true && <p className="text-xs text-emerald-600 dark:text-emerald-300">Good job!</p>}
      <p className="text-sm text-slate-700 dark:text-slate-200">{question}</p>
      <textarea value={response} onChange={(e) => setResponse(e.target.value)} rows={3} placeholder="Type your answer..." className="w-full rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-3 text-sm text-slate-900 dark:text-white" />
      <button
        type="button"
        onClick={handleSubmit}
        disabled={submitting || !response.trim()}
        className="inline-flex items-center gap-2 rounded-xl bg-fuchsia-500 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
      >
        {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />} Submit
      </button>
    </div>
  );
}

/** Interactive Assessment — rounds of questions generated fresh one at a
 * time; a Failed round auto-starts a brand-new one in the same response. */
function AssessmentPractice({ sectionId, personId, questionType, onComplete }: { sectionId: string; personId: string; questionType: string; onComplete: () => void }) {
  const completedRef = useRef(false);
  const [round, setRound] = useState<StructuredLearningAssessmentRoundState | null | undefined>(undefined);
  const [selectedOption, setSelectedOption] = useState<number | null>(null);
  const [openAnswer, setOpenAnswer] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [lastGrade, setLastGrade] = useState<{ Correctness: string; Feedback: string } | null>(null);
  const [roundResult, setRoundResult] = useState<{ Passed: boolean; FinalScore: number } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const passed = round && !round.CurrentQuestion ? (roundResult?.Passed ?? round.Status === 'Passed') : false;

  useEffect(() => {
    if (passed && !completedRef.current) {
      completedRef.current = true;
      onComplete();
    }
  }, [passed, onComplete]);

  useEffect(() => {
    if (!personId) return;
    getActiveStructuredLearningAssessmentRound(sectionId, personId)
      .then((active) => setRound(active))
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
  }, [sectionId, personId]);

  async function handleStart() {
    setSubmitting(true);
    setError(null);
    try {
      const state = await startStructuredLearningAssessmentRound(sectionId, personId);
      setRound(state);
      setRoundResult(null);
      setLastGrade(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleSubmitAnswer() {
    if (!round?.CurrentQuestion) return;
    const isMultipleChoice = questionType === 'MultipleChoice';
    if (isMultipleChoice && selectedOption === null) return;
    if (!isMultipleChoice && !openAnswer.trim()) return;
    const answer = isMultipleChoice ? String(selectedOption) : openAnswer;

    setSubmitting(true);
    setError(null);
    try {
      const result = await submitStructuredLearningAssessmentAnswer(round.CurrentQuestion.StructuredLearningAssessmentQuestionAttemptId, answer);
      setLastGrade(result.Grade);
      setSelectedOption(null);
      setOpenAnswer('');
      if (result.RoundComplete) {
        setRoundResult(result.Passed !== null ? { Passed: result.Passed, FinalScore: result.FinalScore ?? 0 } : null);
        if (result.NextQuestion && result.NewStructuredLearningAssessmentRoundId) {
          setRound({
            StructuredLearningAssessmentRoundId: result.NewStructuredLearningAssessmentRoundId,
            RoundNumber: result.NewRoundNumber ?? round.RoundNumber + 1,
            Status: 'InProgress',
            FinalScore: null,
            TotalQuestions: round.TotalQuestions,
            CurrentQuestion: result.NextQuestion,
          });
        } else {
          setRound({ ...round, CurrentQuestion: null, Status: result.Passed ? 'Passed' : 'Failed', FinalScore: result.FinalScore ?? null });
        }
      } else {
        setRound({ ...round, CurrentQuestion: result.NextQuestion });
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  if (error) return <p className="mt-3 text-sm text-red-600 dark:text-red-300">{error}</p>;
  if (round === undefined) return <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">Loading...</p>;

  if (round === null) {
    return (
      <button type="button" onClick={handleStart} disabled={submitting} className="mt-3 inline-flex items-center gap-2 rounded-xl bg-emerald-500 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">
        {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <ClipboardCheck className="h-4 w-4" />} Start assessment
      </button>
    );
  }

  if (!round.CurrentQuestion) {
    return (
      <div className={`mt-3 rounded-xl border p-4 text-sm ${passed ? 'border-emerald-200 dark:border-emerald-400/30 bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-200' : 'border-red-200 dark:border-red-400/30 bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-200'}`}>
        <p className="font-semibold">{passed ? 'Passed!' : 'Not yet — try again.'}</p>
        {(roundResult?.FinalScore ?? round.FinalScore) != null && <p className="mt-0.5 text-xs">Score: {roundResult?.FinalScore ?? round.FinalScore}</p>}
        {!passed && (
          <button type="button" onClick={handleStart} className="mt-2 text-xs font-medium underline">
            Retry
          </button>
        )}
      </div>
    );
  }

  const q = round.CurrentQuestion;
  const gradeColor =
    lastGrade?.Correctness === 'Correct'
      ? 'border-emerald-200 dark:border-emerald-400/30 bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-200'
      : lastGrade?.Correctness === 'PartiallyCorrect'
        ? 'border-amber-200 dark:border-amber-400/30 bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-200'
        : 'border-red-200 dark:border-red-400/30 bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-200';

  return (
    <div className="mt-3 space-y-3">
      <p className="text-xs text-slate-500 dark:text-slate-400">Question {q.QuestionIndex} of {round.TotalQuestions} — Round {round.RoundNumber}</p>
      <p className="text-sm text-slate-700 dark:text-slate-200">{q.QuestionText}</p>
      {lastGrade && <div className={`rounded-lg border p-2.5 text-xs ${gradeColor}`}>{lastGrade.Feedback}</div>}
      {questionType === 'MultipleChoice' && q.Options ? (
        <div className="space-y-2">
          {q.Options.map((opt, index) => (
            <label
              key={index}
              className={`flex cursor-pointer items-center gap-2 rounded-lg border p-2.5 text-sm ${
                selectedOption === index ? 'border-emerald-400 bg-emerald-50 dark:bg-emerald-500/10 text-slate-900 dark:text-white' : 'border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.02] text-slate-700 dark:text-slate-300'
              }`}
            >
              <input type="radio" name={`sl-assessment-${q.StructuredLearningAssessmentQuestionAttemptId}`} checked={selectedOption === index} onChange={() => setSelectedOption(index)} className="h-4 w-4" />
              {opt}
            </label>
          ))}
        </div>
      ) : (
        <textarea value={openAnswer} onChange={(e) => setOpenAnswer(e.target.value)} rows={3} placeholder="Type your answer..." className="w-full rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-3 text-sm text-slate-900 dark:text-white" />
      )}
      <button
        type="button"
        onClick={handleSubmitAnswer}
        disabled={submitting}
        className="inline-flex items-center gap-2 rounded-xl bg-emerald-500 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
      >
        {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />} Submit
      </button>
    </div>
  );
}

function StepContent({ sectionId, personId, step, questionType, onComplete }: { sectionId: string; personId: string; step: StructuredLearningStepDetail; questionType: string; onComplete: () => void }) {
  const meta = STEP_META[step.StepType] ?? { label: step.StepType, Icon: BookOpenText };

  return (
    <div className="space-y-4">
      {step.StepType === 'Teaching' && (
        <VoiceTutorAgent structuredLearningSectionStepId={step.StructuredLearningSectionStepId} />
      )}
      <div className="overflow-hidden rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03]">
        {(step.StepType === 'Hypothesis' || step.StepType === 'Teaching') && <StepIllustration stepDataJson={step.StepDataJson} />}
        <div className="p-4">
          <p className="text-xs font-semibold uppercase tracking-wide text-brand-600 dark:text-brand-400">{meta.label}</p>
          {step.StepType === 'Hypothesis' && step.Content && (
            <p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-slate-700 dark:text-slate-200">
              {step.Content.replace(/^ContractVersion:\s*[^\n]+\s*/i, '').trim() || step.Content}
            </p>
          )}
          {step.StepType === 'Hypothesis' && <HypothesisQuestionCard sectionId={sectionId} personId={personId} step={step} />}
          {step.StepType !== 'Hypothesis' && step.Content && <p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-slate-700 dark:text-slate-200">{step.Content}</p>}
          {step.StepType === 'DeepDive' && step.StepDataJson && (
            <DeepDiveQuiz sectionId={sectionId} stepId={step.StructuredLearningSectionStepId} stepDataJson={step.StepDataJson} onComplete={onComplete} />
          )}
          {step.StepType === 'Recall' && <RecallPractice sectionId={sectionId} personId={personId} onComplete={onComplete} />}
          {step.StepType === 'Production' && (
            <textarea rows={3} placeholder="Write your answer here..." className="mt-3 w-full rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-3 text-sm text-slate-900 dark:text-white" />
          )}
          {step.StepType === 'Assessment' && <AssessmentPractice sectionId={sectionId} personId={personId} questionType={questionType} onComplete={onComplete} />}
        </div>
      </div>
    </div>
  );
}

function StepProgress({ steps, currentIndex, maxReachedIndex, onSelectStep }: { steps: StructuredLearningStepDetail[]; currentIndex: number; maxReachedIndex: number; onSelectStep: (index: number) => void }) {
  const orderedSteps = [...steps].sort((a, b) => STEP_ORDER.indexOf(a.StepType) - STEP_ORDER.indexOf(b.StepType));
  return (
    <div className="mt-6 flex items-center">
      {orderedSteps.map((step, index) => {
        const meta = STEP_META[step.StepType] ?? { label: step.StepType, Icon: BookOpenText };
        const Icon = meta.Icon;
        const isDone = index < currentIndex;
        const isActive = index === currentIndex;
        const isReachable = index <= maxReachedIndex;
        return (
          <div key={step.StructuredLearningSectionStepId} className="flex flex-1 items-center">
            <button
              type="button"
              disabled={!isReachable}
              onClick={() => onSelectStep(index)}
              className={`flex flex-col items-center gap-1 ${isReachable ? 'cursor-pointer' : 'cursor-not-allowed opacity-40'}`}
            >
              <div
                className={`flex h-9 w-9 items-center justify-center rounded-full border-2 ${
                  isDone ? 'border-transparent bg-gradient-to-br from-brand-500 to-accent-500 text-white' : isActive ? 'border-brand-400 bg-brand-50 dark:bg-brand-500/10 text-brand-600 dark:text-brand-300' : 'border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] text-slate-400 dark:text-slate-500'
                }`}
              >
                {isDone ? <Check className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
              </div>
              <span className={`text-[11px] font-medium ${isActive ? 'text-slate-900 dark:text-white' : isDone ? 'text-brand-600 dark:text-brand-300' : 'text-slate-400 dark:text-slate-500'}`}>{meta.label}</span>
            </button>
            {index < orderedSteps.length - 1 && <div className={`mx-1 h-0.5 flex-1 ${index < currentIndex ? 'bg-brand-500' : 'bg-slate-200 dark:bg-white/10'}`} />}
          </div>
        );
      })}
    </div>
  );
}

/**
 * Structured Learning's real student-facing section wizard (2026-08-14) —
 * the actual `human-os-web` equivalent of capabilitystudio's
 * StructuredLearningSectionPage.tsx (author/reviewer preview only, always
 * PersonId=null). Same step-by-step gating rules (DeepDive/Recall/
 * Assessment must be genuinely cleared before advancing), but every API
 * call carries the REAL signed-in student's personId — see
 * /memories/repo/structured-learning-runtime-parity.md.
 */
export default function StructuredLearningSectionPage() {
  const navigate = useNavigate();
  const { documentId, sectionId } = useParams<{ documentId: string; sectionId: string }>();
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const [document, setDocument] = useState<StructuredLearningDocumentDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [stepIndex, setStepIndex] = useState(0);
  const [maxReachedIndex, setMaxReachedIndex] = useState(0);

  useEffect(() => {
    if (!documentId) return;
    getStructuredLearningDocument(documentId)
      .then(setDocument)
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
  }, [documentId]);

  useEffect(() => {
    setStepIndex(0);
    setMaxReachedIndex(0);
  }, [sectionId]);

  const sections = document?.Sections ?? [];
  const currentIndex = sections.findIndex((s) => s.StructuredLearningDocumentSectionId === sectionId);
  const currentSection = currentIndex >= 0 ? sections[currentIndex] : null;
  const previousSection = currentIndex > 0 ? sections[currentIndex - 1] : null;
  const nextSection = currentIndex >= 0 && currentIndex < sections.length - 1 ? sections[currentIndex + 1] : null;

  const steps = currentSection?.Steps ?? [];
  const currentStep = steps[stepIndex] ?? null;
  const isFirstStep = stepIndex === 0;
  const isLastStep = stepIndex === steps.length - 1;

  useEffect(() => {
    if (!currentStep) return;
    if (!GATED_STEP_TYPES.has(currentStep.StepType)) {
      setMaxReachedIndex((m) => Math.max(m, stepIndex + 1));
    }
  }, [currentStep, stepIndex]);

  const handleStepComplete = useCallback(() => {
    setMaxReachedIndex((m) => Math.max(m, stepIndex + 1));
  }, [stepIndex]);

  const canAdvancePastCurrentStep = stepIndex < maxReachedIndex;

  function goToStep(index: number) {
    if (index > maxReachedIndex) return;
    setStepIndex(index);
  }

  function goPrevious() {
    if (!isFirstStep) {
      setStepIndex((i) => i - 1);
      return;
    }
    if (previousSection) navigate(`/structured-learning/${documentId}/sections/${previousSection.StructuredLearningDocumentSectionId}`);
  }

  function goNext() {
    if (!canAdvancePastCurrentStep) return;
    if (!isLastStep) {
      setStepIndex((i) => i + 1);
      return;
    }
    if (nextSection) navigate(`/structured-learning/${documentId}/sections/${nextSection.StructuredLearningDocumentSectionId}`);
    else navigate(`/structured-learning/${documentId}`);
  }

  if (error) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16">
        <div className="rounded-2xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/5 p-6 text-red-700 dark:text-red-300">Could not load this section: {error}</div>
      </div>
    );
  }

  if (!document || !personId) {
    return <div className="flex min-h-[50vh] items-center justify-center text-slate-500 dark:text-slate-400">Loading...</div>;
  }

  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <Link to={`/structured-learning/${documentId}`} className="inline-flex items-center gap-1.5 text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white">
        <ArrowLeft className="h-4 w-4" /> Back to map
      </Link>

      <div className="mt-4 text-center">
        <p className="text-xs font-medium uppercase tracking-wide text-brand-600 dark:text-brand-400">{currentSection?.ChapterTitle}</p>
        <h1 className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">{currentSection?.SubchapterTitle}</h1>
      </div>

      {steps.length > 0 && <StepProgress steps={steps} currentIndex={stepIndex} maxReachedIndex={maxReachedIndex} onSelectStep={goToStep} />}

      <div className="mt-8">
        {currentStep && currentSection ? (
          <StepContent
            key={currentStep.StructuredLearningSectionStepId}
            sectionId={currentSection.StructuredLearningDocumentSectionId}
            personId={personId}
            step={currentStep}
            questionType={document.AssessmentQuestionType}
            onComplete={handleStepComplete}
          />
        ) : (
          <p className="text-sm text-slate-500 dark:text-slate-400">No steps yet.</p>
        )}
      </div>

      <div className="mt-8 flex items-center justify-between gap-3">
        <button
          type="button"
          disabled={isFirstStep && !previousSection}
          onClick={goPrevious}
          className="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] px-4 py-2.5 text-sm font-medium text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-30"
        >
          <ArrowLeft className="h-4 w-4" /> Previous
        </button>
        <button
          type="button"
          onClick={goNext}
          disabled={!canAdvancePastCurrentStep}
          title={canAdvancePastCurrentStep ? undefined : 'Complete this step first'}
          className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/20 disabled:cursor-not-allowed disabled:opacity-30"
        >
          Next <ArrowLeft className="h-4 w-4 rotate-180" />
        </button>
      </div>
    </div>
  );
}
