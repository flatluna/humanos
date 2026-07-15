import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { useMsal, useIsAuthenticated } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import { loginRequest } from './index';

// ── Types ─────────────────────────────────────────────────────────────────

export interface HumanOsUser {
  /** Azure Object ID — identity primary key, shared across the whole
   *  twinetwork CIAM ecosystem (same login as genesis-personas). */
  oid: string;
  /** Azure (CIAM) directory ID this token was issued from. Constant for
   *  every user in this shared external tenant — NOT what distinguishes
   *  one Human OS customer company from another (see `tenantId`). */
  tid: string;
  name: string;
  email: string;
  /** Human OS `Person.PersonId` — null until onboarding creates it. */
  personId: string | null;
  /** Human OS `Tenant.TenantId` (the customer company) — null until
   *  onboarding creates it. */
  tenantId: string | null;
  tenantName: string | null;
  /** Has this person already completed onboarding (Tenant + Person +
   *  PersonProfile all exist)? */
  onboarded: boolean;
  /** When the current token was issued (claim "iat") — a proxy for last login. */
  lastLogin: string | null;
}

interface AuthContextValue {
  user: HumanOsUser | null;
  isAuthenticated: boolean;
  /** true while MSAL is processing or the backend lookup is in flight. */
  isLoading: boolean;
  login: () => Promise<void>;
  logout: () => Promise<void>;
  /** Call after onboarding completes to reload the person/tenant. */
  refreshUser: () => Promise<void>;
}

// ── Context ───────────────────────────────────────────────────────────────

const AuthContext = createContext<AuthContextValue>({
  user: null,
  isAuthenticated: false,
  isLoading: true,
  login: async () => {},
  logout: async () => {},
  refreshUser: async () => {},
});

// ── Helpers ───────────────────────────────────────────────────────────────

/** Extracts the OID from the MSAL token — works for both Entra ID and CIAM. */
function extractOid(claims: Record<string, unknown> | undefined, homeAccountId: string): string {
  return (claims?.oid as string) ?? (claims?.sub as string) ?? homeAccountId.split('.')[0] ?? '';
}

function extractTid(claims: Record<string, unknown> | undefined, homeAccountId: string): string {
  return (claims?.tid as string) ?? homeAccountId.split('.')[1] ?? '';
}

interface MeResponse {
  personId: string;
  tenantId: string;
  tenantName: string;
  email: string | null;
}

// ── Provider ──────────────────────────────────────────────────────────────

export function AuthProvider({ children }: { children: ReactNode }) {
  const { instance, inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const [user, setUser] = useState<HumanOsUser | null>(null);
  const [isLoading, setIsLoading] = useState(false);

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
    const iat = claims?.iat as number | undefined;
    const lastLogin = iat ? new Date(iat * 1000).toISOString() : null;

    if (!oid) {
      // OID not available yet — the token may still be loading.
      setIsLoading(false);
      return;
    }

    try {
      const res = await fetch('/api/me', {
        headers: { 'X-Azure-OID': oid, 'X-Azure-TID': tid },
      });

      if (res.ok) {
        const data = (await res.json()) as MeResponse;
        setUser({
          oid,
          tid,
          name,
          email: data.email ?? email,
          personId: data.personId,
          tenantId: data.tenantId,
          tenantName: data.tenantName,
          onboarded: true,
          lastLogin,
        });
      } else if (res.status === 404) {
        // First time — needs to complete onboarding (create the company + admin).
        setUser({ oid, tid, name, email, personId: null, tenantId: null, tenantName: null, onboarded: false, lastLogin });
      } else {
        setUser({ oid, tid, name, email, personId: null, tenantId: null, tenantName: null, onboarded: false, lastLogin });
      }
    } catch {
      // Backend unavailable (e.g. dev without the Functions host running) —
      // treat as not yet onboarded rather than pretending success.
      setUser({ oid, tid, name, email, personId: null, tenantId: null, tenantName: null, onboarded: false, lastLogin });
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
      if (isAuthenticated) setIsLoading(true); // show loading while fetching the user
      loadUser();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, inProgress]);

  return (
    <AuthContext.Provider value={{ user, isAuthenticated, isLoading, login, logout, refreshUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
