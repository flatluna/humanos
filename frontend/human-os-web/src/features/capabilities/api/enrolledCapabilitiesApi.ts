import { apiDelete, apiGet, apiPost } from './httpClient';

/**
 * "Mis Capacidades" — client for the standalone Capability enrollment
 * endpoints (2026-07-24). Mirrors enrolledProgramsApi.ts's DTOs
 * (Contracts/Capabilities/EnrolledCapabilityContracts.cs), serialized
 * camelCase by FunctionResponseFactory (same convention as
 * capabilityGraphApi.ts / enrolledProgramsApi.ts).
 *
 * Deliberately separate from just running a course: GET /capabilities/{id}
 * ("Empezar") stays free for anyone, without ever creating an enrollment
 * row. Only enrollCapability() ("Inscribirme") does.
 */

export type EnrolledCapabilityStatus = 'NotStarted' | 'InProgress' | 'Completed';

export interface EnrolledCapability {
  capabilityId: string;
  name: string;
  description?: string | null;
  subjectCode?: string | null;
  subjectName?: string | null;
  hasCoverImage?: boolean;
  enrolledDate: string;
  status: EnrolledCapabilityStatus;
  totalNodes: number;
  masteredNodes: number;
  /** Average FinalScore (0-100) across every node mastered so far. Null
   * (not omitted) while none are mastered yet — always check for `null`
   * here, not just `undefined`. */
  averageScore?: number | null;
}

export interface CapabilityEnrollmentAction {
  capabilityId: string;
  enrolled: boolean;
  enrolledDate?: string | null;
}

export function getEnrolledCapabilities(personId: string): Promise<EnrolledCapability[]> {
  return apiGet<EnrolledCapability[]>(`/people/${personId}/enrolled-capabilities`);
}

export function enrollCapability(personId: string, capabilityId: string): Promise<CapabilityEnrollmentAction> {
  return apiPost<CapabilityEnrollmentAction>(`/people/${personId}/capabilities/${capabilityId}/enroll`);
}

export function unenrollCapability(personId: string, capabilityId: string): Promise<CapabilityEnrollmentAction> {
  return apiDelete<CapabilityEnrollmentAction>(`/people/${personId}/capabilities/${capabilityId}/enroll`);
}
