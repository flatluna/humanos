import { useTranslation } from 'react-i18next';
import { Target, ListChecks, Flag, Building2 } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { useLocalizedText } from '@/localization/useLocalizedText';
import type { JobDescription } from '../types';

interface JobDescriptionSummaryProps {
  jobDescription: JobDescription;
  onReflectsMyRole: () => void;
  onWorkIsDifferent: () => void;
}

export function JobDescriptionSummary({
  jobDescription,
  onReflectsMyRole,
  onWorkIsDifferent,
}: JobDescriptionSummaryProps) {
  const { t } = useTranslation();
  const localize = useLocalizedText();

  return (
    <Card className="p-6 sm:p-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
            {t('growthPlan.workContext.fields.currentRole')}
          </p>
          <h2 className="mt-1 text-xl font-semibold text-slate-900 dark:text-white">
            {localize(jobDescription.jobTitle)}
          </h2>
        </div>

        <Badge tone={jobDescription.source === 'organization' ? 'accent' : 'neutral'}>
          {jobDescription.source === 'organization'
            ? t('growthPlan.roleExperience.sources.organization')
            : t('growthPlan.roleExperience.missingJobDescription.employeeProvidedLabel')}
        </Badge>
      </div>

      <div className="mt-6 space-y-6">
        <section>
          <div className="flex items-center gap-2 text-slate-400 dark:text-white/40">
            <Target className="h-4 w-4" />
            <h3 className="text-xs font-semibold uppercase tracking-widest">
              {t('growthPlan.roleExperience.jobDescription.rolePurposeLabel')}
            </h3>
          </div>
          <p className="mt-2 text-sm text-slate-700 dark:text-white/80">{localize(jobDescription.rolePurpose)}</p>
        </section>

        {jobDescription.primaryResponsibilities.length > 0 && (
          <section>
            <div className="flex items-center gap-2 text-slate-400 dark:text-white/40">
              <ListChecks className="h-4 w-4" />
              <h3 className="text-xs font-semibold uppercase tracking-widest">
                {t('growthPlan.roleExperience.jobDescription.primaryResponsibilitiesLabel')}
              </h3>
            </div>
            <ul className="mt-2 space-y-1.5">
              {jobDescription.primaryResponsibilities.map((responsibility) => (
                <li key={responsibility.id} className="flex gap-2 text-sm text-slate-700 dark:text-white/80">
                  <span aria-hidden="true" className="text-slate-300 dark:text-white/20">
                    •
                  </span>
                  {localize(responsibility.text)}
                </li>
              ))}
            </ul>
          </section>
        )}

        {jobDescription.expectedOutcomes.length > 0 && (
          <section>
            <div className="flex items-center gap-2 text-slate-400 dark:text-white/40">
              <Flag className="h-4 w-4" />
              <h3 className="text-xs font-semibold uppercase tracking-widest">
                {t('growthPlan.roleExperience.jobDescription.expectedOutcomesLabel')}
              </h3>
            </div>
            <ul className="mt-2 space-y-1.5">
              {jobDescription.expectedOutcomes.map((outcome) => (
                <li key={outcome.id} className="flex gap-2 text-sm text-slate-700 dark:text-white/80">
                  <span aria-hidden="true" className="text-slate-300 dark:text-white/20">
                    •
                  </span>
                  {localize(outcome.text)}
                </li>
              ))}
            </ul>
          </section>
        )}

        {jobDescription.coreCapabilities.length > 0 && (
          <section>
            <div className="flex items-center gap-2 text-slate-400 dark:text-white/40">
              <Building2 className="h-4 w-4" />
              <h3 className="text-xs font-semibold uppercase tracking-widest">
                {t('growthPlan.roleExperience.jobDescription.organizationRequirementsLabel')}
              </h3>
            </div>
            <div className="mt-2 flex flex-wrap gap-1.5">
              {jobDescription.coreCapabilities.map((capability) => (
                <Badge key={localize(capability)}>{localize(capability)}</Badge>
              ))}
            </div>
          </section>
        )}
      </div>

      <div className="mt-6 flex flex-col gap-3 border-t border-slate-100 pt-5 sm:flex-row dark:border-white/10">
        <button
          type="button"
          onClick={onReflectsMyRole}
          className="inline-flex min-h-11 items-center justify-center rounded-full bg-slate-900 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
        >
          {t('growthPlan.roleExperience.jobDescription.reflectsMyRole')}
        </button>
        <button
          type="button"
          onClick={onWorkIsDifferent}
          className="inline-flex min-h-11 items-center justify-center rounded-full border border-slate-200 px-5 py-2.5 text-sm font-medium text-slate-600 transition hover:border-slate-300 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/70 dark:hover:border-white/20 dark:hover:text-white"
        >
          {t('growthPlan.roleExperience.jobDescription.workIsDifferent')}
        </button>
      </div>
    </Card>
  );
}
