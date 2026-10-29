/** Decodes (without verifying — the backend already verified the signature
 *  at issuance/exchange time) the payload of the Studio launch JWT. Studio
 *  only ever holds this token in sessionStorage and sends it back as
 *  `Authorization: Bearer` on every API call so the *backend* can re-verify
 *  the signature/expiry server-side (see NgoAutomateApp backend
 *  `StudioSsoToken.Validate`); the client-side decode here is only for
 *  reading claims to render the UI (tenant name, expiry, etc). */
export interface StudioLaunchClaims {
  oid: string | null;
  email: string;
  tenantId: string;
  tenantName: string;
  memberId: string;
  memberType: string;
  sourceApplication: string;
  returnUrl: string;
  studioView: boolean;
  studioCreate: boolean;
  studioEdit: boolean;
  studioDelete: boolean;
  /** Standard JWT expiry claim, seconds since epoch. */
  exp: number;
}

export function decodeJwtPayload(token: string): StudioLaunchClaims | null {
  try {
    const [, payloadPart] = token.split('.');
    if (!payloadPart) return null;
    const base64 = payloadPart.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const json = decodeURIComponent(
      atob(padded)
        .split('')
        .map((c) => `%${c.charCodeAt(0).toString(16).padStart(2, '0')}`)
        .join(''),
    );
    const raw = JSON.parse(json) as Record<string, string | number | boolean>;
    return {
      oid: (raw.oid as string) || null,
      email: raw.email as string,
      tenantId: raw.tenantId as string,
      tenantName: raw.tenantName as string,
      memberId: raw.memberId as string,
      memberType: raw.memberType as string,
      sourceApplication: raw.sourceApplication as string,
      returnUrl: raw.returnUrl as string,
      studioView: raw.studioView === 'True' || raw.studioView === true,
      studioCreate: raw.studioCreate === 'True' || raw.studioCreate === true,
      studioEdit: raw.studioEdit === 'True' || raw.studioEdit === true,
      studioDelete: raw.studioDelete === 'True' || raw.studioDelete === true,
      exp: Number(raw.exp),
    };
  } catch {
    return null;
  }
}
