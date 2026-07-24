import { apiGet } from './httpClient';

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
 * — never show it as a real choice in the Studio wizard. */
export function getCapabilityDomains(language = 'es'): Promise<CapabilityDomain[]> {
  return apiGet<CapabilityDomain[]>(`/capability-domains?language=${language}`).then((domains) =>
    domains.filter((d) => !d.Code.startsWith('TEST'))
  );
}
