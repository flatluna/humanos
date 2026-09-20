import { GraduationCap, Home, Brain, TrendingUp, UserCircle, Target, BookCheck, Bot, Network, Trophy, FlaskConical } from 'lucide-react';
import { NavLink } from 'react-router';
import { useTranslation } from 'react-i18next';
import { clsx } from 'clsx';
import { useSidebarStore } from '@/store/useSidebarStore';
import { useAuth } from '@/auth/AuthContext';

/** Sidebar for the employee portal using standard react-i18next translations. */
export function EmployeeSidebar() {
  const { t } = useTranslation();
  const { user } = useAuth();

  const labels = {
    // "Tec Contigo" only for the academic (Tec de Monterrey) tenant — every
    // other tenant gets the generic "Hoy"/"Today" label instead.
    today: user?.isAcademicTenant ? t('nav.today') : t('nav.todayGeneric', { defaultValue: 'Hoy' }),
    objectives: t('nav.objectives'),
    availableCourses: t('nav.availableCourses', { defaultValue: 'Cursos Disponibles' }),
    programs: t('nav.programs', { defaultValue: 'Programas' }),
    learningLabs: t('nav.learningLabs', { defaultValue: 'Learning Labs' }),
    programEntities: t('nav.programEntities', { defaultValue: 'Entidades del Programa' }),
    programAgents: t('nav.programAgents', { defaultValue: 'Agentes de Programa' }),
    aiAgents: t('nav.aiAgents', { defaultValue: 'Agentes de Curso' }),
    brain: t('nav.brain'),
    evolution: t('nav.evolution'),
    achievements: t('nav.achievements', { defaultValue: 'Mis Logros' }),
    profile: t('nav.profile'),
    courses: t('nav.courses'),
    job: t('nav.job'),
    department: t('nav.department'),
    hr: t('nav.hr'),
    expand: t('nav.expand'),
    collapse: t('nav.collapse'),
  };

  const navLinks = [
    { to: '/', end: true, icon: Home, label: labels.today, iconColor: 'text-blue-500 dark:text-blue-400' },
    { to: '/programs', end: false, icon: GraduationCap, label: labels.programs, iconColor: 'text-purple-500 dark:text-purple-400' },
    { to: '/learning-labs', end: false, icon: FlaskConical, label: labels.learningLabs, iconColor: 'text-teal-500 dark:text-teal-400' },
    { to: '/program-entities', end: false, icon: Network, label: labels.programEntities, iconColor: 'text-amber-500 dark:text-amber-400' },
    { to: '/program-agents', end: false, icon: Bot, label: labels.programAgents, iconColor: 'text-violet-500 dark:text-violet-400' },
    { to: '/available-courses', end: false, icon: BookCheck, label: labels.availableCourses, iconColor: 'text-emerald-500 dark:text-emerald-400' },
    { to: '/ai-agents', end: false, icon: Bot, label: labels.aiAgents, iconColor: 'text-cyan-500 dark:text-cyan-400' },
    { to: '/objectives', end: false, icon: Target, label: labels.objectives, iconColor: 'text-rose-500 dark:text-rose-400' },
    { to: '/brain', end: false, icon: Brain, label: labels.brain, iconColor: 'text-fuchsia-500 dark:text-fuchsia-400' },
    { to: '/my-evolution', end: false, icon: TrendingUp, label: labels.evolution, iconColor: 'text-amber-500 dark:text-amber-400' },
    { to: '/achievements', end: false, icon: Trophy, label: labels.achievements, iconColor: 'text-yellow-500 dark:text-yellow-400' },
    { to: '/profile', end: false, icon: UserCircle, label: labels.profile, iconColor: 'text-sky-500 dark:text-sky-400' },
  ];
  const courseCategoryLinks = [
    { to: '/capabilities/job-skills', label: labels.job },
    { to: '/capabilities/department-processes', label: labels.department },
    { to: '/capabilities/hr-processes', label: labels.hr },
  ];
  const visibleNavLinks = navLinks.filter((link) => link.to !== '/objectives' && link.to !== '/capabilities');
  const collapsed = useSidebarStore((state) => state.collapsed);

  const navItemClass = ({ isActive }: { isActive: boolean }) =>
    clsx(
      'group flex items-center gap-2.5 rounded-xl px-3 py-2 text-sm font-medium transition-all',
      collapsed && 'justify-center px-2',
      isActive
        ? 'bg-brand-500/10 text-brand-700 dark:text-brand-300 shadow-xs'
        : 'text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/5',
    );

  const subNavItemClass = ({ isActive }: { isActive: boolean }) =>
    clsx(
      'block rounded-md px-2.5 py-1.5 text-xs font-medium transition',
      isActive
        ? 'bg-brand-500/10 text-brand-700 dark:text-brand-300'
        : 'text-slate-500 hover:bg-slate-100 hover:text-slate-800 dark:text-white/50 dark:hover:bg-white/5 dark:hover:text-white/80',
    );

  return (
    <aside
      className={clsx(
        'flex-none border-r border-slate-200 px-4 py-6 transition-[width] dark:border-white/10',
        collapsed ? 'w-16' : 'w-60',
      )}
    >
      <nav>
        <ul className="space-y-1">
          {visibleNavLinks.slice(0, 1).map((link) => (
            <li key={link.to}>
              <NavLink to={link.to} end={link.end} className={navItemClass} title={collapsed ? link.label : undefined}>
                <link.icon className={clsx('h-4 w-4 shrink-0 transition-transform group-hover:scale-110', link.iconColor)} />
                {!collapsed && <span>{link.label}</span>}
              </NavLink>
            </li>
          ))}
          {false && (
            <li>
              <NavLink
                to="/capabilities"
                className={navItemClass}
                title={collapsed ? labels.courses : undefined}
              >
                <GraduationCap className="h-4 w-4 shrink-0 text-indigo-500 dark:text-indigo-400 transition-transform group-hover:scale-110" />
                {!collapsed && <span>{labels.courses}</span>}
              </NavLink>
              {!collapsed && (
                <ul className="ml-4 mt-1 space-y-0.5 border-l border-slate-200 pl-3 dark:border-white/10">
                  {courseCategoryLinks.map((link) => (
                    <li key={link.to}>
                      <NavLink to={link.to} className={subNavItemClass}>
                        {link.label}
                      </NavLink>
                    </li>
                  ))}
                </ul>
              )}
            </li>
          )}
          {visibleNavLinks.slice(1).map((link) => (
            <li key={link.to}>
              <NavLink to={link.to} end={link.end} className={navItemClass} title={collapsed ? link.label : undefined}>
                <link.icon className={clsx('h-4 w-4 shrink-0 transition-transform group-hover:scale-110', link.iconColor)} />
                {!collapsed && <span>{link.label}</span>}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  );
}
