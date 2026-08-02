import { Moon, Sun } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useThemeStore } from '@/store/useThemeStore';

/** Top bar for the employee portal — deliberately plain/corporate (no
 * "Engram Academy" consumer marketing), same visual family (brand/accent
 * palette) as the other two Human OS apps but its own identity, per the
 * explicit requirement that employees get a distinct, company-appropriate
 * experience instead of the independent-student consumer app. */
export function EmployeeHeader() {
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useThemeStore();

  const initials = (user?.name ?? '?')
    .split(' ')
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();

  return (
    <header className="flex items-center justify-between border-b border-slate-200 bg-white px-6 py-3 dark:border-white/10 dark:bg-[#05060a]">
      <div className="flex items-center gap-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-brand-500 to-accent-500 text-sm font-bold text-white">
          HO
        </div>
        <div>
          <p className="text-sm font-semibold text-slate-900 dark:text-white">Human OS</p>
          <p className="text-xs text-slate-500 dark:text-white/50">{user?.tenantName ?? 'Mis Cursos'}</p>
        </div>
      </div>

      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={toggleTheme}
          aria-label="Cambiar tema"
          className="flex h-9 w-9 items-center justify-center rounded-full text-slate-500 transition hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
        >
          {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
        </button>
        <button
          type="button"
          onClick={() => void logout()}
          className="flex items-center gap-2 rounded-full bg-slate-100 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-200 dark:bg-white/10 dark:text-white dark:hover:bg-white/20"
        >
          <span className="flex h-6 w-6 items-center justify-center rounded-full bg-brand-500 text-[11px] font-bold text-white">
            {initials}
          </span>
          {user?.name}
        </button>
      </div>
    </header>
  );
}
