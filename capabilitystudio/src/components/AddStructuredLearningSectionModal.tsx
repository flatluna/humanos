import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { X, Sparkles, Loader2, ChevronRight, CheckCircle2, Circle, UploadCloud, ImageOff } from 'lucide-react';
import {
  addStructuredLearningSection,
  type StructuredLearningSectionDetail,
} from '../lib/api/structuredLearningApi';
import { readFileAsBase64 } from '../lib/api/pdfPipelineApi';

/** How long (ms) each simulated progress step stays on screen before
 * advancing — mirrors AddGraphNodeModal.tsx's own STEP_DURATION_MS
 * (a single HTTP call gives no true incremental backend progress). */
const STEP_DURATION_MS = 3200;

/** The four ways this modal can produce the new section's illustrations —
 * mirrors AddGraphNodeModal.tsx's IllustrationMode exactly (same backend
 * request field/values, see StructuredLearningPedagogyService
 * .GenerateIllustrationsAsync). */
type IllustrationMode = 'None' | 'AiGenerated' | 'UploadDirect' | 'AiGeneratedFromUpload';

/**
 * "Edit mode" add-section modal (2026-08-11, simplified 2026-11-11) —
 * Structured Learning's counterpart to AddGraphNodeModal, adapted to this
 * pipeline's own LINEAR chapter sequencing: a single "insert after"
 * selector (not a full prerequisite checklist) picks WHERE the new
 * section lands among the chapter's existing sections, and the same
 * 4-way illustration mode choice (AI automatic / upload directly / AI
 * using upload as reference / none) is offered. Generation (section text
 * + all 6 pedagogical steps, plus illustrations) happens synchronously in
 * ONE backend call.
 */
export default function AddStructuredLearningSectionModal({
  documentId,
  chapterTitle,
  chapterSections,
  languageCode,
  onClose,
  onCreated,
}: {
  documentId: string;
  chapterTitle: string;
  /** Every existing section already in this chapter, in document order —
   * used to build the "insert after" position selector. */
  chapterSections: StructuredLearningSectionDetail[];
  languageCode: 'es' | 'en';
  onClose: () => void;
  onCreated: (section: StructuredLearningSectionDetail) => void;
}) {
  const { t } = useTranslation();
  const [instruction, setInstruction] = useState('');
  // Single "insert after" choice replaces the old Requires/Is-required-by
  // checklists (2026-11-11) — null means "at the very beginning of the
  // chapter", otherwise the id of the existing section the new one goes
  // right after. Defaults to the last existing section, i.e. "at the end",
  // same default behavior as before this simplification.
  const [insertAfterId, setInsertAfterId] = useState<string | null>(
    chapterSections.length > 0 ? chapterSections[chapterSections.length - 1].StructuredLearningDocumentSectionId : null
  );
  const [illustrationMode, setIllustrationMode] = useState<IllustrationMode>('AiGenerated');
  const [uploadedImageFile, setUploadedImageFile] = useState<File | null>(null);
  const [uploadedImagePreviewUrl, setUploadedImagePreviewUrl] = useState<string | null>(null);
  const [referenceImageUsageInstruction, setReferenceImageUsageInstruction] = useState('');
  // null = inherit the document's own Step 1 web-search choice (default);
  // true/false explicitly overrides it just for this new section.
  const [usePerplexitySearch, setUsePerplexitySearch] = useState<boolean | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [stepIndex, setStepIndex] = useState(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const needsUploadedImage = illustrationMode === 'UploadDirect' || illustrationMode === 'AiGeneratedFromUpload';
  const needsReferenceImageUsageInstruction = illustrationMode === 'AiGeneratedFromUpload';

  function handleFileSelected(file: File | null) {
    setUploadedImageFile(file);
    setUploadedImagePreviewUrl((prev) => {
      if (prev) URL.revokeObjectURL(prev);
      return file ? URL.createObjectURL(file) : null;
    });
  }

  const steps = useMemo(() => {
    const base = [
      t('addStructuredLearningSectionModal.stepGenerating'),
      t('addStructuredLearningSectionModal.stepVerifying'),
    ];
    if (illustrationMode === 'UploadDirect') base.push(t('addStructuredLearningSectionModal.stepUploadingImage'));
    else if (illustrationMode === 'AiGeneratedFromUpload') base.push(t('addStructuredLearningSectionModal.stepGeneratingFromReference'));
    else if (illustrationMode === 'AiGenerated') base.push(t('addStructuredLearningSectionModal.stepIllustrations'));
    base.push(t('addStructuredLearningSectionModal.stepSaving'));
    return base;
  }, [illustrationMode, t]);

  useEffect(() => {
    if (!submitting) {
      setStepIndex(0);
      if (timerRef.current) clearInterval(timerRef.current);
      return;
    }
    timerRef.current = setInterval(() => {
      setStepIndex((i) => Math.min(i + 1, steps.length - 1));
    }, STEP_DURATION_MS);
    return () => {
      if (timerRef.current) clearInterval(timerRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [submitting]);

  useEffect(() => {
    return () => {
      if (uploadedImagePreviewUrl) URL.revokeObjectURL(uploadedImagePreviewUrl);
    };
  }, [uploadedImagePreviewUrl]);

  async function handleSubmit() {
    if (!instruction.trim() || submitting) return;
    if (needsUploadedImage && !uploadedImageFile) return;
    if (needsReferenceImageUsageInstruction && !referenceImageUsageInstruction.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      const uploadedImageBase64 = uploadedImageFile ? await readFileAsBase64(uploadedImageFile) : null;
      // insertAfterId === null means "at the very beginning": pin the new
      // section right before the chapter's current first section instead
      // (requiresSectionIds stays empty). Otherwise just requiring the
      // chosen section is enough — the backend bumps every later
      // section's SortOrder by 1, so it naturally lands right after it.
      const requiresSectionIds = insertAfterId ? [insertAfterId] : [];
      const requiredBySectionIds =
        !insertAfterId && chapterSections.length > 0 ? [chapterSections[0].StructuredLearningDocumentSectionId] : [];
      const section = await addStructuredLearningSection(
        documentId,
        chapterTitle,
        instruction.trim(),
        languageCode,
        requiresSectionIds,
        requiredBySectionIds,
        illustrationMode,
        uploadedImageBase64,
        uploadedImageFile?.type ?? null,
        needsReferenceImageUsageInstruction ? referenceImageUsageInstruction.trim() : null,
        usePerplexitySearch
      );
      onCreated(section);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setSubmitting(false);
    }
  }

  function handleClose() {
    if (submitting) return;
    onClose();
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-sm" onClick={handleClose}>
      <div
        className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl border border-white/10 bg-slate-950 p-6 shadow-2xl shadow-black/40"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-start justify-between gap-3">
          <div>
            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{chapterTitle}</p>
            <h2 className="mt-1 flex items-center gap-2 text-lg font-semibold text-white">
              <Sparkles className="h-5 w-5 text-brand-400" />
              {t('structuredLearningMap.addSectionTitle')}
            </h2>
          </div>
          <button
            type="button"
            onClick={handleClose}
            disabled={submitting}
            className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white disabled:opacity-40"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {submitting ? (
          <ProgressView steps={steps} currentIndex={stepIndex} />
        ) : (
          <>
            <label className="mb-1.5 block text-xs font-medium text-slate-400">{t('structuredLearningMap.addSectionPromptLabel')}</label>
            <textarea
              value={instruction}
              onChange={(e) => setInstruction(e.target.value)}
              rows={4}
              placeholder={t('structuredLearningMap.addSectionPromptPlaceholder')}
              className="input w-full"
            />

            <div className="mt-5">
              <p className="mb-1.5 text-xs font-medium uppercase tracking-wide text-slate-400">
                {t('addStructuredLearningSectionModal.insertPositionLabel')}
              </p>
              <select
                value={insertAfterId ?? ''}
                onChange={(e) => setInsertAfterId(e.target.value || null)}
                className="input w-full"
              >
                <option value="">{t('addStructuredLearningSectionModal.insertAtBeginning')}</option>
                {chapterSections.map((section) => (
                  <option key={section.StructuredLearningDocumentSectionId} value={section.StructuredLearningDocumentSectionId}>
                    {t('addStructuredLearningSectionModal.insertAfter', { title: section.SubchapterTitle })}
                  </option>
                ))}
              </select>
            </div>

            <div className="mt-5">
              <p className="mb-2 text-xs font-medium uppercase tracking-wide text-slate-400">
                {t('addStructuredLearningSectionModal.webSearchLabel')}
              </p>
              <div className="space-y-2">
                <WebSearchOption
                  value={null}
                  current={usePerplexitySearch}
                  onSelect={setUsePerplexitySearch}
                  label={t('addStructuredLearningSectionModal.webSearchInherit')}
                  hint={t('addStructuredLearningSectionModal.webSearchInheritHint')}
                />
                <WebSearchOption
                  value={true}
                  current={usePerplexitySearch}
                  onSelect={setUsePerplexitySearch}
                  label={t('addStructuredLearningSectionModal.webSearchOn')}
                  hint={t('addStructuredLearningSectionModal.webSearchOnHint')}
                />
                <WebSearchOption
                  value={false}
                  current={usePerplexitySearch}
                  onSelect={setUsePerplexitySearch}
                  label={t('addStructuredLearningSectionModal.webSearchOff')}
                />
              </div>
            </div>

            <div className="mt-5">
              <p className="mb-2 text-xs font-medium uppercase tracking-wide text-slate-400">
                {t('addStructuredLearningSectionModal.illustrationModeLabel')}
              </p>
              <div className="space-y-2">
                <IllustrationModeOption
                  value="AiGenerated"
                  current={illustrationMode}
                  onSelect={setIllustrationMode}
                  label={t('addStructuredLearningSectionModal.illustrationModeAi')}
                  hint={t('addStructuredLearningSectionModal.illustrationModeAiHint')}
                />
                <IllustrationModeOption
                  value="UploadDirect"
                  current={illustrationMode}
                  onSelect={setIllustrationMode}
                  label={t('addStructuredLearningSectionModal.illustrationModeUpload')}
                  hint={t('addStructuredLearningSectionModal.illustrationModeUploadHint')}
                />
                <IllustrationModeOption
                  value="AiGeneratedFromUpload"
                  current={illustrationMode}
                  onSelect={setIllustrationMode}
                  label={t('addStructuredLearningSectionModal.illustrationModeUploadAi')}
                  hint={t('addStructuredLearningSectionModal.illustrationModeUploadAiHint')}
                />
                <IllustrationModeOption
                  value="None"
                  current={illustrationMode}
                  onSelect={setIllustrationMode}
                  label={t('addStructuredLearningSectionModal.illustrationModeNone')}
                />
              </div>

              {needsUploadedImage && (
                <div className="mt-3 rounded-xl border border-white/10 bg-white/[0.02] p-3">
                  {uploadedImagePreviewUrl ? (
                    <div className="flex items-center gap-3">
                      <img src={uploadedImagePreviewUrl} alt="" className="h-16 w-16 shrink-0 rounded-lg object-contain object-center" />
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm text-slate-300">{uploadedImageFile?.name}</p>
                        <button
                          type="button"
                          onClick={() => fileInputRef.current?.click()}
                          className="mt-1 text-xs font-medium text-brand-400 hover:text-brand-300"
                        >
                          {t('addStructuredLearningSectionModal.uploadImageChange')}
                        </button>
                      </div>
                      <button
                        type="button"
                        onClick={() => handleFileSelected(null)}
                        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-white/10 hover:text-white"
                      >
                        <X className="h-4 w-4" />
                      </button>
                    </div>
                  ) : (
                    <button
                      type="button"
                      onClick={() => fileInputRef.current?.click()}
                      className="flex w-full items-center justify-center gap-2 rounded-lg border border-dashed border-white/15 py-3 text-sm text-slate-400 hover:border-white/25 hover:text-slate-300"
                    >
                      <UploadCloud className="h-4 w-4" />
                      {t('addStructuredLearningSectionModal.uploadImageButton')}
                    </button>
                  )}
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    className="hidden"
                    onChange={(e) => handleFileSelected(e.target.files?.[0] ?? null)}
                  />
                  {!uploadedImageFile && (
                    <p className="mt-2 flex items-center gap-1 text-xs text-amber-300">
                      <ImageOff className="h-3.5 w-3.5" /> {t('addStructuredLearningSectionModal.uploadImageRequired')}
                    </p>
                  )}
                </div>
              )}

              {needsReferenceImageUsageInstruction && (
                <div className="mt-3">
                  <label className="mb-1.5 block text-xs font-medium text-slate-400">
                    {t('addStructuredLearningSectionModal.referenceImageUsageLabel')}
                  </label>
                  <textarea
                    value={referenceImageUsageInstruction}
                    onChange={(e) => setReferenceImageUsageInstruction(e.target.value)}
                    rows={2}
                    placeholder={t('addStructuredLearningSectionModal.referenceImageUsagePlaceholder')}
                    className="input w-full"
                  />
                  {!referenceImageUsageInstruction.trim() && (
                    <p className="mt-2 flex items-center gap-1 text-xs text-amber-300">
                      <ImageOff className="h-3.5 w-3.5" /> {t('addStructuredLearningSectionModal.referenceImageUsageRequired')}
                    </p>
                  )}
                </div>
              )}
            </div>

            {error && <p className="mt-4 rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-300">{error}</p>}

            <div className="mt-6 flex items-center justify-end gap-3">
              <button
                type="button"
                onClick={handleClose}
                className="rounded-xl border border-white/10 bg-white/[0.03] px-4 py-2 text-sm font-medium text-slate-200 hover:bg-white/[0.06]"
              >
                {t('structuredLearningMap.addSectionCancel')}
              </button>
              <button
                type="button"
                onClick={handleSubmit}
                disabled={
                  !instruction.trim() ||
                  (needsUploadedImage && !uploadedImageFile) ||
                  (needsReferenceImageUsageInstruction && !referenceImageUsageInstruction.trim())
                }
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 disabled:cursor-not-allowed disabled:opacity-40"
              >
                {t('structuredLearningMap.addSectionSubmit')} <ChevronRight className="h-4 w-4" />
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function WebSearchOption({
  value,
  current,
  onSelect,
  label,
  hint,
}: {
  value: boolean | null;
  current: boolean | null;
  onSelect: (value: boolean | null) => void;
  label: string;
  hint?: string;
}) {
  const selected = value === current;
  return (
    <label
      className={`flex cursor-pointer items-start gap-2.5 rounded-xl border px-3 py-2.5 transition-colors ${
        selected ? 'border-brand-500/50 bg-brand-500/10' : 'border-white/10 bg-white/[0.02] hover:bg-white/[0.05]'
      }`}
    >
      <input
        type="radio"
        checked={selected}
        onChange={() => onSelect(value)}
        className="mt-0.5 h-3.5 w-3.5 border-white/20 bg-white/[0.03] text-brand-500"
      />
      <div className="min-w-0">
        <p className="text-sm text-slate-200">{label}</p>
        {hint && <p className="mt-0.5 text-xs text-slate-500">{hint}</p>}
      </div>
    </label>
  );
}

function IllustrationModeOption({
  value,
  current,
  onSelect,
  label,
  hint,
}: {
  value: IllustrationMode;
  current: IllustrationMode;
  onSelect: (mode: IllustrationMode) => void;
  label: string;
  hint?: string;
}) {
  const selected = value === current;
  return (
    <label
      className={`flex cursor-pointer items-start gap-2.5 rounded-xl border px-3 py-2.5 transition-colors ${
        selected ? 'border-brand-500/50 bg-brand-500/10' : 'border-white/10 bg-white/[0.02] hover:bg-white/[0.05]'
      }`}
    >
      <input
        type="radio"
        checked={selected}
        onChange={() => onSelect(value)}
        className="mt-0.5 h-3.5 w-3.5 border-white/20 bg-white/[0.03] text-brand-500"
      />
      <div className="min-w-0">
        <p className="text-sm text-slate-200">{label}</p>
        {hint && <p className="mt-0.5 text-xs text-slate-500">{hint}</p>}
      </div>
    </label>
  );
}

function ProgressView({ steps, currentIndex }: { steps: string[]; currentIndex: number }) {
  const { t } = useTranslation();
  return (
    <div className="mt-6">
      <div className="space-y-3">
        {steps.map((label, i) => {
          const isDone = i < currentIndex;
          const isCurrent = i === currentIndex;
          return (
            <div key={label} className="flex items-center gap-3">
              {isDone ? (
                <CheckCircle2 className="h-5 w-5 flex-none text-emerald-400" />
              ) : isCurrent ? (
                <Loader2 className="h-5 w-5 flex-none animate-spin text-brand-400" />
              ) : (
                <Circle className="h-5 w-5 flex-none text-slate-700" />
              )}
              <span className={`text-sm ${isDone ? 'text-slate-400 line-through' : isCurrent ? 'text-white' : 'text-slate-600'}`}>{label}</span>
            </div>
          );
        })}
      </div>
      <p className="mt-6 rounded-lg border border-amber-500/20 bg-amber-500/10 px-3 py-2 text-xs text-amber-300">
        {t('addStructuredLearningSectionModal.progressWarning')}
      </p>
    </div>
  );
}

