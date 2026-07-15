import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FileUp, MessageSquareText, Sparkles, CheckCircle2, AlertTriangle } from 'lucide-react';
import { useUploadJobDescriptionFile } from '../hooks/useJobDescriptionSource';
import { useJobDescriptionSourceStore } from '../store/useJobDescriptionSourceStore';

type CaptureMode = 'options' | 'paste' | 'describe';

const ACCEPTED_FILE_TYPES = '.pdf,application/pdf';

/** The compact "Configurar mi Rol" widget shown when Human OS can't
 *  retrieve the employee's role (e.g. Work Context failed to load). It
 *  offers the three ways to provide a Job Description as an
 *  employee-provided source — upload (PDF only), paste, or describe —
 *  without ever presenting the result as an official/organization-
 *  validated record. Reuses the same store/service the dedicated Job
 *  Description Source screen uses, so anything captured here shows up
 *  there too.
 */
export function JobDescriptionQuickCapture() {
  const { t } = useTranslation();
  const [mode, setMode] = useState<CaptureMode>('options');
  const [text, setText] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  const uploadMutation = useUploadJobDescriptionFile();
  const setEmployeeContext = useJobDescriptionSourceStore((state) => state.setEmployeeContext);

  function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (file) {
      uploadMutation.mutate(file);
    }
    event.target.value = '';
  }

  function handleSaveText() {
    if (!text.trim()) return;
    setEmployeeContext({
      rolePurpose: text.trim(),
      mainResponsibilities: [],
      expectedResults: [],
      missingContext: null,
      uploadedFileName: null,
      uploadedFileStoragePath: null,
    });
    setText('');
    setMode('options');
  }

  return (
    <div className="mx-auto w-full max-w-lg text-left">
      {mode === 'options' && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <input
            ref={inputRef}
            type="file"
            accept={ACCEPTED_FILE_TYPES}
            onChange={handleFileChange}
            className="sr-only"
            aria-label={t('growthPlan.workContext.configureRole.uploadPdf')}
          />
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={uploadMutation.isPending}
            className="flex min-h-28 flex-col items-center justify-center gap-2.5 rounded-2xl border border-slate-200 bg-white px-4 py-5 text-center shadow-sm transition hover:-translate-y-0.5 hover:border-blue-300 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:pointer-events-none disabled:opacity-60 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-blue-400/40"
          >
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
              <FileUp className="h-5 w-5" />
            </span>
            <span className="text-sm font-medium text-slate-700 dark:text-white/80">
              {uploadMutation.isPending
                ? t('growthPlan.workContext.configureRole.uploading')
                : t('growthPlan.workContext.configureRole.uploadPdf')}
            </span>
          </button>

          <button
            type="button"
            onClick={() => setMode('paste')}
            className="flex min-h-28 flex-col items-center justify-center gap-2.5 rounded-2xl border border-slate-200 bg-white px-4 py-5 text-center shadow-sm transition hover:-translate-y-0.5 hover:border-blue-300 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-blue-400/40"
          >
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
              <MessageSquareText className="h-5 w-5" />
            </span>
            <span className="text-sm font-medium text-slate-700 dark:text-white/80">
              {t('growthPlan.workContext.configureRole.pasteDescription')}
            </span>
          </button>

          <button
            type="button"
            onClick={() => setMode('describe')}
            className="flex min-h-28 flex-col items-center justify-center gap-2.5 rounded-2xl border border-slate-200 bg-white px-4 py-5 text-center shadow-sm transition hover:-translate-y-0.5 hover:border-blue-300 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-blue-400/40"
          >
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
              <Sparkles className="h-5 w-5" />
            </span>
            <span className="text-sm font-medium text-slate-700 dark:text-white/80">
              {t('growthPlan.workContext.configureRole.describeWithGuide')}
            </span>
          </button>
        </div>
      )}

      {(mode === 'paste' || mode === 'describe') && (
        <div className="flex flex-col gap-3">
          <label className="block">
            <span className="text-sm font-medium text-slate-700 dark:text-white/80">
              {mode === 'paste'
                ? t('growthPlan.workContext.configureRole.pasteHeading')
                : t('growthPlan.workContext.configureRole.describeHeading')}
            </span>
            <textarea
              value={text}
              onChange={(event) => setText(event.target.value)}
              rows={4}
              autoFocus
              placeholder={t('growthPlan.workContext.configureRole.textareaPlaceholder')}
              className="mt-2 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
            />
          </label>

          <div className="flex flex-col-reverse gap-3 sm:flex-row">
            <button
              type="button"
              onClick={() => {
                setText('');
                setMode('options');
              }}
              className="inline-flex min-h-11 items-center justify-center rounded-full border border-slate-200 px-6 py-2.5 text-sm font-medium text-slate-700 transition hover:border-slate-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/70 dark:hover:border-white/20"
            >
              {t('growthPlan.workContext.configureRole.cancel')}
            </button>

            <button
              type="button"
              onClick={handleSaveText}
              disabled={!text.trim()}
              className="inline-flex min-h-11 items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
            >
              {t('growthPlan.workContext.configureRole.save')}
            </button>
          </div>
        </div>
      )}

      {uploadMutation.isError && (
        <p className="mt-3 flex items-center gap-2 text-sm text-amber-600 dark:text-amber-400">
          <AlertTriangle className="h-4 w-4 shrink-0" />
          {t('growthPlan.workContext.configureRole.uploadError')}
        </p>
      )}

      {uploadMutation.isSuccess && (
        <p className="mt-3 flex items-center gap-2 text-sm text-emerald-600 dark:text-emerald-400">
          <CheckCircle2 className="h-4 w-4 shrink-0" />
          {t('growthPlan.workContext.configureRole.uploaded')}
        </p>
      )}
    </div>
  );
}
