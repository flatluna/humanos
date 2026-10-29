import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { getLaunchSession, clearLaunchSession, STORAGE_CHANGE_EVENT, type LaunchSession } from './launchSession';

// ── Types ─────────────────────────────────────────────────────────────────

export interface StudioUser {
  oid: string | null;
  email: string;
  memberId: string;
  /** 'Employee' | 'Volunteer' | whatever the parent app's member model
   *  calls it — used only for display (see components/Layout.tsx). */
  memberType: string;
  tenantId: string | null;
  tenantName: string | null;
  /** Human-readable name of the app that launched Studio (e.g. "NGO
   *  Automate") — drives the dynamic "Back to X" label in Layout.tsx. */
  sourceApplication: string | null;
  /** URL of the parent app that launched Studio — drives the "back" button
   *  in Layout.tsx instead of a hardcoded Human OS URL. */
  returnUrl: string | null;
}

interface AuthContextValue {
  user: StudioUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  signedInEmail: string | null;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue>({
  user: null,
  isAuthenticated: false,
  isLoading: false,
  signedInEmail: null,
  logout: async () => {},
});

function toStudioUser(session: LaunchSession): StudioUser {
  const { claims } = session;
  return {
    oid: claims.oid,
    email: claims.email,
    memberId: claims.memberId,
    memberType: claims.memberType,
    tenantId: claims.tenantId,
    tenantName: claims.tenantName,
    sourceApplication: claims.sourceApplication || null,
    returnUrl: claims.returnUrl || null,
  };
}

// ── Provider ──────────────────────────────────────────────────────────────

/** Studio's identity now comes exclusively from the launch session JWT
 *  established by LaunchPage (2026-08-19) — no MSAL, no standalone login.
 *  See docs/STUDIO_LAUNCH_INTEGRATION.md for the full contract. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<StudioUser | null>(() => {
    const session = getLaunchSession();
    return session ? toStudioUser(session) : null;
  });

  useEffect(() => {
    // A parent-tab or another Studio tab clearing/renewing the session
    // (e.g. after logout) should be reflected here too.
    function handleStorage() {
      const session = getLaunchSession();
      setUser(session ? toStudioUser(session) : null);
    }
    window.addEventListener('storage', handleStorage);
    // Same-tab launch (LaunchPage saves the session without a full reload)
    // must also update the context so the header renders the user immediately.
    window.addEventListener(STORAGE_CHANGE_EVENT, handleStorage);
    return () => {
      window.removeEventListener('storage', handleStorage);
      window.removeEventListener(STORAGE_CHANGE_EVENT, handleStorage);
    };
  }, []);

  async function logout() {
    clearLaunchSession();
    setUser(null);
    // No standalone login screen exists anymore — send the user back to
    // Studio's own entry point, which will show the "open from your app"
    // message (StudioAuthGate reason="no-launch").
    window.location.href = '/';
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: user !== null,
        isLoading: false,
        signedInEmail: user?.email ?? null,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}

