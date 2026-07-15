import { useTranslation } from 'react-i18next';
import { ShieldCheck } from 'lucide-react';

/** Concise, non-legalistic privacy notice — a single sentence with a
 *  shield icon, not a large disclaimer block.
 */
export function PrivacyNotice() {
  const { t } = useTranslation();

  return (
    <div className="flex items-start gap-2.5 rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-500 dark:bg-white/[0.03] dark:text-white/50">
      <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-slate-400 dark:text-white/40" />
      <p>{t('growthPlan.workContext.privacyNotice')}</p>
    </div>
  );
}
