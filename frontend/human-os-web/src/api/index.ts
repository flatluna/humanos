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

/** Attach a Bearer token from MSAL before every request. The backend
 *  Functions are all `AuthorizationLevel.Anonymous` today (no JWT
 *  validation yet), so a failed token acquisition (e.g. the API resource
 *  not being exposed/consented yet in Entra ID) must NOT block the
 *  request — just proceed without the header. Once the backend starts
 *  actually enforcing the token, a real failure here should surface as a
 *  401 from the backend itself instead of silently killing every request
 *  client-side. */
apiClient.interceptors.request.use(async (config) => {
  const accounts = msalInstance.getAllAccounts();
  const account = msalInstance.getActiveAccount() ?? accounts[0] ?? null;

  if (account) {
    // The backend keys identity-checked endpoints (e.g. HrForceAdvanceStepFunction)
    // off these headers today, not the Bearer token (see AuthContext.tsx).
    const claims = account.idTokenClaims as Record<string, unknown> | undefined;
    const oid = (claims?.oid as string) ?? (claims?.sub as string) ?? account.homeAccountId.split('.')[0];
    const tid = (claims?.tid as string) ?? account.homeAccountId.split('.')[1];
    config.headers['X-Azure-OID'] = oid;
    config.headers['X-Azure-TID'] = tid;

    try {
      const result = await msalInstance.acquireTokenSilent({
        scopes: humanOsApiScopes,
        account,
      });

      config.headers.Authorization = `Bearer ${result.accessToken}`;
    } catch (error) {
      console.warn('Could not acquire an API access token — sending request without one.', error);
    }
  }

  return config;
});
