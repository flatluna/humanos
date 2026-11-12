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

// Auto-relaunch (2026-11-09): when the launch session JWT expires, or
// Studio is opened in a fresh tab that never got one, StudioAuthGate used
// to dead-end on "go back to your app and click Studio" — annoying for an
// identity the parent app (Enterprise/Family/etc.) already knows about via
// its own persistent login (MSAL). We persist just enough to silently
// re-request a fresh launch code from that same parent app's backend
// in localStorage (survives new tabs/reloads, unlike the sessionStorage
// launch session itself) and clear it on explicit logout so a signed-out
// user isn't silently signed back in.
const RELAUNCH_KEY = 'humanos.studio.relaunch';

export interface RelaunchInfo {
  /** Parent app's "create launch" endpoint, e.g.
   *  "http://localhost:7071/api/humanos/studio-launch" — derived from the
   *  exchangeUrl LaunchPage used (same path, minus the trailing "/exchange"). */
  base: string;
  oid: string | null;
  tid: string | null;
  email: string;
  /** Optional launch role override, e.g. "University" — see
   *  HumanOsStudioLaunchFunctions.CreateAsync's `asRole` query param. */
  asRole?: string;
}

export function saveRelaunchInfo(info: RelaunchInfo): void {
  try {
    localStorage.setItem(RELAUNCH_KEY, JSON.stringify(info));
  } catch {
    // Best-effort — storage can be unavailable (private mode, quota); the
    // user just falls back to the manual "go back to your app" screen.
  }
}

export function getRelaunchInfo(): RelaunchInfo | null {
  try {
    const raw = localStorage.getItem(RELAUNCH_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as RelaunchInfo;
  } catch {
    return null;
  }
}

export function clearRelaunchInfo(): void {
  localStorage.removeItem(RELAUNCH_KEY);
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
  clearRelaunchInfo();
  notifySessionChange();
}

