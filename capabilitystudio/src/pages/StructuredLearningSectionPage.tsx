import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  ArrowLeft,
  Check,
  Lightbulb,
  BookOpenText,
  Layers,
  Brain,
  PenLine,
  ClipboardCheck,
  CheckCircle2,
  Loader2,
  Send,
  X,
  Sparkles,
  Pencil,
  Trash2,
  Plus,
  Network,
  Video,
  ChevronUp,
  ChevronDown,
} from 'lucide-react';
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
  editStructuredLearningStep,
  setStructuredLearningStepContent,
  addStructuredLearningDeepDiveCard,
  editStructuredLearningDeepDiveCard,
  deleteStructuredLearningDeepDiveCard,
  listStructuredLearningSectionVideos,
  startStructuredLearningVideo,
  getStructuredLearningVideoStatus,
  deleteStructuredLearningVideo,
  mergeStructuredLearningVideos,
  cutStructuredLearningVideoSegments,
  undoStructuredLearningVideoEdit,
  setStructuredLearningStepTimeline,
  type StructuredLearningDocumentDetail,
  type StructuredLearningStepDetail,
  type StructuredLearningAssessmentRoundState,
  type StructuredLearningSectionVideo,
  type StructuredLearningVideoRunStatus,
} from '../lib/api/structuredLearningApi';
import { apiImageUrl } from '../lib/api/httpClient';
import LoadingSpinner from '../components/LoadingSpinner';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import PreviewModeSwitcher from '../components/PreviewModeSwitcher';
import KnowledgeGraphSection from '../components/KnowledgeGraphSection';
import { usePreviewMode, withPreviewMode, type PreviewMode } from '../lib/previewMode';

const STEP_ORDER = ['Hypothesis', 'Teaching', 'DeepDive', 'Recall', 'Production', 'Assessment'];

/** Gated step types (2026-08-10) — mirrors Graph's PreviewNodePage
 * `isGatedStepType`: DeepDive/Recall/Assessment must actually be cleared
 * (not just visited) before the wizard lets the student move past them.
 * Hypothesis/Teaching/Production stay free (no backend evaluation gate
 * exists for Production yet — same as Graph's own "no attempt cap"). */
const GATED_STEP_TYPES = new Set(['DeepDive', 'Recall', 'Assessment']);

/** Mirrors backend's RecallLoopGate.MaxAttemptsPerItem. */
const RECALL_MAX_ATTEMPTS = 5;

const STEP_META: Record<string, { labelKey: string; Icon: typeof Lightbulb }> = {
  Hypothesis: { labelKey: 'stepHypothesis', Icon: Lightbulb },
  Teaching: { labelKey: 'stepTeaching', Icon: BookOpenText },
  DeepDive: { labelKey: 'stepDeepDive', Icon: Layers },
  Recall: { labelKey: 'stepRecall', Icon: Brain },
  Production: { labelKey: 'stepProduction', Icon: PenLine },
  Assessment: { labelKey: 'stepAssessment', Icon: ClipboardCheck },
};

type HypothesisQuestion = {
  question: string;
  options: string[];
  correctIndex: number | null;
};

function parseHypothesisQuestions(step: StructuredLearningStepDetail): HypothesisQuestion[] | null {
  if (!step || !step.StepDataJson) return null;

  try {
    const parsed = JSON.parse(step.StepDataJson) as unknown;
    const questionList = Array.isArray(parsed)
      ? parsed
      : Array.isArray((parsed as { questions?: unknown })?.questions)
        ? (parsed as { questions: unknown[] }).questions
        : Array.isArray((parsed as { Questions?: unknown })?.Questions)
          ? (parsed as { Questions: unknown[] }).Questions
          : null;

    if (!questionList) return null;

    const normalized = questionList
      .map((item) => {
        const q = item as Record<string, unknown>;
        const question = typeof q.question === 'string'
          ? q.question
          : typeof q.Question === 'string'
            ? q.Question
            : typeof q.prompt === 'string'
              ? q.prompt
              : typeof q.Prompt === 'string'
                ? q.Prompt
                : '';
        const optionList = Array.isArray(q.options)
          ? q.options
          : Array.isArray(q.Options)
            ? q.Options
            : Array.isArray(q.choices)
              ? q.choices
              : Array.isArray(q.Choices)
                ? q.Choices
                : [];
        const options = optionList
          .map((option) => (typeof option === 'string' ? option : String(option ?? '').trim()))
          .filter((option) => option.length > 0);
        const correctIndex = typeof q.correctIndex === 'number'
          ? q.correctIndex
          : typeof q.CorrectIndex === 'number'
            ? q.CorrectIndex
            : typeof q.correctAnswerIndex === 'number'
              ? q.correctAnswerIndex
              : typeof q.CorrectAnswerIndex === 'number'
                ? q.CorrectAnswerIndex
                : null;

        if (!question.trim() || options.length === 0) return null;

        return {
          question: question.trim(),
          options,
          correctIndex: correctIndex != null && correctIndex >= 0 && correctIndex < options.length ? correctIndex : null,
        };
      })
      .filter((question): question is HypothesisQuestion => question !== null);

    return normalized.length > 0 ? normalized : null;
  } catch {
    return null;
  }
}

function HypothesisQuestionCard({ sectionId, step }: { sectionId: string; step: StructuredLearningStepDetail }) {
  const questions = parseHypothesisQuestions(step);
  const [selectedAnswers, setSelectedAnswers] = useState<Record<number, number>>({});
  const [submitted, setSubmitted] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Loads this student's previously saved answers, if any (2026-08-13 fix —
  // replaces the OLD localStorage-only design: answers are now persisted
  // server-side per StructuredLearningHypothesisResponse row so they survive
  // reloads/devices and are visible to an instructor).
  useEffect(() => {
    if (!step.StructuredLearningSectionStepId) return;
    getStructuredLearningHypothesisResponses(sectionId)
      .then((saved) => {
        if (saved.length === 0) return;
        const restored = saved.reduce<Record<number, number>>((acc, r) => {
          acc[r.QuestionIndex] = r.SelectedOptionIndex;
          return acc;
        }, {});
        setSelectedAnswers(restored);
        setSubmitted(true);
      })
      .catch(() => {
        // No saved answers yet, or transient error — leave the form blank.
      });
  }, [sectionId, step.StructuredLearningSectionStepId]);

  if (!questions || questions.length === 0) return null;

  const answeredCount = Object.keys(selectedAnswers).length;
  const isComplete = questions.length > 0 && answeredCount === questions.length && questions.every((_, index) => selectedAnswers[index] !== undefined);

  async function handleSave() {
    setSaving(true);
    setError(null);
    try {
      await submitStructuredLearningHypothesisResponses(
        sectionId,
        Object.entries(selectedAnswers).map(([questionIndex, selectedOptionIndex]) => ({
          questionIndex: Number(questionIndex),
          selectedOptionIndex,
        }))
      );
      setSubmitted(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="mt-4 space-y-4 rounded-2xl border border-brand-500/20 bg-brand-500/[0.04] p-4">
      <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-brand-300">Before we start</p>

      {submitted ? (
        <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/10 p-3 text-sm text-emerald-100">
          Thank you for your responses. We now will start learning.
        </div>
      ) : (
        <div className="space-y-4">
          {error && <p className="text-sm text-red-300">{error}</p>}
          {questions.map((question, index) => {
            const selectedIndex = selectedAnswers[index];

            return (
              <div key={`${question.question}-${index}`} className="rounded-xl border border-white/10 bg-slate-950/20 p-3">
                <p className="text-sm font-medium text-white">{index + 1}. {question.question}</p>
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
                            ? 'border-brand-500/50 bg-brand-500/10 text-white'
                            : 'border-white/10 bg-white/[0.02] text-slate-200 hover:border-white/20 hover:bg-white/[0.04]'
                        }`}
                      >
                        <span className={`flex h-6 w-6 items-center justify-center rounded-full text-[10px] font-bold ${
                          isSelected ? 'bg-brand-500/20 text-brand-100' : 'bg-white/5 text-slate-300'
                        }`}>
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

          <div className="flex items-center justify-between gap-3 rounded-xl border border-white/10 bg-white/[0.02] p-3">
            <p className="text-xs text-slate-300">{answeredCount}/{questions.length} answered</p>
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

/**
 * Structured Learning's per-section detail view (2026-08-10, rewritten
 * per explicit user feedback: this must be a STEP-BY-STEP wizard, exactly
 * like PreviewNodePage's own stepper — Hypothesis first, then Teaching,
 * etc., one step visible at a time — never a long vertical scroll of
 * every step stacked together). Mirrors PreviewNodePage's own step
 * progress bar (icons in circles connected by a line, current step
 * highlighted, completed ones checked). Gated (2026-08-10, mirrors
 * Graph's PreviewNodePage `isGatedStepType`): DeepDive/Recall/Assessment
 * must report real completion (via each sub-component's `onComplete`)
 * before the wizard lets the student move past them — only
 * Hypothesis/Teaching/Production stay freely clickable ahead of time.
 * Hypothesis/Teaching/DeepDive steps render the SAME real-time voice
 * VoiceTutorAgent orb Graph uses, wired to a NEW StructuredLearningStep
 * voice-session path (VoiceTutorSessionFunction.RunStructuredLearningStepAsync)
 * — Recall/Assessment stay text-only (same rule as Graph's own Recall/
 * Assessment voice exclusion).
 */
export default function StructuredLearningSectionPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { documentId, sectionId } = useParams<{ documentId: string; sectionId: string }>();
  const [mode, setMode] = usePreviewMode();
  const [document, setDocument] = useState<StructuredLearningDocumentDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [stepIndex, setStepIndex] = useState(0);
  // Furthest step index reached in the CURRENT section — a gated step
  // (DeepDive/Recall/Assessment) only bumps this once its own onComplete
  // fires; ungated steps bump it immediately on arrival (see effect below).
  const [maxReachedIndex, setMaxReachedIndex] = useState(0);
  const [showKnowledgeGraph, setShowKnowledgeGraph] = useState(false);
  const [knowledgeGraphInitialView, setKnowledgeGraphInitialView] = useState<'graph' | 'facts'>('graph');

  useEffect(() => {
    if (!documentId) return;
    getStructuredLearningDocument(documentId)
      .then(setDocument)
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
  }, [documentId]);

  // A different section was navigated to — always start at its first step.
  useEffect(() => {
    setStepIndex(0);
    setMaxReachedIndex(0);
  }, [sectionId]);

  const sections = document?.Sections ?? [];
  const currentIndex = sections.findIndex((s) => s.StructuredLearningDocumentSectionId === sectionId);
  const currentSection = currentIndex >= 0 ? sections[currentIndex] : null;
  const previousSection = currentIndex > 0 ? sections[currentIndex - 1] : null;
  const nextSection = currentIndex >= 0 && currentIndex < sections.length - 1 ? sections[currentIndex + 1] : null;

  // The Production ("Apply it") step is temporarily disabled (2026-08-23,
  // explicit user request) — rather than showing it in the step network
  // just to display a "coming soon" placeholder, exclude it entirely from
  // navigation so students never see or click through a dead step.
  const steps = (currentSection?.Steps ?? []).filter((s) => s.StepType !== 'Production');
  const currentStep = steps[stepIndex] ?? null;
  const isFirstStep = stepIndex === 0;
  const isLastStep = stepIndex === steps.length - 1;

  // Ungated steps unlock the NEXT index the instant they're visited — only
  // DeepDive/Recall/Assessment wait for their own onComplete callback below.
  useEffect(() => {
    if (!currentStep) return;
    if (!GATED_STEP_TYPES.has(currentStep.StepType)) {
      setMaxReachedIndex((m) => Math.max(m, stepIndex + 1));
    }
  }, [currentStep, stepIndex]);

  const handleStepComplete = useCallback(() => {
    setMaxReachedIndex((m) => Math.max(m, stepIndex + 1));
  }, [stepIndex]);

  // Review & acceptance / Edit modes are reviewer-facing (not a real
  // student's progression) — every step is freely clickable in either,
  // mirroring PreviewNodePage's own StepperBar `clickable = mode !== 'real'`.
  const canAdvancePastCurrentStep = mode !== 'real' || stepIndex < maxReachedIndex;

  function goToStep(index: number) {
    if (mode === 'real' && index > maxReachedIndex) return;
    setStepIndex(index);
  }

  function goPrevious() {
    if (!isFirstStep) {
      setStepIndex((i) => i - 1);
      return;
    }
    if (previousSection) {
      navigate(withPreviewMode(`/structured-learning/${documentId}/course/sections/${previousSection.StructuredLearningDocumentSectionId}`, mode));
    }
  }

  function goNext() {
    if (!canAdvancePastCurrentStep) return;
    if (!isLastStep) {
      setStepIndex((i) => i + 1);
      return;
    }
    if (nextSection) {
      navigate(withPreviewMode(`/structured-learning/${documentId}/course/sections/${nextSection.StructuredLearningDocumentSectionId}`, mode));
    } else {
      navigate(withPreviewMode(`/structured-learning/${documentId}/course`, mode));
    }
  }

  if (error) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16">
        <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-red-300">{t('structuredLearningCourse.loadError', { error })}</div>
      </div>
    );
  }

  if (!document) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <LoadingSpinner label={t('structuredLearningCourse.loading')} />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6 lg:px-8">
      <Link to={withPreviewMode(`/structured-learning/${documentId}/course`, mode)} className="inline-flex items-center gap-1.5 text-sm text-slate-400 hover:text-white">
        <ArrowLeft className="h-4 w-4" /> {t('structuredLearningCourse.backToMap')}
      </Link>

      <div className="mt-4 text-center">
        <p className="text-xs font-medium uppercase tracking-wide text-brand-400">{currentSection?.ChapterTitle}</p>
        <h1 className="mt-1 text-2xl font-bold text-white">{currentSection?.SubchapterTitle}</h1>
        {documentId && sectionId && (
          <button
            type="button"
            onClick={() => { setKnowledgeGraphInitialView('graph'); setShowKnowledgeGraph(true); }}
            className="mt-4 inline-flex items-center gap-2 rounded-xl border border-emerald-400/30 bg-emerald-500/10 px-3 py-2 text-xs font-semibold text-emerald-200 transition hover:border-emerald-300/60 hover:bg-emerald-500/20"
          >
            <Network className="h-4 w-4" /> {t('structuredLearningCourse.viewKnowledgeGraph')}
          </button>
        )}
        {documentId && sectionId && (
          <button
            type="button"
            onClick={() => { setKnowledgeGraphInitialView('facts'); setShowKnowledgeGraph(true); }}
            className="ml-2 mt-4 inline-flex items-center gap-2 rounded-xl border border-brand-400/30 bg-brand-500/10 px-3 py-2 text-xs font-semibold text-brand-200 transition hover:border-brand-300/60 hover:bg-brand-500/20"
          >
            {t('structuredLearningCourse.viewKnowledgeFacts')}
          </button>
        )}
        <div className="mt-4 flex justify-center">
          <PreviewModeSwitcher mode={mode} onChange={setMode} />
        </div>
        {mode === 'demo' && <p className="mt-2 text-xs text-amber-300">{t('structuredLearningMap.demoModeNotice')}</p>}
      </div>

      {showKnowledgeGraph && documentId && sectionId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm" role="dialog" aria-modal="true" aria-label="Knowledge Graph">
          <div className="max-h-[90vh] w-full max-w-6xl overflow-y-auto rounded-2xl border border-emerald-400/25 bg-slate-950 p-4 shadow-2xl sm:p-6">
            <div className="mb-4 flex items-center justify-between gap-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-emerald-400">Engram</p>
                <h2 className="mt-1 text-lg font-semibold text-white">{knowledgeGraphInitialView === 'facts' ? 'Knowledge Facts' : 'Knowledge Graph'}</h2>
              </div>
              <button
                type="button"
                onClick={() => setShowKnowledgeGraph(false)}
                className="rounded-lg p-2 text-slate-400 transition hover:bg-white/10 hover:text-white"
                aria-label="Cerrar Knowledge Graph"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
            <KnowledgeGraphSection documentId={documentId} sectionId={sectionId} initialView={knowledgeGraphInitialView} canRegenerateFacts={mode === 'edit'} />
          </div>
        </div>
      )}

      {steps.length > 0 && (
        <StepProgress steps={steps} currentIndex={stepIndex} maxReachedIndex={maxReachedIndex} freeNavigation={mode !== 'real'} onSelectStep={goToStep} />
      )}

      <div className="mt-8">
        {currentStep && currentSection ? (
          <StepContent
            key={currentStep.StructuredLearningSectionStepId}
            step={currentStep}
            sectionId={currentSection.StructuredLearningDocumentSectionId}
            questionType={document.AssessmentQuestionType}
            mode={mode}
            onComplete={handleStepComplete}
            onStepUpdated={(updated) =>
              setDocument((prev) =>
                prev
                  ? {
                      ...prev,
                      Sections: prev.Sections.map((s) =>
                        s.StructuredLearningDocumentSectionId === currentSection.StructuredLearningDocumentSectionId
                          ? { ...s, Steps: s.Steps.map((st) => (st.StructuredLearningSectionStepId === updated.StructuredLearningSectionStepId ? updated : st)) }
                          : s
                      ),
                    }
                  : prev
              )
            }
          />
        ) : (
          <p className="text-sm text-slate-500">{t('structuredLearningCourse.noStepsYet')}</p>
        )}
      </div>

      <div className="mt-8 flex items-center justify-between gap-3">
        <button
          type="button"
          disabled={isFirstStep && !previousSection}
          onClick={goPrevious}
          className="inline-flex items-center gap-1.5 rounded-xl border border-white/10 bg-white/[0.03] px-4 py-2.5 text-sm font-medium text-slate-200 hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-30"
        >
          <ArrowLeft className="h-4 w-4" /> {t('structuredLearningCourse.wizardPrevious')}
        </button>
        <button
          type="button"
          onClick={goNext}
          disabled={!canAdvancePastCurrentStep}
          title={canAdvancePastCurrentStep ? undefined : (t('structuredLearningCourse.stepLocked') as string)}
          className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2.5 text-sm font-semibold text-white shadow-lg shadow-brand-500/20 disabled:cursor-not-allowed disabled:opacity-30"
        >
          {t('structuredLearningCourse.wizardNext')} <ArrowLeft className="h-4 w-4 rotate-180" />
        </button>
      </div>
    </div>
  );
}

function StepProgress({
  steps,
  currentIndex,
  maxReachedIndex,
  freeNavigation,
  onSelectStep,
}: {
  steps: StructuredLearningStepDetail[];
  currentIndex: number;
  maxReachedIndex: number;
  freeNavigation: boolean;
  onSelectStep: (index: number) => void;
}) {
  const { t } = useTranslation();
  const orderedSteps = [...steps].sort((a, b) => STEP_ORDER.indexOf(a.StepType) - STEP_ORDER.indexOf(b.StepType));

  return (
    <div className="mt-6 flex items-center">
      {orderedSteps.map((step, index) => {
        const meta = STEP_META[step.StepType] ?? { labelKey: step.StepType, Icon: BookOpenText };
        const Icon = meta.Icon;
        const isDone = index < currentIndex;
        const isActive = index === currentIndex;
        const isReachable = freeNavigation || index <= maxReachedIndex;
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
                  isDone
                    ? 'border-transparent bg-gradient-to-br from-brand-500 to-accent-500 text-white'
                    : isActive
                      ? 'border-brand-400 bg-brand-500/10 text-brand-300'
                      : 'border-white/10 bg-white/[0.03] text-slate-500'
                }`}
              >
                {isDone ? <Check className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
              </div>
              <span className={`text-[11px] font-medium ${isActive ? 'text-white' : isDone ? 'text-brand-300' : 'text-slate-500'}`}>
                {t(`structuredLearningCourse.${meta.labelKey}`)}
              </span>
            </button>
            {index < orderedSteps.length - 1 && <div className={`mx-1 h-0.5 flex-1 ${index < currentIndex ? 'bg-brand-500' : 'bg-white/10'}`} />}
          </div>
        );
      })}
    </div>
  );
}

/** Renders ONE step's content — the current step in the wizard. */
function StepContent({
  step,
  sectionId,
  questionType,
  mode,
  onComplete,
  onStepUpdated,
}: {
  step: StructuredLearningStepDetail;
  sectionId: string;
  questionType: string;
  mode: PreviewMode;
  onComplete: () => void;
  onStepUpdated: (updated: StructuredLearningStepDetail) => void;
}) {
  const { t } = useTranslation();
  // Which kind of item the Learn wizard is currently showing (2026-08-22) —
  // while it's on a Video, the tutor orb/static illustration are hidden so
  // only the video is on screen (mirrors the outer stepper's own "one thing
  // at a time" rule).
  const [teachingWizardItemType, setTeachingWizardItemType] = useState<'Text' | 'Video'>('Text');
  // Bumped whenever a video is added/deleted/merged (VideoGenerationBox) so
  // TeachingTimelineEditor's own independent video list re-fetches instead
  // of going stale until a full page reload.
  const [videoRefreshKey, setVideoRefreshKey] = useState(0);
  const meta = STEP_META[step.StepType] ?? { labelKey: step.StepType, Icon: BookOpenText };
  const isEditing = mode === 'edit';
  const isEditableStepType = step.StepType === 'Hypothesis' || step.StepType === 'Teaching' || step.StepType === 'Production';

  const teachingShowsVideo = step.StepType === 'Teaching' && !isEditing && teachingWizardItemType === 'Video';

  return (
    <div className="space-y-4">
      {step.StepType === 'Teaching' && !teachingShowsVideo && (
        <VoiceTutorAgent
          structuredLearningSectionStepId={step.StructuredLearningSectionStepId}
          startLabel={t('structuredLearningCourse.talkToTutor')}
          autoConnect={!isEditing}
        />
      )}

      <div className="overflow-hidden rounded-3xl border border-white/10 bg-slate-950/30 shadow-2xl shadow-black/10">
        {(step.StepType === 'Hypothesis' || (step.StepType === 'Teaching' && !teachingShowsVideo)) && <StepIllustration stepDataJson={step.StepDataJson} />}
        <div className="px-5 py-6 sm:px-8 sm:py-8">
          <p className="text-xs font-semibold uppercase tracking-wide text-brand-400">{t(`structuredLearningCourse.${meta.labelKey}`)}</p>
          {step.StepType === 'Hypothesis' && step.Content && (
            <RichStepContent content={step.Content.replace(/^ContractVersion:\s*[^\n]+\s*/i, '').trim() || step.Content} />
          )}
          {step.StepType === 'Hypothesis' && !isEditing && (
            <HypothesisQuestionCard sectionId={sectionId} step={step} />
          )}
          {step.StepType === 'Teaching' && !isEditing ? (
            <TeachingTimelineWizard sectionId={sectionId} step={step} onItemTypeChange={setTeachingWizardItemType} />
          ) : (
            step.StepType !== 'Hypothesis' && !(step.StepType === 'Production' && !isEditing) && step.Content && <RichStepContent content={step.Content} />
          )}

          {isEditing && isEditableStepType && (
            <>
              <StepEditBox sectionId={sectionId} step={step} onUpdated={onStepUpdated} />
              <ManualEditBox sectionId={sectionId} step={step} onUpdated={onStepUpdated} />
              {step.StepType === 'Teaching' && <VideoGenerationBox sectionId={sectionId} step={step} onVideosChanged={() => setVideoRefreshKey((k) => k + 1)} />}
              {step.StepType === 'Teaching' && <TeachingTimelineEditor sectionId={sectionId} step={step} onUpdated={onStepUpdated} refreshKey={videoRefreshKey} />}
            </>
          )}

          {step.StepType === 'DeepDive' && step.StepDataJson && (
            isEditing ? (
              <DeepDiveCardsEditor sectionId={sectionId} step={step} onUpdated={onStepUpdated} />
            ) : (
              <DeepDiveQuiz sectionId={sectionId} stepId={step.StructuredLearningSectionStepId} stepDataJson={step.StepDataJson} mode={mode} onComplete={onComplete} />
            )
          )}
          {step.StepType === 'Recall' &&
            (isEditing ? (
              <p className="mt-3 text-sm text-slate-500">{t('structuredLearningCourse.editModeGeneratedLive')}</p>
            ) : (
              <RecallPractice sectionId={sectionId} onComplete={onComplete} />
            ))}
          {step.StepType === 'Production' && !isEditing && (
            <p className="mt-3 text-sm text-slate-500">{t('structuredLearningCourse.productionStepDisabledNotice')}</p>
          )}
          {step.StepType === 'Assessment' &&
            (isEditing ? (
              <p className="mt-3 text-sm text-slate-500">{t('structuredLearningCourse.editModeGeneratedLive')}</p>
            ) : (
              <AssessmentPractice sectionId={sectionId} questionType={questionType} onComplete={onComplete} />
            ))}
        </div>
      </div>
    </div>
  );
}

function RichStepContent({ content }: { content: string }) {
  const blocks = normalizeRichBlocks(content);

  return (
    <div className="mt-6 max-w-4xl space-y-6 text-[16px] leading-8 text-slate-200 sm:text-[17px]">
      {blocks.map((block, index) => {
        const lines = block.split('\n').map((line) => line.trim()).filter(Boolean);
        const isList = lines.length > 0 && lines.every((line) => /^([-*]|\d+[.)])\s+/.test(line));
        const heading = lines.length === 1 && (/^#{1,3}\s+/.test(lines[0]) || (lines[0].length < 90 && /:$/.test(lines[0])));

        if (heading) {
          const text = lines[0].replace(/^#{1,3}\s+/, '');
          return <h3 key={index} className="pt-1 text-base font-semibold leading-6 text-white">{text}</h3>;
        }

        if (isList) {
          const ordered = /^\d+[.)]/.test(lines[0]);
          const List = ordered ? 'ol' : 'ul';
          return (
            <List key={index} className={`space-y-2 pl-6 ${ordered ? 'list-decimal' : 'list-disc'}`}>
              {lines.map((line, lineIndex) => <li key={lineIndex} className="pl-1">{formatInlineText(line.replace(/^([-*]|\d+[.)])\s+/, ''))}</li>)}
            </List>
          );
        }

        return <p key={index}>{lines.map((line, lineIndex) => <span key={lineIndex}>{lineIndex > 0 && <br />}{formatInlineText(line)}</span>)}</p>;
      })}
    </div>
  );
}

function splitLongBlock(block: string): string[] {
  if (!block || block.length < 420) return [block];
  const sentences = block.match(/[^.!?]+[.!?]+(?=\s|$)/g)?.map((sentence) => sentence.trim()).filter(Boolean);
  if (!sentences || sentences.length < 3) return [block];
  const groups: string[] = [];
  for (let index = 0; index < sentences.length; index += 2) {
    groups.push(sentences.slice(index, index + 2).join(' '));
  }
  return groups;
}

function normalizeRichBlocks(content: string): string[] {
  const normalized = content
    .replace(/\r\n/g, '\n')
    .replace(/\s+•\s+/g, '\n• ')
    .replace(/\s+(Why this matters)\s+/gi, '\n\n$1\n')
    .replace(/\s+(Next, connect)\s+/gi, '\n\n$1 ')
    .replace(/\s+(Foundations of AI:|What is generative AI\?|AI value also comes from other capabilities:|Responsible AI means)/g, '\n\n$1');

  return normalized
    .split(/\n\s*\n|\n/)
    .map((block) => block.trim())
    .filter(Boolean)
    .flatMap((block) => splitLongBlock(block));
}

function formatInlineText(value: string): ReactNode {
  const parts = value.split(/(\*\*[^*]+\*\*|__[^_]+__)/g);
  return parts.map((part, index) => {
    const bold = /^\*\*[^*]+\*\*$/.test(part) || /^__[^_]+__$/.test(part);
    return bold ? <strong key={index} className="font-semibold text-white">{part.slice(2, -2)}</strong> : <span key={index}>{part}</span>;
  });
}

/** "Edit with AI" box (2026-08-10) — Structured Learning's Edit mode,
 * mirrors PreviewNodeBlueprintView's per-step editor exactly: a free-text
 * instruction box that rewrites this ONE step's Content via
 * StructuredLearningStepEditorAgent. Only rendered for Hypothesis/Teaching/
 * Production — DeepDive is edited card-by-card (DeepDiveCardsEditor);
 * Recall/Assessment have no static Content (generated live). */
function StepEditBox({
  sectionId,
  step,
  onUpdated,
}: {
  sectionId: string;
  step: StructuredLearningStepDetail;
  onUpdated: (updated: StructuredLearningStepDetail) => void;
}) {
  const { t } = useTranslation();
  const [instruction, setInstruction] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  async function handleApply() {
    if (!instruction.trim()) return;
    setSubmitting(true);
    setError(null);
    setSuccess(false);
    try {
      const updated = await editStructuredLearningStep(sectionId, step.StepType, instruction.trim());
      onUpdated(updated);
      setInstruction('');
      setSuccess(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mt-4 rounded-xl border border-sky-400/20 bg-sky-500/[0.04] p-3">
      <p className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-sky-300">
        <Sparkles className="h-3.5 w-3.5" /> {t('structuredLearningCourse.editWithAiLabel')}
      </p>
      <textarea
        value={instruction}
        onChange={(e) => setInstruction(e.target.value)}
        rows={2}
        placeholder={t('structuredLearningCourse.editWithAiPlaceholder')}
        className="input w-full"
      />
      {error && <p className="mt-1.5 text-xs text-red-300">{error}</p>}
      {success && <p className="mt-1.5 flex items-center gap-1 text-xs text-emerald-300"><Check className="h-3 w-3" /> {t('structuredLearningCourse.editApplied')}</p>}
      <button
        type="button"
        onClick={handleApply}
        disabled={submitting || !instruction.trim()}
        className="mt-2 inline-flex items-center gap-1.5 rounded-lg bg-sky-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
      >
        {submitting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Sparkles className="h-3.5 w-3.5" />}
        {t('structuredLearningCourse.editApply')}
      </button>
    </div>
  );
}

/** Visual frame-by-frame video trimmer (2026-08-22) — extracts a filmstrip
 * of thumbnail frames client-side (via a hidden &lt;video&gt; + &lt;canvas&gt;,
 * seeking through the clip) and overlays two draggable range handles for
 * start/end, instead of making the reviewer type raw seconds. Calls the
 * same trim endpoint on Apply. */
function VideoFrameTrimModal({
  illustrationId,
  src,
  onClose,
  onApplied,
}: {
  illustrationId: string;
  src: string;
  onClose: () => void;
  onApplied: () => void;
}) {
  const { t } = useTranslation();
  const hiddenVideoRef = useRef<HTMLVideoElement | null>(null);
  const previewVideoRef = useRef<HTMLVideoElement | null>(null);
  const [duration, setDuration] = useState(0);
  const [frames, setFrames] = useState<string[]>([]);
  const [extracting, setExtracting] = useState(true);
  const [scrubTime, setScrubTime] = useState(0);
  const [applying, setApplying] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [deletedFrames, setDeletedFrames] = useState<Set<number>>(new Set());
  const [frameCount, setFrameCount] = useState(2);
  const FRAME_STEP_SECONDS = 0.5;

  useEffect(() => {
    const video = hiddenVideoRef.current;
    if (!video) return;
    let cancelled = false;

    function waitForSeek(targetTime: number): Promise<void> {
      return new Promise<void>((resolve) => {
        let done = false;
        const finish = () => {
          if (done) return;
          done = true;
          video!.removeEventListener('seeked', onSeeked);
          clearTimeout(timeoutId);
          resolve();
        };
        const onSeeked = () => finish();
        // Some browsers/servers can stall a seek indefinitely (e.g. a
        // Range request that never resolves) — never let one bad frame
        // hang the whole extraction, just move on after 4s.
        const timeoutId = setTimeout(finish, 4000);
        video!.addEventListener('seeked', onSeeked);
        video!.currentTime = targetTime;
      });
    }

    async function extractFrames() {
      try {
        await new Promise<void>((resolve) => {
          if (video!.readyState >= 1 && !Number.isNaN(video!.duration)) resolve();
          else {
            video!.addEventListener('loadedmetadata', () => resolve(), { once: true });
            video!.addEventListener('error', () => resolve(), { once: true });
          }
        });
        if (cancelled) return;
        const dur = video!.duration;
        if (!dur || Number.isNaN(dur) || !Number.isFinite(dur)) {
          setExtracting(false);
          setError('Could not read video duration for frame preview.');
          return;
        }
        setDuration(dur);

        // One thumbnail every ~0.5s (capped so a long merged video doesn't
        // try to extract hundreds of frames) so the reviewer can pick out
        // and delete an exact bad moment, not just a coarse 1/14th slice.
        const count = Math.min(Math.max(Math.round(dur / FRAME_STEP_SECONDS) + 1, 2), 120);
        setFrameCount(count);

        const canvas = document.createElement('canvas');
        canvas.width = 96;
        canvas.height = 54;
        const ctx = canvas.getContext('2d');
        const captured: string[] = [];

        for (let i = 0; i < count; i += 1) {
          if (cancelled) return;
          const time = (i / (count - 1)) * Math.max(dur - 0.05, 0);
          await waitForSeek(time);
          if (cancelled) return;
          try {
            if (ctx) {
              ctx.drawImage(video!, 0, 0, canvas.width, canvas.height);
              captured.push(canvas.toDataURL('image/jpeg', 0.6));
            }
          } catch { /* skip an unreadable frame, keep going */ }
        }
        if (!cancelled) {
          setFrames(captured);
          setExtracting(false);
        }
      } catch (err) {
        if (!cancelled) {
          setExtracting(false);
          setError(err instanceof Error ? err.message : String(err));
        }
      }
    }

    extractFrames();
    return () => { cancelled = true; };
  }, []);

  function seekPreview(time: number) {
    if (previewVideoRef.current) previewVideoRef.current.currentTime = time;
  }

  function toggleFrameDeleted(index: number) {
    setDeletedFrames((prev) => {
      const next = new Set(prev);
      if (next.has(index)) next.delete(index); else next.add(index);
      return next;
    });
  }

  async function handleApply() {
    setApplying(true);
    setError(null);
    try {
      const step = duration / (frameCount - 1);
      // Only individually marked frames are cut — the scrub position
      // is just for navigation/preview, it does not delete anything.
      const cutIntervals: [number, number][] = [];
      for (const i of deletedFrames) {
        const center = i * step;
        cutIntervals.push([Math.max(center - step / 2, 0), Math.min(center + step / 2, duration)]);
      }

      if (cutIntervals.length === 0) {
        setError('Mark at least one frame for deletion first.');
        return;
      }

      cutIntervals.sort((a, b) => a[0] - b[0]);
      const mergedCuts: [number, number][] = [];
      for (const [s, e] of cutIntervals) {
        const last = mergedCuts[mergedCuts.length - 1];
        if (last && s <= last[1] + 0.01) last[1] = Math.max(last[1], e);
        else mergedCuts.push([s, e]);
      }
      const keepRanges: { start: number; end: number }[] = [];
      let cursor = 0;
      for (const [s, e] of mergedCuts) {
        if (s - cursor > 0.05) keepRanges.push({ start: cursor, end: s });
        cursor = Math.max(cursor, e);
      }
      if (duration - cursor > 0.05) keepRanges.push({ start: cursor, end: duration });

      if (keepRanges.length === 0) {
        setError('Cannot delete the entire video.');
        return;
      }

      await cutStructuredLearningVideoSegments(illustrationId, keepRanges);
      onApplied();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setApplying(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4" onClick={onClose}>
      <div className="w-full max-w-xl rounded-2xl border border-white/10 bg-slate-900 p-4 shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="mb-3 flex items-center justify-between">
          <p className="text-sm font-semibold text-white">{t('structuredLearningCourse.videoTrimStart')}</p>
          <button type="button" onClick={onClose} className="rounded p-1 text-slate-400 hover:text-white" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>

        <video ref={hiddenVideoRef} src={src} className="hidden" muted preload="auto" crossOrigin="anonymous" />
        <video ref={previewVideoRef} src={src} controls className="mb-3 h-40 w-full rounded-lg bg-black" crossOrigin="anonymous" />

        {extracting ? (
          <div className="flex h-16 items-center justify-center gap-2 text-xs text-slate-400">
            <Loader2 className="h-4 w-4 animate-spin" /> Extracting frames...
          </div>
        ) : (
          <div className="relative mb-3 select-none">
            <div className="flex overflow-x-auto rounded-lg border border-white/10">
              {frames.map((f, i) => {
                const frameTime = frameCount > 1 ? (i / (frameCount - 1)) * duration : 0;
                const isScrubFrame = Math.abs(frameTime - scrubTime) < (duration / (frameCount - 1)) / 2;
                return (
                  <button
                    key={i}
                    type="button"
                    onClick={() => toggleFrameDeleted(i)}
                    className="relative flex w-10 shrink-0 flex-col"
                    aria-label="Toggle delete frame"
                  >
                    <span className={`relative h-14 w-10 ${isScrubFrame ? 'ring-2 ring-inset ring-violet-400' : ''}`}>
                      <img
                        src={f}
                        alt=""
                        className={`h-14 w-10 object-cover ${deletedFrames.has(i) ? 'opacity-30' : ''}`}
                      />
                      {deletedFrames.has(i) && (
                        <span className="absolute inset-0 flex items-center justify-center bg-red-600/40">
                          <X className="h-3 w-3 text-white" />
                        </span>
                      )}
                    </span>
                    <span className="border-t border-white/10 bg-slate-950 py-0.5 text-center text-[8px] leading-none text-slate-400">
                      {frameTime.toFixed(1)}s
                    </span>
                  </button>
                );
              })}
            </div>
          </div>
        )}

        <div className="mb-3 space-y-2">
          <div className="flex items-center gap-2 text-xs text-slate-300">
            <span className="w-14 shrink-0">{t('structuredLearningCourse.videoScrubLabel')}</span>
            <input
              type="range"
              min={0}
              max={duration}
              step={0.05}
              value={scrubTime}
              onChange={(e) => { const v = parseFloat(e.target.value); setScrubTime(v); seekPreview(v); }}
              className="flex-1 accent-violet-500"
            />
            <span className="w-12 shrink-0 text-right tabular-nums">{scrubTime.toFixed(1)}s</span>
          </div>
        </div>

        {error && <p className="mb-2 text-xs text-red-300">{error}</p>}

        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:bg-white/5">
            {t('structuredLearningCourse.videoMergeCancel')}
          </button>
          <button
            type="button"
            onClick={handleApply}
            disabled={applying || extracting}
            className="inline-flex items-center gap-1.5 rounded-lg bg-violet-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
          >
            {applying ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null}
            {t('structuredLearningCourse.videoTrimApply')}
          </button>
        </div>
      </div>
    </div>
  );
}

type TeachingTimelineItem =
  | { Type: 'Text' }
  | { Type: 'Video'; IllustrationId: string };

function parseTeachingTimeline(timelineJson: string | null | undefined): TeachingTimelineItem[] | null {
  if (!timelineJson) return null;
  try {
    const parsed = JSON.parse(timelineJson) as unknown;
    if (!Array.isArray(parsed)) return null;
    return parsed.filter(
      (item): item is TeachingTimelineItem =>
        !!item && typeof item === 'object' && ((item as { Type?: string }).Type === 'Text' || (item as { Type?: string }).Type === 'Video')
    );
  } catch {
    return null;
  }
}

/** Author-facing order editor for the Teaching step's "wizard" (2026-08-22)
 * — lets the reviewer arrange the step's narration paragraphs (auto-split
 * from Content, same split RichStepContent uses) and its generated videos
 * into ONE numbered sequence (1) Video, 2) Text, 3) Video, ...), saved as
 * step.TimelineJson. In real/demo mode, TeachingTimelineWizard below then
 * plays this exact sequence one item at a time instead of showing
 * everything at once. */
function TeachingTimelineEditor({
  sectionId,
  step,
  onUpdated,
  refreshKey,
}: {
  sectionId: string;
  step: StructuredLearningStepDetail;
  onUpdated: (updated: StructuredLearningStepDetail) => void;
  refreshKey: number;
}) {
  const { t } = useTranslation();
  const [videos, setVideos] = useState<StructuredLearningSectionVideo[]>([]);
  const [loadingVideos, setLoadingVideos] = useState(true);
  const [order, setOrder] = useState<TeachingTimelineItem[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoadingVideos(true);
    listStructuredLearningSectionVideos(sectionId)
      .then((list) => { if (!cancelled) setVideos(list); })
      .catch(() => { if (!cancelled) setVideos([]); })
      .finally(() => { if (!cancelled) setLoadingVideos(false); });
    return () => { cancelled = true; };
  }, [sectionId, refreshKey]);

  useEffect(() => {
    if (loadingVideos) return;
    const saved = parseTeachingTimeline(step.TimelineJson);
    // Only 3 kinds of item ever exist: the ONE whole Text block (Learn —
    // full lesson text with Talk to tutor + image, always present, default
    // first) and up to 4 individual Videos.
    const allItems: TeachingTimelineItem[] = [
      { Type: 'Text' },
      ...videos.map((v): TeachingTimelineItem => ({ Type: 'Video', IllustrationId: v.IllustrationId })),
    ];
    if (saved && saved.length > 0) {
      const validSaved = saved.filter((item) => item.Type === 'Text' || videos.some((v) => v.IllustrationId === item.IllustrationId));
      const missing = allItems.filter(
        (item) =>
          !validSaved.some((s) =>
            item.Type === 'Text' ? s.Type === 'Text' : s.Type === 'Video' && s.IllustrationId === item.IllustrationId
          )
      );
      setOrder([...validSaved, ...missing]);
    } else {
      setOrder(allItems);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loadingVideos, step.StructuredLearningSectionStepId, videos.length]);

  function itemLabel(item: TeachingTimelineItem): string {
    if (item.Type === 'Text') return t('structuredLearningCourse.timelineTextItemLabel');
    // Purpose is always "Teaching" for every video in this step — not a
    // real per-video name — so label by creation order instead, otherwise
    // every video in this list looks like the same duplicated item.
    const videoIndex = videos.findIndex((v) => v.IllustrationId === item.IllustrationId);
    return `🎬 ${t('structuredLearningCourse.timelineVideoItemLabel', { number: videoIndex + 1 })}`;
  }

  function move(index: number, delta: 1 | -1) {
    setOrder((prev) => {
      const next = [...prev];
      const target = index + delta;
      if (target < 0 || target >= next.length) return prev;
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  async function handleSave() {
    setSaving(true);
    setError(null);
    setSuccess(false);
    try {
      await setStructuredLearningStepTimeline(step.StructuredLearningSectionStepId, JSON.stringify(order));
      onUpdated({ ...step, TimelineJson: JSON.stringify(order) });
      setSuccess(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  }

  async function handleClear() {
    setSaving(true);
    setError(null);
    setSuccess(false);
    try {
      await setStructuredLearningStepTimeline(step.StructuredLearningSectionStepId, null);
      onUpdated({ ...step, TimelineJson: null });
      setOrder([{ Type: 'Text' }, ...videos.map((v): TeachingTimelineItem => ({ Type: 'Video', IllustrationId: v.IllustrationId }))]);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  }

  if (loadingVideos) return null;
  if (videos.length === 0) return null;

  return (
    <div className="mt-4 rounded-xl border border-amber-400/20 bg-amber-500/[0.04] p-3">
      <p className="mb-1.5 text-xs font-semibold text-amber-300">{t('structuredLearningCourse.timelineOrderLabel')}</p>
      <p className="mb-2 text-[11px] text-slate-400">{t('structuredLearningCourse.timelineOrderHint')}</p>
      <ol className="space-y-1.5">
        {order.map((item, index) => (
          <li
            key={item.Type === 'Text' ? 'text' : `video-${item.IllustrationId}`}
            className="flex items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-2 py-1.5 text-xs text-slate-200"
          >
            <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-amber-500/30 text-[10px] font-bold text-amber-200">
              {index + 1}
            </span>
            <span className="flex-1 truncate">{itemLabel(item)}</span>
            <button type="button" onClick={() => move(index, -1)} disabled={index === 0} className="rounded p-0.5 hover:bg-white/10 disabled:opacity-30" aria-label="Mover arriba">
              <ChevronUp className="h-3.5 w-3.5" />
            </button>
            <button type="button" onClick={() => move(index, 1)} disabled={index === order.length - 1} className="rounded p-0.5 hover:bg-white/10 disabled:opacity-30" aria-label="Mover abajo">
              <ChevronDown className="h-3.5 w-3.5" />
            </button>
          </li>
        ))}
      </ol>
      {error && <p className="mt-1.5 text-xs text-red-300">{error}</p>}
      {success && <p className="mt-1.5 flex items-center gap-1 text-xs text-emerald-300"><Check className="h-3 w-3" /> {t('structuredLearningCourse.timelineOrderSaved')}</p>}
      <div className="mt-2 flex gap-2">
        <button
          type="button"
          onClick={handleSave}
          disabled={saving || order.length === 0}
          className="inline-flex items-center gap-1.5 rounded-lg bg-amber-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
        >
          {saving ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null}
          {t('structuredLearningCourse.timelineOrderSave')}
        </button>
        {step.TimelineJson && (
          <button
            type="button"
            onClick={handleClear}
            disabled={saving}
            className="rounded-lg border border-white/10 px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-white/[0.06] disabled:opacity-50"
          >
            {t('structuredLearningCourse.timelineOrderClear')}
          </button>
        )}
      </div>
    </div>
  );
}

/** Student/reviewer-facing "wizard" playback (2026-08-22) of a Teaching
 * step's Text-Tutor + Video items, one at a time with its own Previous/
 * Next — always on for Teaching in real/demo mode. Uses the author's saved
 * TeachingTimelineEditor order (step.TimelineJson) when set, otherwise
 * falls back to the natural registration order: Text first, then each
 * generated Video in creation order. */
function TeachingTimelineWizard({
  sectionId,
  step,
  onItemTypeChange,
}: {
  sectionId: string;
  step: StructuredLearningStepDetail;
  onItemTypeChange: (type: 'Text' | 'Video') => void;
}) {
  const { t } = useTranslation();
  const [videos, setVideos] = useState<StructuredLearningSectionVideo[]>([]);
  const [loadingVideos, setLoadingVideos] = useState(true);
  const [index, setIndex] = useState(0);

  useEffect(() => {
    let cancelled = false;
    listStructuredLearningSectionVideos(sectionId)
      .then((list) => { if (!cancelled) setVideos(list); })
      .catch(() => { if (!cancelled) setVideos([]); })
      .finally(() => { if (!cancelled) setLoadingVideos(false); });
    return () => { cancelled = true; };
  }, [sectionId]);

  useEffect(() => {
    setIndex(0);
  }, [step.StructuredLearningSectionStepId]);

  const saved = parseTeachingTimeline(step.TimelineJson);
  const validSaved = saved?.filter((item) => item.Type === 'Text' || videos.some((v) => v.IllustrationId === item.IllustrationId)) ?? null;
  const defaultItems: TeachingTimelineItem[] = [
    { Type: 'Text' },
    ...videos.map((v): TeachingTimelineItem => ({ Type: 'Video', IllustrationId: v.IllustrationId })),
  ];
  const missingFromSaved = validSaved
    ? defaultItems.filter((item) => !validSaved.some((s) => (item.Type === 'Text' ? s.Type === 'Text' : s.Type === 'Video' && s.IllustrationId === item.IllustrationId)))
    : [];
  const items = validSaved && validSaved.length > 0 ? [...validSaved, ...missingFromSaved] : defaultItems;
  const current = items[Math.min(index, items.length - 1)] as TeachingTimelineItem | undefined;

  useEffect(() => {
    if (current) onItemTypeChange(current.Type);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [current?.Type === 'Video' ? current.IllustrationId : current?.Type]);

  if (loadingVideos) return null;
  if (!current) return null;

  const isFirst = index === 0;
  const isLast = index >= items.length - 1;

  return (
    <div className="mt-6 space-y-4">
      <div className="flex flex-wrap gap-2">
        {items.map((item, i) => (
          <button
            key={item.Type === 'Text' ? 'text' : `video-${item.IllustrationId}`}
            type="button"
            onClick={() => setIndex(i)}
            className={`flex h-10 min-w-[2.5rem] items-center justify-center rounded-full border px-2.5 text-sm font-semibold transition ${
              i === index
                ? 'border-brand-400 bg-brand-500/20 text-brand-200'
                : 'border-white/10 bg-white/[0.02] text-slate-400 hover:border-white/20'
            }`}
          >
            {item.Type === 'Video' ? <Video className="h-5 w-5" /> : i + 1}
          </button>
        ))}
      </div>

      {current.Type === 'Video' ? (
        <div className="space-y-2">
          <p className="flex items-center gap-1.5 text-sm font-medium text-brand-300">
            <Video className="h-4 w-4" /> {t('structuredLearningCourse.videoStartPrompt')}
          </p>
          <video
            src={apiImageUrl(`/illustrations/structured-learning/${current.IllustrationId}/image`)}
            controls
            className="w-full rounded-xl bg-black"
          />
        </div>
      ) : (
        <RichStepContent content={step.Content} />
      )}

      {items.length > 1 && (
        <div className="flex items-center justify-between gap-3">
          <button
            type="button"
            disabled={isFirst}
            onClick={() => setIndex((i) => Math.max(0, i - 1))}
            className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs font-medium text-slate-200 hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-30"
          >
            <ArrowLeft className="h-3.5 w-3.5" /> {t('structuredLearningCourse.wizardPrevious')}
          </button>
          <span className="text-[11px] text-slate-500">{index + 1} / {items.length}</span>
          <button
            type="button"
            disabled={isLast}
            onClick={() => setIndex((i) => Math.min(items.length - 1, i + 1))}
            className="inline-flex items-center gap-1.5 rounded-lg bg-gradient-to-r from-brand-500 to-accent-500 px-3 py-1.5 text-xs font-semibold text-white disabled:cursor-not-allowed disabled:opacity-30"
          >
            {t('structuredLearningCourse.wizardNext')} <ArrowLeft className="h-3.5 w-3.5 rotate-180" />
          </button>
        </div>
      )}
    </div>
  );
}

/** "Add video" box (2026-08-21, Google Veo) — Teaching step only, hard
 * cap of 2 videos per section enforced server-side (real billed cost per
 * call). Same async start/poll shape as pedagogy generation: kick off a
 * run, poll every few seconds until Completed/Failed, then show the
 * resulting .mp4. */
function VideoGenerationBox({
  sectionId,
  step,
  onVideosChanged,
}: {
  sectionId: string;
  step: StructuredLearningStepDetail;
  onVideosChanged: () => void;
}) {
  const { t } = useTranslation();
  const [prompt, setPrompt] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [runStatus, setRunStatus] = useState<StructuredLearningVideoRunStatus | null>(null);
  const [videos, setVideos] = useState<StructuredLearningSectionVideo[]>([]);
  const [loadingVideos, setLoadingVideos] = useState(true);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [referenceImage, setReferenceImage] = useState<{ base64: string; contentType: string; previewUrl: string } | null>(null);
  const [videoModel, setVideoModel] = useState<'veo' | 'omni'>('veo');
  const [mergeMode, setMergeMode] = useState(false);
  const [mergeSelection, setMergeSelection] = useState<string[]>([]);
  const [merging, setMerging] = useState(false);
  const [trimmingId, setTrimmingId] = useState<string | null>(null);
  const [trimVersions, setTrimVersions] = useState<Record<string, number>>({});
  const [undoingId, setUndoingId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    listStructuredLearningSectionVideos(sectionId)
      .then((list) => { if (!cancelled) setVideos(list); })
      .catch(() => { if (!cancelled) setVideos([]); })
      .finally(() => { if (!cancelled) setLoadingVideos(false); });
    return () => { cancelled = true; };
  }, [sectionId]);

  useEffect(() => {
    if (!runStatus || runStatus.Stage !== 'Running') return;
    const interval = setInterval(async () => {
      try {
        const updated = await getStructuredLearningVideoStatus(runStatus.RunId);
        setRunStatus(updated);
        if (updated.Stage === 'Completed') {
          listStructuredLearningSectionVideos(sectionId).then(setVideos).catch(() => {});
          onVideosChanged();
        }
      } catch (err) {
        setRunStatus({ ...runStatus, Stage: 'Failed', ErrorMessage: err instanceof Error ? err.message : String(err) });
      }
    }, 8000);
    return () => clearInterval(interval);
  }, [runStatus, sectionId]);

  async function handleReferenceImageChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      const base64 = dataUrl.split(',')[1] ?? '';
      setReferenceImage({ base64, contentType: file.type || 'image/png', previewUrl: dataUrl });
    };
    reader.readAsDataURL(file);
  }

  async function handleGenerate() {
    if (!prompt.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      const status = await startStructuredLearningVideo(
        sectionId, prompt.trim(), step.StepType, videoModel, referenceImage?.base64, referenceImage?.contentType
      );
      setRunStatus(status);
      setPrompt('');
      setReferenceImage(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(illustrationId: string) {
    setDeletingId(illustrationId);
    try {
      await deleteStructuredLearningVideo(illustrationId);
      setVideos((prev) => prev.filter((v) => v.IllustrationId !== illustrationId));
      onVideosChanged();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setDeletingId(null);
    }
  }

  function toggleMergeSelection(illustrationId: string) {
    setMergeSelection((prev) =>
      prev.includes(illustrationId) ? prev.filter((id) => id !== illustrationId) : [...prev, illustrationId]
    );
  }

  function moveMergeSelection(index: number, delta: 1 | -1) {
    setMergeSelection((prev) => {
      const next = [...prev];
      const target = index + delta;
      if (target < 0 || target >= next.length) return prev;
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  async function handleMerge() {
    if (mergeSelection.length < 2) return;
    setMerging(true);
    setError(null);
    try {
      await mergeStructuredLearningVideos(sectionId, mergeSelection);
      setMergeSelection([]);
      setMergeMode(false);
      const updated = await listStructuredLearningSectionVideos(sectionId);
      setVideos(updated);
      onVideosChanged();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setMerging(false);
    }
  }

  async function handleTrim(illustrationId: string) {
    setTrimmingId(illustrationId);
  }

  function handleTrimApplied(illustrationId: string) {
    setTrimVersions((prev) => ({ ...prev, [illustrationId]: (prev[illustrationId] ?? 0) + 1 }));
    setTrimmingId(null);
  }

  async function handleUndo(illustrationId: string) {
    setUndoingId(illustrationId);
    setError(null);
    try {
      await undoStructuredLearningVideoEdit(illustrationId);
      setTrimVersions((prev) => ({ ...prev, [illustrationId]: (prev[illustrationId] ?? 0) + 1 }));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setUndoingId(null);
    }
  }

  const maxReached = videos.length >= 4;

  return (
    <div className="mt-4 rounded-xl border border-violet-400/20 bg-violet-500/[0.04] p-3">
      <div className="mb-1.5 flex items-center justify-between">
        <p className="flex items-center gap-1.5 text-xs font-semibold text-violet-300">
          <Video className="h-3.5 w-3.5" /> {t('structuredLearningCourse.videoCountLabel', { count: videos.length })}
        </p>
        {videos.length >= 2 && (
          <button
            type="button"
            onClick={() => { setMergeMode((v) => !v); setMergeSelection([]); }}
            className="text-[11px] font-medium text-violet-300 underline hover:text-violet-200"
          >
            {mergeMode ? t('structuredLearningCourse.videoMergeCancel') : t('structuredLearningCourse.videoMergeStart')}
          </button>
        )}
      </div>

      {!loadingVideos && videos.length > 0 && (
        <div className="mb-2 flex flex-wrap gap-2">
          {videos.map((video, videoIndex) => (
            <div key={video.IllustrationId} className="relative">
              <span className="absolute left-1.5 top-1.5 z-10 rounded-full border border-white/20 bg-slate-900/90 px-2 py-0.5 text-[10px] font-bold text-violet-200 shadow">
                {t('structuredLearningCourse.timelineVideoItemLabel', { number: videoIndex + 1 })}
              </span>
              <video
                src={apiImageUrl(`/illustrations/structured-learning/${video.IllustrationId}/image?v=${trimVersions[video.IllustrationId] ?? 0}`)}
                controls={!mergeMode}
                className="h-40 rounded-lg bg-black"
              />
              {mergeMode ? (
                <button
                  type="button"
                  onClick={() => toggleMergeSelection(video.IllustrationId)}
                  className={`absolute inset-0 flex items-center justify-center rounded-lg border-2 transition ${
                    mergeSelection.includes(video.IllustrationId)
                      ? 'border-violet-300 bg-violet-500/30'
                      : 'border-transparent bg-black/0 hover:bg-black/20'
                  }`}
                  aria-label="Seleccionar para unir"
                >
                  {mergeSelection.includes(video.IllustrationId) && (
                    <span className="flex h-8 w-8 items-center justify-center rounded-full border-2 border-violet-300 bg-violet-500 text-sm font-bold text-white shadow-lg">
                      {mergeSelection.indexOf(video.IllustrationId) + 1}
                    </span>
                  )}
                </button>
              ) : (
                <button
                  type="button"
                  onClick={() => handleDelete(video.IllustrationId)}
                  disabled={deletingId === video.IllustrationId}
                  className="absolute -right-1.5 -top-1.5 rounded-full border border-white/20 bg-slate-900 p-1 text-white shadow-lg hover:bg-red-600 disabled:opacity-50"
                  aria-label="Eliminar video"
                >
                  {deletingId === video.IllustrationId ? <Loader2 className="h-3 w-3 animate-spin" /> : <Trash2 className="h-3 w-3" />}
                </button>
              )}

              {!mergeMode && (
                <div className="absolute bottom-1.5 left-1.5 flex gap-1">
                  <button
                    type="button"
                    onClick={() => handleTrim(video.IllustrationId)}
                    className="rounded-full border border-white/20 bg-slate-900/80 px-1.5 py-0.5 text-[10px] font-medium text-slate-300 hover:bg-slate-800"
                  >
                    {t('structuredLearningCourse.videoTrimStart')}
                  </button>
                  <button
                    type="button"
                    onClick={() => handleUndo(video.IllustrationId)}
                    disabled={undoingId === video.IllustrationId}
                    className="rounded-full border border-white/20 bg-slate-900/80 px-1.5 py-0.5 text-[10px] font-medium text-slate-300 hover:bg-slate-800 disabled:opacity-50"
                  >
                    {undoingId === video.IllustrationId ? <Loader2 className="h-2.5 w-2.5 animate-spin" /> : t('structuredLearningCourse.videoUndo')}
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {trimmingId && (
        <VideoFrameTrimModal
          key={trimmingId}
          illustrationId={trimmingId}
          src={apiImageUrl(`/illustrations/structured-learning/${trimmingId}/image?v=${trimVersions[trimmingId] ?? 0}`)}
          onClose={() => setTrimmingId(null)}
          onApplied={() => handleTrimApplied(trimmingId)}
        />
      )}

      {mergeMode && (
        <div className="mb-2 space-y-2">
          <p className="text-[11px] text-slate-400">{t('structuredLearningCourse.videoMergeInstructions', { count: mergeSelection.length })}</p>
          {mergeSelection.length > 0 && (
            <ol className="space-y-1">
              {mergeSelection.map((id, index) => (
                <li key={id} className="flex items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-2 py-1 text-xs text-slate-300">
                  <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-violet-500/30 text-[10px] font-bold text-violet-200">{index + 1}</span>
                  <span className="flex-1 truncate">{videos.find((v) => v.IllustrationId === id)?.Purpose ?? id}</span>
                  <button type="button" onClick={() => moveMergeSelection(index, -1)} disabled={index === 0} className="rounded p-0.5 hover:bg-white/10 disabled:opacity-30" aria-label="Mover arriba">
                    <ChevronUp className="h-3.5 w-3.5" />
                  </button>
                  <button type="button" onClick={() => moveMergeSelection(index, 1)} disabled={index === mergeSelection.length - 1} className="rounded p-0.5 hover:bg-white/10 disabled:opacity-30" aria-label="Mover abajo">
                    <ChevronDown className="h-3.5 w-3.5" />
                  </button>
                  <button type="button" onClick={() => toggleMergeSelection(id)} className="rounded p-0.5 text-red-300 hover:bg-red-500/10" aria-label="Quitar">
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </li>
              ))}
            </ol>
          )}
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={handleMerge}
              disabled={mergeSelection.length < 2 || merging}
              className="ml-auto inline-flex items-center gap-1.5 whitespace-nowrap rounded-lg bg-violet-500 px-2.5 py-1 text-xs font-semibold text-white disabled:opacity-50"
            >
              {merging ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Video className="h-3.5 w-3.5" />}
              {t('structuredLearningCourse.videoMergeButton')}
            </button>
          </div>
          {merging && (
            <p className="flex items-center gap-1.5 text-xs text-violet-300">
              <Loader2 className="h-3 w-3 animate-spin" /> {t('structuredLearningCourse.videoMerging')}
            </p>
          )}
          {error && <p className="text-xs text-red-300">{error}</p>}
        </div>
      )}

      {maxReached ? (
        <p className="text-xs text-slate-500">{t('structuredLearningCourse.videoMaxReached')}</p>
      ) : (
        <>
          <div className="mb-2 flex gap-2">
            <button
              type="button"
              onClick={() => setVideoModel('veo')}
              disabled={submitting || runStatus?.Stage === 'Running'}
              className={`flex-1 rounded-lg border px-2.5 py-1.5 text-left text-xs transition ${
                videoModel === 'veo' ? 'border-violet-400 bg-violet-500/15 text-violet-100' : 'border-white/10 bg-white/[0.02] text-slate-400 hover:bg-white/[0.05]'
              }`}
            >
              <span className="font-semibold">Veo 3.1 Fast</span> · $0.30/video
            </button>
            <button
              type="button"
              onClick={() => setVideoModel('omni')}
              disabled={submitting || runStatus?.Stage === 'Running'}
              className={`flex-1 rounded-lg border px-2.5 py-1.5 text-left text-xs transition ${
                videoModel === 'omni' ? 'border-violet-400 bg-violet-500/15 text-violet-100' : 'border-white/10 bg-white/[0.02] text-slate-400 hover:bg-white/[0.05]'
              }`}
            >
              <span className="font-semibold">Gemini Omni Flash (Preview)</span> · $0.55/video
            </button>
          </div>
          <textarea
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            rows={2}
            placeholder={t('structuredLearningCourse.videoPromptPlaceholder')}
            className="input w-full"
            disabled={submitting || runStatus?.Stage === 'Running'}
          />
          <div className="mt-2 flex items-center gap-2">
            {referenceImage ? (
              <div className="relative">
                <img src={referenceImage.previewUrl} alt="Imagen de referencia" className="h-12 w-12 rounded-lg object-cover" />
                <button
                  type="button"
                  onClick={() => setReferenceImage(null)}
                  disabled={submitting || runStatus?.Stage === 'Running'}
                  className="absolute -right-1.5 -top-1.5 rounded-full border border-white/20 bg-slate-900 p-0.5 text-white shadow-lg hover:bg-red-600 disabled:opacity-50"
                  aria-label="Quitar imagen de referencia"
                >
                  <Trash2 className="h-2.5 w-2.5" />
                </button>
              </div>
            ) : (
              <label className="flex cursor-pointer items-center gap-1.5 rounded-lg border border-dashed border-violet-400/40 px-2.5 py-1.5 text-xs text-violet-300 hover:bg-violet-500/10">
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  className="hidden"
                  onChange={handleReferenceImageChange}
                  disabled={submitting || runStatus?.Stage === 'Running'}
                />
                {t('structuredLearningCourse.videoUploadImage')}
              </label>
            )}
          </div>
          {error && <p className="mt-1.5 text-xs text-red-300">{error}</p>}
          {runStatus?.Stage === 'Running' && (
            <p className="mt-1.5 flex items-center gap-1 text-xs text-violet-300"><Loader2 className="h-3 w-3 animate-spin" /> {t('structuredLearningCourse.videoGenerating')}</p>
          )}
          {runStatus?.Stage === 'Failed' && <p className="mt-1.5 text-xs text-red-300">{runStatus.ErrorMessage}</p>}
          {runStatus?.Stage === 'Completed' && <p className="mt-1.5 flex items-center gap-1 text-xs text-emerald-300"><Check className="h-3 w-3" /> {t('structuredLearningCourse.videoGenerated')}</p>}
          <button
            type="button"
            onClick={handleGenerate}
            disabled={submitting || !prompt.trim() || runStatus?.Stage === 'Running'}
            className="mt-2 inline-flex items-center gap-1.5 rounded-lg bg-violet-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
          >
            {submitting || runStatus?.Stage === 'Running' ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Video className="h-3.5 w-3.5" />}
            {t('structuredLearningCourse.videoGenerateButton')}
          </button>
        </>
      )}
    </div>
  );
}

/** Manual raw-text edit box (2026-08-11) — the "just let me delete the
 * sentence I don't like" escape hatch: no AI involved, the reviewer edits
 * Content directly and must explicitly confirm they read it and take
 * responsibility for it (the AI is not perfect and can alter or invent
 * content) before it can be saved — mirrors editStructuredLearningStep's
 * placement but calls setStructuredLearningStepContent instead. */
function ManualEditBox({
  sectionId,
  step,
  onUpdated,
}: {
  sectionId: string;
  step: StructuredLearningStepDetail;
  onUpdated: (updated: StructuredLearningStepDetail) => void;
}) {
  const { t } = useTranslation();
  const [editing, setEditing] = useState(false);
  const [content, setContent] = useState(step.Content);
  const [confirmed, setConfirmed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function startEditing() {
    setContent(step.Content);
    setConfirmed(false);
    setError(null);
    setEditing(true);
  }

  function cancel() {
    setEditing(false);
    setConfirmed(false);
    setError(null);
  }

  async function handleSave() {
    if (!confirmed || !content.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      const updated = await setStructuredLearningStepContent(sectionId, step.StepType, content.trim(), confirmed);
      onUpdated(updated);
      setEditing(false);
      setConfirmed(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  if (!editing) {
    return (
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={startEditing}
          className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs font-semibold text-slate-300 hover:bg-white/[0.08]"
        >
          <Pencil className="h-3.5 w-3.5" /> {t('structuredLearningCourse.manualEditButton')}
        </button>
        {step.IsHumanApproved && step.HumanApprovedAtUtc && (
          <p className="flex items-center gap-1 text-xs text-emerald-300">
            <Check className="h-3.5 w-3.5" />
            {t('structuredLearningCourse.humanApprovedBadge', { date: new Date(step.HumanApprovedAtUtc).toLocaleDateString() })}
          </p>
        )}
      </div>
    );
  }

  return (
    <div className="mt-4 rounded-xl border border-amber-400/20 bg-amber-500/[0.04] p-3">
      <p className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-amber-300">
        <Pencil className="h-3.5 w-3.5" /> {t('structuredLearningCourse.manualEditLabel')}
      </p>
      <textarea
        value={content}
        onChange={(e) => setContent(e.target.value)}
        rows={8}
        className="input w-full text-sm"
      />
      <label className="mt-2.5 flex items-start gap-2 text-xs text-slate-300">
        <input
          type="checkbox"
          checked={confirmed}
          onChange={(e) => setConfirmed(e.target.checked)}
          className="mt-0.5 h-3.5 w-3.5 flex-none rounded border-white/20 bg-white/[0.03]"
        />
        <span>{t('structuredLearningCourse.manualEditConfirmLabel')}</span>
      </label>
      {error && <p className="mt-1.5 text-xs text-red-300">{error}</p>}
      <div className="mt-2.5 flex gap-2">
        <button
          type="button"
          onClick={handleSave}
          disabled={submitting || !confirmed || !content.trim()}
          className="inline-flex items-center gap-1.5 rounded-lg bg-amber-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
        >
          {submitting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Check className="h-3.5 w-3.5" />}
          {t('structuredLearningCourse.manualEditSave')}
        </button>
        <button
          type="button"
          onClick={cancel}
          disabled={submitting}
          className="rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs font-semibold text-slate-300 hover:bg-white/[0.08]"
        >
          {t('structuredLearningCourse.editCancel')}
        </button>
      </div>
    </div>
  );
}

/** Deep Dive card editor (2026-08-10) — Edit mode's static, editable card
 * list, replacing the interactive quiz. Mirrors PreviewNodeBlueprintView's
 * Deep Dive card editing (Pencil/Trash icons + inline edit/confirm boxes +
 * "+ Add Card"), backed by StructuredLearningExampleCardEditorAgent. */
function DeepDiveCardsEditor({
  sectionId,
  step,
  onUpdated,
}: {
  sectionId: string;
  step: StructuredLearningStepDetail;
  onUpdated: (updated: StructuredLearningStepDetail) => void;
}) {
  const { t } = useTranslation();
  let cards: DeepDiveCard[] = [];
  try {
    cards = step.StepDataJson ? (JSON.parse(step.StepDataJson) as DeepDiveCard[]) : [];
  } catch {
    cards = [];
  }

  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [deletingIndex, setDeletingIndex] = useState<number | null>(null);
  const [adding, setAdding] = useState(false);
  const [instruction, setInstruction] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function resetForms() {
    setEditingIndex(null);
    setDeletingIndex(null);
    setAdding(false);
    setInstruction('');
    setError(null);
  }

  async function handleEditCard(index: number) {
    if (!instruction.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      const updated = await editStructuredLearningDeepDiveCard(sectionId, index, instruction.trim());
      onUpdated(updated);
      resetForms();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleAddCard() {
    if (!instruction.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      const updated = await addStructuredLearningDeepDiveCard(sectionId, instruction.trim());
      onUpdated(updated);
      resetForms();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDeleteCard(index: number) {
    setSubmitting(true);
    setError(null);
    try {
      const updated = await deleteStructuredLearningDeepDiveCard(sectionId, index);
      onUpdated(updated);
      resetForms();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mt-3 space-y-3">
      {cards.map((card, index) => (
        <div key={index} className="overflow-hidden rounded-xl border border-white/10 bg-white/[0.02]">
          {card.IllustrationIds && card.IllustrationIds.length > 0 ? (
            <div className="grid grid-cols-2 gap-1 p-1">
              {card.IllustrationIds.map((id, i) => (
                <ZoomableIllustration key={id} illustrationId={id} alt={`${card.Title} — step ${i + 1}`} className="h-28 w-full rounded-lg bg-slate-950/50 object-contain object-center" />
              ))}
            </div>
          ) : card.IllustrationId ? (
            <ZoomableIllustration illustrationId={card.IllustrationId} alt={card.Title} className="h-56 w-full rounded-t-xl bg-slate-950/50 object-contain object-center" />
          ) : card.Emoji ? (
            <div className="flex h-24 w-full items-center justify-center bg-white/[0.03] text-4xl">{card.Emoji}</div>
          ) : null}
          <div className="p-3">
            <div className="flex items-start justify-between gap-2">
              <div>
                <div className="text-sm font-semibold text-white">{card.Title}</div>
                <p className="mt-1 whitespace-pre-wrap text-sm text-slate-300">{card.Text}</p>
              </div>
              <div className="flex flex-none gap-1">
                <button
                  type="button"
                  onClick={() => {
                    resetForms();
                    setEditingIndex(index);
                  }}
                  className="rounded-lg border border-white/10 bg-white/[0.03] p-1.5 text-slate-300 hover:bg-white/[0.08]"
                >
                  <Pencil className="h-3.5 w-3.5" />
                </button>
                <button
                  type="button"
                  onClick={() => {
                    resetForms();
                    setDeletingIndex(index);
                  }}
                  className="rounded-lg border border-red-400/20 bg-red-500/[0.05] p-1.5 text-red-300 hover:bg-red-500/10"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>

            {editingIndex === index && (
              <div className="mt-3 rounded-lg border border-sky-400/20 bg-sky-500/[0.04] p-2.5">
                <p className="mb-1.5 text-xs font-semibold text-sky-300">{t('structuredLearningCourse.editThisCardLabel')}</p>
                <textarea value={instruction} onChange={(e) => setInstruction(e.target.value)} rows={2} className="input w-full" />
                {error && <p className="mt-1.5 text-xs text-red-300">{error}</p>}
                <div className="mt-2 flex gap-2">
                  <button
                    type="button"
                    onClick={() => handleEditCard(index)}
                    disabled={submitting || !instruction.trim()}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-sky-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
                  >
                    {submitting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Check className="h-3.5 w-3.5" />}
                    {t('structuredLearningCourse.editApply')}
                  </button>
                  <button type="button" onClick={resetForms} className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:bg-white/[0.06]">
                    {t('structuredLearningCourse.editCancel')}
                  </button>
                </div>
              </div>
            )}

            {deletingIndex === index && (
              <div className="mt-3 rounded-lg border border-red-400/30 bg-red-500/[0.06] p-2.5">
                <p className="text-xs text-red-200">{t('structuredLearningCourse.deleteCardConfirm')}</p>
                <div className="mt-2 flex gap-2">
                  <button
                    type="button"
                    onClick={() => handleDeleteCard(index)}
                    disabled={submitting}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-red-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
                  >
                    {submitting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Trash2 className="h-3.5 w-3.5" />}
                    {t('structuredLearningCourse.deleteCardButton')}
                  </button>
                  <button type="button" onClick={resetForms} className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:bg-white/[0.06]">
                    {t('structuredLearningCourse.editCancel')}
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      ))}

      {adding ? (
        <div className="rounded-xl border border-sky-400/20 bg-sky-500/[0.04] p-3">
          <p className="mb-1.5 text-xs font-semibold text-sky-300">{t('structuredLearningCourse.addCardLabel')}</p>
          <textarea value={instruction} onChange={(e) => setInstruction(e.target.value)} rows={2} className="input w-full" />
          {error && <p className="mt-1.5 text-xs text-red-300">{error}</p>}
          <div className="mt-2 flex gap-2">
            <button
              type="button"
              onClick={handleAddCard}
              disabled={submitting || !instruction.trim()}
              className="inline-flex items-center gap-1.5 rounded-lg bg-sky-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
            >
              {submitting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Plus className="h-3.5 w-3.5" />}
              {t('structuredLearningCourse.addCardButton')}
            </button>
            <button type="button" onClick={resetForms} className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:bg-white/[0.06]">
              {t('structuredLearningCourse.editCancel')}
            </button>
          </div>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => {
            resetForms();
            setAdding(true);
          }}
          className="inline-flex items-center gap-1.5 rounded-xl border border-white/10 bg-white/[0.03] px-3 py-2 text-xs font-medium text-slate-200 hover:bg-white/[0.06]"
        >
          <Plus className="h-3.5 w-3.5" /> {t('structuredLearningCourse.addCardLabel')}
        </button>
      )}
    </div>
  );
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

function StepIllustration({ stepDataJson }: { stepDataJson: string | null }) {
  if (!stepDataJson) return null;
  let illustrationId: string | null = null;
  try {
    illustrationId = (JSON.parse(stepDataJson) as { IllustrationId?: string | null }).IllustrationId ?? null;
  } catch {
    return null;
  }
  if (!illustrationId) return null;

  return <ZoomableIllustration illustrationId={illustrationId} alt="" className="h-72 w-full rounded-xl bg-slate-950/50 object-contain object-center" />;
}

function ZoomableIllustration({ illustrationId, alt, className }: { illustrationId: string; alt: string; className: string }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open]);

  const src = apiImageUrl(`/illustrations/structured-learning/${illustrationId}/image`);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className="group relative block w-full cursor-zoom-in" aria-label={t('structuredLearningCourse.enlargeIllustration')}>
        <img src={src} alt={alt} className={className} />
        <span className="pointer-events-none absolute bottom-2 right-2 rounded-lg bg-black/65 px-2 py-1 text-[10px] font-semibold text-white opacity-0 transition group-hover:opacity-100">{t('structuredLearningCourse.enlargeIllustrationTooltip')}</span>
      </button>
      {open && (
        <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/85 p-4 backdrop-blur-sm" role="dialog" aria-modal="true" aria-label={t('structuredLearningCourse.enlargedIllustration')} onClick={() => setOpen(false)}>
          <div className="relative flex max-h-[95vh] max-w-[95vw] items-center justify-center" onClick={(event) => event.stopPropagation()}>
            <img src={src} alt={alt} className="max-h-[90vh] max-w-[92vw] rounded-xl object-contain shadow-2xl" />
            <button type="button" onClick={() => setOpen(false)} className="absolute -right-2 -top-2 rounded-full border border-white/20 bg-slate-900 p-2 text-white shadow-lg hover:bg-slate-800" aria-label={t('structuredLearningCourse.closeIllustration')}>
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}
    </>
  );
}

/**
 * Interactive Deep Dive — card-by-card, SEQUENTIAL and MANDATORY-ALL quiz
 * loop (2026-08-10), ported from PreviewNodePage's Graph experience:
 * every card must be explained back and mastered, in order, before the
 * step is Complete — no attempt cap, no regression, just keep retrying
 * the SAME card. Backed by StructuredLearningDeepDiveEngine (mirrors
 * TutorService.SubmitDeepDiveAttemptAsync/DeepDiveLoopGate). Each card
 * gets its own compact VoiceTutorAgent — a spoken explanation that the
 * Agent itself accepts (`mark_explanation_understood`) is forwarded here
 * exactly like a typed answer, via `onExplanationAccepted`.
 */
function DeepDiveQuiz({
  sectionId,
  stepId,
  stepDataJson,
  mode,
  onComplete,
}: {
  sectionId: string;
  stepId: string;
  stepDataJson: string;
  mode: PreviewMode;
  onComplete: () => void;
}) {
  const { t } = useTranslation();
  let cards: DeepDiveCard[] = [];
  try {
    cards = JSON.parse(stepDataJson) as DeepDiveCard[];
  } catch {
    cards = [];
  }

  // Free card-to-card navigation, no forced Q&A gate (2026-08-23, explicit
  // user request: don't make the student answer a question to advance —
  // let them read/hear the card's explanation and move to the next one
  // freely). Previously this only applied to Review & acceptance/Edit and
  // forced a grading loop in `real` mode; now every mode behaves the same.
  const freeNavigation = true;

  const startedRef = useRef(false);
  const completedRef = useRef(false);
  const [cardIndex, setCardIndex] = useState(0);
  const [currentPrompt, setCurrentPrompt] = useState<string | undefined>(undefined);
  const [response, setResponse] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [pendingAdvance, setPendingAdvance] = useState<{ nextCardIndex: number } | null>(null);
  const [complete, setComplete] = useState(false);
  const [reviewCardIndex, setReviewCardIndex] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const markCompleteOnce = () => {
    setComplete(true);
    if (!completedRef.current) {
      completedRef.current = true;
      onComplete();
    }
  };

  useEffect(() => {
    if (freeNavigation) {
      // Unlike Review & acceptance/Edit (already ungated regardless), here
      // the student must page through to the LAST card before the outer
      // step unlocks — see the "Finish" button below, which is the only
      // thing that calls markCompleteOnce() in this branch.
      return;
    }
    if (startedRef.current) return;
    startedRef.current = true;
    getStructuredLearningDeepDiveState(sectionId)
      .then((state) => {
        setCardIndex(state.CardIndex);
        setCurrentPrompt(state.NextQuestion || undefined);
        if (state.Complete) markCompleteOnce();
      })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sectionId, freeNavigation]);

  async function handleSubmit(overrideText?: string) {
    const text = (overrideText ?? response).trim();
    if (!text || submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      const result = await submitStructuredLearningDeepDiveAttempt(sectionId, text, currentPrompt);
      setResponse('');
      if (result.Complete) {
        markCompleteOnce();
      } else if (result.Mastered) {
        setPendingAdvance({ nextCardIndex: result.NextCardIndex });
      } else {
        setCardIndex(result.CardIndex);
        setCurrentPrompt(result.Message);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  function handlePendingAdvanceContinue() {
    if (!pendingAdvance) return;
    setCardIndex(pendingAdvance.nextCardIndex);
    setCurrentPrompt(undefined);
    setPendingAdvance(null);
  }

  if (error) return <p className="mt-3 text-sm text-red-300">{error}</p>;
  if (cards.length === 0) return null;

  // Free card-to-card browsing in Review & acceptance/Edit (2026-08-23,
  // explicit user request): no grading, no completion banner, no
  // explain-and-submit gate at all — just the card's own content plus the
  // tutor agent, with plain Previous/Next buttons between the cards.
  if (freeNavigation) {
    const freeCard = cards[Math.min(cardIndex, cards.length - 1)];
    return (
      <div className="mt-3 space-y-3">
        {cards.length > 1 && (
          <div className="flex flex-wrap gap-1.5">
            {cards.map((c, index) => (
              <button
                key={index}
                type="button"
                onClick={() => setCardIndex(index)}
                title={c.Title}
                className={`flex h-7 min-w-[1.75rem] items-center justify-center rounded-full border px-2 text-[11px] font-semibold transition ${
                  index === cardIndex
                    ? 'border-brand-400 bg-brand-500/20 text-brand-200'
                    : 'border-white/10 bg-white/[0.02] text-slate-400 hover:bg-white/[0.06]'
                }`}
              >
                {index + 1}
              </button>
            ))}
          </div>
        )}
        <div className="rounded-xl border border-white/10 bg-white/[0.03] p-4">
          {freeCard.IllustrationIds && freeCard.IllustrationIds.length > 0 ? (
            <div className="mb-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
              {freeCard.IllustrationIds.map((id, i) => (
                <div key={id} className="space-y-1">
                  <ZoomableIllustration
                    illustrationId={id}
                    alt={`${freeCard.Title} — step ${i + 1}`}
                    className="h-auto max-h-48 w-full rounded-lg object-contain bg-black/20"
                  />
                  <p className="text-center text-[11px] font-medium text-slate-400">Step {i + 1}</p>
                </div>
              ))}
            </div>
          ) : freeCard.IllustrationId ? (
            <ZoomableIllustration
              illustrationId={freeCard.IllustrationId}
              alt={freeCard.Title}
              className="mb-2 h-auto max-h-72 w-full rounded-lg object-contain bg-black/20"
            />
          ) : (
            freeCard.Emoji && <div className="mb-2 text-3xl leading-none">{freeCard.Emoji}</div>
          )}
          <p className="text-sm font-semibold text-white">{freeCard.Title}</p>
          <p className="mt-1 whitespace-pre-wrap text-sm text-slate-300">{freeCard.Text}</p>

          <VoiceTutorAgent
            structuredLearningSectionStepId={stepId}
            exampleCardIndex={cardIndex}
            reviewOnly
            autoConnect={false}
            compact
          />
        </div>
        {cards.length > 1 && (
          <div className="flex items-center justify-between gap-3">
            <button
              type="button"
              disabled={cardIndex === 0}
              onClick={() => setCardIndex((i) => Math.max(0, i - 1))}
              className="rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-30"
            >
              {t('structuredLearningCourse.wizardPrevious')}
            </button>
            {cardIndex < cards.length - 1 ? (
              <button
                type="button"
                onClick={() => setCardIndex((i) => Math.min(cards.length - 1, i + 1))}
                className="rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-white/[0.06]"
              >
                {t('structuredLearningCourse.wizardNext')}
              </button>
            ) : (
              <button
                type="button"
                onClick={() => markCompleteOnce()}
                className="rounded-lg bg-gradient-to-r from-brand-500 to-accent-500 px-3 py-1.5 text-xs font-semibold text-white"
              >
                {t('structuredLearningCourse.deepDiveContinue')}
              </button>
            )}
          </div>
        )}
      </div>
    );
  }

  // Complete (2026-08-10, explicit user request: "what happens if a
  // student wants to see Deep Dive again?") — still show every card as a
  // clickable, mastered pill so the student can revisit any of them
  // read-only at any time, instead of just a bare "done" banner with no
  // way back into the content. Mirrors Graph's own card-index-review
  // pattern, just available for the whole step's lifetime here (Graph's
  // real-mode session moves past DeepDive entirely once complete, so it
  // never needs this after-the-fact case).
  if (complete) {
    return (
      <div className="mt-3 space-y-3">
        <div className="flex items-center gap-2 rounded-xl border border-emerald-400/30 bg-emerald-500/10 p-3 text-sm text-emerald-200">
          <CheckCircle2 className="h-4 w-4" /> {t('structuredLearningCourse.deepDiveComplete')}
        </div>
        {cards.length > 0 && (
          <div className="space-y-2">
            {cards.map((c, index) => (
              <button
                key={index}
                type="button"
                onClick={() => setReviewCardIndex(index)}
                className="flex w-full cursor-pointer items-center justify-between gap-3 rounded-xl border border-emerald-400/30 bg-emerald-500/5 px-4 py-2.5 text-left transition hover:bg-emerald-500/10"
              >
                <span className="flex items-center gap-2 text-sm font-medium text-emerald-100">
                  <Check className="h-4 w-4 shrink-0 text-emerald-300" />
                  {c.Title}
                </span>
                <span className="shrink-0 text-xs text-emerald-400/80">{t('structuredLearningCourse.deepDiveReviewLabel')}</span>
              </button>
            ))}
          </div>
        )}
        {reviewCardIndex !== null && (
          <DeepDiveCardReviewOverlay
            card={cards[reviewCardIndex]}
            index={reviewCardIndex}
            stepId={stepId}
            onClose={() => setReviewCardIndex(null)}
          />
        )}
      </div>
    );
  }

  const card = cards[Math.min(cardIndex, cards.length - 1)];

  return (
    <div className="mt-3 space-y-3">
      {cards.length > 1 && (
        <div className="flex flex-wrap gap-1.5">
          {cards.map((c, index) => {
            const isMastered = index < cardIndex;
            const isCurrent = index === cardIndex;
            const isReviewable = isMastered || isCurrent;
            return (
              <button
                key={index}
                type="button"
                disabled={!isReviewable}
                onClick={() => isReviewable && setReviewCardIndex(index)}
                title={c.Title}
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

      <div className="rounded-xl border border-white/10 bg-white/[0.03] p-4">
        {card.IllustrationIds && card.IllustrationIds.length > 0 ? (
          <div className="mb-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
            {card.IllustrationIds.map((id, i) => (
              <div key={id} className="space-y-1">
                <ZoomableIllustration
                  illustrationId={id}
                  alt={`${card.Title} — step ${i + 1}`}
                  className="h-auto max-h-48 w-full rounded-lg object-contain bg-black/20"
                />
                <p className="text-center text-[11px] font-medium text-slate-400">Step {i + 1}</p>
              </div>
            ))}
          </div>
        ) : card.IllustrationId ? (
          <ZoomableIllustration
            illustrationId={card.IllustrationId}
            alt={card.Title}
            className="mb-2 h-auto max-h-72 w-full rounded-lg object-contain bg-black/20"
          />
        ) : (
          card.Emoji && <div className="mb-2 text-3xl leading-none">{card.Emoji}</div>
        )}
        <p className="text-sm font-semibold text-white">{card.Title}</p>
        <p className="mt-1 whitespace-pre-wrap text-sm text-slate-300">{card.Text}</p>

        <VoiceTutorAgent
          structuredLearningSectionStepId={stepId}
          exampleCardIndex={cardIndex}
          autoConnect={false}
          compact
          onExplanationAccepted={(explanation) => void handleSubmit(explanation)}
        />
      </div>

      <p className="text-sm text-slate-300">{currentPrompt || t('structuredLearningCourse.deepDiveOpeningPrompt', { title: card.Title })}</p>

      {pendingAdvance ? (
        <div className="rounded-xl border border-emerald-400/30 bg-emerald-500/[0.08] p-4 text-center">
          <p className="text-sm font-semibold text-emerald-200">{t('structuredLearningCourse.deepDiveCardMastered')}</p>
          <button
            type="button"
            onClick={handlePendingAdvanceContinue}
            className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2 text-sm font-semibold text-white"
          >
            {t('structuredLearningCourse.deepDiveContinue')}
          </button>
        </div>
      ) : (
        <>
          <textarea
            value={response}
            onChange={(e) => setResponse(e.target.value)}
            rows={3}
            placeholder={t('structuredLearningCourse.deepDivePlaceholder')}
            className="input w-full"
          />
          <button
            type="button"
            onClick={() => handleSubmit()}
            disabled={submitting || !response.trim()}
            className="inline-flex items-center gap-2 rounded-xl bg-fuchsia-500 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
          >
            {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
            {t('structuredLearningCourse.deepDiveSubmit')}
          </button>
        </>
      )}

      {reviewCardIndex !== null && (
        <DeepDiveCardReviewOverlay
          card={cards[reviewCardIndex]}
          index={reviewCardIndex}
          stepId={stepId}
          onClose={() => setReviewCardIndex(null)}
        />
      )}
    </div>
  );
}

/** Read-only overlay for revisiting an already-mastered Deep Dive card
 * (ported from PreviewNodePage's DeepDiveCardReviewModal) — purely
 * display + a read-only voice explain, never touches grading state. */
function DeepDiveCardReviewOverlay({
  card,
  index,
  stepId,
  onClose,
}: {
  card: DeepDiveCard;
  index: number;
  stepId: string;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-sm">
      <div className="max-h-[85vh] w-full max-w-md overflow-y-auto rounded-2xl border border-white/10 bg-slate-950 p-6 shadow-2xl shadow-black/40">
        <div className="mb-3 flex items-start justify-between gap-3">
          <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
            {t('structuredLearningCourse.deepDiveCardLabel')} {index + 1}
          </p>
          <button type="button" onClick={onClose} className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>
        {card.IllustrationIds && card.IllustrationIds.length > 0 ? (
          <div className="mb-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
            {card.IllustrationIds.map((id, i) => (
              <div key={id} className="space-y-1">
                <img
                  src={apiImageUrl(`/illustrations/structured-learning/${id}/image`)}
                  alt={`${card.Title} — step ${i + 1}`}
                  className="h-auto max-h-48 w-full rounded-lg object-contain bg-black/20"
                />
                <p className="text-center text-[11px] font-medium text-slate-400">Step {i + 1}</p>
              </div>
            ))}
          </div>
        ) : card.IllustrationId ? (
          <img
            src={apiImageUrl(`/illustrations/structured-learning/${card.IllustrationId}/image`)}
            alt={card.Title}
            className="mb-2 h-auto max-h-72 w-full rounded-lg object-contain bg-black/20"
          />
        ) : (
          card.Emoji && <div className="mb-2 text-3xl leading-none">{card.Emoji}</div>
        )}
        <p className="text-sm font-semibold text-white">{card.Title}</p>
        <p className="mt-1 whitespace-pre-wrap text-sm text-slate-300">{card.Text}</p>

        <VoiceTutorAgent
          structuredLearningSectionStepId={stepId}
          exampleCardIndex={index}
          reviewOnly
          autoConnect={false}
          compact
        />
      </div>
    </div>
  );
}


/** Interactive Recall practice — every turn grades the previous answer AND
 * generates a genuinely new question (StructuredLearningRecallEngine),
 * never a static bank. */
function RecallPractice({ sectionId, onComplete }: { sectionId: string; onComplete: () => void }) {
  const { t } = useTranslation();
  const startedRef = useRef(false);
  const completedRef = useRef(false);
  const [question, setQuestion] = useState<string | null>(null);
  const [response, setResponse] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [attemptsUsed, setAttemptsUsed] = useState(0);
  const [itemsMastered, setItemsMastered] = useState(0);
  const [itemsRequired, setItemsRequired] = useState(3);
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

  // Bootstrap-safe (2026-08-10 bug fix): a blind POST with no
  // studentResponse on every mount used to 400 once the section's first
  // question had already been asked in an earlier visit. This GET never
  // grades anything, so it's safe to call unconditionally on mount.
  useEffect(() => {
    if (startedRef.current) return;
    startedRef.current = true;
    getStructuredLearningRecallState(sectionId)
      .then((result) => {
        setQuestion(result.NextQuestion);
        setItemsRequired(result.ItemsRequired);
        setItemsMastered(result.ItemsMastered);
        setAttemptsUsed(result.AttemptsUsedForItem);
        if (result.Complete) markCompleteOnce();
      })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sectionId]);

  async function handleSubmit() {
    if (!response.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      const result = await submitStructuredLearningRecallAttempt(sectionId, response);
      setQuestion(result.NextQuestion);
      setItemsMastered(result.ItemsMastered);
      setItemsRequired(result.ItemsRequired);
      setAttemptsUsed(result.AttemptsUsedForItem);
      setLastMastered(result.MasteredThisAttempt);
      setResponse('');
      if (result.Complete) {
        markCompleteOnce();
      } else {
        setComplete(false);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  if (error) return <p className="mt-3 text-sm text-red-300">{error}</p>;

  if (complete) {
    return (
      <div className="mt-3 flex items-center gap-2 rounded-xl border border-emerald-400/30 bg-emerald-500/10 p-3 text-sm text-emerald-200">
        <CheckCircle2 className="h-4 w-4" /> {t('structuredLearningCourse.recallComplete')}
      </div>
    );
  }

  if (!question) {
    return <LoadingSpinner label={t('structuredLearningCourse.recallLoading')} />;
  }

  return (
    <div className="mt-3 space-y-3">
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
        {t('structuredLearningCourse.recallAttemptCount', { attempt: attemptsUsed + 1, max: RECALL_MAX_ATTEMPTS })}
      </p>
      <div className="flex items-center gap-2">
        <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-white/10">
          <div
            className="h-full rounded-full bg-fuchsia-400 transition-all"
            style={{ width: `${Math.min(100, (itemsMastered / itemsRequired) * 100)}%` }}
          />
        </div>
        <span className="text-xs text-slate-400">{t('structuredLearningCourse.recallProgress', { mastered: itemsMastered, required: itemsRequired })}</span>
      </div>
      {lastMastered === true && <p className="text-xs text-emerald-300">{t('structuredLearningCourse.recallGoodJob')}</p>}
      <p className="text-sm text-slate-200">{question}</p>
      <textarea
        value={response}
        onChange={(e) => setResponse(e.target.value)}
        rows={3}
        placeholder={t('structuredLearningCourse.recallPlaceholder')}
        className="input w-full"
      />
      <button
        type="button"
        onClick={handleSubmit}
        disabled={submitting || !response.trim()}
        className="inline-flex items-center gap-2 rounded-xl bg-fuchsia-500 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
      >
        {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
        {t('structuredLearningCourse.recallSubmit')}
      </button>
    </div>
  );
}

/** Interactive Assessment — rounds of questions generated fresh one at a
 * time (StructuredLearningAssessmentEngine); a Failed round auto-starts a
 * brand-new one with different questions in the same response. */
function AssessmentPractice({
  sectionId,
  questionType,
  onComplete,
}: {
  sectionId: string;
  questionType: string;
  onComplete: () => void;
}) {
  const { t } = useTranslation();
  const completedRef = useRef(false);
  const [round, setRound] = useState<StructuredLearningAssessmentRoundState | null | undefined>(undefined);
  const [selectedOption, setSelectedOption] = useState<number | null>(null);
  const [openAnswer, setOpenAnswer] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [lastGrade, setLastGrade] = useState<{ Correctness: string; Feedback: string } | null>(null);
  const [roundResult, setRoundResult] = useState<{ Passed: boolean; FinalScore: number } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const passed =
    round && !round.CurrentQuestion ? (roundResult?.Passed ?? round.Status === 'Passed') : false;

  useEffect(() => {
    if (passed && !completedRef.current) {
      completedRef.current = true;
      onComplete();
    }
  }, [passed, onComplete]);

  useEffect(() => {
    getActiveStructuredLearningAssessmentRound(sectionId)
      .then((active) => setRound(active))
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
  }, [sectionId]);

  async function handleStart() {
    setSubmitting(true);
    setError(null);
    try {
      const state = await startStructuredLearningAssessmentRound(sectionId);
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

  if (error) return <p className="mt-3 text-sm text-red-300">{error}</p>;

  if (round === undefined) {
    return <LoadingSpinner label={t('structuredLearningCourse.assessmentLoading')} />;
  }

  if (round === null) {
    return (
      <button
        type="button"
        onClick={handleStart}
        disabled={submitting}
        className="mt-3 inline-flex items-center gap-2 rounded-xl bg-emerald-500 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
      >
        {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <ClipboardCheck className="h-4 w-4" />}
        {t('structuredLearningCourse.assessmentStart')}
      </button>
    );
  }

  if (!round.CurrentQuestion) {
    return (
      <div className={`mt-3 rounded-xl border p-4 text-sm ${passed ? 'border-emerald-400/30 bg-emerald-500/10 text-emerald-200' : 'border-red-400/30 bg-red-500/10 text-red-200'}`}>
        <p className="font-semibold">{passed ? t('structuredLearningCourse.assessmentPassed') : t('structuredLearningCourse.assessmentFailed')}</p>
        {(roundResult?.FinalScore ?? round.FinalScore) != null && (
          <p className="mt-0.5 text-xs">{t('structuredLearningCourse.assessmentScoreLabel', { score: roundResult?.FinalScore ?? round.FinalScore })}</p>
        )}
        {passed && (
          <button type="button" onClick={handleStart} className="mt-2 text-xs font-medium underline">
            {t('structuredLearningCourse.assessmentRetry')}
          </button>
        )}
      </div>
    );
  }

  const q = round.CurrentQuestion;
  const gradeColor =
    lastGrade?.Correctness === 'Correct'
      ? 'border-emerald-400/30 bg-emerald-500/10 text-emerald-200'
      : lastGrade?.Correctness === 'PartiallyCorrect'
        ? 'border-amber-400/30 bg-amber-500/10 text-amber-200'
        : 'border-red-400/30 bg-red-500/10 text-red-200';

  return (
    <div className="mt-3 space-y-3">
      <p className="text-xs text-slate-400">
        {t('structuredLearningCourse.assessmentQuestionOf', { index: q.QuestionIndex, total: round.TotalQuestions, round: round.RoundNumber })}
      </p>
      <p className="text-sm text-slate-200">{q.QuestionText}</p>

      {lastGrade && <div className={`rounded-lg border p-2.5 text-xs ${gradeColor}`}>{lastGrade.Feedback}</div>}

      {questionType === 'MultipleChoice' && q.Options ? (
        <div className="space-y-2">
          {q.Options.map((opt, index) => (
            <label
              key={index}
              className={`flex cursor-pointer items-center gap-2 rounded-lg border p-2.5 text-sm ${
                selectedOption === index ? 'border-emerald-400/60 bg-emerald-500/10 text-white' : 'border-white/10 bg-white/[0.02] text-slate-300'
              }`}
            >
              <input
                type="radio"
                name={`sl-assessment-${q.StructuredLearningAssessmentQuestionAttemptId}`}
                checked={selectedOption === index}
                onChange={() => setSelectedOption(index)}
                className="h-4 w-4"
              />
              {opt}
            </label>
          ))}
        </div>
      ) : (
        <textarea
          value={openAnswer}
          onChange={(e) => setOpenAnswer(e.target.value)}
          rows={3}
          placeholder={t('structuredLearningCourse.assessmentAnswerPlaceholder')}
          className="input w-full"
        />
      )}

      <button
        type="button"
        onClick={handleSubmitAnswer}
        disabled={submitting}
        className="inline-flex items-center gap-2 rounded-xl bg-emerald-500 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
      >
        {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
        {t('structuredLearningCourse.assessmentSubmit')}
      </button>
    </div>
  );
}
