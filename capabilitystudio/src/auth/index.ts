import { PublicClientApplication, LogLevel, type Configuration } from '@azure/msal-browser';

// ══════════════════════════════════════════════════════════════
// MSAL CONFIGURATION — Microsoft Entra External ID (CIAM)
// Reused verbatim from human-os-web/human-os-enterprise/human-os-employee
// (same tenant, same app registration): shared login across the whole
// twinetwork ecosystem, not a separate registration per app. Because it's
// the SAME Client ID + tenant already carrying an active session from
// whichever app the user came from, signing in here is silent SSO — no
// second password prompt.
//
// App Registration:
//   - Name:      twinetstudiologin
//   - Client ID: 2d6b1a7e-e1f4-4c18-bf7d-c3fb7bc86b6d
//   - Platform:  Single-page application
//
// ⚠️ ACTION REQUIRED (cannot be done from code): this app runs on
// http://localhost:3010, but as of 2026-08-05 the `twinetstudiologin` app
// registration does not yet have it registered as an SPA redirect URI
// (only 5178/5179/5180 are). Add http://localhost:3010 as an ADDITIONAL
// redirect URI (Entra Portal → App registrations → twinetstudiologin →
// Authentication → Single-page application → Add URI) — SPA registrations
// support multiple redirect URIs, so this does not affect the other three
// apps. Until that's added, login here will fail with AADSTS50011
// (redirect_uri_mismatch).
// ══════════════════════════════════════════════════════════════

const CIAM_METADATA = JSON.stringify({
  token_endpoint: 'https://twinetwork.ciamlogin.com/0e9c8663-a4ff-440e-af94-be25e63a1a6a/oauth2/v2.0/token',
  jwks_uri: 'https://twinetwork.ciamlogin.com/0e9c8663-a4ff-440e-af94-be25e63a1a6a/discovery/v2.0/keys',
  issuer: 'https://0e9c8663-a4ff-440e-af94-be25e63a1a6a.ciamlogin.com/0e9c8663-a4ff-440e-af94-be25e63a1a6a/v2.0',
  authorization_endpoint: 'https://twinetwork.ciamlogin.com/0e9c8663-a4ff-440e-af94-be25e63a1a6a/oauth2/v2.0/authorize',
  end_session_endpoint: 'https://twinetwork.ciamlogin.com/0e9c8663-a4ff-440e-af94-be25e63a1a6a/oauth2/v2.0/logout',
  response_types_supported: ['code', 'id_token', 'code id_token'],
  scopes_supported: ['openid', 'profile', 'email', 'offline_access'],
  subject_types_supported: ['pairwise'],
  id_token_signing_alg_values_supported: ['RS256'],
  userinfo_endpoint: 'https://graph.microsoft.com/oidc/userinfo',
  cloud_instance_name: 'microsoftonline.com',
  msgraph_host: 'graph.microsoft.com',
});

const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_AZURE_CLIENT_ID ?? '2d6b1a7e-e1f4-4c18-bf7d-c3fb7bc86b6d',
    authority: 'https://twinetwork.ciamlogin.com/twinetwork.onmicrosoft.com/',
    knownAuthorities: ['twinetwork.ciamlogin.com'],
    authorityMetadata: CIAM_METADATA,
    redirectUri: import.meta.env.VITE_REDIRECT_URI ?? 'http://localhost:3010',
    postLogoutRedirectUri: import.meta.env.VITE_POST_LOGOUT_REDIRECT_URI ?? 'http://localhost:3010',
  },
  cache: {
    cacheLocation: 'sessionStorage',
  },
  system: {
    loggerOptions: {
      logLevel: LogLevel.Warning,
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) return;
        if (message?.includes('CacheManager:getIdToken')) return;
        switch (level) {
          case LogLevel.Error:
            console.error(message);
            return;
          case LogLevel.Warning:
            console.warn(message);
            return;
          case LogLevel.Info:
            console.info(message);
            return;
          case LogLevel.Verbose:
            console.debug(message);
            return;
        }
      },
    },
  },
};

export const msalInstance = new PublicClientApplication(msalConfig);

export const loginRequest = {
  scopes: [] as string[],
  prompt: 'select_account',
};

/** Scopes used when acquiring tokens for the Human OS API — see the
 * known infra gap noted in AuthContext (acquireTokenSilent guarded by
 * try/catch since api://human-os isn't exposed/consented yet). */
export const humanOsApiScopes = import.meta.env.VITE_API_SCOPE
  ? [import.meta.env.VITE_API_SCOPE as string]
  : [];

/** Best-effort X-Azure-OID/X-Azure-TID headers for the currently signed-in
 * account (same header pattern the backend already keys identity-checked
 * endpoints off of — see GetCapabilitiesFunction/StartPdfCapabilityGraphFunction).
 * Returns an empty object when nobody is signed in yet, so callers can just
 * spread the result into their own headers unconditionally. */
export function getIdentityHeaders(): Record<string, string> {
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0] ?? null;
  if (!account) return {};

  const claims = account.idTokenClaims as Record<string, unknown> | undefined;
  const oid = (claims?.oid as string) ?? (claims?.sub as string) ?? account.homeAccountId.split('.')[0];
  const tid = (claims?.tid as string) ?? account.homeAccountId.split('.')[1];

  return { 'X-Azure-OID': oid, 'X-Azure-TID': tid };
}
