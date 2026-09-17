import { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { AccessDenied } from '../components/AccessDenied';
import { getIdentityHeaders } from '../auth';
import { getLaunchSession, hasValidLaunchSession } from '../auth/launchSession';

/** Shell gate for the whole Studio app (2026-08-19): Studio no longer
 *  supports standalone MSAL login — it can only be opened via a parent
 *  app's "Studio" button, which lands on /launch and establishes a
 *  launch session (signed JWT, see auth/launchSession.ts). No launch
 *  session, no access — there is no login screen to fall back to. */
export default function StudioAuthGate() {
  const { signedInEmail } = useAuth();
  const hasLaunchSession = hasValidLaunchSession();
  const [studioAccess, setStudioAccess] = useState<boolean | null>(null);

  useEffect(() => {
    if (!hasLaunchSession) {
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
