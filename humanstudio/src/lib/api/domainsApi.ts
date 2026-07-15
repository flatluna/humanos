import { apiGet } from './httpClient';

/** Backend response shape (PascalCase) — see CapabilityDomainService.cs. */
export interface BackendCapabilityDomain {
  CapabilityDomainId: string;
  Code: string;
  Name: string;
  Description: string;
}

export function getCapabilityDomains(language = 'es'): Promise<BackendCapabilityDomain[]> {
  return apiGet<BackendCapabilityDomain[]>(`/capability-domains?language=${language}`);
}
