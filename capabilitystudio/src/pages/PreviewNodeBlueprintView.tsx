import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import DOMPurify from 'dompurify';
import { Lightbulb, BookOpen, RotateCcw, Hammer, ClipboardCheck, Check, Sparkles, Images, Pencil, Plus, X, Trash2 } from 'lucide-react';
import { apiImageUrl } from '../lib/api/httpClient';
import {
  getNodeBlueprint,
  editNodeBlueprintStep,
  editDeepDiveCard,
  addDeepDiveCard,
  deleteDeepDiveCard,
  type ExperienceStepType,
  type NodeBlueprintDto,
  type BlueprintStepDto,
} from '../lib/api/runtimeApi';
import { getGeneratedByLabel } from '../lib/illustrationLabel';
import LoadingSpinner from '../components/LoadingSpinner';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import { withPreviewMode, type PreviewMode } from '../lib/previewMode';

const STEP_ORDER: ExperienceStepType[] = ['Hypothesis', 'Teaching', 'DeepDive', 'Recall', 'Production', 'Assessment'];

const STEP_ICONS: Record<ExperienceStepType, typeof Lightbulb> = {
  Hypothesis: Lightbulb,
  Teaching: BookOpen,
  DeepDive: Images,
  Recall: RotateCcw,
  Production: Hammer,
  Assessment: ClipboardCheck,
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
  const { t } = useTranslation();
  const { capabilityId, nodeId } = useParams<{ capabilityId: string; nodeId: string }>();

  const [phase, setPhase] = useState<'loading' | 'ready' | 'error'>('loading');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [blueprint, setBlueprint] = useState<NodeBlueprintDto | null>(null);
  const [selectedStep, setSelectedStep] = useState<ExperienceStepType>('Hypothesis');

  const [instruction, setInstruction] = useState('');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [savedNotice, setSavedNotice] = useState(false);

  // Deep Dive card-by-card editing (2026-07-30) — separate state from the
  // generic step-instruction box above, scoped to ONE card index at a time.
  const [editingCardIndex, setEditingCardIndex] = useState<number | null>(null);
  const [cardInstruction, setCardInstruction] = useState('');
  const [savingCard, setSavingCard] = useState(false);
  const [cardSaveError, setCardSaveError] = useState<string | null>(null);

  // Deep Dive card deletion (2026-08-06) — a lightweight "click to confirm"
  // inline prompt (no LLM call, no full modal, mirrors the level-name-edit
  // Check/X confirm pattern used elsewhere in Studio) since removing one
  // card of several is a much lower-stakes action than deleting a whole
  // node/capability.
  const [deletingCardIndex, setDeletingCardIndex] = useState<number | null>(null);
  const [deletingCardBusy, setDeletingCardBusy] = useState(false);
  const [cardDeleteError, setCardDeleteError] = useState<string | null>(null);

  const [addingCard, setAddingCard] = useState(false);
  const [newCardInstruction, setNewCardInstruction] = useState('');
  const [savingNewCard, setSavingNewCard] = useState(false);
  const [newCardError, setNewCardError] = useState<string | null>(null);

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
          setErrorMessage(t('previewNodeBlueprint.loadErrorFallback'));
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

  const applyStepUpdate = (updated: BlueprintStepDto) => {
    setBlueprint((prev) => (prev ? { ...prev, Steps: prev.Steps.map((s) => (s.StepType === updated.StepType ? updated : s)) } : prev));
  };

  const handleApplyEdit = async () => {
    if (!nodeId || !instruction.trim() || saving) return;
    setSaving(true);
    setSaveError(null);
    setSavedNotice(false);
    try {
      const updated = await editNodeBlueprintStep(nodeId, selectedStep, instruction.trim());
      applyStepUpdate(updated);
      setInstruction('');
      setSavedNotice(true);
    } catch {
      setSaveError(t('previewNodeBlueprint.editErrorFallback'));
    } finally {
      setSaving(false);
    }
  };

  const handleApplyCardEdit = async () => {
    if (!nodeId || editingCardIndex === null || !cardInstruction.trim() || savingCard) return;
    setSavingCard(true);
    setCardSaveError(null);
    try {
      const updated = await editDeepDiveCard(nodeId, editingCardIndex, cardInstruction.trim());
      applyStepUpdate(updated);
      setEditingCardIndex(null);
      setCardInstruction('');
    } catch {
      setCardSaveError(t('previewNodeBlueprint.cardEditErrorFallback'));
    } finally {
      setSavingCard(false);
    }
  };

  const handleApplyAddCard = async () => {
    if (!nodeId || !newCardInstruction.trim() || savingNewCard) return;
    setSavingNewCard(true);
    setNewCardError(null);
    try {
      const updated = await addDeepDiveCard(nodeId, newCardInstruction.trim());
      applyStepUpdate(updated);
      setAddingCard(false);
      setNewCardInstruction('');
    } catch {
      setNewCardError(t('previewNodeBlueprint.cardAddErrorFallback'));
    } finally {
      setSavingNewCard(false);
    }
  };

  const handleConfirmDeleteCard = async (index: number) => {
    if (!nodeId || deletingCardBusy) return;
    setDeletingCardBusy(true);
    setCardDeleteError(null);
    try {
      const updated = await deleteDeepDiveCard(nodeId, index);
      applyStepUpdate(updated);
      setDeletingCardIndex(null);
    } catch {
      setCardDeleteError(t('previewNodeBlueprint.cardDeleteErrorFallback'));
    } finally {
      setDeletingCardBusy(false);
    }
  };

  if (phase === 'loading') {
    return <LoadingSpinner label={t('previewNodeBlueprint.loading')} />;
  }

  if (phase === 'error' || !blueprint) {
    return (
      <div className="mx-auto max-w-2xl px-6 py-16 text-center">
        <p className="text-red-300">{errorMessage ?? t('previewNodeBlueprint.nodeLoadErrorFallback')}</p>
        <Link to={backLink} className="mt-4 inline-block text-sm text-brand-400 hover:text-brand-300">
          {t('previewNodeBlueprint.backToMap')}
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <Link to={backLink} className="text-sm text-slate-400 hover:text-white">
        {t('previewNodeBlueprint.backToMap')}
      </Link>

      <div className="mt-4 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
        <div className="flex flex-wrap items-center gap-2">
          <span className="rounded-full bg-brand-500 px-3 py-1 text-xs font-semibold text-[#fff]">
            {mode === 'demo' ? t('previewNodeBlueprint.demoModeLabel') : t('previewNodeBlueprint.editModeLabel')}
          </span>
          <span className="text-xs text-slate-400">
            {mode === 'demo'
              ? t('previewNodeBlueprint.demoModeDescription')
              : t('previewNodeBlueprint.editModeDescription')}
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
                onClick={() => {
                  if (!hasContent) return;
                  setSelectedStep(stepType);
                  setEditingCardIndex(null);
                  setAddingCard(false);
                }}
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
                <span className={`text-[11px] font-medium ${isActive ? 'text-white' : 'text-slate-500'}`}>{t(`previewNode.stepLabels.${stepType}`)}</span>
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
                {t('previewNodeBlueprint.assessmentCriteriaNotice')}
              </p>
            )}
            <div
              className="prose prose-invert prose-sm max-w-none whitespace-pre-wrap text-slate-200"
              dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(currentStep.Content, { ALLOWED_TAGS: ['p', 'br', 'strong', 'em', 'ul', 'ol', 'li', 'a'] }) }}
            />

            {selectedStep === 'DeepDive' && currentStep.ExampleCards && currentStep.ExampleCards.length > 0 && (
              <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {currentStep.ExampleCards.map((card, index) => (
                  <div key={index} className="relative rounded-xl border border-white/10 bg-white/[0.02] p-4">
                    {mode === 'edit' && editingCardIndex !== index && deletingCardIndex !== index && (
                      <div className="absolute right-2 top-2 flex items-center gap-1.5">
                        <button
                          onClick={() => {
                            setEditingCardIndex(index);
                            setCardInstruction('');
                            setCardSaveError(null);
                          }}
                          title={t('previewNodeBlueprint.editThisCard')}
                          className="rounded-full border border-white/10 bg-white/[0.05] p-1.5 text-slate-300 hover:border-brand-400/50 hover:text-brand-300"
                        >
                          <Pencil className="h-3.5 w-3.5" />
                        </button>
                        <button
                          onClick={() => {
                            setDeletingCardIndex(index);
                            setCardDeleteError(null);
                          }}
                          title={t('previewNodeBlueprint.deleteThisCard')}
                          className="rounded-full border border-white/10 bg-white/[0.05] p-1.5 text-slate-300 hover:border-red-500 hover:text-red-400"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    )}
                    {card.IllustrationId ? (
                      <img
                        src={apiImageUrl(`/illustrations/${card.IllustrationId}/image`)}
                        alt={card.Title}
                        className="mb-2 h-32 w-full rounded-lg object-cover"
                      />
                    ) : (
                      card.Emoji && <div className="mb-2 text-3xl leading-none">{card.Emoji}</div>
                    )}
                    <p className="text-sm font-semibold text-white">{card.Title}</p>
                    <div
                      className="prose prose-invert prose-sm mt-1 max-w-none text-slate-300"
                      dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(card.Text, { ALLOWED_TAGS: ['p', 'br', 'strong', 'em', 'ul', 'ol', 'li', 'a'] }) }}
                    />
                    {nodeId && (
                      <VoiceTutorAgent
                        capabilityGraphNodeId={nodeId}
                        stepType="DeepDive"
                        exampleCardIndex={index}
                        autoConnect={false}
                        compact
                      />
                    )}

                    {editingCardIndex === index && (
                      <div className="mt-3 rounded-lg border border-brand-400/20 bg-brand-500/[0.05] p-3">
                        <textarea
                          value={cardInstruction}
                          onChange={(e) => setCardInstruction(e.target.value)}
                          rows={2}
                          placeholder={t('previewNodeBlueprint.cardEditPlaceholder')}
                          className="w-full rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1.5 text-xs text-white placeholder:text-slate-500 focus:border-brand-400 focus:outline-none"
                        />
                        {cardSaveError && <p className="mt-1.5 text-xs text-red-300">{cardSaveError}</p>}
                        <div className="mt-2 flex items-center gap-2">
                          <button
                            onClick={handleApplyCardEdit}
                            disabled={savingCard || !cardInstruction.trim()}
                            className="rounded-lg bg-gradient-to-r from-brand-500 to-accent-500 px-3 py-1.5 text-xs font-semibold text-[#fff] disabled:opacity-50"
                          >
                            {savingCard ? t('previewNodeBlueprint.applying') : t('previewNodeBlueprint.applyChange')}
                          </button>
                          <button
                            onClick={() => {
                              setEditingCardIndex(null);
                              setCardSaveError(null);
                            }}
                            disabled={savingCard}
                            className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:text-white"
                          >
                            <X className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      </div>
                    )}

                    {deletingCardIndex === index && (
                      <div className="mt-3 rounded-lg border border-red-500/30 bg-red-500/[0.06] p-3">
                        <p className="text-xs text-red-200">{t('previewNodeBlueprint.confirmDeleteCard')}</p>
                        {cardDeleteError && <p className="mt-1.5 text-xs text-red-300">{cardDeleteError}</p>}
                        <div className="mt-2 flex items-center gap-2">
                          <button
                            onClick={() => void handleConfirmDeleteCard(index)}
                            disabled={deletingCardBusy}
                            className="rounded-lg bg-red-500 px-3 py-1.5 text-xs font-semibold text-[#fff] disabled:opacity-50"
                          >
                            {deletingCardBusy ? t('previewNodeBlueprint.deleting') : t('previewNodeBlueprint.confirmDeleteCardButton')}
                          </button>
                          <button
                            onClick={() => {
                              setDeletingCardIndex(null);
                              setCardDeleteError(null);
                            }}
                            disabled={deletingCardBusy}
                            className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:text-white"
                          >
                            <X className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}

            {selectedStep === 'DeepDive' && mode === 'edit' && currentStep.ExampleCards && (
              <div className="mt-4">
                {!addingCard ? (
                  <button
                    onClick={() => {
                      setAddingCard(true);
                      setNewCardInstruction('');
                      setNewCardError(null);
                    }}
                    className="flex items-center gap-1.5 rounded-lg border border-dashed border-white/15 px-3 py-2 text-xs font-medium text-slate-300 hover:border-brand-400/50 hover:text-brand-300"
                  >
                    <Plus className="h-3.5 w-3.5" /> {t('previewNodeBlueprint.addNewCard')}
                  </button>
                ) : (
                  <div className="rounded-xl border border-brand-400/20 bg-brand-500/[0.05] p-4">
                    <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-brand-300">
                      <Plus className="h-3.5 w-3.5" /> {t('previewNodeBlueprint.addNewCard')}
                    </p>
                    <textarea
                      value={newCardInstruction}
                      onChange={(e) => setNewCardInstruction(e.target.value)}
                      rows={2}
                      placeholder={t('previewNodeBlueprint.addCardPlaceholder')}
                      className="mt-2 w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white placeholder:text-slate-500 focus:border-brand-400 focus:outline-none"
                    />
                    {newCardError && <p className="mt-2 text-sm text-red-300">{newCardError}</p>}
                    <div className="mt-3 flex items-center gap-2">
                      <button
                        onClick={handleApplyAddCard}
                        disabled={savingNewCard || !newCardInstruction.trim()}
                        className="rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 disabled:opacity-50"
                      >
                        {savingNewCard ? t('previewNodeBlueprint.applying') : t('previewNodeBlueprint.addCardButton')}
                      </button>
                      <button
                        onClick={() => setAddingCard(false)}
                        disabled={savingNewCard}
                        className="rounded-xl border border-white/10 px-4 py-2.5 text-sm text-slate-300 hover:text-white"
                      >
                        {t('common.cancel')}
                      </button>
                    </div>
                  </div>
                )}
              </div>
            )}


            {currentStep.Illustrations.map((illustration) => (
              <div key={illustration.IllustrationId} className="mt-4 overflow-hidden rounded-xl border border-white/10 bg-white/[0.02]">
                <img
                  src={apiImageUrl(`/illustrations/${illustration.IllustrationId}/image`)}
                  alt={illustration.Caption ?? ''}
                  className="h-auto w-full max-w-sm object-contain"
                />
                {illustration.ImageModel && (
                  <p className="px-3 py-1.5 text-[10px] italic text-slate-500">{getGeneratedByLabel(illustration.ImageModel, t)}</p>
                )}
              </div>
            ))}

            {mode === 'edit' && (
              <div className="mt-6 rounded-xl border border-brand-400/20 bg-brand-500/[0.05] p-4">
                <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-brand-300">
                  <Sparkles className="h-3.5 w-3.5" /> {t('previewNodeBlueprint.editStepWithAi')}
                </p>
                <textarea
                  value={instruction}
                  onChange={(e) => setInstruction(e.target.value)}
                  rows={3}
                  placeholder={t('previewNodeBlueprint.editPlaceholder')}
                  className="mt-2 w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-white placeholder:text-slate-500 focus:border-brand-400 focus:outline-none"
                />
                {saveError && <p className="mt-2 text-sm text-red-300">{saveError}</p>}
                {savedNotice && !saveError && (
                  <p className="mt-2 flex items-center gap-1 text-sm text-emerald-300">
                    <Check className="h-4 w-4" /> {t('previewNodeBlueprint.changesApplied')}
                  </p>
                )}
                <button
                  onClick={handleApplyEdit}
                  disabled={saving || !instruction.trim()}
                  className="mt-3 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 disabled:opacity-50"
                >
                  {saving ? t('previewNodeBlueprint.applying') : t('previewNodeBlueprint.applyChange')}
                </button>
              </div>
            )}
          </>
        ) : (
          <p className="text-sm text-slate-400">{t('previewNodeBlueprint.noBlueprintYet')}</p>
        )}
      </div>
    </div>
  );
}
