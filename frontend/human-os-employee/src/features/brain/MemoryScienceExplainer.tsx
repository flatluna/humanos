import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronDown, ChevronUp, Circle, Percent, Zap, FlaskConical } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/Card';
import { COLOR_STOPS } from './memoryScience';

const STORAGE_KEY = 'humanos-employee-brain-explainer-collapsed';

export function MemoryScienceExplainer() {
  const { t } = useTranslation();
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === '1';
    } catch {
      return false;
    }
  });

  function toggle() {
    setCollapsed((prev) => {
      const next = !prev;
      try {
        localStorage.setItem(STORAGE_KEY, next ? '1' : '0');
      } catch {
        /* ignore */
      }
      return next;
    });
  }

  return (
    <Card className="mt-6 overflow-hidden border-accent-500/20 bg-gradient-to-br from-accent-500/5 via-transparent to-brand-500/5 p-0">
      <button type="button" onClick={toggle} className="flex w-full items-center justify-between gap-3 p-4 text-left">
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-accent-500 to-brand-600 text-white shadow shadow-accent-500/30">
            <FlaskConical className="h-4.5 w-4.5" />
          </div>
          <div>
            <p className="text-sm font-semibold text-slate-900 dark:text-white">
              {t('brain.explainer.title')}
            </p>
            {collapsed && (
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {t('brain.explainer.tapToView')}
              </p>
            )}
          </div>
        </div>
        {collapsed ? (
          <ChevronDown className="h-5 w-5 shrink-0 text-slate-400" />
        ) : (
          <ChevronUp className="h-5 w-5 shrink-0 text-slate-400" />
        )}
      </button>

      <AnimatePresence initial={false}>
        {!collapsed && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.25, ease: 'easeInOut' }}
            className="overflow-hidden"
          >
            <div className="space-y-5 px-4 pb-5 pt-1 sm:px-6">
              <div>
                <p className="mb-2 text-xs font-bold uppercase tracking-wide text-accent-500 dark:text-accent-400">
                  {t('brain.explainer.symbolsTitle')}
                </p>
                <ul className="space-y-2.5">
                  <li className="flex flex-col gap-2 text-sm text-slate-600 dark:text-slate-300">
                    <div className="flex items-start gap-2.5">
                      <Circle className="mt-0.5 h-4 w-4 shrink-0 fill-current text-emerald-400" />
                      <span>
                        {t('brain.explainer.coloredRingText')}
                      </span>
                    </div>
                    <div className="ml-6 flex flex-wrap items-center gap-3">
                      {[
                        { stop: COLOR_STOPS[0], label: t('brain.explainer.strong') },
                        { stop: COLOR_STOPS[2], label: t('brain.explainer.fading') },
                        { stop: COLOR_STOPS[4], label: t('brain.explainer.almostForgotten') },
                      ].map(({ stop, label }) => (
                        <span key={stop[1]} className="flex items-center gap-1.5">
                          <span
                            className="h-3.5 w-3.5 shrink-0 rounded-full ring-1 ring-white/20"
                            style={{ background: stop[1] }}
                          />
                          <span className="text-xs text-slate-500 dark:text-slate-400">{label}</span>
                        </span>
                      ))}
                    </div>
                  </li>
                  <li className="flex items-start gap-2.5 text-sm text-slate-600 dark:text-slate-300">
                    <Percent className="mt-0.5 h-4 w-4 shrink-0 text-amber-400" />
                    <span>
                      {t('brain.explainer.percentText')}
                    </span>
                  </li>
                  <li className="flex items-start gap-2.5 text-sm text-slate-600 dark:text-slate-300">
                    <Zap className="mt-0.5 h-4 w-4 shrink-0 text-violet-400" />
                    <span>
                      {t('brain.explainer.automaticText')}
                    </span>
                  </li>
                </ul>
              </div>

              <div>
                <p className="mb-2 text-xs font-bold uppercase tracking-wide text-brand-500 dark:text-brand-400">
                  {t('brain.explainer.algorithmTitle')}
                </p>
                <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300">
                  {t('brain.explainer.algorithmDescription')}
                </p>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </Card>
  );
}
