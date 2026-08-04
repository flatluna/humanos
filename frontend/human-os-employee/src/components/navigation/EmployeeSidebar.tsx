import { GraduationCap, Home, Brain, UserCircle } from 'lucide-react';
import { NavLink } from 'react-router';
import { clsx } from 'clsx';

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
 * search-param comparison needed. */
const COURSE_CATEGORY_LINKS = [
  { to: '/capabilities/job-skills', label: 'Mi Puesto' },
  { to: '/capabilities/soft-skills', label: 'Soft Skills' },
  { to: '/capabilities/department-processes', label: 'Procesos de Depto.' },
  { to: '/capabilities/hr-processes', label: 'Procesos de RRHH' },
];

export function EmployeeSidebar() {
  const navItemClass = ({ isActive }: { isActive: boolean }) =>
    clsx(
      'flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition',
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
    <aside className="hidden w-60 flex-none border-r border-slate-200 px-4 py-6 dark:border-white/10 md:block">
      <nav>
        <ul className="space-y-1">
          <li>
            <NavLink to="/" end className={navItemClass}>
              <Home className="h-4 w-4" />
              Hoy
            </NavLink>
          </li>
          <li>
            <NavLink to="/capabilities" className={navItemClass}>
              <GraduationCap className="h-4 w-4" />
              Mis Cursos
            </NavLink>
            <ul className="ml-4 mt-1 space-y-0.5 border-l border-slate-200 pl-3 dark:border-white/10">
              {COURSE_CATEGORY_LINKS.map((link) => (
                <li key={link.to}>
                  <NavLink to={link.to} className={subNavItemClass}>
                    {link.label}
                  </NavLink>
                </li>
              ))}
            </ul>
          </li>
          <li>
            <NavLink to="/brain" className={navItemClass}>
              <Brain className="h-4 w-4" />
              Mi Cerebro
            </NavLink>
          </li>
          <li>
            <NavLink to="/profile" className={navItemClass}>
              <UserCircle className="h-4 w-4" />
              Mi Perfil
            </NavLink>
          </li>
        </ul>
      </nav>
    </aside>
  );
}
