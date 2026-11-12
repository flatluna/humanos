import { useTranslation } from 'react-i18next';
import { Heart, Workflow, ShieldCheck, Cpu, Database, ArrowLeft } from 'lucide-react';
import { SectionCard } from '@/components/enterprise-evolution/SectionCard';

export function FoundationsPage() {
  const { t } = useTranslation();

  return (
    <div className="mx-auto max-w-6xl space-y-8">
      <div>
        <a
          href="/enterprise-evolution"
          className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-400 hover:text-slate-600 dark:text-white/40 dark:hover:text-white/70"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          {t('enterpriseEvolution.foundations.backToHome')}
        </a>
        <p className="mt-3 text-xs font-semibold uppercase tracking-widest text-emerald-600 dark:text-emerald-400">
          {t('enterpriseEvolution.foundations.eyebrow')}
        </p>
        <h1 className="mt-1 text-2xl font-semibold text-slate-900 dark:text-white sm:text-3xl">
          {t('enterpriseEvolution.foundations.title')}
        </h1>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-slate-500 dark:text-white/50">
          {t('enterpriseEvolution.foundations.subtitle')}
        </p>
      </div>

      <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <SectionCard
          title={t('enterpriseEvolution.foundations.cards.culturePrinciples.title')}
          description={t('enterpriseEvolution.foundations.cards.culturePrinciples.description')}
          icon={Heart}
          bar="from-rose-500 to-pink-400"
          chip="bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300"
          href="/enterprise-evolution/foundations/culture-principles"
        />
        <SectionCard
          title={t('enterpriseEvolution.foundations.cards.businessProcesses.title')}
          description={t('enterpriseEvolution.foundations.cards.businessProcesses.description')}
          icon={Workflow}
          bar="from-blue-500 to-sky-400"
          chip="bg-blue-100 text-blue-600 dark:bg-blue-500/15 dark:text-blue-300"
          href="/enterprise-evolution/foundations/business-processes"
        />
        <SectionCard
          title={t('enterpriseEvolution.foundations.cards.policies.title')}
          description={t('enterpriseEvolution.foundations.cards.policies.description')}
          icon={ShieldCheck}
          bar="from-amber-500 to-orange-400"
          chip="bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300"
        />
        <SectionCard
          title={t('enterpriseEvolution.foundations.cards.technologiesApplications.title')}
          description={t('enterpriseEvolution.foundations.cards.technologiesApplications.description')}
          icon={Cpu}
          bar="from-violet-500 to-indigo-400"
          chip="bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300"
        />
        <SectionCard
          title={t('enterpriseEvolution.foundations.cards.referenceCatalogs.title')}
          description={t('enterpriseEvolution.foundations.cards.referenceCatalogs.description')}
          icon={Database}
          bar="from-teal-500 to-emerald-400"
          chip="bg-teal-100 text-teal-600 dark:bg-teal-500/15 dark:text-teal-300"
        />
      </div>
    </div>
  );
}
