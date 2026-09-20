import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { useMsal, useIsAuthenticated } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import { loginRequest } from './index';

// ── Types ─────────────────────────────────────────────────────────────────

export type OrgRole = 'Employee' | 'Manager' | 'HRAdmin';

export interface EmployeePortalUser {
  oid: string;
  tid: string;
  name: string;
  email: string;
  personId: string;
  tenantId: string;
  tenantName: string | null;
  /** True when the Tenant is an academic institution (Tenant.OrgType ==
   *  "Academic", e.g. Tecnológico de Monterrey) rather than a corporate
   *  employer — drives branding/presentation (logo, header copy, whether
   *  the student-only "Tec Contigo" section is reachable). */
  isAcademicTenant: boolean;
  /** Person.OrgRole from /api/me. Unlike human-os-enterprise, this app does
   *  NOT gate access by role — Employee/Manager/HRAdmin can all log in here
   *  to take their own assigned courses; it's purely informational. */
  orgRole: OrgRole;
  /** Org chart assignment (2026-08-02, "Mi Perfil") — null until an
   *  HRAdmin assigns them. Lets the profile page show/derive "Ocupación"
   *  from the real Job Role instead of free text, and fetch the full job
   *  description (skills/responsibilities) for a "my role & development"
   *  section. */
  departmentId: string | null;
  departmentName: string | null;
  jobRoleId: string | null;
  jobRoleTitle: string | null;
  managerId: string | null;
  managerDisplayName: string | null;
}

interface AuthContextValue {
  user: EmployeePortalUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  /** true if the signed-in identity has no Tenant (individual account)
   *  or no Person at all yet — this portal requires an existing,
   *  onboarded company account; there is no separate onboarding flow
   *  here (onboarding happens in human-os-web). */
  notEnterpriseAccount: boolean;
  backendError: boolean;
  /** The MSAL account's email/username, tracked independently of `user`
   *  so AccessDenied can show *which* account was checked — this was
   *  the key piece of info missing while debugging why the screen kept
   *  showing up (the actual signed-in account didn't match what was
   *  assumed). */
  signedInEmail: string | null;
  login: () => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue>({
  user: null,
  isAuthenticated: false,
  isLoading: true,
  notEnterpriseAccount: false,
  backendError: false,
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
  isAcademicTenant: boolean;
  email: string | null;
  preferredLanguage: string | null;
  orgRole: string;
  departmentId: string | null;
  departmentName: string | null;
  jobRoleId: string | null;
  jobRoleTitle: string | null;
  managerId: string | null;
  managerDisplayName: string | null;
}

// ── Provider ──────────────────────────────────────────────────────────────

export function AuthProvider({ children }: { children: ReactNode }) {
  const { instance, inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const [user, setUser] = useState<EmployeePortalUser | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [backendError, setBackendError] = useState(false);
  const [notEnterpriseAccount, setNotEnterpriseAccount] = useState(false);
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
      // cache: 'no-store' — this endpoint has no Cache-Control header, and
      // the browser's default heuristic HTTP caching was silently reusing a
      // stale response across reloads (e.g. from before a Person got linked
      // to a Tenant), so we must force a fresh request every time.
      const res = await fetch('/api/me', {
        cache: 'no-store',
        headers: { 'X-Azure-OID': oid, 'X-Azure-TID': tid, 'X-Azure-Email': email },
      });

      if (res.ok) {
        const data = (await res.json()) as MeResponse;
        setBackendError(false);

        if (!data.tenantId) {
          // Individual (no-company) account — this console has nothing
          // for them, they belong in the consumer app instead.
          setNotEnterpriseAccount(true);
          setUser(null);
          return;
        }

        setNotEnterpriseAccount(false);
        setUser({
          oid,
          tid,
          name,
          email: data.email ?? email,
          personId: data.personId,
          tenantId: data.tenantId,
          tenantName: data.tenantName,
          isAcademicTenant: data.isAcademicTenant,
          orgRole: (data.orgRole as OrgRole) ?? 'Employee',
          departmentId: data.departmentId,
          departmentName: data.departmentName,
          jobRoleId: data.jobRoleId,
          jobRoleTitle: data.jobRoleTitle,
          managerId: data.managerId,
          managerDisplayName: data.managerDisplayName,
        });
      } else if (res.status === 404) {
        // No Human OS account at all yet — must onboard via human-os-web
        // first; this console does not have its own onboarding flow.
        setBackendError(false);
        setNotEnterpriseAccount(true);
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

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated,
        isLoading,
        notEnterpriseAccount,
        backendError,
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
