import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronDown, ChevronUp, Sparkles } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import type { AnalysisSource, RoleAlignmentFinding } from '../../types';
import type { RoleAlignmentWizardStep } from '../types';

interface RoleAlignmentSummaryStepProps {
  /** The full 12-step map (including unavailable steps). Only
   *  available dimension steps with at least one finding are rendered
   *  as a row — summary/finalReview are excluded here. */
  steps: RoleAlignmentWizardStep[];
  findings: RoleAlignmentFinding[];
  onStartReview: () => void;
}

const SOURCE_KEYS: AnalysisSource[] = ['organization', 'jobDescription', 'resume', 'employeeDeclared', 'agentInferred'];

export function RoleAlignmentSummaryStep({ steps, findings, onStartReview }: RoleAlignmentSummaryStepProps) {
  const { t } = useTranslation();
  const [areSourcesVisible, setAreSourcesVisible] = useState(false);

  const dimensionRows = steps.filter(
    (step) => step.id !== 'summary' && step.id !== 'finalReview' && step.isAvailable && step.findingCount > 0,
  );

  const findingCountBySource = SOURCE_KEYS.map((source) => ({
    source,
    count: findings.filter((finding) => finding.source === source).length,
  })).filter((entry) => entry.count > 0);

  return (
    <div className="space-y-5">
      <Card className="p-6 sm:p-8">
        <div className="flex items-center gap-2 text-blue-600 dark:text-blue-300">
          <Sparkles className="h-5 w-5" />
          <p className="text-sm font-semibold uppercase tracking-wide">
            {t('growthPlan.roleExperience.alignmentGuide.title')}
          </p>
        </div>

        <p className="mt-4 text-base text-slate-700 dark:text-white/80">
          {t('growthPlan.roleExperience.alignmentGuide.summary.introLine1')}
        </p>
        <p className="mt-2 text-base font-medium text-slate-900 dark:text-white">
          {t('growthPlan.roleExperience.alignmentGuide.summary.introLine2')}
        </p>
      </Card>

      {dimensionRows.length > 0 && (
        <Card className="p-6 sm:p-8">
          <p className="text-sm font-semibold text-slate-900 dark:text-white">
            {t('growthPlan.roleExperience.alignmentGuide.summary.dimensionsHeading')}
          </p>
          <ul className="mt-4 divide-y divide-slate-100 dark:divide-white/10">
            {dimensionRows.map((step) => (
              <li key={step.id} className="flex items-center justify-between py-2.5 text-sm">
                <span className="text-slate-600 dark:text-white/70">
                  {t(`growthPlan.roleExperience.alignmentGuide.stepLabels.${step.id}`)}
                </span>
                <Badge tone="accent">{step.findingCount}</Badge>
              </li>
            ))}
          </ul>
        </Card>
      )}

      <Card className="p-6 sm:p-8">
        <button
          type="button"
          onClick={() => setAreSourcesVisible((visible) => !visible)}
          className="flex w-full items-center justify-between text-left"
          aria-expanded={areSourcesVisible}
        >
          <span className="text-sm font-semibold text-slate-900 dark:text-white">
            {t('growthPlan.roleExperience.alignmentGuide.summary.sourcesHeading')}
          </span>
          {areSourcesVisible ? (
            <ChevronUp className="h-4 w-4 text-slate-400 dark:text-white/40" />
          ) : (
            <ChevronDown className="h-4 w-4 text-slate-400 dark:text-white/40" />
          )}
        </button>

        {areSourcesVisible && (
          <div className="mt-4">
            <p className="text-sm text-slate-500 dark:text-white/50">
              {t('growthPlan.roleExperience.alignmentGuide.summary.sourcesDescription')}
            </p>
            <ul className="mt-3 flex flex-wrap gap-2">
              {findingCountBySource.map(({ source, count }) => (
                <li key={source}>
                  <Badge>
                    {t(`growthPlan.roleExperience.sources.${source}`)} · {count}
                  </Badge>
                </li>
              ))}
            </ul>
          </div>
        )}
      </Card>

      <Card className="p-6 sm:p-8">
        <p className="text-sm text-slate-600 dark:text-white/70">
          {t('growthPlan.roleExperience.alignmentGuide.summary.provisionalNotice')}
        </p>

        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <button
            type="button"
            onClick={() => setAreSourcesVisible(true)}
            className="inline-flex min-h-11 items-center justify-center rounded-full border border-slate-200 px-6 py-2.5 text-sm font-medium text-slate-700 transition hover:border-slate-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/70 dark:hover:border-white/20"
          >
            {t('growthPlan.roleExperience.alignmentGuide.summary.reviewSources')}
          </button>
          <button
            type="button"
            onClick={onStartReview}
            className="inline-flex min-h-11 items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            {t('growthPlan.roleExperience.alignmentGuide.summary.startReview')}
          </button>
        </div>
      </Card>
    </div>
  );
}
