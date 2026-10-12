import { NavLink } from 'react-router';
import { clsx } from 'clsx';
import {
  LayoutDashboard,
  Users,
  Compass,
  Layers,
  GraduationCap,
  LineChart,
  Settings,
  Sparkles,
  HeartHandshake,
} from 'lucide-react';
import { useLanguage } from '../../context/LanguageContext';
import { useFamily } from '../../context/FamilyContext';

export const FAMILY_NAV_ITEMS = [
  {
    to: '/',
    end: true,
    icon: LayoutDashboard,
    labelKey: 'nav.overview',
    accentColor: 'emerald',
    badge: null,
  },
  {
    to: '/family',
    end: false,
    icon: Users,
    labelKey: 'nav.family',
    accentColor: 'indigo',
    badge: '5 miembros',
  },
  {
    to: '/personal-development',
    end: false,
    icon: Compass,
    labelKey: 'nav.personalDev',
    accentColor: 'rose',
    badge: 'Bienestar',
  },
  {
    to: '/capabilities',
    end: false,
    icon: Layers,
    labelKey: 'nav.capabilities',
    accentColor: 'violet',
    badge: 'K-12+ Skills',
  },
  {
    to: '/learning',
    end: false,
    icon: GraduationCap,
    labelKey: 'nav.learning',
    accentColor: 'amber',
    badge: 'SAT / IB',
  },
  {
    to: '/insights',
    end: false,
    icon: LineChart,
    labelKey: 'nav.insights',
    accentColor: 'teal',
    badge: 'Analítica',
  },
  {
    to: '/admin',
    end: false,
    icon: Settings,
    labelKey: 'nav.admin',
    accentColor: 'slate',
    badge: null,
  },
] as const;

const ACCENT_STYLES: Record<string, { active: string; chip: string; dot: string }> = {
  emerald: {
    active: 'bg-gradient-to-r from-emerald-600 to-teal-600 text-white shadow-md shadow-emerald-600/30 font-semibold',
    chip: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300',
    dot: 'bg-emerald-500',
  },
  indigo: {
    active: 'bg-gradient-to-r from-indigo-600 to-violet-600 text-white shadow-md shadow-indigo-600/30 font-semibold',
    chip: 'bg-indigo-100 text-indigo-700 dark:bg-indigo-500/20 dark:text-indigo-300',
    dot: 'bg-indigo-500',
  },
  violet: {
    active: 'bg-gradient-to-r from-violet-600 to-purple-600 text-white shadow-md shadow-violet-600/30 font-semibold',
    chip: 'bg-violet-100 text-violet-700 dark:bg-violet-500/20 dark:text-violet-300',
    dot: 'bg-violet-500',
  },
  rose: {
    active: 'bg-gradient-to-r from-rose-600 to-pink-600 text-white shadow-md shadow-rose-600/30 font-semibold',
    chip: 'bg-rose-100 text-rose-700 dark:bg-rose-500/20 dark:text-rose-300',
    dot: 'bg-rose-500',
  },
  amber: {
    active: 'bg-gradient-to-r from-amber-500 to-orange-500 text-white shadow-md shadow-amber-500/30 font-semibold',
    chip: 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300',
    dot: 'bg-amber-500',
  },
  teal: {
    active: 'bg-gradient-to-r from-teal-600 to-cyan-600 text-white shadow-md shadow-teal-600/30 font-semibold',
    chip: 'bg-teal-100 text-teal-700 dark:bg-teal-500/20 dark:text-teal-300',
    dot: 'bg-teal-500',
  },
  slate: {
    active: 'bg-gradient-to-r from-warm-700 to-warm-900 text-white shadow-md shadow-warm-800/30 font-semibold',
    chip: 'bg-warm-200 text-warm-700 dark:bg-white/10 dark:text-warm-300',
    dot: 'bg-warm-500',
  },
};

export function FamilySidebar() {
  const { t, lang } = useLanguage();
  const { members, selectedMemberId, setSelectedMemberId, setIsAiCoachOpen } = useFamily();

  return (
    <aside className="sticky top-20 hidden h-[calc(100vh-5rem)] w-72 shrink-0 flex-col justify-between overflow-y-auto border-r border-warm-200/80 bg-white/70 px-4 py-6 backdrop-blur-sm lg:flex dark:border-white/10 dark:bg-[#0b0f17]/70">
      <div className="space-y-6">
        {/* Member Focus Filter Bar */}
        <div className="rounded-2xl border border-warm-200/80 bg-warm-50/70 p-3 dark:border-white/10 dark:bg-white/5">
          <div className="flex items-center justify-between pb-2">
            <span className="text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
              {t('nav.switchMember')}
            </span>
            {selectedMemberId && (
              <button
                type="button"
                onClick={() => setSelectedMemberId(null)}
                className="text-[11px] font-semibold text-emerald-600 hover:underline dark:text-emerald-400"
              >
                {lang === 'es' ? 'Mostrar todos' : 'Show all'}
              </button>
            )}
          </div>

          <div className="flex flex-wrap gap-1.5">
            <button
              type="button"
              onClick={() => setSelectedMemberId(null)}
              className={clsx(
                'rounded-lg px-2.5 py-1 text-xs font-medium transition',
                selectedMemberId === null
                  ? 'bg-emerald-600 text-white shadow-sm font-semibold'
                  : 'bg-white text-warm-700 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-300 dark:hover:bg-warm-700',
              )}
            >
              {lang === 'es' ? 'Todos (5)' : 'All (5)'}
            </button>

            {members.map((m) => (
              <button
                key={m.id}
                type="button"
                onClick={() => setSelectedMemberId(m.id)}
                className={clsx(
                  'flex items-center gap-1.5 rounded-lg px-2 py-1 text-xs font-medium transition',
                  selectedMemberId === m.id
                    ? 'bg-emerald-600 text-white shadow-sm font-semibold'
                    : 'bg-white text-warm-700 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-300 dark:hover:bg-warm-700',
                )}
              >
                <span className={`flex h-4 w-4 items-center justify-center rounded-full bg-gradient-to-br ${m.avatarBg} text-[9px] font-bold text-white`}>
                  {m.initial}
                </span>
                <span>{m.name.split(' ')[0]}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Main Navigation (Exact required list) */}
        <nav className="space-y-1">
          <p className="px-3 pb-2 text-[11px] font-bold uppercase tracking-widest text-warm-400 dark:text-warm-500">
            {t('nav.sectionTitle')}
          </p>

          <ul className="space-y-1.5">
            {FAMILY_NAV_ITEMS.map(({ to, end, icon: Icon, labelKey, accentColor, badge }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    clsx(
                      'group flex items-center justify-between rounded-xl px-3 py-2.5 text-sm font-medium transition-all duration-200',
                      isActive
                        ? ACCENT_STYLES[accentColor].active
                        : 'text-warm-700 hover:bg-warm-100/80 hover:text-warm-900 dark:text-warm-300 dark:hover:bg-white/5 dark:hover:text-white',
                    )
                  }
                >
                  {({ isActive }) => (
                    <>
                      <div className="flex items-center gap-3">
                        <span
                          className={clsx(
                            'flex h-8 w-8 shrink-0 items-center justify-center rounded-xl transition',
                            isActive ? 'bg-white/20 text-white' : ACCENT_STYLES[accentColor].chip,
                          )}
                        >
                          <Icon className="h-4 w-4" />
                        </span>
                        <span className="font-semibold">{t(labelKey)}</span>
                      </div>

                      {badge && !isActive && (
                        <span className="rounded-full bg-warm-100 px-2 py-0.5 text-[10px] font-semibold text-warm-600 dark:bg-white/10 dark:text-warm-300">
                          {badge}
                        </span>
                      )}
                    </>
                  )}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      </div>

      {/* Bottom household coaching widget */}
      <div className="mt-6 space-y-3">
        <div className="rounded-2xl border border-emerald-200/80 bg-gradient-to-br from-emerald-50 via-teal-50 to-white p-4 shadow-sm dark:border-emerald-500/20 dark:from-emerald-950/40 dark:via-warm-900 dark:to-warm-900">
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-600 text-white">
              <Sparkles className="h-3.5 w-3.5" />
            </span>
            <span className="text-xs font-bold text-warm-900 dark:text-white">Family Coach AI</span>
          </div>

          <p className="mt-2 text-xs leading-relaxed text-warm-600 dark:text-warm-300">
            {lang === 'es'
              ? 'Tutoría bilingüe activa: Recomendación de hábitos y plan de admisiones USA/LATAM.'
              : 'Active bilingual coaching: Habit tuning & top-tier USA/LATAM admissions roadmap.'}
          </p>

          <button
            type="button"
            onClick={() => setIsAiCoachOpen(true)}
            className="mt-3 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-emerald-600 px-3 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-emerald-700"
          >
            <Sparkles className="h-3.5 w-3.5" />
            {t('nav.coachButton')}
          </button>
        </div>

        <div className="flex items-center justify-between px-2 text-[11px] text-warm-400 dark:text-warm-500">
          <span className="flex items-center gap-1">
            <HeartHandshake className="h-3.5 w-3.5 text-emerald-500" />
            {lang === 'es' ? 'Plan Familiar Profesional' : 'Executive Family Plan'}
          </span>
          <span className="font-semibold text-emerald-600 dark:text-emerald-400">Activo</span>
        </div>
      </div>
    </aside>
  );
}
