import { apiGet } from './httpClient';

/**
 * Capability Domain — REAL backend concept (SQL table `CapabilityDomain`,
 * endpoint `GET /capability-domains`). Philosophical dimension (Mind/Build/
 * Home/Life/Value/Future), NOT a topic. NOT used in student navigation —
 * kept here only in case a future secondary badge/profile view needs it.
 */
export interface BackendCapabilityDomain {
  CapabilityDomainId: string;
  Code: string;
  Name: string;
  Description: string;
}

export function getCapabilityDomains(language = 'es'): Promise<BackendCapabilityDomain[]> {
  return apiGet<BackendCapabilityDomain[]>(`/capability-domains?language=${language}`);
}
