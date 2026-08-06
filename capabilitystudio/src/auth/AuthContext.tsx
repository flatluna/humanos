import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { useMsal, useIsAuthenticated } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import { loginRequest } from './index';

// ── Types ─────────────────────────────────────────────────────────────────

export type OrgRole = 'Employee' | 'Manager' | 'HRAdmin';

export interface StudioUser {
  oid: string;
  tid: string;
  name: string;
  email: string;
  personId: string;
  /** null for an individual/Consumer account — allowed to create personal
   *  courses. Non-null means an org member — only OrgRole 'HRAdmin' is
   *  allowed to create courses for their tenant (see canAuthorCourses). */
  tenantId: string | null;
  tenantName: string | null;
  orgRole: OrgRole;
}

interface AuthContextValue {
  user: StudioUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  backendError: boolean;
  /** true if this account has no Human OS Person at all yet — Studio has
   *  no onboarding flow of its own; the user must sign up via
   *  human-os-web first. */
  noAccount: boolean;
  /** Studio's own access rule (2026-08-05): a Consumer (no tenant) or an
   *  HRAdmin can author courses; a plain Employee/Manager of an
   *  organization cannot — they consume assigned content only. */
  canAuthorCourses: boolean;
  signedInEmail: string | null;
  login: () => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue>({
  user: null,
  isAuthenticated: false,
  isLoading: true,
  backendError: false,
  noAccount: false,
  canAuthorCourses: false,
  signedInEmail: null,
  login: async () => {},
  logout: async () => {},
  refreshUser: async () => {},
});

function extractOid(claims: Record<string, unknown> | undefined, homeAccountId: string): string {
  return (claims?.oid as string) ?? (claims?.sub as string) ?? homeAccountId.split('.')[0] ?? '';
}

function extractTid(claims: Record<string, unknown> | undefined, homeAccountId: string): string {
  return (claims?.tid as string) ?? homeAccountId.split('.')[1] ?? '';
}

interface MeResponse {
  personId: string;
  tenantId: string | null;
  tenantName: string | null;
  email: string | null;
  orgRole: string;
}

// ── Provider ──────────────────────────────────────────────────────────────

export function AuthProvider({ children }: { children: ReactNode }) {
  const { instance, inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const [user, setUser] = useState<StudioUser | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [backendError, setBackendError] = useState(false);
  const [noAccount, setNoAccount] = useState(false);
  const [signedInEmail, setSignedInEmail] = useState<string | null>(null);

  async function login() {
    try {
      await instance.loginRedirect(loginRequest);
    } catch (err) {
      console.error('[Auth] loginRedirect failed:', err);
    }
  }

  async function logout() {
    setUser(null);
    await instance.logoutRedirect();
  }

  async function loadUser() {
    if (inProgress !== InteractionStatus.None) return;
    if (!isAuthenticated) {
      setIsLoading(false);
      return;
    }

    const account = instance.getActiveAccount() ?? instance.getAllAccounts()[0] ?? null;
    if (!account) {
      setIsLoading(false);
      return;
    }

    const claims = account.idTokenClaims as Record<string, unknown> | undefined;
    const oid = extractOid(claims, account.homeAccountId);
    const tid = extractTid(claims, account.homeAccountId);
    const name = account.name ?? (claims?.name as string) ?? 'Usuario';
    const email =
      account.username ?? (claims?.email as string) ?? (claims?.preferred_username as string) ?? '';
    setSignedInEmail(email || null);

    if (!oid) {
      setIsLoading(false);
      return;
    }

    try {
      const res = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:7071/api'}/me`, {
        cache: 'no-store',
        headers: { 'X-Azure-OID': oid, 'X-Azure-TID': tid },
      });

      if (res.ok) {
        const data = (await res.json()) as MeResponse;
        setBackendError(false);
        setNoAccount(false);
        setUser({
          oid,
          tid,
          name,
          email: data.email ?? email,
          personId: data.personId,
          tenantId: data.tenantId,
          tenantName: data.tenantName,
          orgRole: (data.orgRole as OrgRole) ?? 'Employee',
        });
      } else if (res.status === 404) {
        // No Human OS account at all yet — must sign up via human-os-web
        // first; Studio does not have its own onboarding flow.
        setBackendError(false);
        setNoAccount(true);
        setUser(null);
      } else {
        setBackendError(true);
      }
    } catch {
      setBackendError(true);
    } finally {
      setIsLoading(false);
    }
  }

  async function refreshUser() {
    setIsLoading(true);
    await loadUser();
  }

  useEffect(() => {
    if (inProgress === InteractionStatus.None) {
      if (isAuthenticated) setIsLoading(true);
      loadUser();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, inProgress]);

  const canAuthorCourses = !!user && (user.tenantId === null || user.orgRole === 'HRAdmin');

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated,
        isLoading,
        backendError,
        noAccount,
        canAuthorCourses,
        signedInEmail,
        login,
        logout,
        refreshUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
