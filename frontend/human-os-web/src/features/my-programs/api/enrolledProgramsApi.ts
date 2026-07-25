import { apiGet } from '@/features/capabilities/api/httpClient';

/**
 * "Mis Programas" — client for GET /people/{personId}/enrolled-programs.
 * Mirrors GrowthPlanService.GetEnrolledProgramsAsync's DTOs
 * (Contracts/GrowthPlan/EnrolledProgramContracts.cs), serialized camelCase
 * by FunctionResponseFactory (same convention as capabilityGraphApi.ts).
 */

export type EnrolledCapabilityStatus = 'NotStarted' | 'InProgress' | 'Completed';

export interface EnrolledProgramCapability {
  capabilityId: string;
  name: string;
  sortOrder: number;
  isRequired: boolean;
  status: EnrolledCapabilityStatus;
  totalNodes: number;
  masteredNodes: number;
  /** Average FinalScore (0-100) across every node mastered so far in this
   * capability. Null (not omitted) while none are mastered yet — the
   * backend serializes a nullable int, so always check for `null` here,
   * not just `undefined`. */
  averageScore?: number | null;
}

export interface EnrolledProgram {
  programId: string;
  code: string;
  name: string;
  description?: string | null;
  acceptedDate: string;
  /** Average FinalScore (0-100) across every node mastered so far, across
   * ALL of this program's capabilities — "how am I doing up to today".
   * Null (not omitted) while nothing has been mastered yet. */
  averageScore?: number | null;
  capabilities: EnrolledProgramCapability[];
}

export function getEnrolledPrograms(personId: string): Promise<EnrolledProgram[]> {
  return apiGet<EnrolledProgram[]>(`/people/${personId}/enrolled-programs`);
}
