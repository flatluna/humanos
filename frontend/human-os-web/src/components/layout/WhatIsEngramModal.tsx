import { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { X, Brain, Share2, Zap, Users } from 'lucide-react';

interface WhatIsEngramModalProps {
  onClose: () => void;
}

const FOCUSABLE_SELECTOR = 'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])';

/** Explains the product's core concepts (Engram, Graph Learning, the Memory
 *  Paradox, Humans First · AI Agents Second) — opened from the "What is an
 *  Engram?" button in the app header, so the differentiator is one click
 *  away from anywhere in the logged-in app. Rendered via a portal into
 *  document.body: the header uses backdrop-blur, which (in Chromium)
 *  makes it a containing block for `position: fixed` descendants, so
 *  without the portal this modal rendered squeezed into the header's own
 *  small box instead of centered in the viewport. */
export function WhatIsEngramModal({ onClose }: WhatIsEngramModalProps) {
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

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-slate-900/50 p-0 sm:items-center sm:p-6">
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="engram-explainer-title"
        className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-t-3xl bg-white p-6 sm:rounded-3xl sm:p-8 dark:bg-[#0b0c10]"
      >
        <div className="flex items-start justify-between gap-4">
          <h2 id="engram-explainer-title" className="text-xl font-semibold text-slate-900 dark:text-white">
            {t('engramExplainer.title')}
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

        <p className="mt-3 text-sm leading-relaxed text-slate-600 dark:text-white/70">{t('engramExplainer.intro')}</p>

        <div className="mt-6 space-y-5">
          <section className="flex gap-3.5">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-fuchsia-500 to-fuchsia-600 text-white shadow-sm shadow-fuchsia-500/30">
              <Brain className="h-4.5 w-4.5" />
            </div>
            <div>
              <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                {t('engramExplainer.sections.engram.title')}
              </h3>
              <p className="mt-1 text-sm leading-relaxed text-slate-600 dark:text-white/60">
                {t('engramExplainer.sections.engram.body')}
              </p>
            </div>
          </section>

          <section className="flex gap-3.5">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-500 to-indigo-600 text-white shadow-sm shadow-indigo-500/30">
              <Share2 className="h-4.5 w-4.5" />
            </div>
            <div>
              <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                {t('engramExplainer.sections.graphLearning.title')}
              </h3>
              <p className="mt-1 text-sm leading-relaxed text-slate-600 dark:text-white/60">
                {t('engramExplainer.sections.graphLearning.body')}
              </p>
            </div>
          </section>

          <section className="flex gap-3.5">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-amber-500 to-orange-500 text-white shadow-sm shadow-amber-500/30">
              <Zap className="h-4.5 w-4.5" />
            </div>
            <div>
              <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                {t('engramExplainer.sections.memoryParadox.title')}
              </h3>
              <p className="mt-1 text-sm leading-relaxed text-slate-600 dark:text-white/60">
                {t('engramExplainer.sections.memoryParadox.body')}
              </p>
            </div>
          </section>

          <section className="flex gap-3.5">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500 to-blue-600 text-white shadow-sm shadow-blue-500/30">
              <Users className="h-4.5 w-4.5" />
            </div>
            <div>
              <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                {t('engramExplainer.sections.humansFirst.title')}
              </h3>
              <p className="mt-1 text-sm leading-relaxed text-slate-600 dark:text-white/60">
                {t('engramExplainer.sections.humansFirst.body')}
              </p>
            </div>
          </section>
        </div>

        <button
          type="button"
          onClick={onClose}
          className="mt-6 inline-flex w-full items-center justify-center rounded-full bg-slate-900 px-6 py-3 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
        >
          {t('engramExplainer.closeButton')}
        </button>
      </div>
    </div>,
    document.body,
  );
}
