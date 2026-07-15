import { useTranslation } from 'react-i18next';
import { UserCircle2, Upload, PenLine, Sparkles, SkipForward, type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui/Card';

interface ProfessionalProfileOptionsProps {
  onSelectUpload: () => void;
  onSkip: () => void;
}

interface OptionDefinition {
  key: 'useExistingProfile' | 'uploadResume' | 'addManually' | 'buildWithAgent' | 'skipForNow';
  icon: LucideIcon;
  enabled: boolean;
}

const OPTIONS: OptionDefinition[] = [
  { key: 'useExistingProfile', icon: UserCircle2, enabled: false },
  { key: 'uploadResume', icon: Upload, enabled: true },
  { key: 'addManually', icon: PenLine, enabled: false },
  { key: 'buildWithAgent', icon: Sparkles, enabled: false },
  { key: 'skipForNow', icon: SkipForward, enabled: true },
];

/** The five ways an employee can help Human OS understand their
 *  professional experience. Only "Upload Résumé" and "Skip for Now" are
 *  wired up in this increment — the rest are visible (for information
 *  architecture completeness) but intentionally disabled until built.
 */
export function ProfessionalProfileOptions({ onSelectUpload, onSkip }: ProfessionalProfileOptionsProps) {
  const { t } = useTranslation();

  function handleClick(key: OptionDefinition['key']) {
    if (key === 'uploadResume') {
      onSelectUpload();
    } else if (key === 'skipForNow') {
      onSkip();
    }
  }

  return (
    <Card className="p-6 sm:p-8">
      <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
        {t('growthPlan.roleExperience.professionalProfile.sectionTitle')}
      </h2>

      <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-2">
        {OPTIONS.map(({ key, icon: Icon, enabled }) => (
          <button
            key={key}
            type="button"
            onClick={() => handleClick(key)}
            disabled={!enabled}
            aria-disabled={!enabled}
            className="flex min-h-11 items-center gap-3 rounded-xl border border-slate-200 px-4 py-3 text-left text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-slate-200 disabled:hover:bg-transparent dark:border-white/10 dark:text-white/80 dark:hover:border-white/20 dark:hover:bg-white/5"
          >
            <Icon className="h-4 w-4 shrink-0 text-slate-400 dark:text-white/40" />
            {t(`growthPlan.roleExperience.professionalProfile.${key}`)}
          </button>
        ))}
      </div>
    </Card>
  );
}
