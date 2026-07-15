import { useTranslation } from 'react-i18next';
import { Sparkles, type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import type { Translations } from '@/locales/types';

type NavLabelKey = keyof Translations['nav'];

interface ComingSoonPageProps {
  titleKey: NavLabelKey;
  icon?: LucideIcon;
}

/** Minimal, on-brand placeholder for routes reserved in the information
 *  architecture but not yet designed/implemented.
 */
export function ComingSoonPage({ titleKey, icon: Icon = Sparkles }: ComingSoonPageProps) {
  const { t } = useTranslation();

  return (
    <div className="mx-auto max-w-3xl px-6 py-16">
      <Card className="flex flex-col items-center gap-4 p-12 text-center">
        <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
          <Icon className="h-6 w-6" />
        </div>
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">{t(`nav.${titleKey}`)}</h1>
        <p className="max-w-sm text-slate-500 dark:text-white/50">{t('common.comingSoon')}</p>
      </Card>
    </div>
  );
}
