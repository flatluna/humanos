import { apiGet } from './httpClient';
import { CapabilitySummary } from '../../types';

/** Real backend response shape (PascalCase) — see CapabilityResponse.cs. */
export interface BackendCapability {
  CapabilityId: string;
  CapabilityDomainId: string;
  DomainCode: string;
  Code: string;
  Name: string;
  Description: string | null;
  IsActive: boolean;
  LevelCount: number;
  ModuleCount: number;
  Levels: string[];
  CreatedDate: string;
  UpdatedDate: string;
}

function toCapabilitySummary(capability: BackendCapability): CapabilitySummary {
  return {
    capabilityId: capability.CapabilityId,
    title: capability.Name,
    description: capability.Description ?? '',
    domain: capability.DomainCode,
    levels: capability.Levels as CapabilitySummary['levels'],
    moduleCount: capability.ModuleCount,
    status: capability.IsActive ? 'Published' : 'Archived',
    createdAt: capability.CreatedDate,
    updatedAt: capability.UpdatedDate,
  };
}

/**
 * Real "Capability Library" list — 100% backed by the database
 * (GET /capabilities), most recently updated first. No mock/demo data, no
 * client-side singleton to go stale or get wiped by HMR.
 */
export async function getCapabilities(): Promise<CapabilitySummary[]> {
  const capabilities = await apiGet<BackendCapability[]>('/capabilities?language=es');
  return capabilities
    .map(toCapabilitySummary)
    .sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime());
}
