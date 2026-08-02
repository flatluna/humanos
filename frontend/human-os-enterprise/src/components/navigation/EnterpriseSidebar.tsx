import { NavLink } from 'react-router';
import { clsx } from 'clsx';
import {
  LayoutDashboard,
  Building2,
  Briefcase,
  GraduationCap,
  GitBranch,
  BarChart3,
  Sparkles,
  ExternalLink,
  Users,
} from 'lucide-react';

const NAV_ITEMS = [
  { to: '/', end: true, icon: LayoutDashboard, label: 'Resumen', color: 'brand' },
  { to: '/organizacion', end: false, icon: Building2, label: 'Organización', color: 'violet' },
  { to: '/personal', end: false, icon: Users, label: 'Personal', color: 'sky' },
  { to: '/roles', end: false, icon: Briefcase, label: 'Roles y Puestos', color: 'amber' },
  { to: '/skills', end: false, icon: GraduationCap, label: 'Catálogo de Skills', color: 'teal' },
  { to: '/roadmaps', end: false, icon: GitBranch, label: 'Rutas de Carrera', color: 'emerald' },
  { to: '/reportes', end: false, icon: BarChart3, label: 'Reportes de Brechas', color: 'rose' },
] as const;

const COLOR_STYLES: Record<(typeof NAV_ITEMS)[number]['color'], { chip: string; active: string }> = {
  brand: {
    chip: 'bg-brand-100 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300',
    active: 'bg-gradient-to-r from-brand-600 to-brand-500 text-white shadow-md shadow-brand-500/30',
  },
  violet: {
    chip: 'bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300',
    active: 'bg-gradient-to-r from-violet-600 to-violet-500 text-white shadow-md shadow-violet-500/30',
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
  teal: {
    chip: 'bg-teal-100 text-teal-600 dark:bg-teal-500/15 dark:text-teal-300',
    active: 'bg-gradient-to-r from-teal-600 to-teal-500 text-white shadow-md shadow-teal-500/30',
  },
};

const CAPABILITY_STUDIO_URL = 'http://localhost:3002';

export function EnterpriseSidebar() {
  return (
    <aside className="sticky top-16 hidden h-[calc(100vh-4rem)] w-64 shrink-0 flex-col justify-between overflow-y-auto border-r border-slate-200 bg-white px-3 py-6 md:flex dark:border-white/10 dark:bg-[#05060a]">
      <nav className="space-y-1">
        <p className="px-3 pb-2 text-[11px] font-semibold uppercase tracking-widest text-slate-400 dark:text-white/30">
          Consola RRHH
        </p>
        <ul className="space-y-1">
          {NAV_ITEMS.map(({ to, end, icon: Icon, label, color }) => (
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
                    <span>{label}</span>
                  </>
                )}
              </NavLink>
            </li>
          ))}
        </ul>

        <p className="px-3 pt-6 pb-2 text-[11px] font-semibold uppercase tracking-widest text-slate-400 dark:text-white/30">
          Creación de contenido
        </p>
        <a
          href={CAPABILITY_STUDIO_URL}
          target="_blank"
          rel="noreferrer"
          className="flex items-center gap-3 rounded-xl px-2.5 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/5"
        >
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-brand-500 to-accent-500 text-white shadow-sm shadow-accent-500/30">
            <Sparkles className="h-4 w-4" />
          </span>
          <span className="flex-1">Capability Studio</span>
          <ExternalLink className="h-3.5 w-3.5 shrink-0 opacity-60" />
        </a>
      </nav>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-gradient-to-br from-slate-50 to-white p-3 text-xs text-slate-500 dark:border-white/10 dark:from-white/5 dark:to-white/[0.02] dark:text-white/50">
        <p className="font-semibold text-slate-700 dark:text-white/70">Human OS Enterprise</p>
        <p className="mt-1">Consola de capacitación organizacional.</p>
      </div>
    </aside>
  );
}
