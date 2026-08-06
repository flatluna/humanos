import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { X, ShieldOff, BotOff, DatabaseZap, CheckCircle2 } from 'lucide-react';

interface PrivacyDetailsModalProps {
  onClose: () => void;
}

const FOCUSABLE_SELECTOR = 'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])';

/** Full explanation of how student data is handled — never shared/sold,
 *  never used to train AI, and stored securely in Azure SQL — opened from
 *  the Privacy page's "see how we protect your data" button. */
export function PrivacyDetailsModal({ onClose }: PrivacyDetailsModalProps) {
  const { t } = useTranslation();
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const dialogNode = dialogRef.current;
    dialogNode?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR)?.focus();

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose();
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  const bullets = t('privacyPage.modal.sections.azureSql.bullets', { returnObjects: true }) as string[];

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-slate-900/50 p-0 sm:items-center sm:p-6">
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="privacy-modal-title"
        className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-t-3xl bg-white p-6 sm:rounded-3xl sm:p-8 dark:bg-[#0b0c10]"
      >
        <div className="flex items-start justify-between gap-4">
          <h2 id="privacy-modal-title" className="text-xl font-semibold text-slate-900 dark:text-white">
            {t('privacyPage.modal.title')}
          </h2>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('common.close')}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-slate-200 text-slate-500 transition hover:border-slate-300 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/60 dark:hover:border-white/20 dark:hover:text-white"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <p className="mt-3 text-sm leading-relaxed text-slate-600 dark:text-white/70">{t('privacyPage.modal.intro')}</p>

        <div className="mt-6 space-y-5">
          <section className="flex gap-3">
            <ShieldOff className="mt-0.5 h-5 w-5 shrink-0 text-emerald-500" />
            <div>
              <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                {t('privacyPage.modal.sections.noSharing.title')}
              </h3>
              <p className="mt-1 text-sm leading-relaxed text-slate-600 dark:text-white/60">
                {t('privacyPage.modal.sections.noSharing.body')}
              </p>
            </div>
          </section>

          <section className="flex gap-3">
            <BotOff className="mt-0.5 h-5 w-5 shrink-0 text-emerald-500" />
            <div>
              <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                {t('privacyPage.modal.sections.noAiTraining.title')}
              </h3>
              <p className="mt-1 text-sm leading-relaxed text-slate-600 dark:text-white/60">
                {t('privacyPage.modal.sections.noAiTraining.body')}
              </p>
            </div>
          </section>

          <section className="flex gap-3">
            <DatabaseZap className="mt-0.5 h-5 w-5 shrink-0 text-blue-500" />
            <div className="min-w-0">
              <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                {t('privacyPage.modal.sections.azureSql.title')}
              </h3>
              <p className="mt-1 text-sm leading-relaxed text-slate-600 dark:text-white/60">
                {t('privacyPage.modal.sections.azureSql.body')}
              </p>
              <ul className="mt-3 space-y-2">
                {bullets.map((bullet) => (
                  <li key={bullet} className="flex items-start gap-2 text-sm text-slate-600 dark:text-white/60">
                    <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-blue-500" />
                    <span>{bullet}</span>
                  </li>
                ))}
              </ul>
            </div>
          </section>
        </div>

        <div className="mt-6 rounded-2xl bg-slate-50 p-4 text-sm text-slate-600 dark:bg-white/[0.03] dark:text-white/60">
          {t('privacyPage.modal.closingNote')}
        </div>

        <button
          type="button"
          onClick={onClose}
          className="mt-6 inline-flex w-full items-center justify-center rounded-full bg-slate-900 px-6 py-3 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
        >
          {t('privacyPage.modal.closeButton')}
        </button>
      </div>
    </div>
  );
}
