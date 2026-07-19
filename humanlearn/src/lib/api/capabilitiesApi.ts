import { apiGet } from './httpClient';

/** Backend response shape (PascalCase) for a Capability list item. */
export interface BackendCapabilitySummary {
  CapabilityId: string;
  Name: string;
  Description?: string;
  CapabilityDomainId?: string;
}

export function getCapabilities(): Promise<BackendCapabilitySummary[]> {
  return apiGet<BackendCapabilitySummary[]>('/capabilities');
}
