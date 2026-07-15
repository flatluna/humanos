import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { useMsal, useIsAuthenticated } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import { loginRequest } from '../config/authConfig';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface PersonasUser {
  /** Azure Object ID — clave primaria de identidad */
  oid:         string;
  /** Nombre del usuario en Entra ID */
  name:        string;
  /** Email del usuario en Entra ID */
  email:       string;
  /** ConsumidorCuenta.Id — null si aún no se ha registrado (onboarding pendiente) */
  cuentaId:    string | null;
  /** ¿Ya completó el registro de su cuenta consumidor? */
  onboarded:   boolean;
  /** Momento en que se emitió el token actual (claim "iat") — proxy del último login */
  lastLogin:   string | null;
}

interface AuthContextValue {
  user:            PersonasUser | null;
  isAuthenticated: boolean;
  /** true mientras MSAL procesa o se consulta el backend */
  isLoading:       boolean;
  login:           () => Promise<void>;
  logout:          () => Promise<void>;
  /** Llama después del onboarding para recargar la cuenta */
  refreshUser:     () => Promise<void>;
}

// ── Context ───────────────────────────────────────────────────────────────────

const AuthContext = createContext<AuthContextValue>({
  user:            null,
  isAuthenticated: false,
  isLoading:       true,
  login:           async () => {},
  logout:          async () => {},
  refreshUser:     async () => {},
});

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Extrae el OID del token MSAL — soporta Entra ID y CIAM */
function extractOid(claims: Record<string, unknown> | undefined, homeAccountId: string): string {
  return (
    (claims?.oid as string)
    ?? (claims?.sub as string)
    ?? homeAccountId.split('.')[0]
    ?? ''
  );
}

// ── Provider ──────────────────────────────────────────────────────────────────

export function AuthProvider({ children }: { children: ReactNode }) {
  const { instance, inProgress } = useMsal();
  const isAuthenticated           = useIsAuthenticated();
  const [user,      setUser]      = useState<PersonasUser | null>(null);
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
    if (!isAuthenticated) { setIsLoading(false); return; }

    const account = instance.getActiveAccount()
      ?? instance.getAllAccounts()[0]
      ?? null;

    if (!account) { setIsLoading(false); return; }

    const claims    = account.idTokenClaims as Record<string, unknown> | undefined;
    const oid       = extractOid(claims, account.homeAccountId);
    const name      = account.name ?? (claims?.name as string) ?? 'Usuario';
    const email     = account.username ?? (claims?.email as string) ?? (claims?.preferred_username as string) ?? '';
    const iat       = claims?.iat as number | undefined;
    const lastLogin = iat ? new Date(iat * 1000).toISOString() : null;

    if (!oid) {
      // OID no disponible aún — el token puede estar cargando
      setIsLoading(false);
      return;
    }

    try {
      const res = await fetch('/api/consumidor/me', {
        headers: { 'X-Azure-OID': oid },
      });

      if (res.ok) {
        const data = await res.json();
        // El nombre visible en la app usa el perfil real (editable) en vez del
        // displayName de la cuenta de Microsoft, que puede tener errores/typos
        // y no está sincronizado con lo que el usuario captura en su perfil.
        const admin        = (data.perfiles as Array<{ esAdmin: boolean; nombre: string; apellidoPaterno: string }> | undefined)
          ?.find((p) => p.esAdmin);
        const profileName  = admin ? `${admin.nombre} ${admin.apellidoPaterno}`.trim() : '';
        setUser({ oid, name: profileName || name, email, cuentaId: data.id ?? null, onboarded: true, lastLogin });
      } else if (res.status === 404) {
        // Primera vez — necesita registrar su cuenta consumidor
        setUser({ oid, name, email, cuentaId: null, onboarded: false, lastLogin });
      } else {
        setUser({ oid, name, email, cuentaId: null, onboarded: false, lastLogin });
      }
    } catch {
      // Backend no disponible (ej. dev sin backend) — tratar como no registrado
      setUser({ oid, name, email, cuentaId: null, onboarded: false, lastLogin });
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
      if (isAuthenticated) setIsLoading(true); // show loading while fetching user
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
