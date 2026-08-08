import { NavLink } from 'react-router';
import { useTranslation } from 'react-i18next';
import { clsx } from 'clsx';
import { Sparkles, ExternalLink, Menu } from 'lucide-react';
import { NAV_GROUPS } from './navItems';
import { useSidebarStore } from '@/store/useSidebarStore';
import { useAuth } from '@/auth/AuthContext';
import { ProfileMenu } from '@/components/layout/ProfileMenu';

// Capability Studio (:3010 locally) is the AUTHORING app. Only a Consumer
// (no tenant) or an org's HRAdmin can create courses there (2026-08-05
// ownership/visibility model) — a plain Employee/Manager can't, so the
// link is hidden for them. Env-driven so production points at the deployed
// Studio SWA instead of localhost (2026-08-07 fix).
const CAPABILITY_STUDIO_URL = import.meta.env.VITE_CAPABILITY_STUDIO_URL ?? 'http://localhost:3010';

export function AppSidebar() {
  const { t } = useTranslation();
  const collapsed = useSidebarStore((state) => state.collapsed);
  const toggleSidebar = useSidebarStore((state) => state.toggle);
  const { user } = useAuth();

  return (
    <aside
      className={clsx(
        'sticky top-16 hidden h-[calc(100vh-4rem)] shrink-0 flex-col justify-between overflow-y-auto border-r border-slate-200 bg-white px-3 py-6 md:flex dark:border-white/10 dark:bg-[#05060a]',
        collapsed ? 'md:w-20' : 'md:w-20 lg:w-64',
      )}
    >
      <nav aria-label={t('nav.mobileNavigation')} className="space-y-6">
        <button
          type="button"
          onClick={toggleSidebar}
          aria-label={collapsed ? t('common.expandSidebar') : t('common.collapseSidebar')}
          className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:text-white/40 dark:hover:bg-white/5 dark:hover:text-white"
        >
          <Menu className="h-5 w-5" />
        </button>

        {NAV_GROUPS.map((group) => (
          <div key={group.labelKey}>
            <p
              className={clsx(
                'px-3 text-[11px] font-semibold uppercase tracking-widest text-slate-400 dark:text-white/30',
                collapsed ? 'hidden' : 'hidden lg:block',
              )}
            >
              {t(group.labelKey)}
            </p>
            <ul className="mt-2 space-y-1">
              {group.items.map(({ to, icon: Icon, labelKey, disabled, end }) => (
                <li key={to}>
                  {disabled ? (
                    <span
                      aria-disabled="true"
                      title={t('nav.comingSoon')}
                      className="flex cursor-not-allowed items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-slate-300 dark:text-white/20"
                    >
                      <Icon className="h-5 w-5 shrink-0" />
                      <span className={collapsed ? 'hidden' : 'hidden lg:inline'}>{t(labelKey)}</span>
                    </span>
                  ) : (
                    <NavLink
                      to={to}
                      end={end}
                      className={({ isActive }) =>
                        clsx(
                          'flex items-center gap-3 rounded-xl border-l-2 px-3 py-2.5 text-sm font-medium transition',
                          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500',
                          isActive
                            ? 'border-blue-600 bg-blue-50 text-blue-700 dark:border-blue-400 dark:bg-blue-500/10 dark:text-blue-300'
                            : 'border-transparent text-slate-500 hover:bg-slate-100 hover:text-slate-900 dark:text-white/50 dark:hover:bg-white/5 dark:hover:text-white',
                        )
                      }
                    >
                      <Icon className="h-5 w-5 shrink-0" />
                      <span className={collapsed ? 'hidden' : 'hidden lg:inline'}>{t(labelKey)}</span>
                    </NavLink>
                  )}
                </li>
              ))}
            </ul>
          </div>
        ))}

        {(!user?.tenantId || user.orgRole === 'HRAdmin') && (
          <div>
            <p
              className={clsx(
                'px-3 text-[11px] font-semibold uppercase tracking-widest text-slate-400 dark:text-white/30',
                collapsed ? 'hidden' : 'hidden lg:block',
              )}
            >
              {t('nav.groupCreate')}
            </p>
            <ul className="mt-2 space-y-1">
              <li>
                <a
                  href={CAPABILITY_STUDIO_URL}
                  target="_blank"
                  rel="noreferrer"
                  className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-slate-500 transition hover:bg-slate-100 hover:text-slate-900 dark:text-white/50 dark:hover:bg-white/5 dark:hover:text-white"
                >
                  <Sparkles className="h-5 w-5 shrink-0" />
                  <span className={clsx('flex-1', collapsed ? 'hidden' : 'hidden lg:inline')}>
                    {t('nav.createCourse')}
                  </span>
                  <ExternalLink className={clsx('h-3.5 w-3.5 shrink-0 opacity-60', collapsed ? 'hidden' : 'hidden lg:inline')} />
                </a>
              </li>
            </ul>
          </div>
        )}
      </nav>

      <div className="space-y-3 border-t border-slate-100 pt-4 dark:border-white/10">
        <ProfileMenu userName={user?.name ?? ''} variant="full" collapsed={collapsed} />
      </div>
    </aside>
  );
}
