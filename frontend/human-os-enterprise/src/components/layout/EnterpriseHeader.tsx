import { useState } from 'react';
import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { Moon, Sun, LogOut, ChevronDown, UserCircle } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useThemeStore } from '@/store/useThemeStore';
import { LanguageSwitcher } from './LanguageSwitcher';

const ROLE_BADGE_STYLES: Record<string, string> = {
  HRAdmin: 'bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300',
  Manager: 'bg-violet-100 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300',
  Employee: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/60',
};

export function EnterpriseHeader() {
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useThemeStore();
  const [menuOpen, setMenuOpen] = useState(false);

  const roleTranslated = user?.orgRole
    ? t(`roles.${user.orgRole}` as const, { defaultValue: user.orgRole })
    : '';

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-slate-200 bg-white/80 px-6 backdrop-blur dark:border-white/10 dark:bg-[#05060a]/80">
      <div className="flex items-center gap-3">
        <img src="/engramslogo.png" alt="Engram" className="h-9 w-9 rounded-xl object-cover shadow-md shadow-brand-500/30" />
        <div className="leading-tight">
          <div className="flex items-center gap-2">
            <p className="text-sm font-semibold text-slate-900 dark:text-white">{t('header.title')}</p>
            {user?.tenantName && (
              <span className="rounded-md bg-brand-500/10 px-2 py-0.5 text-xs font-semibold text-brand-600 dark:bg-brand-500/20 dark:text-brand-300">
                {user.tenantName}
              </span>
            )}
          </div>
          {user?.tenantId && (
            <p className="font-mono text-[11px] text-slate-400 dark:text-white/40">
              Tenant ID: <span className="select-all font-medium text-slate-500 dark:text-white/60">{user.tenantId}</span>
            </p>
          )}
        </div>
      </div>

      <div className="flex items-center gap-2">
        {user?.orgRole && (
          <span
            className={`hidden rounded-full px-3 py-1 text-xs font-semibold sm:inline-block ${
              ROLE_BADGE_STYLES[user.orgRole] ?? ROLE_BADGE_STYLES.Employee
            }`}
          >
            {roleTranslated}
          </span>
        )}

        <LanguageSwitcher />

        <button
          type="button"
          onClick={toggleTheme}
          className="flex h-9 w-9 items-center justify-center rounded-full text-slate-500 transition hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
          aria-label={t('header.themeToggle')}
          title={t('header.themeToggle')}
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
            <div className="absolute right-0 mt-2 w-64 rounded-xl border border-slate-200 bg-white p-1 shadow-lg dark:border-white/10 dark:bg-[#0a0b12]">
              <div className="px-3 py-2 text-xs text-slate-400 dark:text-white/40">
                <p className="font-semibold text-slate-700 dark:text-white/80">{user?.name}</p>
                <p className="truncate">{user?.email}</p>
                <p className="mt-0.5">{t('header.roleLabel', { role: roleTranslated })}</p>
                {user?.tenantName && (
                  <p className="mt-1.5 font-medium text-slate-600 dark:text-white/70">
                    Empresa: {user.tenantName}
                  </p>
                )}
                {user?.tenantId && (
                  <p className="mt-0.5 font-mono text-[10px] text-slate-400 dark:text-white/40 break-all select-all">
                    Tenant ID: {user.tenantId}
                  </p>
                )}
              </div>
              <div className="my-1 border-t border-slate-100 dark:border-white/10" />
              <Link
                to={user?.personId ? `/personal/${user.personId}` : '/perfil'}
                onClick={() => setMenuOpen(false)}
                className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-slate-600 transition hover:bg-slate-100 hover:text-slate-900 dark:text-white/70 dark:hover:bg-white/5 dark:hover:text-white"
              >
                <UserCircle className="h-4 w-4 text-slate-400 dark:text-white/40" />
                {t('header.profile')}
              </Link>
              <button
                type="button"
                onClick={() => void logout()}
                className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-slate-600 transition hover:bg-red-50 hover:text-red-600 dark:text-white/70 dark:hover:bg-red-500/10 dark:hover:text-red-300"
              >
                <LogOut className="h-4 w-4" />
                {t('header.signOut')}
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
