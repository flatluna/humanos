import { apiGet, API_BASE_URL } from '@/features/capabilities/api/httpClient';

/**
 * Programs CATALOG (2026-07-25) — client for GET /programs, which lists
 * EVERY active Program (mirrors CapabilityResponse's role for the
 * Capabilities catalog). Distinct from `enrolledProgramsApi.ts`'s
 * GET /people/{personId}/enrolled-programs, which only returns programs
 * the person is ALREADY enrolled in ("Mis Programas"). Powers the new
 * "Programas" catalog page (/programs/catalog) where a person can browse
 * every Program and "Inscribirme" directly, mirroring the Capacidades
 * catalog's enroll flow.
 */
export interface ProgramCatalogEntry {
  programId: string;
  code: string;
  name: string;
  description?: string | null;
  objectives?: string | null;
  requirements?: string | null;
  hasLogo: boolean;
  isActive: boolean;
  capabilityCount: number;
  createdDate: string;
  updatedDate: string;
}

export function getPrograms(): Promise<ProgramCatalogEntry[]> {
  return apiGet<ProgramCatalogEntry[]>('/programs');
}

export function getProgramLogoUrl(programId: string): string {
  return `${API_BASE_URL}/programs/${programId}/logo`;
}
