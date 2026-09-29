import { useEffect, useRef, useState } from 'react';
import { ChevronDown, LogOut, Moon, Sun, Menu, MessageCircle } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useThemeStore } from '@/store/useThemeStore';
import { useSidebarStore } from '@/store/useSidebarStore';
import { useHumanOsAgentPanelStore } from '@/store/useHumanOsAgentPanelStore';
import { setLanguage, useI18n } from '@/features/capabilities/i18n/useI18n';
import { GamificationProfilePanel } from '@/features/gamification/components/GamificationProfilePanel';

/** Top bar for the employee portal — deliberately plain/corporate (no
 * "Engram Academy" consumer marketing), same visual family (brand/accent
 * palette) as the other two Human OS apps but its own identity, per the
 * explicit requirement that employees get a distinct, company-appropriate
 * experience instead of the independent-student consumer app. */
export function EmployeeHeader() {
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useThemeStore();
  const { language, t } = useI18n();
  const collapsed = useSidebarStore((state) => state.collapsed);
  const toggleSidebar = useSidebarStore((state) => state.toggle);
  const openAgentPanel = useHumanOsAgentPanelStore((state) => state.open);
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
    <header className="relative flex items-center justify-between border-b border-slate-200 bg-white px-4 py-3 sm:px-6 dark:border-white/10 dark:bg-[#05060a]">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={toggleSidebar}
          aria-label={collapsed ? 'Expand menu' : 'Collapse menu'}
          title={collapsed ? 'Expand menu' : 'Collapse menu'}
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-slate-200 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900 dark:border-white/10 dark:text-white/60 dark:hover:bg-white/5 dark:hover:text-white"
        >
          <Menu className="h-5 w-5" />
        </button>

        <img src="/HumanOSLogo.png" alt="Human OS" className="h-11 w-auto max-w-[9rem] object-contain object-left" />
        {user?.isAcademicTenant ? (
          <img src="/TecMOnterreyLogo.png" alt="Tecnológico de Monterrey" className="h-10 w-auto max-w-[9rem] object-contain object-left" />
        ) : (
          user?.tenantName && (
            <span className="rounded-md bg-brand-500/10 px-2.5 py-1 text-sm font-semibold text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
              {user.tenantName}
            </span>
          )
        )}
        <div>
          <p className="text-sm font-semibold text-slate-900 dark:text-white">Human OS</p>
          <p className="text-xs text-slate-500 dark:text-white/50">Aprendizaje basado en ciencia de la memoria: repetición espaciada y curva del olvido</p>
        </div>
      </div>

      {/* Centered Human OS agent launcher — deliberately loud (yellow pulse +
          bouncing arrow) since this is a brand-new feature people won't
          otherwise notice next to the rest of the header controls. */}
      <div className="pointer-events-none absolute left-1/2 top-0.5 hidden -translate-x-1/2 sm:block">
        <ChevronDown className="h-4 w-4 animate-humanOsAgentArrow text-yellow-400 drop-shadow" />
      </div>
      <button
        type="button"
        onClick={openAgentPanel}
        aria-label={language === 'en' ? 'Ask Human OS' : 'Pregúntale a Human OS'}
        className="absolute left-1/2 top-1/2 hidden -translate-x-1/2 -translate-y-1/2 items-center gap-2 rounded-full border-2 border-yellow-400 bg-white px-4 py-1.5 text-sm font-semibold text-slate-800 shadow-lg animate-humanOsAgentPulse transition-transform hover:scale-105 dark:bg-slate-900 dark:text-white sm:flex"
      >
        <MessageCircle className="h-4 w-4 text-yellow-500" />
        {language === 'en' ? 'Ask Human OS' : 'Pregúntale a Human OS'}
      </button>

      <div className="flex items-center gap-3">
        <GamificationProfilePanel />

        <button
          type="button"
          onClick={toggleTheme}
          aria-label="Cambiar tema"
          className="flex h-9 w-9 items-center justify-center rounded-full text-slate-500 transition hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
        >
          {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
        </button>
        <select
          aria-label={t.language}
          value={language}
          onChange={(event) => setLanguage(event.target.value as 'es' | 'en')}
          className="h-9 rounded-lg border border-slate-200 bg-white px-2 text-xs font-semibold text-slate-600 outline-none transition hover:border-brand-400 dark:border-white/10 dark:bg-white/5 dark:text-white/70"
        >
          <option value="es">ES</option>
          <option value="en">EN</option>
        </select>
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
                {t.signOut}
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
