import { PublicClientApplication, LogLevel, type Configuration } from '@azure/msal-browser';

// ══════════════════════════════════════════════════════════════
// MSAL CONFIGURATION — Microsoft Entra External ID (CIAM)
// Reused verbatim from genesis-personas (same tenant, same app
// registration, same callback) per explicit instruction: this is
// the same identity system across the twinetwork ecosystem, not a
// separate Human OS-specific registration.
//
// App Registration:
//   - Name:      twinetstudiologin
//   - Client ID: 2d6b1a7e-e1f4-4c18-bf7d-c3fb7bc86b6d
//   - Platform:  Single-page application
//   - Redirect:  http://localhost:5178  (registered as-is in Entra, no /redirect suffix)
//
// IMPORTANT: the redirect URI below is registered EXACTLY as
// http://localhost:5178 in this Entra app. The Human OS dev server
// must run on port 5178 for the MSAL redirect callback to work —
// see /memories/repo notes for the dev-server-port convention.
// ══════════════════════════════════════════════════════════════

// Discovery document pre-fetched from the same CIAM tenant (twinetwork) used
// by the other apps in this ecosystem, to avoid MSAL v5's instance validation.
// Same metadata, different Client ID.
// Source: https://twinetwork.ciamlogin.com/twinetwork.onmicrosoft.com/v2.0/.well-known/openid-configuration
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
    clientId: import.meta.env.VITE_MSAL_CLIENT_ID ?? '2d6b1a7e-e1f4-4c18-bf7d-c3fb7bc86b6d',
    authority: import.meta.env.VITE_MSAL_AUTHORITY ?? 'https://twinetwork.ciamlogin.com/twinetwork.onmicrosoft.com/',
    knownAuthorities: ['twinetwork.ciamlogin.com'],
    authorityMetadata: CIAM_METADATA,
    redirectUri: import.meta.env.VITE_REDIRECT_URI ?? 'http://localhost:5178',
    postLogoutRedirectUri: import.meta.env.VITE_POST_LOGOUT_REDIRECT_URI ?? 'http://localhost:5178',
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

/**
 * Scopes for the login request. CIAM only requires openid + profile at
 * minimum (MSAL adds those automatically).
 *
 * prompt: 'select_account' — Human OS shares the same CIAM tenant
 * (twinetwork) as the other apps in this ecosystem. Without this, the
 * IdP silently reuses an existing session from another app, signing the
 * user in without an explicit choice. Forcing the account picker makes
 * it a conscious decision every time.
 */
export const loginRequest = {
  scopes: [] as string[],
  prompt: 'select_account',
};

/**
 * Scopes used when acquiring tokens for the Human OS API.
 * TODO: Replace with the actual API scope exposed by the backend App
 * Registration once the backend validates bearer tokens instead of the
 * X-Azure-OID/X-Azure-TID header pattern used today (see AuthContext).
 */
export const humanOsApiScopes: string[] = [
  import.meta.env.VITE_API_SCOPE ?? 'api://human-os/user_impersonation',
];

