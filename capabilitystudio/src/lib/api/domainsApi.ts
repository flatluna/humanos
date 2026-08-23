import { apiGet } from './httpClient';
import i18n from '../../i18n';

/** Backend response shape (PascalCase) — see CapabilityDomainResponse.cs.
 * Studio-only authoring taxonomy (Mind/Build/Home/Life/Value/Future),
 * required by the creation pipeline but never shown to students. */
export interface CapabilityDomain {
  CapabilityDomainId: string;
  Code: string;
  Name: string;
  Description: string | null;
}

/** A stray test fixture domain lives in the DB (TEST-CURADOR-GRAPHARCHITECT)
 * — never show it as a real choice in the Studio wizard. Default language
 * follows i18n's CURRENT language at call time (2026-08-23 fix — used to be
 * hardcoded to 'es' regardless of the Studio UI's own EN/ES toggle). */
export function getCapabilityDomains(language: string = i18n.language): Promise<CapabilityDomain[]> {
  return apiGet<CapabilityDomain[]>(`/capability-domains?language=${language}`).then((domains) =>
    domains.filter((d) => !d.Code.startsWith('TEST'))
  );
}
