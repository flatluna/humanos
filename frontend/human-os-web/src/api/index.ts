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

/** Attach a Bearer token from MSAL before every request. */
apiClient.interceptors.request.use(async (config) => {
  const accounts = msalInstance.getAllAccounts();

  if (accounts.length > 0) {
    const result = await msalInstance.acquireTokenSilent({
      scopes: humanOsApiScopes,
      account: accounts[0],
    });

    config.headers.Authorization = `Bearer ${result.accessToken}`;
  }

  return config;
});
