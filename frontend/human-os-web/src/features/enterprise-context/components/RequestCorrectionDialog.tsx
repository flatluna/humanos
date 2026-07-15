import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { X } from 'lucide-react';
import type { CorrectionReason } from '../types';

const REASONS: CorrectionReason[] = [
  'organization',
  'department',
  'team',
  'role',
  'roleLevel',
  'manager',
  'workLocation',
  'other',
];

interface RequestCorrectionDialogProps {
  onSubmit: (reason: CorrectionReason, details: string) => void;
  onClose: () => void;
  isSubmitting: boolean;
}

const FOCUSABLE_SELECTOR =
  'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])';

export function RequestCorrectionDialog({ onSubmit, onClose, isSubmitting }: RequestCorrectionDialogProps) {
  const { t } = useTranslation();
  const [reason, setReason] = useState<CorrectionReason | null>(null);
  const [details, setDetails] = useState('');
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const dialogNode = dialogRef.current;
    const focusable = dialogNode?.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR);
    focusable?.[0]?.focus();

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose();
        return;
      }

      if (event.key !== 'Tab' || !dialogNode) {
        return;
      }

      const nodes = dialogNode.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR);
      if (nodes.length === 0) {
        return;
      }

      const first = nodes[0];
      const last = nodes[nodes.length - 1];

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    if (reason) {
      onSubmit(reason, details);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-slate-900/40 p-0 sm:items-center sm:p-6">
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="correction-dialog-title"
        aria-describedby="correction-dialog-description"
        className="w-full max-w-lg rounded-t-3xl bg-white p-6 sm:rounded-3xl sm:p-8 dark:bg-[#0b0c10]"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 id="correction-dialog-title" className="text-lg font-semibold text-slate-900 dark:text-white">
              {t('growthPlan.workContext.correction.dialogTitle')}
            </h2>
            <p id="correction-dialog-description" className="mt-1 text-sm text-slate-500 dark:text-white/50">
              {t('growthPlan.workContext.correction.dialogDescription')}
            </p>
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label={t('common.close')}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-slate-200 text-slate-500 transition hover:border-slate-300 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/60 dark:hover:border-white/20 dark:hover:text-white"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="mt-6">
          <fieldset>
            <legend className="text-sm font-medium text-slate-700 dark:text-white/80">
              {t('growthPlan.workContext.correction.reasonLabel')}
            </legend>

            <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
              {REASONS.map((reasonOption) => (
                <label
                  key={reasonOption}
                  className="flex min-h-11 cursor-pointer items-center gap-2.5 rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-700 transition has-[:checked]:border-blue-500 has-[:checked]:bg-blue-50 dark:border-white/10 dark:text-white/80 dark:has-[:checked]:border-blue-400 dark:has-[:checked]:bg-blue-500/10"
                >
                  <input
                    type="radio"
                    name="correction-reason"
                    value={reasonOption}
                    checked={reason === reasonOption}
                    onChange={() => setReason(reasonOption)}
                    className="h-4 w-4 shrink-0 accent-blue-600"
                  />
                  {t(`growthPlan.workContext.correction.reasons.${reasonOption}`)}
                </label>
              ))}
            </div>
          </fieldset>

          <label className="mt-5 block">
            <span className="text-sm font-medium text-slate-700 dark:text-white/80">
              {t('growthPlan.workContext.correction.detailsLabel')}
            </span>
            <textarea
              value={details}
              onChange={(event) => setDetails(event.target.value)}
              rows={3}
              className="mt-2 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
            />
          </label>

          <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
            <button
              type="button"
              onClick={onClose}
              className="min-h-11 rounded-full border border-slate-200 px-5 py-2.5 text-sm font-medium text-slate-600 transition hover:border-slate-300 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/70 dark:hover:border-white/20 dark:hover:text-white"
            >
              {t('growthPlan.workContext.correction.cancel')}
            </button>
            <button
              type="submit"
              disabled={!reason || isSubmitting}
              className="min-h-11 rounded-full bg-slate-900 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
            >
              {t('growthPlan.workContext.correction.submit')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
