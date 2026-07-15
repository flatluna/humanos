import { useTranslation } from 'react-i18next';
import { CheckCircle2, Building2 } from 'lucide-react';
import type { WorkContext } from '../types';

interface InformationSourceNoticeProps {
  workContext: WorkContext;
}

/** Visually secondary caption showing where this information came from,
 *  when it was last synchronized, and its verification status — kept
 *  small and calm so the screen never feels like employee surveillance.
 */
export function InformationSourceNotice({ workContext }: InformationSourceNoticeProps) {
  const { t, i18n } = useTranslation();

  const sourceLabel =
    workContext.dataSource === 'organization'
      ? t('growthPlan.workContext.source.providedByOrganization')
      : t('growthPlan.workContext.source.syncedFromProfile');

  const formattedDate = workContext.lastSynchronizedDate
    ? new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(
        new Date(workContext.lastSynchronizedDate),
      )
    : null;

  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-400 dark:text-white/40">
      <span className="inline-flex items-center gap-1.5">
        <Building2 className="h-3.5 w-3.5" />
        {sourceLabel}
      </span>

      {formattedDate && (
        <span>{t('growthPlan.workContext.source.lastSynchronized', { date: formattedDate })}</span>
      )}

      {workContext.verificationStatus === 'verified' && (
        <span className="inline-flex items-center gap-1.5 text-emerald-600 dark:text-emerald-400">
          <CheckCircle2 className="h-3.5 w-3.5" />
          {t('growthPlan.workContext.source.verified')}
        </span>
      )}
    </div>
  );
}
