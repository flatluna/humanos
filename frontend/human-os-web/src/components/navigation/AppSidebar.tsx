import { NavLink } from 'react-router';
import { useTranslation } from 'react-i18next';
import { clsx } from 'clsx';
import { NAV_GROUPS } from './navItems';
import { useSidebarStore } from '@/store/useSidebarStore';
import { useAuth } from '@/auth/AuthContext';
import { ProfileMenu } from '@/components/layout/ProfileMenu';

export function AppSidebar() {
  const { t } = useTranslation();
  const collapsed = useSidebarStore((state) => state.collapsed);
  const { user } = useAuth();

  return (
    <aside
      className={clsx(
        'sticky top-16 hidden h-[calc(100vh-4rem)] shrink-0 flex-col justify-between overflow-y-auto border-r border-slate-200 bg-white px-3 py-6 md:flex dark:border-white/10 dark:bg-[#05060a]',
        collapsed ? 'md:w-20' : 'md:w-20 lg:w-64',
      )}
    >
      <nav aria-label={t('nav.mobileNavigation')} className="space-y-6">
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
              {group.items.map(({ to, icon: Icon, labelKey, disabled }) => (
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
                      className={({ isActive }) =>
                        clsx(
                          'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition',
                          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500',
                          isActive
                            ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                            : 'text-slate-500 hover:bg-slate-100 hover:text-slate-900 dark:text-white/50 dark:hover:bg-white/5 dark:hover:text-white',
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
      </nav>

      <div className="space-y-3 border-t border-slate-100 pt-4 dark:border-white/10">
        <ProfileMenu userName={user?.name ?? ''} variant="full" collapsed={collapsed} />
      </div>
    </aside>
  );
}
