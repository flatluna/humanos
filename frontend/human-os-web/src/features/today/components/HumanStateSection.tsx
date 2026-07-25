import { useState } from 'react';
import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { Crosshair, Zap, Compass, ShieldCheck, Flame, type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import type { HumanStateMetric } from '../types';
import type { UpsertHumanStateRequest } from '@/api/types';

const ICONS: Record<HumanStateMetric['key'], LucideIcon> = {
  focus: Crosshair,
  energy: Zap,
  purpose: Compass,
  confidence: ShieldCheck,
};

const METRIC_KEYS: HumanStateMetric['key'][] = ['focus', 'energy', 'purpose', 'confidence'];

const CIRCUMFERENCE = 2 * Math.PI * 18;

interface HumanStateSectionProps {
  metrics: HumanStateMetric[];
  hasCheckedInToday: boolean;
  streak: number;
  saving: boolean;
  onSubmit: (values: UpsertHumanStateRequest) => void | Promise<void>;
}

/** "Estado Humano" — real daily self check-in (1-5 tap rating per metric,
 *  stored as 20/40/60/80/100). Shows the rings once checked in today;
 *  otherwise shows the tap-to-rate form. */
export function HumanStateSection({ metrics, hasCheckedInToday, streak, saving, onSubmit }: HumanStateSectionProps) {
  const { t } = useTranslation();
  const [editing, setEditing] = useState(false);
  const [answers, setAnswers] = useState<Partial<Record<HumanStateMetric['key'], number>>>({});

  const showForm = !hasCheckedInToday || editing;
  const canSubmit = METRIC_KEYS.every((key) => answers[key] !== undefined);

  async function handleSubmit() {
    if (!canSubmit) return;
    await onSubmit({
      focus: answers.focus!,
      energy: answers.energy!,
      purpose: answers.purpose!,
      confidence: answers.confidence!,
    });
    setEditing(false);
    setAnswers({});
  }

  return (
    <section aria-labelledby="human-state-heading" className="mx-auto max-w-6xl px-6 py-14">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2
          id="human-state-heading"
          className="text-sm font-medium uppercase tracking-widest text-slate-400 dark:text-white/40"
        >
          {t('humanState.sectionTitle')}
        </h2>

        {hasCheckedInToday && !editing && (
          <div className="flex items-center gap-3">
            {streak > 1 && (
              <span className="flex items-center gap-1 text-xs font-medium text-orange-500 dark:text-orange-400">
                <Flame className="h-3.5 w-3.5" />
                {t('humanState.streak', { count: streak })}
              </span>
            )}
            <button
              type="button"
              onClick={() => setEditing(true)}
              className="text-xs font-medium text-blue-600 hover:text-blue-700 dark:text-blue-300 dark:hover:text-blue-200"
            >
              {t('humanState.updateCheckIn')}
            </button>
          </div>
        )}
      </div>

      {!showForm && (
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
      )}

      {showForm && (
        <Card className="mt-4 p-6">
          <p className="font-medium text-slate-900 dark:text-white">{t('humanState.checkInPrompt')}</p>
          <p className="mt-1 text-xs text-slate-400 dark:text-white/40">{t('humanState.checkInHint')}</p>

          <div className="mt-5 space-y-4">
            {METRIC_KEYS.map((key) => {
              const Icon = ICONS[key];
              return (
                <div key={key} className="flex flex-wrap items-center gap-3">
                  <div className="flex w-28 shrink-0 items-center gap-1.5 text-sm font-medium text-slate-600 dark:text-white/70">
                    <Icon className="h-4 w-4" />
                    {t(`humanState.${key}`)}
                  </div>
                  <div className="flex gap-1.5">
                    {[20, 40, 60, 80, 100].map((value, dotIndex) => (
                      <button
                        key={value}
                        type="button"
                        onClick={() => setAnswers((prev) => ({ ...prev, [key]: value }))}
                        aria-label={`${dotIndex + 1}/5`}
                        className={
                          answers[key] !== undefined && answers[key]! >= value
                            ? 'h-6 w-6 rounded-full bg-blue-500 transition dark:bg-blue-400'
                            : 'h-6 w-6 rounded-full border border-slate-300 bg-transparent transition hover:border-blue-400 dark:border-white/20'
                        }
                      />
                    ))}
                  </div>
                </div>
              );
            })}
          </div>

          <div className="mt-5 flex items-center gap-3">
            <button
              type="button"
              disabled={!canSubmit || saving}
              onClick={handleSubmit}
              className="rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 disabled:cursor-not-allowed disabled:opacity-40 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
            >
              {saving ? t('humanState.checkInSaving') : t('humanState.checkInSubmit')}
            </button>
            {editing && (
              <button
                type="button"
                onClick={() => {
                  setEditing(false);
                  setAnswers({});
                }}
                className="text-sm font-medium text-slate-500 hover:text-slate-700 dark:text-white/50 dark:hover:text-white"
              >
                {t('humanState.checkedInToday')}
              </button>
            )}
          </div>
        </Card>
      )}
    </section>
  );
}
