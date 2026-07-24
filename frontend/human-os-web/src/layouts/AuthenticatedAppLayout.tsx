import { useEffect } from 'react';
import { Outlet } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { OnboardingPage } from '@/features/onboarding';
import { AppHeader } from '@/components/layout/AppHeader';
import { AppSidebar } from '@/components/navigation/AppSidebar';
import { MobileNavigation } from '@/components/navigation/MobileNavigation';

/** Shell shared by every authenticated Human OS screen: header, desktop
 *  sidebar, mobile bottom navigation, and the routed page content.
 *
 *  Gated by the real MSAL session (see src/auth/AuthContext.tsx):
 *    - Not authenticated  → trigger the real Entra CIAM sign-in redirect.
 *    - Authenticated, still resolving the account/backend lookup → spinner.
 *    - Authenticated but the backend lookup itself failed/errored →
 *      error screen, NEVER the onboarding form (a backend/network failure
 *      must not be mistaken for "this identity has no account yet" — see
 *      backendError's doc comment on AuthContextValue, 2026-07-23).
 *    - Authenticated but no Tenant/Person yet (a real 404) → the
 *      company+admin onboarding form, instead of the normal shell.
 *    - Authenticated and onboarded → the real app shell.
 */
export function AuthenticatedAppLayout() {
  const { isAuthenticated, isLoading, backendError, user, login, refreshUser } = useAuth();

  useEffect(() => {
    if (!isAuthenticated && !isLoading) {
      void login();
    }
  }, [isAuthenticated, isLoading, login]);

  if (!isAuthenticated || isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50 dark:bg-[#05060a]">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-slate-300 border-t-blue-600 dark:border-white/20 dark:border-t-blue-400" />
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

  if (user && !user.onboarded) {
    return <OnboardingPage />;
  }

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-[#05060a]">
      <AppHeader />

      <div className="flex">
        <AppSidebar />

        <main className="min-w-0 flex-1 pb-20 md:pb-8">
          <Outlet />
        </main>
      </div>

      <MobileNavigation />
    </div>
  );
}
