import { useEffect, useRef } from 'react';
import { Outlet } from 'react-router';
import { useTranslation } from 'react-i18next';
import { InteractionStatus } from '@azure/msal-browser';
import { useMsal } from '@azure/msal-react';
import { useAuth } from '@/auth/AuthContext';
import { EnterpriseHeader } from '@/components/layout/EnterpriseHeader';
import { EnterpriseSidebar } from '@/components/navigation/EnterpriseSidebar';
import { AgentPanel } from '@/components/agent/AgentPanel';
import { AccessDenied } from '@/components/AccessDenied';

const REDIRECT_GUARD_KEY = 'humanos.enterprise.loginRedirect.startedAt';
const REDIRECT_GUARD_MAX_AGE_MS = 30_000;

/** Shell for the whole Enterprise console: real MSAL session gate, then
 *  Person.OrgRole gate (Manager/HRAdmin only), then header + sidebar +
 *  routed content + the agentic assistant panel. Deliberately separate
 *  from human-os-web's AuthenticatedAppLayout — own shell, own nav. */
export function EnterpriseAppLayout() {
  const { t } = useTranslation();
  const { isAuthenticated, isLoading, backendError, notEnterpriseAccount, user, login, refreshUser } = useAuth();
  const { inProgress } = useMsal();
  const redirectStarted = useRef(false);

  useEffect(() => {
    if (isAuthenticated) {
      sessionStorage.removeItem(REDIRECT_GUARD_KEY);
      redirectStarted.current = false;
      return;
    }

    if (isLoading || inProgress !== InteractionStatus.None || redirectStarted.current) {
      return;
    }

    const now = Date.now();
    const startedAtRaw = sessionStorage.getItem(REDIRECT_GUARD_KEY);
    const startedAt = startedAtRaw ? Number.parseInt(startedAtRaw, 10) : 0;
    const guardActive = Number.isFinite(startedAt) && now - startedAt < REDIRECT_GUARD_MAX_AGE_MS;

    if (guardActive) {
      return;
    }

    sessionStorage.setItem(REDIRECT_GUARD_KEY, now.toString());
    redirectStarted.current = true;
    void login().catch(() => {
      sessionStorage.removeItem(REDIRECT_GUARD_KEY);
      redirectStarted.current = false;
    });
  }, [inProgress, isAuthenticated, isLoading, login]);

  if (!isAuthenticated || isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50 dark:bg-[#05060a]">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-slate-300 border-t-brand-600 dark:border-white/20 dark:border-t-brand-400" />
      </div>
    );
  }

  if (backendError) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-50 px-4 text-center dark:bg-[#05060a]">
        <p className="text-lg font-semibold text-slate-900 dark:text-white">{t('common.connectionErrorTitle')}</p>
        <p className="max-w-md text-sm text-slate-500 dark:text-white/50">
          {t('common.connectionErrorDescription')}
        </p>
        <button
          type="button"
          onClick={() => void refreshUser()}
          className="inline-flex min-h-11 items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
        >
          {t('common.retry')}
        </button>
      </div>
    );
  }

  if (notEnterpriseAccount || !user) {
    return <AccessDenied reason="no-enterprise-account" />;
  }

  if (user.orgRole === 'Employee') {
    return <AccessDenied reason="role" />;
  }

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-[#05060a]">
      <EnterpriseHeader />

      <div className="flex">
        <EnterpriseSidebar />

        <main className="min-w-0 flex-1 px-6 py-8 lg:px-10">
          <Outlet />
        </main>
      </div>

      <AgentPanel />
    </div>
  );
}
