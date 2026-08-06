import { useEffect, useRef } from 'react';
import { Outlet } from 'react-router-dom';
import { InteractionStatus } from '@azure/msal-browser';
import { useMsal } from '@azure/msal-react';
import { useAuth } from '../auth/AuthContext';
import { AccessDenied } from '../components/AccessDenied';

const REDIRECT_GUARD_KEY = 'humanos.studio.loginRedirect.startedAt';
const REDIRECT_GUARD_MAX_AGE_MS = 30_000;

/** Shell gate for the whole Studio app: real MSAL session (silent SSO —
 *  same CIAM tenant/app registration as the other three Human OS apps),
 *  then Studio's own access rule (canAuthorCourses — Consumer or HRAdmin
 *  only, 2026-08-05), then the routed content. Mirrors
 *  human-os-enterprise's EnterpriseAppLayout gate. */
export default function StudioAuthGate() {
  const { isAuthenticated, isLoading, backendError, noAccount, canAuthorCourses, user, login, refreshUser } =
    useAuth();
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
      <div className="flex min-h-screen items-center justify-center bg-slate-950">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-slate-700 border-t-brand-400" />
      </div>
    );
  }

  if (backendError) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-950 px-4 text-center">
        <p className="text-lg font-semibold text-white">No pudimos conectar con el servidor</p>
        <p className="max-w-md text-sm text-slate-400">
          Tu sesión es válida, pero el backend no respondió correctamente. Verifica tu conexión e inténtalo de nuevo.
        </p>
        <button
          type="button"
          onClick={() => void refreshUser()}
          className="inline-flex min-h-11 items-center justify-center rounded-full bg-white px-6 py-2.5 text-sm font-medium text-slate-900 transition hover:bg-white/90"
        >
          Reintentar
        </button>
      </div>
    );
  }

  if (noAccount || !user) {
    return <AccessDenied reason="no-account" />;
  }

  if (!canAuthorCourses) {
    return <AccessDenied reason="role" />;
  }

  return <Outlet />;
}
