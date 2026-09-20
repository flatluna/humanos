import type { ComponentType } from 'react';
import { Navigate } from 'react-router';
import { useAuth } from '@/auth/AuthContext';

/** Guards the student-only "Tec Contigo" section — redirects to the
 * dashboard for any signed-in user whose Tenant isn't Academic. */
export function withAcademicGuard<P extends object>(Component: ComponentType<P>) {
  return function AcademicGuardedRoute(props: P) {
    const { user } = useAuth();
    if (!user?.isAcademicTenant) {
      return <Navigate to="/" replace />;
    }
    return <Component {...props} />;
  };
}
