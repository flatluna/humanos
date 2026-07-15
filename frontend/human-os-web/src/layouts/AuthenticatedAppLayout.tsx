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
 *    - Authenticated but no Tenant/Person yet → the company+admin
 *      onboarding form, instead of the normal shell.
 *    - Authenticated and onboarded → the real app shell.
 */
export function AuthenticatedAppLayout() {
  const { isAuthenticated, isLoading, user, login } = useAuth();

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
