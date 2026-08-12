import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { X, Sparkles, Loader2, ChevronRight, CheckCircle2, Circle, UploadCloud, ImageOff } from 'lucide-react';
import {
  addCapabilityGraphNode,
  type BackendCapabilityGraphNode,
} from '../lib/api/runtimeApi';
import { readFileAsBase64 } from '../lib/api/pdfPipelineApi';

/** How long (ms) each simulated progress step stays on screen before
 * advancing. The last step stays put (spinning) until the real API response
 * arrives — a single HTTP call gives no true incremental backend progress. */
const STEP_DURATION_MS = 3200;

/** The three ways this modal can produce a node's illustration(s)
 * (2026-08-06 "imagen del profesor" feature) — mirrors the backend's
 * AddCapabilityGraphNodeRequest.IllustrationMode string values exactly. */
type IllustrationMode = 'None' | 'AiGenerated' | 'UploadDirect' | 'AiGeneratedFromUpload';

interface AddGraphNodeModalProps {
  capabilityId: string;
  /** Nodes grouped by computed level (same groupIntoLevels output as
   * PreviewGraphPage), used to build the "requiere" / "es requerido por"
   * checklists with sensible pre-selection. */
  levels: BackendCapabilityGraphNode[][];
  /** Level index (0-based) the professor clicked "+ Agregar nodo" on —
   * pre-checks every node in the level below as a prerequisite, so the new
   * node lands at this level by default. */
  targetLevelIndex: number;
  onClose: () => void;
  onCreated: () => void;
}

/**
 * Professional "add node" authoring modal for Capability Studio (professors/
 * universities). A capability's "Nivel" is never a stored column — it is the
 * node's depth in the Requires-edge DAG — so this UI lets the author pick
 * EXACTLY which existing nodes the new node requires (prerequisites, from
 * the level below) and, optionally, which existing nodes should now require
 * it (dependents, from the level above), which is what actually places the
 * new node at the intended level once saved.
 */
export default function AddGraphNodeModal({
  capabilityId,
  levels,
  targetLevelIndex,
  onClose,
  onCreated,
}: AddGraphNodeModalProps) {
  const { t } = useTranslation();
  const belowLevel = targetLevelIndex > 0 ? levels[targetLevelIndex - 1] : [];
  const aboveLevel = targetLevelIndex < levels.length - 1 ? levels[targetLevelIndex + 1] : levels[targetLevelIndex] ?? [];

  const [prompt, setPrompt] = useState('');
  const [requiresIds, setRequiresIds] = useState<Set<string>>(new Set(belowLevel.map((n) => n.CapabilityGraphNodeId)));
  const [requiredByIds, setRequiredByIds] = useState<Set<string>>(new Set());
  const [illustrationMode, setIllustrationMode] = useState<IllustrationMode>('AiGenerated');
  const [uploadedImageFile, setUploadedImageFile] = useState<File | null>(null);
  const [uploadedImagePreviewUrl, setUploadedImagePreviewUrl] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [stepIndex, setStepIndex] = useState(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const needsUploadedImage = illustrationMode === 'UploadDirect' || illustrationMode === 'AiGeneratedFromUpload';

  // Which ChapterIndex to persist the new node under (2026-08-06 fix — nodes
  // used to always land at the very end of chapter-graded graphs because
  // ChapterIndex was never set on them). Read it off an existing sibling
  // node already sitting in the target level; for a brand-new level ("+
  // Agregar nuevo nivel superior") there's no sibling yet, so assign the
  // next index after the highest one seen anywhere in the graph. Left null
  // (unchanged behavior) for graphs that don't use ChapterIndex at all yet
  // (older edge-depth-tiered graphs), where level placement is still purely
  // derived from the Requires/RequiredBy wiring below.
  const targetChapterIndex = useMemo(() => {
    const siblingWithChapter = (levels[targetLevelIndex] ?? []).find(
      (n) => n.ChapterIndex !== null && n.ChapterIndex !== undefined
    );
    if (siblingWithChapter) return siblingWithChapter.ChapterIndex ?? null;

    const allChapterIndexes = levels
      .flat()
      .map((n) => n.ChapterIndex)
      .filter((c): c is number => c !== null && c !== undefined);
    if (allChapterIndexes.length === 0) return null;
    return Math.max(...allChapterIndexes) + 1;
  }, [levels, targetLevelIndex]);

  function handleFileSelected(file: File | null) {
    setUploadedImageFile(file);
    setUploadedImagePreviewUrl((prev) => {
      if (prev) URL.revokeObjectURL(prev);
      return file ? URL.createObjectURL(file) : null;
    });
  }

  const steps = useMemo(() => {
    const base = [
      t('addGraphNodeModal.stepAnalyzing'),
      t('addGraphNodeModal.stepGenerating'),
      t('addGraphNodeModal.stepVerifying'),
    ];
    if (illustrationMode === 'UploadDirect') base.push(t('addGraphNodeModal.stepUploadingImage'));
    else if (illustrationMode === 'AiGeneratedFromUpload') base.push(t('addGraphNodeModal.stepGeneratingFromReference'));
    else if (illustrationMode === 'AiGenerated') base.push(t('addGraphNodeModal.stepIllustrations'));
    base.push(t('addGraphNodeModal.stepSaving'));
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

  // Revoke the object URL used for the upload preview on unmount/replacement,
  // so switching/removing images doesn't leak blob URLs.
  useEffect(() => {
    return () => {
      if (uploadedImagePreviewUrl) URL.revokeObjectURL(uploadedImagePreviewUrl);
    };
  }, [uploadedImagePreviewUrl]);

  function toggle(set: Set<string>, setSet: (s: Set<string>) => void, id: string) {
    const next = new Set(set);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setSet(next);
  }

  async function handleSubmit() {
    if (!prompt.trim() || submitting) return;
    if (needsUploadedImage && !uploadedImageFile) return;
    setSubmitting(true);
    setError(null);
    try {
      const uploadedImageBase64 = uploadedImageFile ? await readFileAsBase64(uploadedImageFile) : null;
      await addCapabilityGraphNode(capabilityId, {
        Prompt: prompt.trim(),
        RequiresNodeIds: Array.from(requiresIds),
        RequiredByNodeIds: Array.from(requiredByIds),
        IllustrationMode: illustrationMode,
        UploadedImageBase64: uploadedImageBase64,
        UploadedImageContentType: uploadedImageFile?.type ?? null,
        TargetChapterIndex: targetChapterIndex,
      });
      onCreated();
    } catch (err) {
      setError(err instanceof Error ? err.message : t('addGraphNodeModal.createErrorFallback'));
      setSubmitting(false);
    }
  }

  function handleClose() {
    if (submitting) return;
    onClose();
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 px-4 py-8" onClick={handleClose}>
      <div
        className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl border border-white/10 bg-slate-900 p-6 shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between">
          <div>
            <h2 className="flex items-center gap-2 text-lg font-semibold text-white">
              <Sparkles className="h-5 w-5 text-brand-400" />
              {t('addGraphNodeModal.title', { level: targetLevelIndex + 1 })}
            </h2>
            <p className="mt-1 text-sm text-slate-400">
              {t('addGraphNodeModal.description')}
            </p>
          </div>
          <button
            type="button"
            onClick={handleClose}
            disabled={submitting}
            className="rounded-lg p-1.5 text-slate-500 hover:bg-white/[0.06] hover:text-white disabled:cursor-not-allowed disabled:opacity-30"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {submitting ? (
          <ProgressView steps={steps} currentIndex={stepIndex} />
        ) : (
          <>
            <div className="mt-5">
              <label className="mb-1.5 block text-xs font-medium uppercase tracking-wide text-slate-400">
                {t('addGraphNodeModal.whatShouldTeach')}
              </label>
              <textarea
                value={prompt}
                onChange={(e) => setPrompt(e.target.value)}
                rows={4}
                placeholder={t('addGraphNodeModal.placeholder')}
                className="input w-full resize-none"
              />
            </div>

            <div className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-2">
              <NodeChecklist
                title={t('addGraphNodeModal.requiresTitle')}
                hint={t('addGraphNodeModal.requiresHint')}
                nodes={belowLevel.length > 0 ? belowLevel : levels[targetLevelIndex] ?? []}
                selected={requiresIds}
                onToggle={(id) => toggle(requiresIds, setRequiresIds, id)}
              />
              <NodeChecklist
                title={t('addGraphNodeModal.requiredByTitle')}
                hint={t('addGraphNodeModal.requiredByHint')}
                nodes={aboveLevel}
                selected={requiredByIds}
                onToggle={(id) => toggle(requiredByIds, setRequiredByIds, id)}
              />
            </div>

            <div className="mt-5">
              <p className="mb-2 text-xs font-medium uppercase tracking-wide text-slate-400">
                {t('addGraphNodeModal.illustrationModeLabel')}
              </p>
              <div className="space-y-2">
                <IllustrationModeOption
                  value="AiGenerated"
                  current={illustrationMode}
                  onSelect={setIllustrationMode}
                  label={t('addGraphNodeModal.illustrationModeAi')}
                  hint={t('addGraphNodeModal.illustrationModeAiHint')}
                />
                <IllustrationModeOption
                  value="UploadDirect"
                  current={illustrationMode}
                  onSelect={setIllustrationMode}
                  label={t('addGraphNodeModal.illustrationModeUpload')}
                  hint={t('addGraphNodeModal.illustrationModeUploadHint')}
                />
                <IllustrationModeOption
                  value="AiGeneratedFromUpload"
                  current={illustrationMode}
                  onSelect={setIllustrationMode}
                  label={t('addGraphNodeModal.illustrationModeUploadAi')}
                  hint={t('addGraphNodeModal.illustrationModeUploadAiHint')}
                />
                <IllustrationModeOption
                  value="None"
                  current={illustrationMode}
                  onSelect={setIllustrationMode}
                  label={t('addGraphNodeModal.illustrationModeNone')}
                />
              </div>

              {needsUploadedImage && (
                <div className="mt-3 rounded-xl border border-white/10 bg-white/[0.02] p-3">
                  {uploadedImagePreviewUrl ? (
                    <div className="flex items-center gap-3">
                      <img
                        src={uploadedImagePreviewUrl}
                        alt=""
                        className="h-16 w-16 shrink-0 rounded-lg object-contain object-center"
                      />
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm text-slate-300">{uploadedImageFile?.name}</p>
                        <button
                          type="button"
                          onClick={() => fileInputRef.current?.click()}
                          className="mt-1 text-xs font-medium text-brand-400 hover:text-brand-300"
                        >
                          {t('addGraphNodeModal.uploadImageChange')}
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
                      {t('addGraphNodeModal.uploadImageButton')}
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
                      <ImageOff className="h-3.5 w-3.5" /> {t('addGraphNodeModal.uploadImageRequired')}
                    </p>
                  )}
                </div>
              )}
            </div>

            {error && (
              <p className="mt-4 rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-300">
                {error}
              </p>
            )}

            <div className="mt-6 flex items-center justify-end gap-3">
              <button
                type="button"
                onClick={handleClose}
                className="rounded-xl border border-white/10 px-4 py-2 text-sm font-medium text-slate-300 hover:bg-white/[0.06]"
              >
                {t('addGraphNodeModal.cancel')}
              </button>
              <button
                type="button"
                onClick={handleSubmit}
                disabled={!prompt.trim() || (needsUploadedImage && !uploadedImageFile)}
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 disabled:cursor-not-allowed disabled:opacity-40"
              >
                {t('addGraphNodeModal.createNode')} <ChevronRight className="h-4 w-4" />
              </button>
            </div>
          </>
        )}
      </div>
    </div>
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
              <span className={`text-sm ${isDone ? 'text-slate-400 line-through' : isCurrent ? 'text-white' : 'text-slate-600'}`}>
                {label}
              </span>
            </div>
          );
        })}
      </div>
      <p className="mt-6 rounded-lg border border-amber-500/20 bg-amber-500/10 px-3 py-2 text-xs text-amber-300">
        {t('addGraphNodeModal.progressWarning')}
      </p>
    </div>
  );
}

function NodeChecklist({
  title,
  hint,
  nodes,
  selected,
  onToggle,
}: {
  title: string;
  hint: string;
  nodes: BackendCapabilityGraphNode[];
  selected: Set<string>;
  onToggle: (id: string) => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="rounded-xl border border-white/10 bg-white/[0.02] p-3">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-300">{title}</p>
      <p className="mt-0.5 text-[11px] text-slate-500">{hint}</p>
      <div className="mt-2 max-h-40 space-y-1 overflow-y-auto">
        {nodes.length === 0 && <p className="py-2 text-xs text-slate-600">{t('addGraphNodeModal.noNodesAvailable')}</p>}
        {nodes.map((node) => (
          <label
            key={node.CapabilityGraphNodeId}
            className="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-sm text-slate-300 hover:bg-white/[0.05]"
          >
            <input
              type="checkbox"
              checked={selected.has(node.CapabilityGraphNodeId)}
              onChange={() => onToggle(node.CapabilityGraphNodeId)}
              className="h-3.5 w-3.5 rounded border-white/20 bg-white/[0.03] text-brand-500"
            />
            {node.Name}
          </label>
        ))}
      </div>
    </div>
  );
}
