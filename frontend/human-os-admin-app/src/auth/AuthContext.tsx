import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { InteractionStatus, type AccountInfo } from '@azure/msal-browser';
import { useIsAuthenticated, useMsal } from '@azure/msal-react';
import { loginRequest } from './index';

interface AuthContextValue {
  account: AccountInfo | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
  login: () => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const { instance, inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (inProgress !== InteractionStatus.None) return;
    const account = instance.getActiveAccount() ?? instance.getAllAccounts()[0] ?? null;
    if (account && !instance.getActiveAccount()) {
      instance.setActiveAccount(account);
    }
  }, [instance, inProgress]);

  const account = instance.getActiveAccount() ?? instance.getAllAccounts()[0] ?? null;

  async function login() {
    setError(null);
    try {
      await instance.loginRedirect(loginRequest);
    } catch (cause) {
      console.error('[Auth] loginRedirect failed:', cause);
      setError(cause instanceof Error ? cause.message : 'No se pudo iniciar sesión con Microsoft.');
    }
  }

  async function logout() {
    setError(null);
    try {
      await instance.logoutRedirect({
        account,
        postLogoutRedirectUri: 'http://localhost:6200',
      });
    } catch (cause) {
      console.error('[Auth] logoutRedirect failed:', cause);
      setError(cause instanceof Error ? cause.message : 'No se pudo cerrar la sesión.');
    }
  }

  const value = useMemo(
    () => ({
      account,
      isAuthenticated,
      isLoading: inProgress !== InteractionStatus.None,
      error,
      login,
      logout,
    }),
    [account, error, inProgress, isAuthenticated],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
}
