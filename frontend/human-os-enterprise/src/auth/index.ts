import { PublicClientApplication, LogLevel, type Configuration } from '@azure/msal-browser';

// ══════════════════════════════════════════════════════════════
// MSAL CONFIGURATION — Microsoft Entra External ID (CIAM)
// Reused verbatim from human-os-web/genesis-personas (same tenant, same
// app registration): shared login across the whole twinetwork ecosystem,
// not a separate registration per app.
//
// App Registration:
//   - Name:      twinetstudiologin
//   - Client ID: 2d6b1a7e-e1f4-4c18-bf7d-c3fb7bc86b6d
//   - Platform:  Single-page application
//
// ⚠️ ACTION REQUIRED (cannot be done from code): this app runs on
// http://localhost:5179, but as of 2026-07-31 the `twinetstudiologin` app
// registration only has http://localhost:5178 registered as an SPA
// redirect URI. Add http://localhost:5179 as an ADDITIONAL redirect URI
// (Entra Portal → App registrations → twinetstudiologin → Authentication
// → Single-page application → Add URI) — SPA registrations support
// multiple redirect URIs, so this does not affect human-os-web. Until
// that's added, login here will fail with AADSTS50011
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
    redirectUri: 'http://localhost:5179',
    postLogoutRedirectUri: 'http://localhost:5179',
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
