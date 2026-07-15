import { useTranslation } from 'react-i18next';
import { ArrowLeft, Construction } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import type { RoleAlignmentWizardStep } from '../types';

interface DimensionStepPlaceholderProps {
  step: RoleAlignmentWizardStep;
  onBackToSummary: () => void;
}

/** Explicit placeholder shown for any wizard step beyond Summary, none
 *  of which have a built screen yet. Never rendered as a blank/empty
 *  page — it always names the dimension and how many findings are
 *  waiting, and always offers a way back. */
export function DimensionStepPlaceholder({ step, onBackToSummary }: DimensionStepPlaceholderProps) {
  const { t } = useTranslation();

  return (
    <Card className="flex flex-col items-center gap-3 p-6 text-center sm:p-8">
      <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-slate-100 text-slate-500 dark:bg-white/5 dark:text-white/50">
        <Construction className="h-6 w-6" />
      </div>

      <p className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
        {t(`growthPlan.roleExperience.alignmentGuide.stepLabels.${step.id}`)}
      </p>
      <p className="font-medium text-slate-900 dark:text-white">
        {t('growthPlan.roleExperience.alignmentGuide.placeholder.heading')}
      </p>
      <p className="max-w-sm text-sm text-slate-500 dark:text-white/50">
        {t('growthPlan.roleExperience.alignmentGuide.placeholder.message')}
      </p>

      {step.findingCount > 0 && <Badge tone="accent">{step.findingCount}</Badge>}

      <button
        type="button"
        onClick={onBackToSummary}
        className="mt-2 inline-flex min-h-11 items-center justify-center gap-2 rounded-full border border-slate-200 px-6 py-2.5 text-sm font-medium text-slate-700 transition hover:border-slate-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/70 dark:hover:border-white/20"
      >
        <ArrowLeft className="h-4 w-4" />
        {t('growthPlan.roleExperience.alignmentGuide.placeholder.backToSummary')}
      </button>
    </Card>
  );
}
