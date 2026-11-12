import { LogLevel, PublicClientApplication, type Configuration, type RedirectRequest } from '@azure/msal-browser';

// ══════════════════════════════════════════════════════════════
// MSAL CONFIGURATION — Microsoft Entra External ID (CIAM)
// Same tenant/CIAM pattern used across the twinetwork/HumanOS ecosystem
// (human-os-admin-app, human-os-employee, human-os-web, etc.), but with
// its own dedicated App Registration for EngramsIQ Family.
//
// App Registration:
//   - Name:      engramsiqfamily
//   - Client ID: d759e6d5-7149-4be2-9acd-e13ca2695af6
//   - Object ID: 9493f024-8377-4375-a582-04221ca9d0fe
//   - Tenant ID: 0e9c8663-a4ff-440e-af94-be25e63a1a6a
//   - Platform:  Single-page application
//   - Redirect:  http://localhost:5174 (must match vite.config.ts server.port)
// ══════════════════════════════════════════════════════════════

// Discovery document pre-fetched from the same CIAM tenant (twinetwork) used
// by the other apps in this ecosystem, to avoid MSAL v5's instance validation.
// Same metadata, different Client ID.
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

const redirectUri = 'http://localhost:5174';

const msalConfig: Configuration = {
  auth: {
    clientId: 'd759e6d5-7149-4be2-9acd-e13ca2695af6',
    authority: 'https://twinetwork.ciamlogin.com/twinetwork.onmicrosoft.com/',
    knownAuthorities: ['twinetwork.ciamlogin.com'],
    authorityMetadata: CIAM_METADATA,
    redirectUri,
    postLogoutRedirectUri: redirectUri,
  },
  cache: {
    cacheLocation: 'sessionStorage',
  },
  system: {
    loggerOptions: {
      logLevel: LogLevel.Warning,
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) return;
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

export const loginRequest: RedirectRequest = {
  scopes: ['openid', 'profile', 'email'],
  prompt: 'select_account',
  extraQueryParameters: { p: 'humanosflow' },
  redirectStartPage: `${redirectUri}${window.location.pathname}${window.location.search}${window.location.hash}`,
};
