import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ShieldCheck, ShieldOff, Ban, BotOff, DatabaseZap } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { PrivacyDetailsModal } from './PrivacyDetailsModal';

const COMMITMENT_ICONS = [ShieldOff, Ban, BotOff, DatabaseZap] as const;
const COMMITMENT_KEYS = ['noSharing', 'noSelling', 'noAiTraining', 'azureSql'] as const;

export function PrivacyPage() {
  const { t } = useTranslation();
  const [modalOpen, setModalOpen] = useState(false);

  return (
    <div className="mx-auto max-w-3xl px-4 py-8 sm:px-6">
      <div className="mb-6 flex items-center gap-3">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
          <ShieldCheck className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">{t('nav.privacyAndData')}</h1>
          <p className="text-sm text-slate-500 dark:text-white/50">{t('privacyPage.heroSubtitle')}</p>
        </div>
      </div>

      <Card className="p-6 sm:p-8">
        <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{t('privacyPage.heroTitle')}</h2>

        <ul className="mt-5 space-y-4">
          {COMMITMENT_KEYS.map((key, i) => {
            const Icon = COMMITMENT_ICONS[i];
            return (
              <li key={key} className="flex items-center gap-3 text-sm text-slate-700 dark:text-white/70">
                <Icon className="h-5 w-5 shrink-0 text-emerald-500" />
                {t(`privacyPage.commitments.${key}`)}
              </li>
            );
          })}
        </ul>

        <button
          type="button"
          onClick={() => setModalOpen(true)}
          className="mt-6 inline-flex items-center justify-center rounded-full bg-slate-900 px-6 py-3 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
        >
          {t('privacyPage.openModalButton')}
        </button>
      </Card>

      {modalOpen && <PrivacyDetailsModal onClose={() => setModalOpen(false)} />}
    </div>
  );
}
