import axios from 'axios';
import { QueryClient } from '@tanstack/react-query';
import { msalInstance, humanOsApiScopes } from '@/auth';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5,
      retry: 1,
    },
  },
});

export const apiClient = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
});

/** Attach a Bearer token from MSAL before every request, plus the
 *  X-Azure-OID/TID headers the backend actually keys off of today (same
 *  pattern as human-os-web — see its AuthContext for the known
 *  api://human-os infra gap this try/catch guards against). */
apiClient.interceptors.request.use(async (config) => {
  const accounts = msalInstance.getAllAccounts();
  const account = msalInstance.getActiveAccount() ?? accounts[0] ?? null;

  if (account) {
    const claims = account.idTokenClaims as Record<string, unknown> | undefined;
    const oid = (claims?.oid as string) ?? (claims?.sub as string) ?? account.homeAccountId.split('.')[0];
    const tid = (claims?.tid as string) ?? account.homeAccountId.split('.')[1];
    config.headers['X-Azure-OID'] = oid;
    config.headers['X-Azure-TID'] = tid;

    if (humanOsApiScopes.length > 0) {
      try {
        const result = await msalInstance.acquireTokenSilent({ scopes: humanOsApiScopes, account });
        config.headers.Authorization = `Bearer ${result.accessToken}`;
      } catch (error) {
        console.warn('Could not acquire an API access token — sending request without one.', error);
      }
    }
  }

  return config;
});
