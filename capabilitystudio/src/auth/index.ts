import { getLaunchSession } from './launchSession';

/** Identity headers sent on every Studio -> backend call. `Authorization`
 *  carries the signed JWT (backends re-verify signature/expiry server
 *  side); the X-Azure-* headers are kept for backends that haven't been
 *  upgraded to validate the JWT yet (legacy fallback during rollout — see
 *  docs/STUDIO_LAUNCH_INTEGRATION.md). Returns an empty object when there's
 *  no active launch session, so callers can spread the result unconditionally. */
export function getIdentityHeaders(): Record<string, string> {
  const session = getLaunchSession();
  if (!session) return {};

  return {
    Authorization: `Bearer ${session.token}`,
    ...(session.claims.oid ? { 'X-Azure-OID': session.claims.oid } : {}),
    'X-Azure-Email': session.claims.email,
    ...(session.claims.tenantId ? { 'X-Studio-Tenant-Id': session.claims.tenantId } : {}),
  };
}

