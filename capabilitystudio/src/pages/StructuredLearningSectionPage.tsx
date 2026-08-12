import { useCallback, useEffect, useRef, useState } from 'react';
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
} from 'lucide-react';
import {
  getStructuredLearningDocument,
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
  type StructuredLearningDocumentDetail,
  type StructuredLearningStepDetail,
  type StructuredLearningAssessmentRoundState,
} from '../lib/api/structuredLearningApi';
import { apiImageUrl } from '../lib/api/httpClient';
import LoadingSpinner from '../components/LoadingSpinner';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import PreviewModeSwitcher from '../components/PreviewModeSwitcher';
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

  const steps = currentSection?.Steps ?? [];
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
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <Link to={withPreviewMode(`/structured-learning/${documentId}/course`, mode)} className="inline-flex items-center gap-1.5 text-sm text-slate-400 hover:text-white">
        <ArrowLeft className="h-4 w-4" /> {t('structuredLearningCourse.backToMap')}
      </Link>

      <div className="mt-4 text-center">
        <p className="text-xs font-medium uppercase tracking-wide text-brand-400">{currentSection?.ChapterTitle}</p>
        <h1 className="mt-1 text-2xl font-bold text-white">{currentSection?.SubchapterTitle}</h1>
        <div className="mt-4 flex justify-center">
          <PreviewModeSwitcher mode={mode} onChange={setMode} />
        </div>
        {mode === 'demo' && <p className="mt-2 text-xs text-amber-300">{t('structuredLearningMap.demoModeNotice')}</p>}
      </div>

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
  const meta = STEP_META[step.StepType] ?? { labelKey: step.StepType, Icon: BookOpenText };
  const isEditing = mode === 'edit';
  const isEditableStepType = step.StepType === 'Hypothesis' || step.StepType === 'Teaching' || step.StepType === 'Production';

  return (
    <div className="space-y-4">
      {(step.StepType === 'Hypothesis' || step.StepType === 'Teaching') && (
        <VoiceTutorAgent
          structuredLearningSectionStepId={step.StructuredLearningSectionStepId}
          startLabel={t('structuredLearningCourse.talkToTutor')}
        />
      )}

      <div className="overflow-hidden rounded-2xl border border-white/10 bg-white/[0.03]">
        {(step.StepType === 'Hypothesis' || step.StepType === 'Teaching') && <StepIllustration stepDataJson={step.StepDataJson} />}
        <div className="p-4">
          <p className="text-xs font-semibold uppercase tracking-wide text-brand-400">{t(`structuredLearningCourse.${meta.labelKey}`)}</p>
          {step.Content && <p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-slate-200">{step.Content}</p>}

          {isEditing && isEditableStepType && (
            <>
              <StepEditBox sectionId={sectionId} step={step} onUpdated={onStepUpdated} />
              <ManualEditBox sectionId={sectionId} step={step} onUpdated={onStepUpdated} />
            </>
          )}

          {step.StepType === 'DeepDive' && step.StepDataJson && (
            isEditing ? (
              <DeepDiveCardsEditor sectionId={sectionId} step={step} onUpdated={onStepUpdated} />
            ) : (
              <DeepDiveQuiz sectionId={sectionId} stepId={step.StructuredLearningSectionStepId} stepDataJson={step.StepDataJson} onComplete={onComplete} />
            )
          )}
          {step.StepType === 'Recall' &&
            (isEditing ? (
              <p className="mt-3 text-sm text-slate-500">{t('structuredLearningCourse.editModeGeneratedLive')}</p>
            ) : (
              <RecallPractice sectionId={sectionId} onComplete={onComplete} />
            ))}
          {step.StepType === 'Production' && !isEditing && <ProductionDraft />}
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
          {card.IllustrationId ? (
            <img src={apiImageUrl(`/illustrations/structured-learning/${card.IllustrationId}/image`)} alt={card.Title} className="h-56 w-full rounded-t-xl bg-slate-950/50 object-contain object-center" />
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

  return <img src={apiImageUrl(`/illustrations/structured-learning/${illustrationId}/image`)} alt="" className="h-72 w-full rounded-xl bg-slate-950/50 object-contain object-center" />;
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
  onComplete,
}: {
  sectionId: string;
  stepId: string;
  stepDataJson: string;
  onComplete: () => void;
}) {
  const { t } = useTranslation();
  let cards: DeepDiveCard[] = [];
  try {
    cards = JSON.parse(stepDataJson) as DeepDiveCard[];
  } catch {
    cards = [];
  }

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
  }, [sectionId]);

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
        {card.IllustrationId ? (
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
        {card.IllustrationId ? (
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


/** Local-only scratch pad — no backend evaluation exists yet for
 * Structured Learning's Production step. */
function ProductionDraft() {
  const { t } = useTranslation();
  const [value, setValue] = useState('');
  return (
    <div className="mt-3">
      <label className="mb-1.5 block text-xs font-medium text-slate-400">{t('structuredLearningCourse.productionDraftLabel')}</label>
      <textarea
        value={value}
        onChange={(e) => setValue(e.target.value)}
        rows={3}
        placeholder={t('structuredLearningCourse.productionDraftPlaceholder')}
        className="input w-full"
      />
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
