import { useTranslation } from 'react-i18next';
import { Library, GitBranch, Users2, Rocket } from 'lucide-react';
import { SectionCard } from '@/components/enterprise-evolution/SectionCard';

export function EnterpriseEvolutionPage() {
  const { t } = useTranslation();

  return (
    <div className="mx-auto max-w-6xl space-y-10">
      <div className="relative overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-indigo-600 via-fuchsia-600 to-rose-500 p-8 text-white shadow-xl shadow-fuchsia-500/20 dark:border-white/10">
        <span className="absolute -right-10 -top-10 h-56 w-56 rounded-full bg-white/10 blur-3xl" />
        <span className="absolute -bottom-16 left-10 h-56 w-56 rounded-full bg-white/10 blur-3xl" />
        <div className="relative flex items-center gap-2 text-xs font-semibold uppercase tracking-widest text-white/80">
          <Rocket className="h-4 w-4" />
          {t('enterpriseEvolution.home.eyebrow')}
        </div>
        <h1 className="relative mt-3 text-3xl font-bold sm:text-4xl">{t('enterpriseEvolution.home.title')}</h1>
        <p className="relative mt-3 max-w-3xl text-sm leading-relaxed text-white/85 sm:text-base">
          {t('enterpriseEvolution.home.subtitle')}
        </p>
      </div>

      <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
        <SectionCard
          title={t('enterpriseEvolution.home.sections.foundations.title')}
          subtitle={t('enterpriseEvolution.home.sections.foundations.subtitle')}
          description={t('enterpriseEvolution.home.sections.foundations.description')}
          icon={Library}
          bar="from-emerald-500 to-teal-400"
          chip="bg-emerald-100 text-emerald-600 dark:bg-emerald-500/15 dark:text-emerald-300"
          href="/enterprise-evolution/foundations"
        />
        <SectionCard
          title={t('enterpriseEvolution.home.sections.initiatives.title')}
          subtitle={t('enterpriseEvolution.home.sections.initiatives.subtitle')}
          description={t('enterpriseEvolution.home.sections.initiatives.description')}
          icon={GitBranch}
          bar="from-indigo-500 to-violet-400"
          chip="bg-indigo-100 text-indigo-600 dark:bg-indigo-500/15 dark:text-indigo-300"
          href="/enterprise-evolution/initiatives"
        />
        <SectionCard
          title={t('enterpriseEvolution.home.sections.humanEvolution.title')}
          subtitle={t('enterpriseEvolution.home.sections.humanEvolution.subtitle')}
          description={t('enterpriseEvolution.home.sections.humanEvolution.description')}
          icon={Users2}
          bar="from-rose-500 to-amber-400"
          chip="bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300"
          href="/enterprise-evolution/human-evolution"
        />
      </div>
    </div>
  );
}
