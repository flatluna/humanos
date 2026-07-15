import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Upload, Loader2, Sparkles, AlertTriangle, X, FileText } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { useLocalizedText } from '@/localization/useLocalizedText';
import type { DeclaredExperienceItem, ResumeDocument, ResumeUploadStatus, ValidationStatus } from '../types';

const ACCEPTED_FILE_TYPES = '.pdf,.docx';

const VALIDATION_TONE: Record<ValidationStatus, 'neutral' | 'accent'> = {
  unvalidated: 'neutral',
  needsValidation: 'neutral',
  partiallyValidated: 'accent',
  validated: 'accent',
};

interface ResumeUploadProps {
  status: ResumeUploadStatus;
  resumeDocument: ResumeDocument | null;
  declaredExperience: DeclaredExperienceItem[];
  errorMessage: string | null;
  onSelectFile: (file: File) => void;
  onRetry: () => void;
  onRemove: () => void;
}

export function ResumeUpload({
  status,
  resumeDocument,
  declaredExperience,
  errorMessage,
  onSelectFile,
  onRetry,
  onRemove,
}: ResumeUploadProps) {
  const { t } = useTranslation();
  const localize = useLocalizedText();
  const inputRef = useRef<HTMLInputElement>(null);

  function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (file) {
      onSelectFile(file);
    }
    event.target.value = '';
  }

  return (
    <Card className="p-6 sm:p-8">
      <input
        ref={inputRef}
        type="file"
        accept={ACCEPTED_FILE_TYPES}
        onChange={handleFileChange}
        className="sr-only"
        aria-label={t('growthPlan.roleExperience.resumeUpload.dropzoneLabel')}
      />

      {status === 'idle' && (
        <div className="flex flex-col items-center gap-3 text-center">
          <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
            <Upload className="h-6 w-6" />
          </div>
          <p className="font-medium text-slate-900 dark:text-white">
            {t('growthPlan.roleExperience.resumeUpload.dropzoneLabel')}
          </p>
          <p className="max-w-sm text-sm text-slate-500 dark:text-white/50">
            {t('growthPlan.roleExperience.resumeUpload.dropzoneHint')}
          </p>
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            className="mt-2 inline-flex min-h-11 items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            {t('growthPlan.roleExperience.resumeUpload.selectFile')}
          </button>
        </div>
      )}

      {(status === 'uploading' || status === 'processing') && (
        <div aria-live="polite" className="flex flex-col items-center gap-3 text-center">
          <Loader2 className="h-8 w-8 animate-spin text-blue-500 motion-reduce:animate-none" />
          <p className="font-medium text-slate-900 dark:text-white">
            {status === 'uploading'
              ? t('growthPlan.roleExperience.resumeUpload.uploading')
              : t('growthPlan.roleExperience.resumeUpload.processing')}
          </p>
          {status === 'processing' && (
            <p className="max-w-sm text-sm text-slate-500 dark:text-white/50">
              {t('growthPlan.roleExperience.resumeUpload.processingDescription')}
            </p>
          )}
        </div>
      )}

      {status === 'error' && (
        <div aria-live="assertive" className="flex flex-col items-center gap-3 text-center">
          <AlertTriangle className="h-8 w-8 text-amber-500" />
          <p className="font-medium text-slate-900 dark:text-white">
            {t('growthPlan.roleExperience.resumeUpload.error')}
          </p>
          {errorMessage && <p className="text-xs text-slate-400 dark:text-white/40">{errorMessage}</p>}
          <button
            type="button"
            onClick={onRetry}
            className="mt-2 inline-flex min-h-11 items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            {t('growthPlan.roleExperience.resumeUpload.retry')}
          </button>
        </div>
      )}

      {status === 'extracted' && resumeDocument && (
        <div aria-live="polite">
          <div className="flex items-center justify-between gap-3">
            <div className="flex min-w-0 items-center gap-2 text-sm text-slate-600 dark:text-white/70">
              <FileText className="h-4 w-4 shrink-0" />
              <span className="truncate">{resumeDocument.fileName}</span>
            </div>
            <button
              type="button"
              onClick={onRemove}
              aria-label={t('growthPlan.roleExperience.resumeUpload.removeFile')}
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-slate-200 text-slate-500 transition hover:border-slate-300 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/60 dark:hover:border-white/20 dark:hover:text-white"
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="mt-5 flex items-center gap-2 text-blue-600 dark:text-blue-300">
            <Sparkles className="h-4 w-4" />
            <p className="text-sm font-semibold">{t('growthPlan.roleExperience.resumeUpload.extractedHeading')}</p>
          </div>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            {t('growthPlan.roleExperience.resumeUpload.extractedDescription')}
          </p>

          <ul className="mt-4 space-y-2.5">
            {declaredExperience.map((item) => (
              <li
                key={item.id}
                className="flex flex-col gap-2 rounded-xl border border-slate-100 px-4 py-3 sm:flex-row sm:items-center sm:justify-between dark:border-white/10"
              >
                <span className="text-sm text-slate-700 dark:text-white/80">{localize(item.text)}</span>
                <Badge tone={VALIDATION_TONE[item.validationStatus]} className="w-fit shrink-0">
                  {t(`growthPlan.roleExperience.validationStatuses.${item.validationStatus}`)}
                </Badge>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Card>
  );
}
