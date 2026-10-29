/** Cross-app launch session — Studio no longer supports standalone MSAL
 *  login (2026-08-19). It can only be opened via a parent app's "Studio"
 *  button, which redirects here with a one-time `?code=` (see
 *  LaunchPage.tsx). That code is exchanged for a signed JWT (see
 *  docs/STUDIO_LAUNCH_INTEGRATION.md), which is then the sole source of
 *  identity/tenant for the rest of the app. Works for any parent app that
 *  implements the same create-launch/exchange pair (NGO backend today). */

import { decodeJwtPayload, type StudioLaunchClaims } from './jwt';

const STORAGE_KEY = 'humanos.studio.launchSession';
export const STORAGE_CHANGE_EVENT = 'humanos-studio-session-change';
// Legacy NGO-membership fallback values cached by StudioAuthGate (see
// Layout.tsx) — must never outlive the launch session that wrote them,
// otherwise a previous user's tenant/company keeps showing up for whoever
// logs in next in the same tab (looked "hardcoded" — 2026-10-06 bug report).
const INHERITED_TENANT_ID_KEY = 'humanos.studio.inheritedTenantId';
const INHERITED_TENANT_NAME_KEY = 'humanos.studio.inheritedTenantName';

function clearInheritedTenant(): void {
  sessionStorage.removeItem(INHERITED_TENANT_ID_KEY);
  sessionStorage.removeItem(INHERITED_TENANT_NAME_KEY);
}

export interface LaunchSession {
  token: string;
  claims: StudioLaunchClaims;
}

function notifySessionChange(): void {
  if (typeof window !== 'undefined') {
    window.dispatchEvent(new Event(STORAGE_CHANGE_EVENT));
  }
}

export function saveLaunchSession(token: string): LaunchSession | null {
  const claims = decodeJwtPayload(token);
  if (!claims) return null;
  const session: LaunchSession = { token, claims };
  // A brand-new launch always carries its own tenant/company claims —
  // drop any stale inherited-tenant cache from a previous session so it
  // can't leak into this one (see clearInheritedTenant's doc comment).
  clearInheritedTenant();
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  notifySessionChange();
  return session;
}

export function getLaunchSession(): LaunchSession | null {
  const raw = sessionStorage.getItem(STORAGE_KEY);
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as LaunchSession;
    // exp is in seconds since epoch (standard JWT claim)
    if (!parsed.claims?.exp || Date.now() >= parsed.claims.exp * 1000) {
      sessionStorage.removeItem(STORAGE_KEY);
      return null;
    }
    return parsed;
  } catch {
    sessionStorage.removeItem(STORAGE_KEY);
    return null;
  }
}

export function hasValidLaunchSession(): boolean {
  return getLaunchSession() !== null;
}

export function clearLaunchSession(): void {
  sessionStorage.removeItem(STORAGE_KEY);
  clearInheritedTenant();
  notifySessionChange();
}

