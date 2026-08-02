import { useEffect, useRef } from 'react';
import { Outlet } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { EmployeeHeader } from '@/components/layout/EmployeeHeader';
import { EmployeeSidebar } from '@/components/navigation/EmployeeSidebar';
import { AccessDenied } from '@/components/AccessDenied';

/** Shell for the whole Employee Portal: real MSAL session gate, then a
 *  "belongs to a company tenant" gate (same as human-os-enterprise), but
 *  deliberately NO OrgRole gate — every employee, manager, and HRAdmin can
 *  use this app to take their own assigned courses. This is intentionally
 *  a separate app/shell from both human-os-web (consumer/independent
 *  students) and human-os-enterprise (HR admin console). */
export function EmployeeAppLayout() {
  const { isAuthenticated, isLoading, backendError, notEnterpriseAccount, user, login, refreshUser } = useAuth();
  const redirectStarted = useRef(false);

  useEffect(() => {
    if (!isAuthenticated && !isLoading && !redirectStarted.current) {
      redirectStarted.current = true;
      void login();
    }
  }, [isAuthenticated, isLoading, login]);

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
        <p className="text-lg font-semibold text-slate-900 dark:text-white">No pudimos conectar con el servidor</p>
        <p className="max-w-md text-sm text-slate-500 dark:text-white/50">
          Tu sesión es válida, pero el backend no respondió correctamente. Verifica tu conexión e inténtalo de nuevo.
        </p>
        <button
          type="button"
          onClick={() => void refreshUser()}
          className="inline-flex min-h-11 items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
        >
          Reintentar
        </button>
      </div>
    );
  }

  if (notEnterpriseAccount || !user) {
    return <AccessDenied reason="no-enterprise-account" />;
  }

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-[#05060a]">
      <EmployeeHeader />

      <div className="flex">
        <EmployeeSidebar />

        <main className="min-w-0 flex-1 px-6 py-8 lg:px-10">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
