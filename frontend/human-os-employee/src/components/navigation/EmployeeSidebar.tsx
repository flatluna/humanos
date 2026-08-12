import { GraduationCap, Home, Brain, TrendingUp, UserCircle, Menu } from 'lucide-react';
import { NavLink } from 'react-router';
import { clsx } from 'clsx';
import { useSidebarStore } from '@/store/useSidebarStore';

/** Sidebar for the employee portal. 2026-08-01: added "Hoy" (dashboard,
 * ported from human-os-web's /today) and "Mi Cerebro" (mastered-engrams +
 * fading-memory dashboard, ported from human-os-web's /brain) alongside
 * the original "Mis Cursos" catalog — see /memories/repo for what each
 * page covers and why some human-os-web features (Programs, Growth Plan,
 * human-state check-in) were deliberately left out of this portal's v1
 * scope.
 *
 * 2026-08-01 (later same day): "Mis Cursos" now points at the employee's
 * own career-plan roadmap (see routes/index.tsx) instead of a free-browse
 * catalog, so the separate "Mi Plan de Capacitación" entry that used to
 * live here was removed — it would now be an exact duplicate.
 *
 * 2026-08-01 (evening, take 3): course categories are now REAL separate
 * routes (`/capabilities/job-skills`, `/capabilities/soft-skills`,
 * `/capabilities/department-processes`, `/capabilities/hr-processes` —
 * see routes/index.tsx) instead of a `?tab=` query param on one shared
 * URL — the user explicitly rejected both the earlier hash-anchor-scroll
 * AND the query-param-tab versions ("quiero que cada uno tenga su propia
 * página"). Since these are now genuine distinct routes, plain `NavLink`
 * with its built-in `isActive` works correctly — no more manual
 * search-param comparison needed.
 *
 * 2026-08-08: added a hamburger toggle (same `useSidebarStore` +
 * zustand-persist pattern as human-os-web's AppSidebar) to collapse this
 * down to icon-only and free up screen space — collapsed state persists
 * across reloads via localStorage. Sub-nav (course categories) and labels
 * hide while collapsed; icons get a `title` tooltip instead. */
const COURSE_CATEGORY_LINKS = [
  { to: '/capabilities/job-skills', label: 'Mi Puesto' },
  { to: '/capabilities/soft-skills', label: 'Soft Skills' },
  { to: '/capabilities/department-processes', label: 'Procesos de Depto.' },
  { to: '/capabilities/hr-processes', label: 'Procesos de RRHH' },
];

const NAV_LINKS = [
  { to: '/', end: true, icon: Home, label: 'Hoy' },
  { to: '/brain', end: false, icon: Brain, label: 'Mi Cerebro' },
  { to: '/my-evolution', end: false, icon: TrendingUp, label: 'Mi Evolución' },
  { to: '/profile', end: false, icon: UserCircle, label: 'Mi Perfil' },
];

export function EmployeeSidebar() {
  const collapsed = useSidebarStore((state) => state.collapsed);
  const toggleSidebar = useSidebarStore((state) => state.toggle);

  const navItemClass = ({ isActive }: { isActive: boolean }) =>
    clsx(
      'flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition',
      collapsed && 'justify-center px-2',
      isActive
        ? 'bg-brand-500/10 text-brand-700 dark:text-brand-300'
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
        'hidden flex-none border-r border-slate-200 px-4 py-6 transition-[width] dark:border-white/10 md:block',
        collapsed ? 'w-16' : 'w-60',
      )}
    >
      <button
        type="button"
        onClick={toggleSidebar}
        aria-label={collapsed ? 'Expandir menú' : 'Colapsar menú'}
        title={collapsed ? 'Expandir menú' : 'Colapsar menú'}
        className={clsx(
          'mb-4 flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-900 dark:text-white/40 dark:hover:bg-white/5 dark:hover:text-white',
          collapsed && 'mx-auto',
        )}
      >
        <Menu className="h-5 w-5" />
      </button>

      <nav>
        <ul className="space-y-1">
          {NAV_LINKS.slice(0, 1).map((link) => (
            <li key={link.to}>
              <NavLink to={link.to} end={link.end} className={navItemClass} title={collapsed ? link.label : undefined}>
                <link.icon className="h-4 w-4 shrink-0" />
                {!collapsed && link.label}
              </NavLink>
            </li>
          ))}
          <li>
            <NavLink
              to="/capabilities"
              className={navItemClass}
              title={collapsed ? 'Mis Cursos' : undefined}
            >
              <GraduationCap className="h-4 w-4 shrink-0" />
              {!collapsed && 'Mis Cursos'}
            </NavLink>
            {!collapsed && (
              <ul className="ml-4 mt-1 space-y-0.5 border-l border-slate-200 pl-3 dark:border-white/10">
                {COURSE_CATEGORY_LINKS.map((link) => (
                  <li key={link.to}>
                    <NavLink to={link.to} className={subNavItemClass}>
                      {link.label}
                    </NavLink>
                  </li>
                ))}
              </ul>
            )}
          </li>
          {NAV_LINKS.slice(1).map((link) => (
            <li key={link.to}>
              <NavLink to={link.to} end={link.end} className={navItemClass} title={collapsed ? link.label : undefined}>
                <link.icon className="h-4 w-4 shrink-0" />
                {!collapsed && link.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  );
}
