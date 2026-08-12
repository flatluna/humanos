import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, Link } from 'react-router-dom';
import {
  Loader2,
  CheckCircle2,
  XCircle,
  ArrowRight,
  Layers,
  FileText,
  Hash,
  Sparkles,
  ChevronDown,
  ChevronRight,
  AlertTriangle,
  PlayCircle,
  UploadCloud,
} from 'lucide-react';
import {
  getStructuredLearningExtractionStatus,
  updateStructuredLearningPedagogyConfig,
  startStructuredLearningPedagogy,
  getStructuredLearningPedagogyStatus,
  getStructuredLearningDocument,
  type StructuredLearningExtractionRunStatus,
  type StructuredLearningPedagogyRunStatus,
  type StructuredLearningDocumentDetail,
  type StructuredLearningAssessmentQuestionType,
} from '../lib/api/structuredLearningApi';
import { apiImageUrl } from '../lib/api/httpClient';
import { readFileAsBase64 } from '../lib/api/pdfPipelineApi';

const POLL_INTERVAL_MS = 2500;

/** Polls a Structured Learning Document extraction run (Step 2), then —
 * once extraction is Completed — lets the designer configure and start
 * Step 3 (pedagogy generation) right on this same page, and finally shows
 * a simple read-only viewer of what got generated per section. */
export default function StructuredLearningRunProgressPage() {
  const { t } = useTranslation();
  const { runId } = useParams<{ runId: string }>();
  const [status, setStatus] = useState<StructuredLearningExtractionRunStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout>>();

  useEffect(() => {
    if (!runId) return;
    let cancelled = false;

    async function poll() {
      try {
        const next = await getStructuredLearningExtractionStatus(runId!);
        if (cancelled) return;
        setStatus(next);
        if (next.Stage === 'Running') {
          timerRef.current = setTimeout(poll, POLL_INTERVAL_MS);
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : t('structuredLearningRunProgress.statusErrorFallback'));
      }
    }

    poll();
    return () => {
      cancelled = true;
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [runId]);

  const stage = status?.Stage;

  return (
    <div className="mx-auto flex min-h-[70vh] max-w-2xl flex-col items-center justify-center px-6 py-16 text-center">
      {error && (
        <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-red-300">{error}</div>
      )}

      {!error && (!status || stage === 'Running') && (
        <div className="animate-fade-in space-y-6">
          <div className="relative mx-auto flex h-20 w-20 items-center justify-center">
            <div className="absolute inset-0 rounded-full bg-brand-500/20 animate-ping" />
            <div className="relative flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-br from-brand-500 to-accent-500">
              <Loader2 className="h-7 w-7 animate-spin text-[#fff]" />
            </div>
          </div>
          <div>
            <h2 className="text-xl font-semibold text-white">{t('structuredLearningRunProgress.extracting')}</h2>
            <p className="mt-2 text-slate-400">{status?.CurrentStepDescription ?? t('structuredLearningRunProgress.startingPipeline')}</p>
            {status?.TotalSections != null && status?.CompletedSections != null && (
              <p className="mt-1 text-sm text-slate-500">
                {t('structuredLearningRunProgress.sectionProgress', {
                  completed: status.CompletedSections,
                  total: status.TotalSections,
                })}
              </p>
            )}
          </div>
        </div>
      )}

      {!error && stage === 'Failed' && (
        <div className="animate-fade-in space-y-4">
          <XCircle className="mx-auto h-14 w-14 text-red-400" />
          <h2 className="text-xl font-semibold text-white">{t('structuredLearningRunProgress.failed')}</h2>
          <p className="text-slate-400">{status?.ErrorMessage ?? t('structuredLearningRunProgress.unexpectedError')}</p>
          <Link to="/new-structured-learning" className="inline-flex items-center gap-1.5 text-sm font-medium text-brand-400 hover:text-brand-300">
            {t('structuredLearningRunProgress.tryAgain')} <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      )}

      {!error && stage === 'Completed' && status?.Result && (
        <div className="animate-fade-in w-full space-y-8 text-left">
          <div className="text-center">
            <CheckCircle2 className="mx-auto h-14 w-14 text-emerald-400" />
            <h2 className="mt-4 text-2xl font-bold text-white">{t('structuredLearningRunProgress.done', { name: status.Result.DocumentName })}</h2>
            <p className="mt-2 text-slate-400">{t('structuredLearningRunProgress.successMessage')}</p>
          </div>

          <div className="grid grid-cols-3 gap-3">
            <ResultStat icon={Layers} label={t('structuredLearningRunProgress.sections')} value={status.Result.SectionCount} />
            <ResultStat icon={FileText} label={t('structuredLearningRunProgress.characters')} value={status.Result.TotalCharacters} />
            <ResultStat icon={Hash} label={t('structuredLearningRunProgress.tokens')} value={status.Result.TotalTokens} />
          </div>

          <PedagogySection documentId={status.Result.StructuredLearningDocumentId} />
        </div>
      )}
    </div>
  );
}

function ResultStat({ icon: Icon, label, value }: { icon: typeof Layers; label: string; value: number }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4 text-center">
      <Icon className="mx-auto h-5 w-5 text-brand-400" />
      <div className="mt-2 text-lg font-bold text-white">{value.toLocaleString()}</div>
      <div className="text-xs text-slate-400">{label}</div>
    </div>
  );
}

function VersionSummaryCard({ document }: { document: StructuredLearningDocumentDetail }) {
  const badges = [
    document.ProcessVersion && { label: 'Process', value: document.ProcessVersion, tone: 'border-teal-500/30 bg-teal-500/10 text-teal-200' },
    document.PromptVersion && { label: 'Prompt', value: document.PromptVersion, tone: 'border-violet-500/30 bg-violet-500/10 text-violet-200' },
    document.ModelName && { label: 'Model', value: document.ModelName, tone: 'border-amber-500/30 bg-amber-500/10 text-amber-200' },
  ].filter(Boolean) as { label: string; value: string; tone: string }[];

  return (
    <div className="rounded-2xl border border-white/10 bg-slate-950/40 p-4">
      <div className="flex items-center justify-between gap-3">
        <div className="text-sm font-semibold text-white">Version control</div>
        <span className="rounded-full border border-brand-500/30 bg-brand-500/10 px-2 py-1 text-[10px] font-medium text-brand-200">
          document
        </span>
      </div>

      <div className="mt-3 flex flex-wrap gap-2">
        {badges.map((badge) => (
          <span key={badge.label} className={`rounded-full border px-2 py-1 text-[10px] font-medium ${badge.tone}`}>
            {badge.label}: {badge.value}
          </span>
        ))}
      </div>

      {document.PromptHash && (
        <div className="mt-3 text-[10px] text-slate-400">
          Prompt hash: <span className="font-mono text-slate-300">{document.PromptHash}</span>
        </div>
      )}
    </div>
  );
}

/** Step 3: configure pedagogical settings, start generation, poll it, then
 * show a simple viewer of what got generated per section. */
function PedagogySection({ documentId }: { documentId: string }) {
  const { t } = useTranslation();

  const [pedagogicalAudience, setPedagogicalAudience] = useState<'Standard' | 'YoungLearner'>('Standard');
  const [pedagogyLanguageCode, setPedagogyLanguageCode] = useState<'es' | 'en'>('es');
  const [includeDeepDive, setIncludeDeepDive] = useState(true);
  const [maxDeepDiveCards, setMaxDeepDiveCards] = useState(3);
  const [includeRecall, setIncludeRecall] = useState(true);
  const [recallItemsRequired, setRecallItemsRequired] = useState(3);
  const [includeProduction, setIncludeProduction] = useState(true);
  const [assessmentQuestionType, setAssessmentQuestionType] = useState<StructuredLearningAssessmentQuestionType>('MultipleChoice');
  const [assessmentQuestionsPerRound, setAssessmentQuestionsPerRound] = useState(5);
  const [referenceImageFile, setReferenceImageFile] = useState<File | null>(null);
  const [referenceImagePreviewUrl, setReferenceImagePreviewUrl] = useState<string | null>(null);
  const [referenceImageUsageInstruction, setReferenceImageUsageInstruction] = useState('');

  const [submitting, setSubmitting] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const [regeneratingAll, setRegeneratingAll] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [pedagogyRunId, setPedagogyRunId] = useState<string | null>(null);
  const [pedagogyStatus, setPedagogyStatus] = useState<StructuredLearningPedagogyRunStatus | null>(null);
  const [documentDetail, setDocumentDetail] = useState<StructuredLearningDocumentDetail | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout>>();

  useEffect(() => {
    if (!pedagogyRunId) return;
    let cancelled = false;

    async function poll() {
      try {
        const next = await getStructuredLearningPedagogyStatus(pedagogyRunId!);
        if (cancelled) return;
        setPedagogyStatus(next);
        if (next.Stage === 'Running') {
          timerRef.current = setTimeout(poll, POLL_INTERVAL_MS);
        } else if (next.Stage === 'Completed') {
          const detail = await getStructuredLearningDocument(documentId);
          if (!cancelled) setDocumentDetail(detail);
        }
      } catch (err) {
        if (!cancelled) setSubmitError(err instanceof Error ? err.message : String(err));
      }
    }

    poll();
    return () => {
      cancelled = true;
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [pedagogyRunId, documentId]);

  useEffect(() => {
    return () => {
      if (referenceImagePreviewUrl) URL.revokeObjectURL(referenceImagePreviewUrl);
    };
  }, [referenceImagePreviewUrl]);

  async function handleGeneratePedagogy() {
    if (referenceImageFile && !referenceImageUsageInstruction.trim()) {
      setSubmitError('Describe how the reference image should be used in the generated lessons.');
      return;
    }

    setSubmitting(true);
    setSubmitError(null);
    try {
      await updateStructuredLearningPedagogyConfig(documentId, {
        pedagogicalAudience: pedagogicalAudience === 'Standard' ? null : pedagogicalAudience,
        maxDeepDiveCardsPerNode: includeDeepDive ? maxDeepDiveCards : null,
        includeProductionStep: includeProduction,
        includeRecallStep: includeRecall,
        recallItemsRequired: includeRecall ? recallItemsRequired : null,
        assessmentQuestionType,
        assessmentQuestionsPerRound,
      });

      const uploadedImageBase64 = referenceImageFile ? await readFileAsBase64(referenceImageFile) : null;
      const run = await startStructuredLearningPedagogy(
        documentId,
        pedagogyLanguageCode,
        false,
        uploadedImageBase64,
        referenceImageFile?.type ?? null,
        referenceImageFile ? referenceImageUsageInstruction.trim() || null : null,
      );
      setPedagogyRunId(run.RunId);
      setPedagogyStatus(run);
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : t('structuredLearningRunProgress.pedagogyErrorGenericStart'));
    } finally {
      setSubmitting(false);
    }
  }

  // Re-triggers generation for a document that already ran once — the
  // backend only reprocesses sections whose PedagogyStatus isn't Completed,
  // so this safely retries just the failed ones.
  async function handleRetryFailed() {
    setRetrying(true);
    setSubmitError(null);
    try {
      const run = await startStructuredLearningPedagogy(documentId, pedagogyLanguageCode);
      setDocumentDetail(null);
      setPedagogyRunId(run.RunId);
      setPedagogyStatus(run);
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : t('structuredLearningRunProgress.pedagogyErrorGenericStart'));
    } finally {
      setRetrying(false);
    }
  }

  // Force-regenerates EVERY section, even ones already Completed — needed
  // to pick up an agent prompt/instructions change (unlike handleRetryFailed,
  // which only reprocesses non-Completed sections).
  async function handleRegenerateAll() {
    setRegeneratingAll(true);
    setSubmitError(null);
    try {
      const run = await startStructuredLearningPedagogy(documentId, pedagogyLanguageCode, true);
      setDocumentDetail(null);
      setPedagogyRunId(run.RunId);
      setPedagogyStatus(run);
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : t('structuredLearningRunProgress.pedagogyErrorGenericStart'));
    } finally {
      setRegeneratingAll(false);
    }
  }

  // Not started yet — show the configuration form.
  if (!pedagogyRunId) {
    return (
      <div className="space-y-5 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
        <h3 className="text-lg font-semibold text-white">{t('structuredLearningRunProgress.pedagogyConfigTitle')}</h3>

        <ConfigField label={t('structuredLearningRunProgress.pedagogyLanguageLabel')}>
          <div className="grid grid-cols-2 gap-2">
            {(['es', 'en'] as const).map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setPedagogyLanguageCode(option)}
                className={`rounded-xl border px-3 py-2 text-sm font-medium transition-colors ${
                  pedagogyLanguageCode === option
                    ? 'border-brand-400/60 bg-brand-500/10 text-white'
                    : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                }`}
              >
                {option === 'es' ? t('common.languageSpanish') : t('common.languageEnglish')}
              </button>
            ))}
          </div>
        </ConfigField>

        <ConfigField label={t('structuredLearningRunProgress.pedagogyAudienceLabel')}>
          <div className="grid grid-cols-2 gap-2">
            {(['Standard', 'YoungLearner'] as const).map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setPedagogicalAudience(option)}
                className={`rounded-xl border px-3 py-2 text-sm font-medium transition-colors ${
                  pedagogicalAudience === option
                    ? 'border-brand-400/60 bg-brand-500/10 text-white'
                    : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                }`}
              >
                {option === 'Standard' ? t('structuredLearningRunProgress.audienceStandard') : t('structuredLearningRunProgress.audienceYoungLearner')}
              </button>
            ))}
          </div>
        </ConfigField>

        <ToggleWithCount
          label={t('structuredLearningRunProgress.pedagogyDeepDiveLabel')}
          checked={includeDeepDive}
          onCheckedChange={setIncludeDeepDive}
          count={maxDeepDiveCards}
          onCountChange={setMaxDeepDiveCards}
          countLabel={t('structuredLearningRunProgress.pedagogyDeepDiveCountLabel')}
        />

        <ToggleWithCount
          label={t('structuredLearningRunProgress.pedagogyRecallLabel')}
          checked={includeRecall}
          onCheckedChange={setIncludeRecall}
          count={recallItemsRequired}
          onCountChange={setRecallItemsRequired}
          countLabel={t('structuredLearningRunProgress.pedagogyRecallCountLabel')}
        />

        <label className="flex items-center gap-3 rounded-xl border border-white/10 bg-white/[0.02] p-3">
          <input
            type="checkbox"
            checked={includeProduction}
            onChange={(e) => setIncludeProduction(e.target.checked)}
            className="h-4 w-4 rounded border-white/20 bg-white/5 text-brand-500 focus:ring-brand-500"
          />
          <span className="text-sm text-white">{t('structuredLearningRunProgress.pedagogyProductionLabel')}</span>
        </label>

        <ConfigField label="Reference image for lesson illustrations">
          <div className="space-y-3 rounded-xl border border-white/10 bg-white/[0.02] p-3">
            {referenceImagePreviewUrl ? (
              <div className="flex items-center gap-3">
                <img src={referenceImagePreviewUrl} alt="reference" className="h-16 w-16 rounded-lg object-cover" />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm text-slate-300">{referenceImageFile?.name}</p>
                  <button
                    type="button"
                    onClick={() => {
                      if (referenceImagePreviewUrl) URL.revokeObjectURL(referenceImagePreviewUrl);
                      setReferenceImagePreviewUrl(null);
                      setReferenceImageFile(null);
                    }}
                    className="mt-1 text-xs font-medium text-brand-400 hover:text-brand-300"
                  >
                    Remove
                  </button>
                </div>
              </div>
            ) : (
              <label className="flex w-full cursor-pointer items-center justify-center gap-2 rounded-lg border border-dashed border-white/15 py-3 text-sm text-slate-400 hover:border-white/25 hover:text-slate-300">
                <UploadCloud className="h-4 w-4" />
                Upload image reference
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  className="hidden"
                  onChange={(e) => {
                    const file = e.target.files?.[0] ?? null;
                    setReferenceImageFile(file);
                    setReferenceImagePreviewUrl((prev) => {
                      if (prev) URL.revokeObjectURL(prev);
                      return file ? URL.createObjectURL(file) : null;
                    });
                  }}
                />
              </label>
            )}

            {referenceImageFile && (
              <div>
                <label className="mb-1.5 block text-xs font-medium text-slate-400">How should this image be used?</label>
                <textarea
                  value={referenceImageUsageInstruction}
                  onChange={(e) => setReferenceImageUsageInstruction(e.target.value)}
                  rows={2}
                  placeholder="e.g. use this exact logo on the volunteers' t-shirts"
                  className="input w-full"
                />
              </div>
            )}
          </div>
        </ConfigField>

        <ConfigField label={t('structuredLearningRunProgress.pedagogyAssessmentTypeLabel')}>
          <div className="grid grid-cols-2 gap-2">
            {(['MultipleChoice', 'OpenEnded'] as const).map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setAssessmentQuestionType(option)}
                className={`rounded-xl border px-3 py-2 text-sm font-medium transition-colors ${
                  assessmentQuestionType === option
                    ? 'border-brand-400/60 bg-brand-500/10 text-white'
                    : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                }`}
              >
                {option === 'MultipleChoice'
                  ? t('structuredLearningRunProgress.assessmentMultipleChoice')
                  : t('structuredLearningRunProgress.assessmentOpenEnded')}
              </button>
            ))}
          </div>
        </ConfigField>

        <ConfigField label={t('structuredLearningRunProgress.pedagogyAssessmentCountLabel')}>
          <input
            type="number"
            min={1}
            max={10}
            value={assessmentQuestionsPerRound}
            onChange={(e) => setAssessmentQuestionsPerRound(Number.parseInt(e.target.value, 10) || 5)}
            className="input"
          />
        </ConfigField>

        {submitError && (
          <div className="rounded-xl border border-red-500/20 bg-red-500/5 p-3 text-sm text-red-300">{submitError}</div>
        )}

        <button
          type="button"
          onClick={handleGeneratePedagogy}
          disabled={submitting}
          className="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-3 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform enabled:hover:scale-[1.02] disabled:cursor-not-allowed disabled:opacity-40"
        >
          <Sparkles className="h-4 w-4" />
          {submitting ? t('structuredLearningRunProgress.pedagogyStarting') : t('structuredLearningRunProgress.pedagogyGenerateButton')}
        </button>
      </div>
    );
  }

  // Running.
  if (pedagogyStatus?.Stage === 'Running' || !pedagogyStatus) {
    return (
      <div className="space-y-3 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
        <div className="flex items-center gap-3">
          <Loader2 className="h-5 w-5 animate-spin text-brand-400" />
          <div>
            <div className="text-sm font-medium text-white">{t('structuredLearningRunProgress.pedagogyGenerating')}</div>
            <div className="text-xs text-slate-400">{pedagogyStatus?.CurrentStepDescription}</div>
          </div>
        </div>
        {pedagogyStatus?.TotalSections != null && pedagogyStatus?.CompletedSections != null && (
          <p className="text-xs text-slate-500">
            {t('structuredLearningRunProgress.sectionProgress', {
              completed: pedagogyStatus.CompletedSections,
              total: pedagogyStatus.TotalSections,
            })}
          </p>
        )}
        {pedagogyStatus?.ActiveSectionTitles && pedagogyStatus.ActiveSectionTitles.length > 0 && (
          <p className="text-xs text-slate-500">{pedagogyStatus.ActiveSectionTitles.join(', ')}</p>
        )}
      </div>
    );
  }

  // Failed (the whole run threw — different from individual sections marked Failed, which still show up in the viewer below).
  if (pedagogyStatus.Stage === 'Failed') {
    return (
      <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-5 text-sm text-red-300">
        {pedagogyStatus.ErrorMessage}
      </div>
    );
  }

  // Completed — show the generated content per section.
  return (
    <div className="space-y-4">
      <Link
        to={`/structured-learning/${documentId}/course`}
        className="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-teal-500 to-emerald-500 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-teal-500/25 transition-transform hover:scale-[1.02]"
      >
        <PlayCircle className="h-4 w-4" />
        {t('structuredLearningRunProgress.pedagogyViewCourseButton')}
      </Link>

      <div className="grid grid-cols-2 gap-3">
        <ResultStat icon={CheckCircle2} label={t('structuredLearningRunProgress.pedagogySectionsCompleted')} value={pedagogyStatus.Result?.SectionsCompleted ?? 0} />
        <ResultStat icon={AlertTriangle} label={t('structuredLearningRunProgress.pedagogySectionsFailed')} value={pedagogyStatus.Result?.SectionsFailed ?? 0} />
      </div>

      {submitError && (
        <div className="rounded-xl border border-red-500/20 bg-red-500/5 p-3 text-sm text-red-300">{submitError}</div>
      )}

      {documentDetail && <VersionSummaryCard document={documentDetail} />}

      <button
        type="button"
        onClick={handleRegenerateAll}
        disabled={regeneratingAll}
        className="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-3 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform enabled:hover:scale-[1.02] disabled:cursor-not-allowed disabled:opacity-40"
      >
        <Sparkles className="h-4 w-4" />
        {regeneratingAll ? t('structuredLearningRunProgress.pedagogyRegeneratingAll') : t('structuredLearningRunProgress.pedagogyRegenerateAllButton')}
      </button>

      {(pedagogyStatus.Result?.SectionsFailed ?? 0) > 0 && (
        <div className="space-y-2">
          <button
            type="button"
            onClick={handleRetryFailed}
            disabled={retrying}
            className="flex w-full items-center justify-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-5 py-3 text-sm font-semibold text-white transition-colors enabled:hover:border-white/20 disabled:cursor-not-allowed disabled:opacity-40"
          >
            <Sparkles className="h-4 w-4" />
            {retrying ? t('structuredLearningRunProgress.pedagogyRetrying') : t('structuredLearningRunProgress.pedagogyRetryButton')}
          </button>
        </div>
      )}

      {documentDetail && (
        <div className="space-y-3">
          {documentDetail.Sections.map((section) => (
            <SectionViewer key={section.StructuredLearningDocumentSectionId} section={section} />
          ))}
        </div>
      )}
    </div>
  );
}

function SectionViewer({ section }: { section: StructuredLearningDocumentDetail['Sections'][number] }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  const statusColor =
    section.PedagogyStatus === 'Completed'
      ? 'text-emerald-300 border-emerald-500/20 bg-emerald-500/5'
      : section.PedagogyStatus === 'Failed'
        ? 'text-red-300 border-red-500/20 bg-red-500/5'
        : 'text-slate-300 border-white/10 bg-white/[0.02]';

  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
      <button type="button" onClick={() => setOpen((prev) => !prev)} className="flex w-full items-center justify-between gap-3 text-left">
        <div>
          <div className="text-sm font-semibold text-white">{section.SubchapterTitle}</div>
          <div className="text-xs text-slate-500">{section.ChapterTitle}</div>
        </div>
        <div className="flex items-center gap-2">
          <span className={`rounded-lg border px-2 py-1 text-[11px] font-medium ${statusColor}`}>{section.PedagogyStatus}</span>
          {open ? <ChevronDown className="h-4 w-4 text-slate-400" /> : <ChevronRight className="h-4 w-4 text-slate-400" />}
        </div>
      </button>

      {open && (
        <div className="mt-3 space-y-3 border-t border-white/10 pt-3">
          {section.PedagogyErrorMessage && (
            <p className="text-xs text-red-300">{section.PedagogyErrorMessage}</p>
          )}
          {section.Steps.map((step, index) => (
            <div key={index} className="overflow-hidden rounded-xl border border-white/10 bg-white/[0.02]">
              {(step.StepType === 'Hypothesis' || step.StepType === 'Teaching') && (
                <StepIllustration stepDataJson={step.StepDataJson} />
              )}
              <div className="p-3">
                <div className="text-xs font-semibold uppercase tracking-wide text-brand-400">{step.StepType}</div>
                {(step.ProcessVersion || step.PromptVersion || step.ModelName) && (
                  <div className="mt-2 flex flex-wrap gap-2">
                    {step.ProcessVersion && <span className="rounded-full border border-teal-500/30 bg-teal-500/10 px-2 py-1 text-[10px] font-medium text-teal-200">Process: {step.ProcessVersion}</span>}
                    {step.PromptVersion && <span className="rounded-full border border-violet-500/30 bg-violet-500/10 px-2 py-1 text-[10px] font-medium text-violet-200">Prompt: {step.PromptVersion}</span>}
                    {step.ModelName && <span className="rounded-full border border-amber-500/30 bg-amber-500/10 px-2 py-1 text-[10px] font-medium text-amber-200">Model: {step.ModelName}</span>}
                  </div>
                )}
                {step.Content && <p className="mt-2 text-sm text-slate-200 whitespace-pre-wrap">{step.Content}</p>}
                {step.StepType === 'DeepDive' && step.StepDataJson ? (
                  <DeepDiveCards stepDataJson={step.StepDataJson} />
                ) : step.StepType !== 'Hypothesis' && step.StepType !== 'Teaching' && step.StepDataJson ? (
                  <pre className="mt-1 overflow-x-auto text-xs text-slate-400">{JSON.stringify(JSON.parse(step.StepDataJson), null, 2)}</pre>
                ) : null}
              </div>
            </div>
          ))}
          {section.Steps.length === 0 && <p className="text-xs text-slate-500">{t('structuredLearningRunProgress.pedagogyNoStepsYet')}</p>}
        </div>
      )}
    </div>
  );
}

function ConfigField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="mb-1.5 block text-sm font-medium text-slate-300">{label}</label>
      {children}
    </div>
  );
}

function ToggleWithCount({
  label,
  checked,
  onCheckedChange,
  count,
  onCountChange,
  countLabel,
}: {
  label: string;
  checked: boolean;
  onCheckedChange: (value: boolean) => void;
  count: number;
  onCountChange: (value: number) => void;
  countLabel: string;
}) {
  return (
    <div className="space-y-2 rounded-xl border border-white/10 bg-white/[0.02] p-3">
      <label className="flex items-center gap-3">
        <input
          type="checkbox"
          checked={checked}
          onChange={(e) => onCheckedChange(e.target.checked)}
          className="h-4 w-4 rounded border-white/20 bg-white/5 text-brand-500 focus:ring-brand-500"
        />
        <span className="text-sm text-white">{label}</span>
      </label>
      {checked && (
        <div className="pl-7">
          <label className="mb-1 block text-xs text-slate-400">{countLabel}</label>
          <input
            type="number"
            min={1}
            max={10}
            value={count}
            onChange={(e) => onCountChange(Number.parseInt(e.target.value, 10) || 1)}
            className="input w-24"
          />
        </div>
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

  return (
    <img
      src={apiImageUrl(`/illustrations/structured-learning/${illustrationId}/image`)}
      alt=""
      className="h-48 w-full object-contain object-center"
    />
  );
}

function DeepDiveCards({ stepDataJson }: { stepDataJson: string }) {
  let cards: DeepDiveCard[] = [];
  try {
    cards = JSON.parse(stepDataJson) as DeepDiveCard[];
  } catch {
    return null;
  }

  return (
    <div className="mt-2 grid grid-cols-1 gap-3 sm:grid-cols-2">
      {cards.map((card, index) => (
        <div key={index} className="overflow-hidden rounded-xl border border-white/10 bg-white/[0.02]">
          {card.IllustrationId ? (
            <img
              src={apiImageUrl(`/illustrations/structured-learning/${card.IllustrationId}/image`)}
              alt={card.Title}
              className="h-40 w-full object-contain object-center"
            />
          ) : card.Emoji ? (
            <div className="flex h-40 w-full items-center justify-center bg-white/[0.03] text-5xl">{card.Emoji}</div>
          ) : null}
          <div className="p-3">
            <div className="text-sm font-semibold text-white">{card.Title}</div>
            <p className="mt-1 text-sm text-slate-300 whitespace-pre-wrap">{card.Text}</p>
          </div>
        </div>
      ))}
    </div>
  );
}

