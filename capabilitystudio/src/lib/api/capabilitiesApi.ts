import { apiGet, apiDelete } from './httpClient';

/** Real backend response shape (PascalCase) — see CapabilityResponse.cs. */
export interface BackendCapability {
  CapabilityId: string;
  CapabilityDomainId: string;
  DomainCode: string;
  SubjectId: string | null;
  SubjectCode: string | null;
  Code: string;
  Name: string;
  Description: string | null;
  IsActive: boolean;
  LevelCount: number;
  ModuleCount: number;
  NodeCount: number;
  HasCoverImage: boolean;
  LearningSummary: string | null;
  Levels: string[];
  CreatedDate: string;
  UpdatedDate: string;
}

export interface GetCapabilitiesFilters {
  language?: string;
  domain?: string;
  subject?: string;
}

/** Real "Capability catalog" list, 100% DB-backed (GET /capabilities). */
export async function getCapabilities(filters: GetCapabilitiesFilters = {}): Promise<BackendCapability[]> {
  const params = new URLSearchParams({ language: filters.language ?? 'es' });
  if (filters.domain) params.set('domain', filters.domain);
  if (filters.subject) params.set('subject', filters.subject);

  const capabilities = await apiGet<BackendCapability[]>(`/capabilities?${params.toString()}`);
  return capabilities.sort((a, b) => new Date(b.UpdatedDate).getTime() - new Date(a.UpdatedDate).getTime());
}

/**
 * Permanently deletes a capability and ALL its content (ACID transaction
 * on the backend). Irreversible — the caller must confirm with the user
 * explicitly first (see DeleteCapabilityModal).
 */
export async function deleteCapability(capabilityId: string): Promise<void> {
  await apiDelete(`/capabilities/${capabilityId}`);
}
