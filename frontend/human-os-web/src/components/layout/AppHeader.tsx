import { useLocation, Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { Bell, PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { LanguageSwitcher } from './LanguageSwitcher';
import { ThemeToggle } from '@/components/theme/ThemeToggle';
import { ProfileMenu } from './ProfileMenu';
import { useSidebarStore } from '@/store/useSidebarStore';
import { useEnterpriseContext } from '@/features/enterprise-context/hooks/useEnterpriseContext';
import { ALL_NAV_ITEMS } from '@/components/navigation/navItems';

/** Known Growth Plan sub-steps that don't have their own top-level nav
 *  entry but should still show a second breadcrumb segment. */
const SUB_ROUTE_LABEL_KEYS: Record<string, string> = {
  '/growth-plan/currentrole': 'growthPlan.workContext.stepLabel',
  '/growth-plan/role-experience': 'growthPlan.roleExperience.pageTitle',
  '/growth-plan/role-experience/alignment-guide': 'growthPlan.roleExperience.alignmentGuide.title',
};

/** Finds the deepest matching nav item (and, when applicable, a known
 *  sub-step) for the current pathname, so the header can show a
 *  lightweight breadcrumb without every page needing to declare its own
 *  title.
 */
function useBreadcrumbLabelKeys() {
  const { pathname } = useLocation();
  const match = ALL_NAV_ITEMS.find((item) => pathname === item.to || pathname.startsWith(`${item.to}/`));
  const subLabelKey = SUB_ROUTE_LABEL_KEYS[pathname];
  return { pageLabelKey: match?.labelKey, pageTo: match?.to, subLabelKey };
}

export function AppHeader() {
  const { t } = useTranslation();
  const collapsed = useSidebarStore((state) => state.collapsed);
  const toggleSidebar = useSidebarStore((state) => state.toggle);
  const { data: enterpriseContext } = useEnterpriseContext();
  const { pageLabelKey, pageTo, subLabelKey } = useBreadcrumbLabelKeys();

  return (
    <header className="sticky top-0 z-30 border-b border-slate-200/80 bg-white/80 backdrop-blur-md dark:border-white/10 dark:bg-[#05060a]/80">
      <div className="flex items-center justify-between px-4 py-3 sm:px-6">
        <div className="flex min-w-0 items-center gap-3">
          <button
            type="button"
            onClick={toggleSidebar}
            aria-label={collapsed ? t('common.expandSidebar') : t('common.collapseSidebar')}
            className="hidden h-9 w-9 shrink-0 items-center justify-center rounded-full border border-slate-200 text-slate-500 transition hover:border-slate-300 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 md:flex dark:border-white/10 dark:text-white/60 dark:hover:border-white/20 dark:hover:text-white"
          >
            {collapsed ? <PanelLeftOpen className="h-4 w-4" /> : <PanelLeftClose className="h-4 w-4" />}
          </button>

          <div className="flex items-center gap-2.5">
            <span className="h-6 w-6 shrink-0 rounded-lg bg-gradient-to-br from-blue-500 to-violet-500" aria-hidden="true" />
            <span className="text-lg font-semibold tracking-tight text-slate-900 dark:text-white">
              {t('common.appName')}
            </span>
          </div>

          {pageLabelKey && (
            <p className="hidden min-w-0 truncate text-sm text-slate-400 sm:block dark:text-white/40">
              <span aria-hidden="true">/</span>{' '}
              {subLabelKey && pageTo ? (
                <Link to={pageTo} className="hover:text-slate-700 hover:underline dark:hover:text-white/70">
                  {t(pageLabelKey)}
                </Link>
              ) : (
                t(pageLabelKey)
              )}
              {subLabelKey && (
                <>
                  {' '}
                  <span aria-hidden="true">/</span> {t(subLabelKey)}
                </>
              )}
            </p>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-3">
          <LanguageSwitcher />
          <ThemeToggle />

          <button
            type="button"
            aria-label={t('common.notifications')}
            className="flex h-9 w-9 items-center justify-center rounded-full border border-slate-200 text-slate-500 transition hover:border-slate-300 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/60 dark:hover:border-white/20 dark:hover:text-white"
          >
            <Bell className="h-4 w-4" />
          </button>

          <ProfileMenu
            userName="Jorge Pérez Luna"
            roleName={enterpriseContext?.roleName}
            organizationName={enterpriseContext?.organizationName}
            variant="compact"
          />
        </div>
      </div>
    </header>
  );
}
