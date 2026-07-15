import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { ArrowLeft } from 'lucide-react';

interface SetupProgressProps {
  currentStep: number;
  totalSteps: number;
  /** Already-translated label for the current step (e.g. "Work Context"). */
  stepLabel: string;
}

/** Compact "Step X of Y" indicator with a thin progress bar and a link
 *  back to the Growth Plan index. Reused across every Growth Plan setup
 *  screen (Work Context, Role Experience, etc.) for a consistent sense
 *  of a lifelong-but-bounded setup process rather than an open-ended
 *  form — and so every step always offers an obvious way back to the
 *  full sequence instead of only relying on the header breadcrumb.
 */
export function SetupProgress({ currentStep, totalSteps, stepLabel }: SetupProgressProps) {
  const { t } = useTranslation();
  const percentage = (currentStep / totalSteps) * 100;

  return (
    <div>
      <Link
        to="/growth-plan"
        className="inline-flex items-center gap-1.5 text-sm font-medium text-slate-500 transition hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:text-white/50 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" />
        {t('growthPlan.overview.backToGrowthPlan')}
      </Link>

      <p className="mt-4 text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
        {t('growthPlan.direction.stepOf', { current: currentStep, total: totalSteps })}
      </p>
      <p className="mt-1 text-sm font-medium text-slate-600 dark:text-white/60">{stepLabel}</p>
      <div className="mt-3 h-1.5 w-full max-w-xs overflow-hidden rounded-full bg-slate-200 dark:bg-white/10">
        <div
          className="h-full rounded-full bg-gradient-to-r from-blue-500 to-violet-500 transition-all duration-500"
          style={{ width: `${percentage}%` }}
        />
      </div>

    </div>
  );
}
