import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { RotateCcw, PenTool, Rocket, FileCheck2, type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { useLocalizedText } from '@/localization/useLocalizedText';
import type { TodayAction, TodayActionType } from '../types';

const ICONS: Record<TodayActionType, LucideIcon> = {
  recall: RotateCcw,
  practice: PenTool,
  project: Rocket,
  evidence: FileCheck2,
};

interface TodayActionsSectionProps {
  actions: TodayAction[];
}

export function TodayActionsSection({ actions }: TodayActionsSectionProps) {
  const { t } = useTranslation();
  const localize = useLocalizedText();

  return (
    <section aria-labelledby="actions-heading" className="mx-auto max-w-6xl px-6 pt-14">
      <h2 id="actions-heading" className="text-2xl font-semibold text-slate-900 dark:text-white">
        {t('actions.sectionTitle')}
      </h2>

      <div className="mt-6 grid gap-5 sm:grid-cols-2">
        {actions.map((action, i) => {
          const Icon = ICONS[action.type];

          return (
            <motion.button
              key={action.id}
              type="button"
              initial={{ opacity: 0, y: 20 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, amount: 0.4 }}
              transition={{ duration: 0.5, delay: i * 0.08 }}
              className="text-left focus-visible:outline-none"
            >
              <Card className="group p-6 transition hover:border-blue-300 hover:shadow-md focus-visible:ring-2 focus-visible:ring-blue-500 dark:hover:border-blue-400/40">
                <div className="flex items-start gap-4">
                  <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 transition group-hover:scale-105 dark:text-blue-300">
                    <Icon className="h-5 w-5" />
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
                      {t(`actions.types.${action.type}`)}
                    </p>
                    <h3 className="mt-1 font-semibold text-slate-900 dark:text-white">{localize(action.title)}</h3>
                    <p className="mt-2 text-sm text-slate-500 dark:text-white/50">
                      <span className="font-medium text-slate-400 dark:text-white/30">
                        {t('actions.whyThisMatters')}:{' '}
                      </span>
                      {localize(action.why)}
                    </p>
                  </div>
                </div>
              </Card>
            </motion.button>
          );
        })}
      </div>
    </section>
  );
}
