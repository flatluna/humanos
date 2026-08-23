/** Cross-app launch session — Studio no longer supports standalone MSAL
 *  login (2026-08-19). It can only be opened via a parent app's "Studio"
 *  button, which redirects here with a one-time `?code=` (see
 *  LaunchPage.tsx). That code is exchanged for a signed JWT (see
 *  docs/STUDIO_LAUNCH_INTEGRATION.md), which is then the sole source of
 *  identity/tenant for the rest of the app. Works for any parent app that
 *  implements the same create-launch/exchange pair (NGO backend today). */

import { decodeJwtPayload, type StudioLaunchClaims } from './jwt';

const STORAGE_KEY = 'humanos.studio.launchSession';

export interface LaunchSession {
  token: string;
  claims: StudioLaunchClaims;
}

export function saveLaunchSession(token: string): LaunchSession | null {
  const claims = decodeJwtPayload(token);
  if (!claims) return null;
  const session: LaunchSession = { token, claims };
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
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
}

