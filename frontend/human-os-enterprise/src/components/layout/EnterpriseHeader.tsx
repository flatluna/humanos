import { useState } from 'react';
import { Moon, Sun, LogOut, ChevronDown } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useThemeStore } from '@/store/useThemeStore';

const ROLE_BADGE_STYLES: Record<string, string> = {
  HRAdmin: 'bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300',
  Manager: 'bg-violet-100 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300',
  Employee: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/60',
};

export function EnterpriseHeader() {
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useThemeStore();
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-slate-200 bg-white/80 px-6 backdrop-blur dark:border-white/10 dark:bg-[#05060a]/80">
      <div className="flex items-center gap-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-brand-600 to-accent-500 text-sm font-bold text-white shadow-md shadow-brand-500/30">
          HE
        </div>
        <div className="leading-tight">
          <p className="text-sm font-semibold text-slate-900 dark:text-white">Human OS Enterprise</p>
          <p className="text-xs text-slate-400 dark:text-white/40">{user?.tenantName ?? 'Consola RRHH'}</p>
        </div>
      </div>

      <div className="flex items-center gap-2">
        {user?.orgRole && (
          <span
            className={`hidden rounded-full px-3 py-1 text-xs font-semibold sm:inline-block ${
              ROLE_BADGE_STYLES[user.orgRole] ?? ROLE_BADGE_STYLES.Employee
            }`}
          >
            {user.orgRole}
          </span>
        )}

        <button
          type="button"
          onClick={toggleTheme}
          className="flex h-9 w-9 items-center justify-center rounded-full text-slate-500 transition hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
          aria-label="Cambiar tema"
        >
          {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
        </button>

        <div className="relative">
          <button
            type="button"
            onClick={() => setMenuOpen((open) => !open)}
            className="flex items-center gap-2 rounded-full py-1 pl-1 pr-2 text-sm font-medium text-slate-700 transition hover:bg-slate-100 dark:text-white/80 dark:hover:bg-white/10"
          >
            <span className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-brand-500 to-accent-500 text-xs font-semibold text-white shadow-sm">
              {(user?.name ?? 'U').slice(0, 1).toUpperCase()}
            </span>
            <span className="hidden sm:inline">{user?.name}</span>
            <ChevronDown className="h-3.5 w-3.5 opacity-60" />
          </button>

          {menuOpen && (
            <div className="absolute right-0 mt-2 w-56 rounded-xl border border-slate-200 bg-white p-1 shadow-lg dark:border-white/10 dark:bg-[#0a0b12]">
              <div className="px-3 py-2 text-xs text-slate-400 dark:text-white/40">
                {user?.email}
                <br />
                Rol: {user?.orgRole}
              </div>
              <button
                type="button"
                onClick={() => void logout()}
                className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-slate-600 transition hover:bg-red-50 hover:text-red-600 dark:text-white/70 dark:hover:bg-red-500/10 dark:hover:text-red-300"
              >
                <LogOut className="h-4 w-4" />
                Cerrar sesión
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
