import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Bot, Send, X, Sparkles } from 'lucide-react';
import { clsx } from 'clsx';

/** Persistent AI-assistant entry point for the enterprise console —
 *  "agentic UX" first pass. Placeholder conversation UI (not wired to a
 *  real agent backend yet); intended target: an org-aware assistant that
 *  can answer "¿qué certificaciones le faltan a mi equipo de Ventas?",
 *  draft a Job Role from a description, or suggest a career roadmap. */
export function AgentPanel() {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className={clsx(
          'fixed bottom-6 right-6 z-40 flex items-center gap-2 rounded-full bg-slate-900 px-4 py-3 text-sm font-medium text-white shadow-xl transition hover:bg-slate-700',
          'dark:bg-white dark:text-slate-900 dark:hover:bg-white/90',
          open && 'hidden',
        )}
      >
        <Sparkles className="h-4 w-4" />
        {t('agent.buttonLabel')}
      </button>

      {open && (
        <div className="fixed inset-y-0 right-0 z-40 flex w-full max-w-sm flex-col border-l border-slate-200 bg-white shadow-2xl dark:border-white/10 dark:bg-[#05060a]">
          <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-white/10">
            <div className="flex items-center gap-2 text-sm font-semibold text-slate-900 dark:text-white">
              <Bot className="h-4 w-4 text-brand-600 dark:text-brand-300" />
              {t('agent.panelTitle')}
            </div>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-slate-100 dark:text-white/40 dark:hover:bg-white/10"
              aria-label={t('agent.close')}
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="flex-1 space-y-3 overflow-y-auto px-4 py-4 text-sm">
            <div className="rounded-2xl rounded-tl-sm bg-slate-100 px-3 py-2 text-slate-700 dark:bg-white/5 dark:text-white/70">
              {t('agent.welcomeMessage')}
            </div>
            <p className="px-1 text-xs text-slate-400 dark:text-white/30">
              {t('agent.soonNotice')}
            </p>
          </div>

          <div className="border-t border-slate-200 p-3 dark:border-white/10">
            <div className="flex items-center gap-2 rounded-full border border-slate-200 bg-slate-50 px-3 py-2 dark:border-white/10 dark:bg-white/5">
              <input
                type="text"
                disabled
                placeholder={t('agent.inputPlaceholder')}
                className="flex-1 bg-transparent text-sm outline-none placeholder:text-slate-400 dark:text-white dark:placeholder:text-white/30"
              />
              <Send className="h-4 w-4 text-slate-300 dark:text-white/20" />
            </div>
          </div>
        </div>
      )}
    </>
  );
}
