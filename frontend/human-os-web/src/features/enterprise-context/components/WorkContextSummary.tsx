import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/Card';
import { WorkContextField } from './WorkContextField';
import type { WorkContext, EmploymentType } from '../types';

const EMPLOYMENT_TYPE_LABEL_KEY: Record<EmploymentType, string> = {
  fullTime: 'growthPlan.workContext.employmentTypes.fullTime',
  partTime: 'growthPlan.workContext.employmentTypes.partTime',
  contractor: 'growthPlan.workContext.employmentTypes.contractor',
  intern: 'growthPlan.workContext.employmentTypes.intern',
};

interface WorkContextSummaryProps {
  workContext: WorkContext;
}

/** One cohesive card presenting the employee's organizational context —
 *  never one card per field. Current Role is the most visually
 *  prominent value; fields with no value are simply not rendered.
 */
export function WorkContextSummary({ workContext }: WorkContextSummaryProps) {
  const { t } = useTranslation();

  const secondaryFields: Array<{ label: string; value: string | null }> = [
    { label: t('growthPlan.workContext.fields.organization'), value: workContext.organization },
    { label: t('growthPlan.workContext.fields.department'), value: workContext.department },
    { label: t('growthPlan.workContext.fields.businessUnit'), value: workContext.businessUnit },
    { label: t('growthPlan.workContext.fields.team'), value: workContext.team },
    { label: t('growthPlan.workContext.fields.roleLevel'), value: workContext.roleLevel },
    { label: t('growthPlan.workContext.fields.jobFamily'), value: workContext.jobFamily },
    { label: t('growthPlan.workContext.fields.workLocation'), value: workContext.workLocation },
    {
      label: t('growthPlan.workContext.fields.employmentType'),
      value: workContext.employmentType ? t(EMPLOYMENT_TYPE_LABEL_KEY[workContext.employmentType]) : null,
    },
    { label: t('growthPlan.workContext.fields.preferredLanguage'), value: workContext.preferredLanguage },
  ].filter((field) => Boolean(field.value));

  return (
    <Card className="p-6 sm:p-8">
      {workContext.currentRole && (
        <div className="mb-6 border-b border-slate-100 pb-6 dark:border-white/10">
          <WorkContextField
            label={t('growthPlan.workContext.fields.currentRole')}
            value={workContext.currentRole}
            prominent
          />
        </div>
      )}

      <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2">
        {secondaryFields.map((field) => (
          <WorkContextField key={field.label} label={field.label} value={field.value as string} />
        ))}
      </dl>

      {workContext.manager && (
        <div className="mt-6 border-t border-slate-100 pt-5 dark:border-white/10">
          <WorkContextField label={t('growthPlan.workContext.fields.manager')} value={workContext.manager} />
        </div>
      )}
    </Card>
  );
}
