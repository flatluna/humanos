import { Outlet } from 'react-router-dom';
import TopBar from './TopBar';
import Sidebar from './Sidebar';
import type { StudentUser } from '../../types';

/**
 * Mock user — matches humanstudio's AppShell.tsx pattern (mock, TODO real
 * auth). Real MSAL/Entra auth exists elsewhere (frontend/human-os-web/src/
 * auth) and a backend GET /api/me (GetMeFunction.cs) already returns
 * PersonId/Email for a signed-in Azure identity — but it doesn't return a
 * display Name yet, and wiring MSAL into a brand-new app is a separate
 * task. Replace this mock with real useAuth()/GetMeFunction data once
 * auth is wired into humanlearn.
 *
 * `oid` MUST be a real, already-seeded Person row's PersonId — it can't be
 * Guid.Empty (backend runtime endpoints like RuntimeStartSession reject
 * Guid.Empty with 400) and it must satisfy the Person FK on
 * LearningSessions (a made-up GUID would fail with a DB FK error). This
 * reuses the existing local dev test person seeded during Paso 5 testing.
 */
export const MOCK_USER: StudentUser = {
  name: 'Alumno Demo',
  email: 'test-learner@humanos.local',
  oid: '22e52050-2b03-475f-93b9-880b81e50663',
};

export default function AppShell() {
  const handleSignOut = () => {
    // TODO: implement real sign-out once auth is wired in.
    console.log('Sign out clicked');
  };

  return (
    <div className="relative flex h-screen flex-col overflow-hidden bg-slate-950">
      <div className="pointer-events-none fixed inset-0 bg-mesh-gradient opacity-40" />
      <TopBar user={MOCK_USER} onSignOut={handleSignOut} />
      <div className="relative flex flex-1 overflow-hidden">
        <Sidebar />
        <main className="flex-1 overflow-y-auto">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
