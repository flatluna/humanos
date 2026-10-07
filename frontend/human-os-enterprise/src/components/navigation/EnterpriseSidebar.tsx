import { NavLink } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useAuth } from '@/auth/AuthContext';
import { clsx } from 'clsx';
import {
  LayoutDashboard,
  Building2,
  Briefcase,
  GraduationCap,
  ListChecks,
  Award,
  GitBranch,
  BarChart3,
  Sparkles,
  ExternalLink,
  Users,
  UserCircle,
  Target,
  ListTree,
  BookOpen,
  Heart,
} from 'lucide-react';

const NAV_ITEMS_DEF = [
  { to: '/', end: true, icon: LayoutDashboard, labelKey: 'nav.overview', color: 'brand' },
  { to: '/organizacion', end: false, icon: Building2, labelKey: 'nav.organization', color: 'violet' },
  { to: '/departamentos', end: false, icon: Building2, labelKey: 'nav.departments', color: 'indigo' },
  { to: '/objetivos', end: false, icon: Target, labelKey: 'nav.businessGoals', color: 'pink' },
  { to: '/personal', end: true, icon: Users, labelKey: 'nav.people', color: 'sky' },
  { to: '/roles', end: false, icon: Briefcase, labelKey: 'nav.roles', color: 'amber' },
  { to: '/responsibilities', end: false, icon: ListChecks, labelKey: 'nav.responsibilities', color: 'blue' },
  { to: '/certifications', end: false, icon: Award, labelKey: 'nav.certifications', color: 'orange' },
  { to: '/skills', end: false, icon: GraduationCap, labelKey: 'nav.skills', color: 'teal' },
  { to: '/proceso-aprendizaje', end: false, icon: ListTree, labelKey: 'nav.processLearning', color: 'violet' },
  { to: '/programar-cursos', end: false, icon: BookOpen, labelKey: 'nav.learningPrograms', color: 'violet' },
  { to: '/roadmaps', end: false, icon: GitBranch, labelKey: 'nav.roadmaps', color: 'emerald' },
  { to: '/reportes', end: false, icon: BarChart3, labelKey: 'nav.reports', color: 'rose' },
] as const;

const COLOR_STYLES: Record<string, { chip: string; active: string }> = {
  brand: {
    chip: 'bg-brand-100 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300',
    active: 'bg-gradient-to-r from-brand-600 to-brand-500 text-white shadow-md shadow-brand-500/30',
  },
  violet: {
    chip: 'bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300',
    active: 'bg-gradient-to-r from-violet-600 to-violet-500 text-white shadow-md shadow-violet-500/30',
  },
  blue: {
    chip: 'bg-blue-100 text-blue-600 dark:bg-blue-500/15 dark:text-blue-300',
    active: 'bg-gradient-to-r from-blue-600 to-indigo-600 text-white shadow-md shadow-blue-500/30',
  },
  orange: {
    chip: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
    active: 'bg-gradient-to-r from-amber-500 to-orange-500 text-white shadow-md shadow-amber-500/30',
  },
  amber: {
    chip: 'bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300',
    active: 'bg-gradient-to-r from-amber-500 to-amber-400 text-white shadow-md shadow-amber-500/30',
  },
  sky: {
    chip: 'bg-sky-100 text-sky-600 dark:bg-sky-500/15 dark:text-sky-300',
    active: 'bg-gradient-to-r from-sky-600 to-sky-500 text-white shadow-md shadow-sky-500/30',
  },
  emerald: {
    chip: 'bg-emerald-100 text-emerald-600 dark:bg-emerald-500/15 dark:text-emerald-300',
    active: 'bg-gradient-to-r from-emerald-600 to-emerald-500 text-white shadow-md shadow-emerald-500/30',
  },
  rose: {
    chip: 'bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300',
    active: 'bg-gradient-to-r from-rose-600 to-rose-500 text-white shadow-md shadow-rose-500/30',
  },
  pink: {
    chip: 'bg-pink-100 text-pink-600 dark:bg-pink-500/15 dark:text-pink-300',
    active: 'bg-gradient-to-r from-pink-600 to-rose-500 text-white shadow-md shadow-pink-500/30',
  },
  teal: {
    chip: 'bg-teal-100 text-teal-600 dark:bg-teal-500/15 dark:text-teal-300',
    active: 'bg-gradient-to-r from-teal-600 to-teal-500 text-white shadow-md shadow-teal-500/30',
  },
  indigo: {
    chip: 'bg-indigo-100 text-indigo-600 dark:bg-indigo-500/15 dark:text-indigo-300',
    active: 'bg-gradient-to-r from-indigo-600 to-indigo-500 text-white shadow-md shadow-indigo-500/30',
  },
};

const CAPABILITY_STUDIO_URL = 'http://localhost:3010';

export function EnterpriseSidebar() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const profileUrl = user?.personId ? `/personal/${user.personId}` : '/perfil';
  const interestsUrl = user?.personId ? `/personal/${user.personId}/intereses` : '/personal';
  const studioUrl = user
    ? `http://localhost:7071/api/humanos/studio-launch?oid=${encodeURIComponent(user.oid)}&tid=${encodeURIComponent(user.tid)}&email=${encodeURIComponent(user.email)}`
    : CAPABILITY_STUDIO_URL;

  const roleLabel = user?.orgRole
    ? t(`roles.${user.orgRole}` as const, { defaultValue: user.orgRole })
    : t('roles.unknown');

  return (
    <aside className="sticky top-16 hidden h-[calc(100vh-4rem)] w-64 shrink-0 flex-col justify-between overflow-y-auto border-r border-slate-200 bg-white px-3 py-6 md:flex dark:border-white/10 dark:bg-[#05060a]">
      <nav className="space-y-1">
        <p className="px-3 pb-2 text-[11px] font-semibold uppercase tracking-widest text-slate-400 dark:text-white/30">
          {t('nav.hrConsole')}
        </p>
        <ul className="space-y-1">
          {NAV_ITEMS_DEF.map(({ to, end, icon: Icon, labelKey, color }) => (
            <li key={to}>
              <NavLink
                to={to}
                end={end}
                className={({ isActive }) =>
                  clsx(
                    'flex items-center gap-3 rounded-xl px-2.5 py-2 text-sm font-medium transition',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500',
                    isActive
                      ? COLOR_STYLES[color].active
                      : 'text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/5',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <span
                      className={clsx(
                        'flex h-8 w-8 shrink-0 items-center justify-center rounded-lg transition',
                        isActive ? 'bg-white/20 text-white' : COLOR_STYLES[color].chip,
                      )}
                    >
                      <Icon className="h-4 w-4" />
                    </span>
                    <span>{t(labelKey as any)}</span>
                  </>
                )}
              </NavLink>
            </li>
          ))}
          <li>
            <NavLink
              to={interestsUrl}
              className={({ isActive }) =>
                clsx(
                  'flex items-center gap-3 rounded-xl px-2.5 py-2 text-sm font-medium transition',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500',
                  isActive
                    ? COLOR_STYLES.pink.active
                    : 'text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/5',
                )
              }
            >
              {({ isActive }) => (
                <>
                  <span
                    className={clsx(
                      'flex h-8 w-8 shrink-0 items-center justify-center rounded-lg transition',
                      isActive ? 'bg-white/20 text-white' : COLOR_STYLES.pink.chip,
                    )}
                  >
                    <Heart className="h-4 w-4" />
                  </span>
                  <span>Intereses, Gustos y Preferencias</span>
                </>
              )}
            </NavLink>
          </li>
        </ul>

        <p className="px-3 pt-6 pb-2 text-[11px] font-semibold uppercase tracking-widest text-slate-400 dark:text-white/30">
          {t('nav.myAccount')}
        </p>
        <ul className="space-y-1">
          <li>
            <NavLink
              to={profileUrl}
              className={({ isActive }) =>
                clsx(
                  'flex items-center gap-3 rounded-xl px-2.5 py-2 text-sm font-medium transition',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500',
                  isActive
                    ? COLOR_STYLES.indigo.active
                    : 'text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/5',
                )
              }
            >
              {({ isActive }) => (
                <>
                  <span
                    className={clsx(
                      'flex h-8 w-8 shrink-0 items-center justify-center rounded-lg transition',
                      isActive ? 'bg-white/20 text-white' : COLOR_STYLES.indigo.chip,
                    )}
                  >
                    <UserCircle className="h-4 w-4" />
                  </span>
                  <span className="flex-1 truncate">{t('nav.myProfile')}</span>
                </>
              )}
            </NavLink>
          </li>
        </ul>

        <p className="px-3 pt-6 pb-2 text-[11px] font-semibold uppercase tracking-widest text-slate-400 dark:text-white/30">
          {t('nav.contentCreation')}
        </p>
        <a
          href={studioUrl}
          target="_blank"
          rel="noreferrer"
          className="flex items-center gap-3 rounded-xl px-2.5 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/5"
        >
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-brand-500 to-accent-500 text-white shadow-sm shadow-accent-500/30">
            <Sparkles className="h-4 w-4" />
          </span>
          <span className="flex-1">{t('nav.capabilityStudio')}</span>
          <ExternalLink className="h-3.5 w-3.5 shrink-0 opacity-60" />
        </a>
      </nav>

      <div className="space-y-3 pt-4">
        {user ? (
          <NavLink
            to={profileUrl}
            className={({ isActive }) =>
              clsx(
                'group flex items-center gap-3 rounded-2xl border p-2.5 transition',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500',
                isActive
                  ? 'border-brand-500/40 bg-brand-50/80 shadow-md shadow-brand-500/10 dark:border-brand-400/30 dark:bg-brand-500/15'
                  : 'border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-white/20 dark:hover:bg-white/[0.06]',
              )
            }
          >
            {({ isActive }) => (
              <>
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-brand-500 to-accent-500 text-sm font-bold text-white shadow-sm shadow-brand-500/20">
                  {(user.name || 'U').slice(0, 1).toUpperCase()}
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-1.5">
                    <p className="truncate text-xs font-semibold text-slate-900 dark:text-white">
                      {user.name}
                    </p>
                  </div>
                  <p className="truncate text-[11px] text-slate-500 dark:text-white/50">
                    {roleLabel}
                  </p>
                </div>
                <UserCircle
                  className={clsx(
                    'h-4 w-4 shrink-0 transition',
                    isActive
                      ? 'text-brand-600 dark:text-brand-400'
                      : 'text-slate-400 group-hover:text-slate-600 dark:text-white/40 dark:group-hover:text-white',
                  )}
                />
              </>
            )}
          </NavLink>
        ) : null}

        <div className="overflow-hidden rounded-xl border border-slate-200 bg-gradient-to-br from-slate-50 to-white p-3 text-xs text-slate-500 dark:border-white/10 dark:from-white/5 dark:to-white/[0.02] dark:text-white/50">
          <p className="font-semibold text-slate-700 dark:text-white/70">{t('nav.footerTitle')}</p>
          <p className="mt-1">{t('nav.footerSubtitle')}</p>
        </div>
      </div>
    </aside>
  );
}
