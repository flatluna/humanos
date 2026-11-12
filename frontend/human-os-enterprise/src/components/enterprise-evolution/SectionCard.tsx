import type { ComponentType } from 'react';
import { useTranslation } from 'react-i18next';
import { ArrowRight, Lock } from 'lucide-react';

export interface SectionCardProps {
  title: string;
  subtitle?: string;
  description: string;
  icon: ComponentType<{ className?: string }>;
  bar: string;
  chip: string;
  href?: string;
  comingSoon?: boolean;
}

/**
 * Large, professional landing card used across the Enterprise Evolution module
 * (home, Foundations, Evolution Initiatives, Human Evolution). Active cards link
 * to their destination; cards without a href render as a muted "Coming soon" tile.
 */
export function SectionCard({ title, subtitle, description, icon: Icon, bar, chip, href, comingSoon }: SectionCardProps) {
  const { t } = useTranslation();
  const isDisabled = !href || comingSoon;

  const content = (
    <>
      <span className={`absolute inset-x-0 top-0 h-1.5 bg-gradient-to-r ${bar}`} />
      <div className="flex items-start justify-between">
        <span className={`flex h-12 w-12 items-center justify-center rounded-2xl ${chip}`}>
          <Icon className="h-6 w-6" />
        </span>
        {isDisabled ? (
          <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wide text-slate-400 dark:bg-white/5 dark:text-white/30">
            <Lock className="h-3 w-3" />
            {t('enterpriseEvolution.comingSoon.badge')}
          </span>
        ) : (
          <ArrowRight className="h-5 w-5 text-slate-300 transition group-hover:translate-x-1 group-hover:text-slate-500 dark:text-white/20 dark:group-hover:text-white/50" />
        )}
      </div>

      <div className="mt-5">
        <h3 className="text-lg font-semibold text-slate-900 dark:text-white">{title}</h3>
        {subtitle && <p className="mt-0.5 text-xs font-medium uppercase tracking-wide text-slate-400 dark:text-white/30">{subtitle}</p>}
        <p className="mt-2 text-sm leading-relaxed text-slate-500 dark:text-white/50">{description}</p>
      </div>
    </>
  );

  const baseClass = 'group relative block overflow-hidden rounded-3xl border p-6 transition';

  if (isDisabled) {
    return (
      <div className={`${baseClass} cursor-not-allowed border-slate-200 bg-slate-50/60 dark:border-white/5 dark:bg-white/[0.02]`}>
        {content}
      </div>
    );
  }

  return (
    <a
      href={href}
      className={`${baseClass} border-slate-200 bg-white hover:-translate-y-1 hover:shadow-xl hover:shadow-slate-200/60 dark:border-white/10 dark:bg-white/5 dark:hover:shadow-none`}
    >
      {content}
    </a>
  );
}
