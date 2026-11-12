import { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { AccessDenied } from '../components/AccessDenied';
import { getIdentityHeaders } from '../auth';
import { getLaunchSession, hasValidLaunchSession, getRelaunchInfo } from '../auth/launchSession';

// Guards a single auto-relaunch attempt per tab so a persistently broken
// parent-app identity (e.g. a deactivated account -> Forbidden) can't loop
// the browser between Enterprise/Family and Studio forever — see the
// auto-relaunch effect below.
const RELAUNCH_ATTEMPTED_KEY = 'humanos.studio.relaunchAttempted';

/** Silently re-requests a fresh launch code from the same parent app that
 *  originally opened Studio (see auth/launchSession.ts's RelaunchInfo),
 *  instead of dead-ending on "go back to your app and click Studio" every
 *  time the 12h launch session expires or a new tab has none yet. Returns
 *  true if a redirect was kicked off (caller should render nothing else). */
function attemptAutoRelaunch(): boolean {
  if (sessionStorage.getItem(RELAUNCH_ATTEMPTED_KEY)) return false;
  const info = getRelaunchInfo();
  if (!info || !info.email) return false;

  sessionStorage.setItem(RELAUNCH_ATTEMPTED_KEY, '1');
  const params = new URLSearchParams();
  if (info.oid) params.set('oid', info.oid);
  if (info.tid) params.set('tid', info.tid);
  params.set('email', info.email);
  if (info.asRole) params.set('asRole', info.asRole);
  params.set('returnTo', `${window.location.pathname}${window.location.search}`);

  window.location.href = `${info.base}?${params.toString()}`;
  return true;
}

/** Shell gate for the whole Studio app (2026-08-19): Studio no longer
 *  supports standalone MSAL login — it can only be opened via a parent
 *  app's "Studio" button, which lands on /launch and establishes a
 *  launch session (signed JWT, see auth/launchSession.ts). No launch
 *  session, no access — there is no login screen to fall back to, but
 *  (2026-11-09) we first try a silent auto-relaunch through the same
 *  parent app before showing that dead end. */
export default function StudioAuthGate() {
  const { signedInEmail } = useAuth();
  const hasLaunchSession = hasValidLaunchSession();
  const [relaunching, setRelaunching] = useState(false);
  const [studioAccess, setStudioAccess] = useState<boolean | null>(null);

  useEffect(() => {
    if (!hasLaunchSession) {
      if (attemptAutoRelaunch()) {
        setRelaunching(true);
      }
      setStudioAccess(null);
      return;
    }

    const launch = getLaunchSession();
    if (launch?.claims?.studioView === true) {
      setStudioAccess(true);
      return;
    }

    let cancelled = false;
    const accessUrl = `${import.meta.env.VITE_PRODUCT_ACCESS_API_URL ?? 'http://localhost:7072/api'}/me/product-access`;
    const identityHeaders = getIdentityHeaders();
    const contextUrl = `${import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:7071/api'}/me/studio-context`;
    const verifyLegacyMembership = async () => {
      const oid = identityHeaders['X-Azure-OID'];
      const email = signedInEmail ?? '';
      if (!oid && !email) throw new Error('No identity available');
      const query = oid ? `oid=${encodeURIComponent(oid)}` : `email=${encodeURIComponent(email)}`;
      const membershipResponse = await fetch(`http://localhost:7072/api/user/tenant?${query}`, { cache: 'no-store' });
      if (!membershipResponse.ok) throw new Error(`Membership request failed: ${membershipResponse.status}`);
      return true;
    };

    const resolveAccess = async () => {
      try {
        const contextResponse = await fetch(contextUrl, { headers: identityHeaders, cache: 'no-store' });
        if (contextResponse.ok) {
          const contextBody = await contextResponse.text();
          const headerTenantId = contextResponse.headers.get('X-Studio-Tenant-Id');
          const headerTenantName = contextResponse.headers.get('X-Studio-Tenant-Name');
          if (headerTenantId) sessionStorage.setItem('humanos.studio.inheritedTenantId', headerTenantId);
          if (headerTenantName) sessionStorage.setItem('humanos.studio.inheritedTenantName', headerTenantName);
          if (contextBody.trim()) {
            const context = JSON.parse(contextBody) as {
              tenantId?: string;
              organizationName?: string;
              permissions?: { studioView?: boolean };
            };
            console.info('[StudioContext] inherited tenant', {
              tenantId: context.tenantId,
              studioView: context.permissions?.studioView,
            });
            if (context.tenantId) {
              sessionStorage.setItem('humanos.studio.inheritedTenantId', context.tenantId);
            }
            if (context.organizationName) {
              sessionStorage.setItem('humanos.studio.inheritedTenantName', context.organizationName);
            }
            if (context.permissions?.studioView === true) return true;
          }
        }

        const response = await fetch(accessUrl, { headers: identityHeaders, cache: 'no-store' });
        if (response.ok) {
          const body = await response.text();
          if (body.trim()) {
            const data = JSON.parse(body) as { permissions?: { studioView?: boolean } };
            if (data.permissions?.studioView === true) return true;
          }
        }
      } catch {
        // Fall through to the membership endpoint during rollout.
      }

      return verifyLegacyMembership();
    };

    resolveAccess()
      .then((allowed) => {
        if (!cancelled) setStudioAccess(allowed);
      })
      .catch(() => {
        if (!cancelled) setStudioAccess(false);
      });

    return () => {
      cancelled = true;
    };
  }, [hasLaunchSession, signedInEmail]);

  if (!hasLaunchSession) {
    if (relaunching) {
      return (
        <div className="flex min-h-screen items-center justify-center bg-slate-950">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-slate-700 border-t-brand-400" />
        </div>
      );
    }
    return <AccessDenied reason="no-launch" />;
  }

  if (studioAccess === null) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-950">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-slate-700 border-t-brand-400" />
      </div>
    );
  }

  if (!studioAccess) {
    return <AccessDenied reason="role" />;
  }

  return <Outlet />;
}
