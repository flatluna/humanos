import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
import { Sparkles, Plus, LayoutGrid, GraduationCap, Coins } from 'lucide-react';
import ActiveRunBanner from './ActiveRunBanner';
import ThemeSwitcher from './ThemeSwitcher';

export default function Layout() {
  const location = useLocation();
  const onProgramsSection = location.pathname.startsWith('/programs');

  return (
    <div className="min-h-full flex flex-col">
      <header className="sticky top-0 z-40 border-b border-white/10 bg-slate-950/80 backdrop-blur-xl">
        <div className="mx-auto max-w-7xl px-6 py-4 flex items-center justify-between gap-6">
          <Link to="/" className="flex items-center gap-2.5 group">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-brand-500 to-accent-500 shadow-lg shadow-brand-500/30 transition-transform group-hover:scale-105">
              <Sparkles className="h-5 w-5 text-[#fff]" strokeWidth={2.25} />
            </div>
            <div className="leading-tight">
              <div className="font-semibold tracking-tight text-white">Capability Studio</div>
              <div className="text-[11px] text-slate-400">Human OS</div>
            </div>
          </Link>

          <nav className="hidden sm:flex items-center gap-1">
            <NavLink
              to="/"
              end
              className={({ isActive }) =>
                `flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                  isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white hover:bg-white/5'
                }`
              }
            >
              <LayoutGrid className="h-4 w-4" />
              Catálogo
            </NavLink>
            <NavLink
              to="/programs"
              className={({ isActive }) =>
                `flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                  isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white hover:bg-white/5'
                }`
              }
            >
              <GraduationCap className="h-4 w-4" />
              Programas
            </NavLink>
            <NavLink
              to="/costs"
              className={({ isActive }) =>
                `flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                  isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white hover:bg-white/5'
                }`
              }
            >
              <Coins className="h-4 w-4" />
              Costos
            </NavLink>
          </nav>

          <div className="flex items-center gap-2">
            <ThemeSwitcher />
            {onProgramsSection ? (
              <Link
                to="/programs/new"
                className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.03] active:scale-[0.98]"
              >
                <Plus className="h-4 w-4" strokeWidth={2.5} />
                Nuevo programa
              </Link>
            ) : (
              <Link
                to="/new"
                className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.03] active:scale-[0.98]"
              >
                <Plus className="h-4 w-4" strokeWidth={2.5} />
                Nueva capability
              </Link>
            )}
          </div>
        </div>
      </header>

      <ActiveRunBanner />

      <main className="flex-1 relative">
        <Outlet />
      </main>

      <footer className="border-t border-white/10 py-6 text-center text-xs text-slate-500">
        Capability Studio · Human OS · Runtime v2 (Curador → GraphArchitect)
      </footer>
    </div>
  );
}
