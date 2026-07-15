import { NavLink } from 'react-router';
import { useTranslation } from 'react-i18next';
import { Building2 } from 'lucide-react';
import { clsx } from 'clsx';
import { NAV_GROUPS } from './navItems';
import { useSidebarStore } from '@/store/useSidebarStore';
import { useEnterpriseContext } from '@/features/enterprise-context/hooks/useEnterpriseContext';
import { ProfileMenu } from '@/components/layout/ProfileMenu';

export function AppSidebar() {
  const { t } = useTranslation();
  const collapsed = useSidebarStore((state) => state.collapsed);
  const { data: enterpriseContext } = useEnterpriseContext();

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
              {group.items.map(({ to, icon: Icon, labelKey }) => (
                <li key={to}>
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
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>

      <div className="space-y-3 border-t border-slate-100 pt-4 dark:border-white/10">
        {enterpriseContext && (
          <div
            aria-label={t('nav.organizationContext')}
            className="flex items-center gap-2.5 rounded-xl px-3 py-2 text-slate-500 dark:text-white/50"
          >
            <Building2 className="h-4 w-4 shrink-0" />
            <span className={collapsed ? 'hidden' : 'hidden lg:block'}>
              <span className="block text-sm font-medium text-slate-700 dark:text-white/80">
                {enterpriseContext.organizationName}
              </span>
              <span className="block text-xs text-slate-400 dark:text-white/40">{enterpriseContext.roleName}</span>
            </span>
          </div>
        )}

        <ProfileMenu
          userName="Jorge Pérez Luna"
          roleName={enterpriseContext?.roleName}
          organizationName={enterpriseContext?.organizationName}
          variant="full"
          collapsed={collapsed}
        />
      </div>
    </aside>
  );
}
