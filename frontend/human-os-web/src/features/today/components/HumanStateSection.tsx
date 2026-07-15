import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { Crosshair, Zap, Compass, ShieldCheck, type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import type { HumanStateMetric } from '../types';

const ICONS: Record<HumanStateMetric['key'], LucideIcon> = {
  focus: Crosshair,
  energy: Zap,
  purpose: Compass,
  confidence: ShieldCheck,
};

const CIRCUMFERENCE = 2 * Math.PI * 18;

interface HumanStateSectionProps {
  metrics: HumanStateMetric[];
}

export function HumanStateSection({ metrics }: HumanStateSectionProps) {
  const { t } = useTranslation();

  return (
    <section aria-labelledby="human-state-heading" className="mx-auto max-w-6xl px-6 py-14">
      <h2
        id="human-state-heading"
        className="text-sm font-medium uppercase tracking-widest text-slate-400 dark:text-white/40"
      >
        {t('humanState.sectionTitle')}
      </h2>

      <div className="mt-4 grid grid-cols-2 gap-4 sm:grid-cols-4">
        {metrics.map((metric, i) => {
          const Icon = ICONS[metric.key];

          return (
            <motion.div
              key={metric.key}
              initial={{ opacity: 0, y: 12 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, amount: 0.5 }}
              transition={{ duration: 0.4, delay: i * 0.06 }}
            >
              <Card className="flex flex-col items-center gap-2 p-5 text-center">
                <div className="relative flex h-12 w-12 items-center justify-center">
                  <svg className="absolute inset-0 -rotate-90" viewBox="0 0 44 44" aria-hidden="true">
                    <circle
                      cx="22"
                      cy="22"
                      r="18"
                      fill="none"
                      strokeWidth="4"
                      className="stroke-slate-100 dark:stroke-white/10"
                    />
                    <circle
                      cx="22"
                      cy="22"
                      r="18"
                      fill="none"
                      strokeWidth="4"
                      strokeLinecap="round"
                      strokeDasharray={CIRCUMFERENCE}
                      strokeDashoffset={CIRCUMFERENCE * (1 - metric.value / 100)}
                      className="stroke-blue-500 dark:stroke-blue-400"
                    />
                  </svg>
                  <Icon className="h-4 w-4 text-slate-600 dark:text-white/70" />
                </div>

                <p className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
                  {t(`humanState.${metric.key}`)}
                </p>
                <p className="text-sm font-semibold text-slate-900 dark:text-white">{metric.value}%</p>
              </Card>
            </motion.div>
          );
        })}
      </div>
    </section>
  );
}
