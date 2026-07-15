import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { ArrowRight, Target, Building2 } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { useLocalizedText } from '@/localization/useLocalizedText';
import type { LocalizedText, OrganizationInitiative, PersonalGoal } from '../types';

interface GrowthAlignmentProps {
  futureSelf: LocalizedText;
  motivations: LocalizedText[];
  personalGoal: PersonalGoal;
  organizationInitiative: OrganizationInitiative | null;
  sharedCapabilities: LocalizedText[];
}

export function GrowthAlignment({
  futureSelf,
  motivations,
  personalGoal,
  organizationInitiative,
  sharedCapabilities,
}: GrowthAlignmentProps) {
  const { t } = useTranslation();
  const localize = useLocalizedText();
  const hasOrganization = organizationInitiative !== null;

  return (
    <section aria-labelledby="alignment-heading" className="mx-auto max-w-6xl px-6 pt-10">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        whileInView={{ opacity: 1, y: 0 }}
        viewport={{ once: true, amount: 0.3 }}
        transition={{ duration: 0.6 }}
      >
        <Card className="p-8 sm:p-10">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div>
              <p className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
                {t('alignment.becoming')}
              </p>
              <p className="mt-1 text-xl font-semibold text-slate-900 dark:text-white">{localize(futureSelf)}</p>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
                {t('alignment.drivenBy')}
              </span>
              {motivations.map((m) => (
                <Badge key={localize(m)}>{localize(m)}</Badge>
              ))}
            </div>
          </div>

          <h2 id="alignment-heading" className="mt-8 text-2xl font-semibold text-slate-900 dark:text-white">
            {t('alignment.sectionTitle')}
          </h2>

          <div
            className={
              hasOrganization
                ? 'mt-6 grid items-center gap-6 md:grid-cols-[1fr_auto_1fr]'
                : 'mt-6 grid items-center gap-6'
            }
          >
            <div className="rounded-2xl border border-slate-200 bg-slate-50 p-6 dark:border-white/10 dark:bg-white/[0.03]">
              <div className="flex items-center gap-2 text-slate-400 dark:text-white/40">
                <Target className="h-4 w-4" />
                <span className="text-xs font-semibold uppercase tracking-widest">{t('alignment.personalGoal')}</span>
              </div>
              <p className="mt-2 text-lg font-semibold text-slate-900 dark:text-white">
                {localize(personalGoal.title)}
              </p>
            </div>

            {hasOrganization && (
              <div className="flex items-center justify-center gap-2 text-slate-300 dark:text-white/20">
                <ArrowRight className="hidden h-5 w-5 md:block" />
                <div className="flex max-w-[180px] flex-col items-center gap-1">
                  <span className="text-center text-[10px] font-semibold uppercase tracking-widest">
                    {t('alignment.sharedCapabilities')}
                  </span>
                  <div className="flex flex-wrap justify-center gap-1.5">
                    {sharedCapabilities.map((c) => (
                      <Badge key={localize(c)} tone="accent">
                        {localize(c)}
                      </Badge>
                    ))}
                  </div>
                </div>
                <ArrowRight className="hidden h-5 w-5 md:block" />
              </div>
            )}

            {hasOrganization && organizationInitiative && (
              <div className="rounded-2xl border border-slate-200 bg-slate-50 p-6 dark:border-white/10 dark:bg-white/[0.03]">
                <div className="flex items-center gap-2 text-slate-400 dark:text-white/40">
                  <Building2 className="h-4 w-4" />
                  <span className="text-xs font-semibold uppercase tracking-widest">
                    {t('alignment.organizationAlignment')}
                  </span>
                </div>
                <p className="mt-2 text-lg font-semibold text-slate-900 dark:text-white">
                  {localize(organizationInitiative.title)}
                </p>
              </div>
            )}
          </div>

          {!hasOrganization && (
            <div className="mt-6 flex flex-wrap gap-1.5">
              {sharedCapabilities.map((c) => (
                <Badge key={localize(c)} tone="accent">
                  {localize(c)}
                </Badge>
              ))}
            </div>
          )}

          <p className="mt-6 text-sm text-slate-500 dark:text-white/50">
            {hasOrganization ? t('alignment.helpsBothMessage') : t('alignment.helpsYouMessage')}
          </p>

          <button
            type="button"
            className="mt-4 text-sm font-medium text-blue-600 transition hover:text-blue-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:text-blue-300 dark:hover:text-blue-200"
          >
            {t('alignment.seeAllGoals')}
          </button>
        </Card>
      </motion.div>
    </section>
  );
}
