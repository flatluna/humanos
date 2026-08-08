import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronDown, ChevronUp, Circle, Percent, Zap, FlaskConical, Sparkles, Smartphone } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { COLOR_STOPS } from './memoryScience';

const STORAGE_KEY = 'humanos-brain-explainer-collapsed';

/** Educational "how does this work" panel shown at the top of /brain.
 *  Explains the ring/percentage/⚡ symbols, the forgetting-curve algorithm
 *  in plain language, and the "why it matters" pitch (real memory + IQ vs.
 *  social-media/GPT cognitive offloading). Collapsible, remembers user's
 *  preference in localStorage so it doesn't nag on every visit. */
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
    <Card className="mt-6 overflow-hidden border-fuchsia-500/20 bg-gradient-to-br from-fuchsia-500/5 via-transparent to-indigo-500/5 p-0">
      <button
        type="button"
        onClick={toggle}
        className="flex w-full items-center justify-between gap-3 p-4 text-left"
      >
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-fuchsia-500 to-indigo-600 text-white shadow shadow-fuchsia-500/30">
            <FlaskConical className="h-4.5 w-4.5" />
          </div>
          <div>
            <p className="text-sm font-semibold text-slate-900 dark:text-white">{t('brain.explainerTitle')}</p>
            {collapsed && (
              <p className="text-xs text-slate-500 dark:text-slate-400">{t('brain.explainerToggleShow')}</p>
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
              {/* Legend: what each symbol means */}
              <div>
                <p className="mb-2 text-xs font-bold uppercase tracking-wide text-fuchsia-500 dark:text-fuchsia-400">
                  {t('brain.explainerLegendTitle')}
                </p>
                <ul className="space-y-2.5">
                  <li className="flex flex-col gap-2 text-sm text-slate-600 dark:text-slate-300">
                    <div className="flex items-start gap-2.5">
                      <Circle className="mt-0.5 h-4 w-4 shrink-0 fill-current text-emerald-400" />
                      <span>{t('brain.explainerLegendRing')}</span>
                    </div>
                    <div className="ml-6 flex flex-wrap items-center gap-3">
                      {[
                        { stop: COLOR_STOPS[0], label: t('brain.explainerColorStrong') },
                        { stop: COLOR_STOPS[2], label: t('brain.explainerColorFading') },
                        { stop: COLOR_STOPS[4], label: t('brain.explainerColorForgotten') },
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
                    <span>{t('brain.explainerLegendPercent')}</span>
                  </li>
                  <li className="flex items-start gap-2.5 text-sm text-slate-600 dark:text-slate-300">
                    <Zap className="mt-0.5 h-4 w-4 shrink-0 text-violet-400" />
                    <span>{t('brain.explainerLegendAutomatic')}</span>
                  </li>
                </ul>
              </div>

              {/* Algorithm explanation */}
              <div>
                <p className="mb-2 text-xs font-bold uppercase tracking-wide text-indigo-500 dark:text-indigo-400">
                  {t('brain.explainerAlgorithmTitle')}
                </p>
                <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300">
                  {t('brain.explainerAlgorithmBody')}
                </p>
              </div>

              {/* Why it matters */}
              <div>
                <p className="mb-2 text-xs font-bold uppercase tracking-wide text-emerald-500 dark:text-emerald-400">
                  {t('brain.explainerBenefitsTitle')}
                </p>
                <ul className="space-y-2.5">
                  <li className="flex items-start gap-2.5 text-sm text-slate-600 dark:text-slate-300">
                    <Sparkles className="mt-0.5 h-4 w-4 shrink-0 text-emerald-400" />
                    <span>{t('brain.explainerBenefitReal')}</span>
                  </li>
                  <li className="flex items-start gap-2.5 text-sm text-slate-600 dark:text-slate-300">
                    <FlaskConical className="mt-0.5 h-4 w-4 shrink-0 text-indigo-400" />
                    <span>{t('brain.explainerBenefitIQ')}</span>
                  </li>
                  <li className="flex items-start gap-2.5 text-sm text-slate-600 dark:text-slate-300">
                    <Smartphone className="mt-0.5 h-4 w-4 shrink-0 text-rose-400" />
                    <span>{t('brain.explainerBenefitSocial')}</span>
                  </li>
                </ul>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </Card>
  );
}
