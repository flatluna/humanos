import { useEffect, useRef, useState } from 'react';
import { ChevronDown, LogOut, Moon, Sun } from 'lucide-react';
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
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  const initials = (user?.name ?? '?')
    .split(' ')
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();

  useEffect(() => {
    function handlePointerDown(event: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setMenuOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setMenuOpen(false);
      }
    }

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, []);

  return (
    <header className="flex items-center justify-between border-b border-slate-200 bg-white px-6 py-3 dark:border-white/10 dark:bg-[#05060a]">
      <div className="flex items-center gap-3">
        <img src="/engramslogo.png?v=2" alt="Engram" className="h-11 w-11 rounded-xl object-cover ring-1 ring-slate-200 dark:ring-white/15" />
        <div>
          <p className="text-sm font-semibold text-slate-900 dark:text-white">Engram</p>
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
        <div ref={menuRef} className="relative">
          <button
            type="button"
            onClick={() => setMenuOpen((open) => !open)}
            aria-haspopup="menu"
            aria-expanded={menuOpen}
            className="flex items-center gap-2 rounded-full bg-slate-100 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-200 dark:bg-white/10 dark:text-white dark:hover:bg-white/20"
          >
            <span className="flex h-6 w-6 items-center justify-center rounded-full bg-brand-500 text-[11px] font-bold text-white">
              {initials}
            </span>
            <span>{user?.name}</span>
            <ChevronDown className="h-3.5 w-3.5 opacity-60" />
          </button>

          {menuOpen && (
            <div
              role="menu"
              className="absolute right-0 mt-2 w-56 rounded-xl border border-slate-200 bg-white p-1 shadow-lg dark:border-white/10 dark:bg-[#0a0b12]"
            >
              <div className="px-3 py-2 text-xs text-slate-500 dark:text-white/40">{user?.name}</div>
              <button
                type="button"
                role="menuitem"
                onClick={() => {
                  setMenuOpen(false);
                  void logout();
                }}
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
