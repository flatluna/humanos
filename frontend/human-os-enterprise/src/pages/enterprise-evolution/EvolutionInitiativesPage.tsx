import { useTranslation } from 'react-i18next';
import {
  ArrowLeft,
  Plus,
  GitBranch,
  MessageSquareQuote,
  Heart,
  Briefcase,
  Building2,
  GraduationCap,
  HeartHandshake,
  UserCheck,
  ListChecks,
  BarChart3,
} from 'lucide-react';

const DIMENSION_ICONS = {
  changeNarrative: MessageSquareQuote,
  culture: Heart,
  projectPortfolio: Briefcase,
  organizationImpact: Building2,
  capabilityBuilding: GraduationCap,
  motivationMentorCoach: HeartHandshake,
  capabilityTutor: UserCheck,
  actions: ListChecks,
  dashboard: BarChart3,
} as const;

export function EvolutionInitiativesPage() {
  const { t } = useTranslation();
  const dimensionKeys = Object.keys(DIMENSION_ICONS) as (keyof typeof DIMENSION_ICONS)[];

  return (
    <div className="mx-auto max-w-6xl space-y-8">
      <div>
        <a
          href="/enterprise-evolution"
          className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-400 hover:text-slate-600 dark:text-white/40 dark:hover:text-white/70"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          {t('enterpriseEvolution.home.title')}
        </a>
        <p className="mt-3 text-xs font-semibold uppercase tracking-widest text-indigo-600 dark:text-indigo-400">
          {t('enterpriseEvolution.initiatives.eyebrow')}
        </p>
        <h1 className="mt-1 text-2xl font-semibold text-slate-900 dark:text-white sm:text-3xl">
          {t('enterpriseEvolution.initiatives.title')}
        </h1>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-slate-500 dark:text-white/50">
          {t('enterpriseEvolution.initiatives.subtitle')}
        </p>
      </div>

      <div className="rounded-3xl border border-dashed border-slate-200 bg-gradient-to-br from-indigo-50/60 to-white p-8 text-center dark:border-white/10 dark:from-indigo-500/5 dark:to-white/[0.02]">
        <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-indigo-100 text-indigo-600 dark:bg-indigo-500/15 dark:text-indigo-300">
          <GitBranch className="h-6 w-6" />
        </span>
        <h2 className="mt-4 text-sm font-semibold text-slate-900 dark:text-white">
          {t('enterpriseEvolution.initiatives.emptyTitle')}
        </h2>
        <p className="mx-auto mt-2 max-w-md text-xs text-slate-500 dark:text-white/50">
          {t('enterpriseEvolution.initiatives.emptyDescription')}
        </p>
        <button
          type="button"
          disabled
          className="mx-auto mt-5 inline-flex cursor-not-allowed items-center gap-1.5 rounded-full bg-gradient-to-r from-indigo-600 to-violet-500 px-4 py-2 text-xs font-semibold text-white opacity-60 shadow-md shadow-indigo-500/20"
        >
          <Plus className="h-4 w-4" />
          {t('enterpriseEvolution.initiatives.newInitiative')}
        </button>
      </div>

      <div>
        <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
          {t('enterpriseEvolution.initiatives.dimensionsTitle')}
        </h3>
        <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
          {dimensionKeys.map((key) => {
            const Icon = DIMENSION_ICONS[key];
            return (
              <div
                key={key}
                className="flex items-start gap-3 rounded-2xl border border-slate-200 bg-white p-4 opacity-80 dark:border-white/10 dark:bg-white/5"
              >
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-slate-100 text-slate-500 dark:bg-white/10 dark:text-white/50">
                  <Icon className="h-4 w-4" />
                </span>
                <div className="min-w-0">
                  <p className="text-sm font-medium text-slate-900 dark:text-white">
                    {t(`enterpriseEvolution.initiatives.dimensions.${key}.title`)}
                  </p>
                  <p className="mt-0.5 text-xs text-slate-500 dark:text-white/40">
                    {t(`enterpriseEvolution.initiatives.dimensions.${key}.description`)}
                  </p>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
