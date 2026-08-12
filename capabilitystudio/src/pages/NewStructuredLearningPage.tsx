import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  ArrowLeft,
  ArrowRight,
  FileText,
  MessageSquareText,
  RefreshCw,
  Wand2,
  ListTree,
  Sparkles,
} from 'lucide-react';
import {
  startStructuredLearningExtraction,
  type StructuredLearningExtractionRunStatus,
} from '../lib/api/structuredLearningApi';
import {
  readFileAsBase64,
  DEFAULT_STUDIO_TENANT_ID,
  previewCapabilityOutline,
  type CapabilityOutlineChapter,
  type CapabilityOutlineSource,
} from '../lib/api/pdfPipelineApi';
import StepIndicator from '../components/StepIndicator';
import TypeOptionCard from '../components/TypeOptionCard';
import FileDropzone from '../components/FileDropzone';
import { useAuth } from '../auth/AuthContext';

const STEPS_KEY = 'newStructuredLearning.steps';

/**
 * Studio's THIRD course-creation method (2026-08-10), Step 2 only so far:
 * new, standalone page — mirrors NewCapabilityPage.tsx's exact 5 steps
 * (Basic info / Material type / Upload content / Review / Learning path
 * design) visually, but is a SEPARATE component that never touches
 * NewCapabilityPage.tsx/NewTraditionalCoursePage.tsx. Calls the new
 * structuredLearningApi endpoints instead of the Graph/Traditional ones.
 */
export default function NewStructuredLearningPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const effectiveTenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;

  const [step, setStep] = useState(1);

  const [documentName, setDocumentName] = useState('');
  const [languageCode, setLanguageCode] = useState<'es' | 'en'>('es');

  const [sourceType, setSourceType] = useState<'pdf' | 'text'>('pdf');
  const [file, setFile] = useState<File | null>(null);
  const [ideaText, setIdeaText] = useState('');

  // Same "Learning path design" outline preview the Graph/Traditional
  // wizards already use — reused as-is, no changes to that endpoint.
  const [outline, setOutline] = useState<CapabilityOutlineChapter[] | null>(null);
  const [outlineSource, setOutlineSource] = useState<CapabilityOutlineSource | null>(null);
  const [outlineLoading, setOutlineLoading] = useState(false);
  const [outlineError, setOutlineError] = useState<string | null>(null);
  const [expandedDocumentText, setExpandedDocumentText] = useState<string | null>(null);

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const canAdvanceFromStep1 = documentName.trim().length > 0;
  const canAdvanceFromStep3 = sourceType === 'pdf' ? !!file : ideaText.trim().length > 0;

  /** Runs the cheap detection-only outline preview — same generic
   * endpoint the Graph/Traditional wizards use. */
  async function loadOutlinePreview() {
    setOutlineLoading(true);
    setOutlineError(null);
    setExpandedDocumentText(null);
    try {
      let result;
      if (sourceType === 'pdf' && file) {
        const contentBase64 = await readFileAsBase64(file);
        result = await previewCapabilityOutline({ fileName: file.name, contentBase64 });
      } else if (sourceType === 'text' && ideaText.trim()) {
        result = await previewCapabilityOutline({
          text: ideaText.trim(),
          capabilityName: documentName.trim(),
          languageCode,
        });
        setExpandedDocumentText(result.ExpandedDocumentText ?? null);
      } else {
        setOutline(null);
        setOutlineSource(null);
        return;
      }

      setOutlineSource(result.Source);
      if (result.Source === 'None' || result.Chapters.length === 0) {
        setOutline(null);
        setOutlineError(result.RejectionReason ?? t('newStructuredLearning.outlineNotFound'));
      } else {
        setOutline(result.Chapters);
      }
    } catch (err) {
      setOutline(null);
      setOutlineSource(null);
      setOutlineError(err instanceof Error ? err.message : t('newStructuredLearning.outlineLoadError'));
    } finally {
      setOutlineLoading(false);
    }
  }

  async function goToDesignStep() {
    setStep(5);
    if (outline === null && outlineSource === null && !outlineLoading) {
      await loadOutlinePreview();
    }
  }

  async function handleSubmit() {
    if (!outline || outline.length === 0) {
      setSubmitError(t('newStructuredLearning.errorMissingOutline'));
      return;
    }

    setSubmitting(true);
    setSubmitError(null);
    try {
      const basePayload = {
        tenantId: effectiveTenantId,
        documentName: documentName.trim(),
        languageCode,
        approvedOutline: outline,
        preExpandedDocumentText: sourceType === 'text' ? expandedDocumentText : null,
      };

      let run: StructuredLearningExtractionRunStatus;
      if (sourceType === 'pdf' && file) {
        run = await startStructuredLearningExtraction({
          ...basePayload,
          fileName: file.name,
          contentBase64: await readFileAsBase64(file),
        });
      } else {
        run = await startStructuredLearningExtraction({
          ...basePayload,
          description: ideaText.trim(),
        });
      }

      navigate(`/structured-learning-runs/${run.RunId}`);
    } catch (err) {
      setSubmitError(
        err instanceof Error && err.message.includes('409')
          ? t('newStructuredLearning.errorAlreadyGenerating')
          : err instanceof Error
            ? err.message
            : t('newStructuredLearning.errorGenericStart'),
      );
      setSubmitting(false);
    }
  }

  const steps = t(STEPS_KEY, { returnObjects: true }) as string[];

  return (
    <div className="mx-auto max-w-3xl px-6 py-12">
      <div className="mb-10">
        <StepIndicator steps={steps} currentStep={step} />
      </div>

      {step === 1 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step1Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newStructuredLearning.step1Subtitle')}</p>
          </div>

          <Field label={t('newStructuredLearning.documentNameLabel')}>
            <input
              type="text"
              value={documentName}
              onChange={(e) => setDocumentName(e.target.value)}
              placeholder={t('newStructuredLearning.documentNamePlaceholder')}
              className="input"
            />
          </Field>

          <Field label={t('newStructuredLearning.contentLanguageLabel')}>
            <div className="grid grid-cols-2 gap-2">
              {(
                [
                  { code: 'es' as const, label: t('common.languageSpanish') },
                  { code: 'en' as const, label: t('common.languageEnglish') },
                ]
              ).map((option) => {
                const active = languageCode === option.code;
                return (
                  <button
                    key={option.code}
                    type="button"
                    onClick={() => setLanguageCode(option.code)}
                    className={`rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors ${
                      active
                        ? 'border-brand-400/60 bg-brand-500/10 text-white'
                        : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                    }`}
                  >
                    {option.label}
                  </button>
                );
              })}
            </div>
          </Field>

          <WizardNav onNext={() => setStep(2)} nextDisabled={!canAdvanceFromStep1} />
        </div>
      )}

      {step === 2 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step2Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newStructuredLearning.step2Subtitle')}</p>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <TypeOptionCard
              icon={FileText}
              title={t('newStructuredLearning.materialTypes.pdfTitle')}
              description={t('newStructuredLearning.materialTypes.pdfDescription')}
              selected={sourceType === 'pdf'}
              onClick={() => setSourceType('pdf')}
            />
            <TypeOptionCard
              icon={MessageSquareText}
              title={t('newStructuredLearning.materialTypes.textTitle')}
              description={t('newStructuredLearning.materialTypes.textDescription')}
              selected={sourceType === 'text'}
              onClick={() => setSourceType('text')}
            />
          </div>

          <WizardNav onBack={() => setStep(1)} onNext={() => setStep(3)} />
        </div>
      )}

      {step === 3 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step3Title')}</h2>
            <p className="mt-1 text-slate-400">
              {sourceType === 'pdf'
                ? t('newStructuredLearning.step3SubtitlePdf')
                : t('newStructuredLearning.step3SubtitleText')}
            </p>
          </div>

          {sourceType === 'pdf' && <FileDropzone file={file} onFileSelected={setFile} />}

          {sourceType === 'text' && (
            <Field label={t('newStructuredLearning.ideaLabel')}>
              <textarea
                value={ideaText}
                onChange={(e) => setIdeaText(e.target.value)}
                rows={8}
                placeholder={t('newStructuredLearning.ideaPlaceholder')}
                className="input resize-none"
              />
            </Field>
          )}

          <WizardNav onBack={() => setStep(2)} onNext={() => setStep(4)} nextDisabled={!canAdvanceFromStep3} />
        </div>
      )}

      {step === 4 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step4Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newStructuredLearning.step4Subtitle')}</p>
          </div>

          <div className="space-y-2 rounded-2xl border border-white/10 bg-white/[0.03] p-4">
            <SummaryRow label={t('newStructuredLearning.summaryDocumentName')} value={documentName} />
            <SummaryRow
              label={t('newStructuredLearning.summaryLanguage')}
              value={languageCode === 'en' ? t('common.languageEnglish') : t('common.languageSpanish')}
            />
            <SummaryRow
              label={t('newStructuredLearning.summarySource')}
              value={sourceType === 'pdf' ? t('newStructuredLearning.sourcePdf') : t('newStructuredLearning.sourceText')}
            />
          </div>

          <WizardNav onBack={() => setStep(3)} onNext={goToDesignStep} />
        </div>
      )}

      {step === 5 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step5Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newStructuredLearning.step5Subtitle')}</p>
          </div>

          {outlineLoading && (
            <div className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.03] p-6 text-sm text-slate-300">
              <RefreshCw className="h-4 w-4 animate-spin text-brand-400" />
              {t('newStructuredLearning.outlineLoading')}
            </div>
          )}

          {!outlineLoading && outline && outline.length > 0 && (
            <>
              <div
                className={`flex items-center gap-2 rounded-xl border p-3 text-xs font-medium ${
                  outlineSource === 'InferredByAI'
                    ? 'border-amber-500/20 bg-amber-500/5 text-amber-300'
                    : 'border-emerald-500/20 bg-emerald-500/5 text-emerald-300'
                }`}
              >
                {outlineSource === 'InferredByAI' ? <Wand2 className="h-4 w-4 shrink-0" /> : <ListTree className="h-4 w-4 shrink-0" />}
                {outlineSource === 'InferredByAI'
                  ? t('newStructuredLearning.outlineSourceInferred')
                  : t('newStructuredLearning.outlineSourceDetected')}
              </div>

              <div className="space-y-4">
                {outline.map((chapter, chapterIndex) => (
                  <div key={chapterIndex} className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                    <div className="font-semibold text-white">{chapter.Title}</div>
                    <div className="mt-3 space-y-2 pl-4">
                      {chapter.Nodes.map((node, nodeIndex) => (
                        <div key={nodeIndex} className="flex items-center gap-2 text-sm text-slate-300">
                          <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-brand-400" />
                          {node.Title}
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>

              <button
                type="button"
                onClick={loadOutlinePreview}
                className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-400 hover:text-slate-200"
              >
                <RefreshCw className="h-3.5 w-3.5" />
                {t('newStructuredLearning.outlineRegenerate')}
              </button>
            </>
          )}

          {!outlineLoading && (!outline || outline.length === 0) && (
            <div className="space-y-3 rounded-2xl border border-white/10 bg-white/[0.03] p-6 text-sm text-slate-300">
              <p>{outlineError ?? t('newStructuredLearning.outlineNotFound')}</p>
              <button
                type="button"
                onClick={loadOutlinePreview}
                className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.02] px-3 py-1.5 text-xs font-medium text-slate-300 hover:border-white/20"
              >
                <RefreshCw className="h-3.5 w-3.5" />
                {t('newStructuredLearning.outlineRetry')}
              </button>
            </div>
          )}

          {submitError && (
            <div className="rounded-xl border border-red-500/20 bg-red-500/5 p-4 text-sm text-red-300">{submitError}</div>
          )}

          <WizardNav
            onBack={() => setStep(4)}
            onNext={handleSubmit}
            nextDisabled={outlineLoading || !outline || outline.length === 0 || submitting}
            nextLabel={submitting ? t('newStructuredLearning.starting') : t('newStructuredLearning.extractSections')}
            nextIcon={Sparkles}
          />
        </div>
      )}
    </div>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <label className="mb-1.5 block text-sm font-medium text-slate-300">{label}</label>
      {children}
    </div>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-4 text-sm">
      <span className="text-slate-400">{label}</span>
      <span className="text-right font-medium text-white">{value}</span>
    </div>
  );
}

function WizardNav({
  onBack,
  onNext,
  nextDisabled,
  nextLabel,
  nextIcon: NextIcon = ArrowRight,
}: {
  onBack?: () => void;
  onNext: () => void;
  nextDisabled?: boolean;
  nextLabel?: string;
  nextIcon?: typeof ArrowRight;
}) {
  const { t } = useTranslation();
  const resolvedNextLabel = nextLabel ?? t('common.continue');
  return (
    <div className="flex items-center justify-between pt-4">
      {onBack ? (
        <button
          type="button"
          onClick={onBack}
          className="flex items-center gap-1.5 rounded-xl px-4 py-2.5 text-sm font-medium text-slate-400 hover:text-white"
        >
          <ArrowLeft className="h-4 w-4" />
          {t('common.back')}
        </button>
      ) : (
        <span />
      )}
      <button
        type="button"
        onClick={onNext}
        disabled={nextDisabled}
        className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform enabled:hover:scale-[1.02] disabled:cursor-not-allowed disabled:opacity-40"
      >
        {resolvedNextLabel}
        <NextIcon className="h-4 w-4" />
      </button>
    </div>
  );
}
