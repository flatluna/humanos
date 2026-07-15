import { Outlet } from 'react-router';
import { useIsAuthenticated, useMsal } from '@azure/msal-react';
import { humanOsApiScopes } from '@/auth';

export function RootLayout() {
  const { instance } = useMsal();
  const isAuthenticated = useIsAuthenticated();

  if (!isAuthenticated) {
    instance.loginRedirect({ scopes: humanOsApiScopes });
    return null;
  }

  return (
    <main>
      {/* TODO: Add navigation shell and global layout. */}
      <Outlet />
    </main>
  );
}
