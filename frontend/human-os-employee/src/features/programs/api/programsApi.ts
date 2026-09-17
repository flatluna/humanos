import { apiGet, apiPost, apiDelete, API_BASE_URL } from '@/features/capabilities/api/httpClient';

export interface ProgramCourseItem {
  programCapabilityId: string;
  capabilityId: string;
  capabilityCode: string;
  capabilityName: string;
  subjectCode: string | null;
  sortOrder: number;
  isRequired: boolean;
  phaseLabel: string | null;
  objectives: string | null;
  requirements: string | null;
  levelCount: number;
  nodeCount: number;
  hasCoverImage: boolean;
  courseType?: 'StructuredLearning' | 'TraditionalChapters' | 'Capability' | string | null;
}

export interface ProgramCatalogDetail {
  programId: string;
  code: string;
  name: string;
  description: string | null;
  objectives: string | null;
  requirements: string | null;
  hasLogo: boolean;
  isActive: boolean;
  capabilityCount: number;
  createdDate: string;
  updatedDate: string;
  capabilities: ProgramCourseItem[];
}

export interface ProgramCatalogEntry {
  programId: string;
  code: string;
  name: string;
  description: string | null;
  objectives: string | null;
  requirements: string | null;
  hasLogo: boolean;
  isActive: boolean;
  capabilityCount: number;
  createdDate: string;
  updatedDate: string;
}

export interface ProgramEnrollmentAction {
  programId: string;
  enrolled: boolean;
  enrolledDate?: string | null;
}

export interface EnrolledProgramCapabilityProgress {
  capabilityId: string;
  name: string;
  sortOrder: number;
  isRequired: boolean;
  status: 'NotStarted' | 'InProgress' | 'Completed';
  totalNodes: number;
  masteredNodes: number;
  averageScore?: number | null;
}

export interface EnrolledProgramSummary {
  programId: string;
  code: string;
  name: string;
  description?: string | null;
  acceptedDate: string;
  averageScore?: number | null;
  capabilities: EnrolledProgramCapabilityProgress[];
}

export async function getPrograms(): Promise<ProgramCatalogEntry[]> {
  const data = await apiGet<{ value?: ProgramCatalogEntry[] } | ProgramCatalogEntry[]>('/programs');
  if (Array.isArray(data)) return data;
  if (data && Array.isArray((data as { value?: ProgramCatalogEntry[] }).value)) {
    return (data as { value: ProgramCatalogEntry[] }).value;
  }
  return [];
}

export async function getProgramById(programId: string): Promise<ProgramCatalogDetail> {
  return apiGet<ProgramCatalogDetail>(`/programs/${programId}`);
}

/**
 * Returns a mapping of Course/Capability ID to the Program(s) it belongs to.
 * Used by Available Courses to detect whether a course is part of a program.
 */
export async function getCourseProgramMembershipMap(): Promise<Map<string, { programId: string; programName: string }>> {
  const programs = await getPrograms();
  const membershipMap = new Map<string, { programId: string; programName: string }>();

  await Promise.all(
    programs.map(async (prog) => {
      try {
        const detail = await getProgramById(prog.programId);
        if (detail.capabilities) {
          for (const cap of detail.capabilities) {
            if (!membershipMap.has(cap.capabilityId)) {
              membershipMap.set(cap.capabilityId, {
                programId: prog.programId,
                programName: prog.name,
              });
            }
          }
        }
      } catch (err) {
        console.error(`Failed to load details for program ${prog.programId}:`, err);
      }
    })
  );

  return membershipMap;
}

export function getProgramLogoUrl(programId: string): string {
  return `${API_BASE_URL}/programs/${programId}/logo`;
}

export async function getEnrolledPrograms(personId: string): Promise<EnrolledProgramSummary[]> {
  const data = await apiGet<{ value?: EnrolledProgramSummary[] } | EnrolledProgramSummary[]>(
    `/people/${personId}/enrolled-programs`
  );
  if (Array.isArray(data)) return data;
  if (data && Array.isArray((data as { value?: EnrolledProgramSummary[] }).value)) {
    return (data as { value: EnrolledProgramSummary[] }).value;
  }
  return [];
}

export async function enrollProgram(personId: string, programId: string): Promise<ProgramEnrollmentAction> {
  return apiPost<ProgramEnrollmentAction>(`/people/${personId}/programs/${programId}/enroll`);
}

export async function unenrollProgram(personId: string, programId: string): Promise<ProgramEnrollmentAction> {
  return apiDelete<ProgramEnrollmentAction>(`/people/${personId}/programs/${programId}/enroll`);
}

export interface ProgramEntityOccurrence {
  courseId: string;
  courseName: string;
  engramId: string;
  engramTitle: string;
  chapterTitle?: string | null;
  sortOrder: number;
  sourceText: string;
  startPosition?: number | null;
  endPosition?: number | null;
}

export interface ProgramEntityItem {
  entityId: string;
  canonicalName: string;
  entityType: string;
  description?: string | null;
  ontologicalClass?: string | null;
  aliases: string[];
  totalOccurrences: number;
  distinctCoursesCount: number;
  occurrences: ProgramEntityOccurrence[];
}

export interface ProgramCourseSummary {
  courseId: string;
  courseName: string;
  engramCount: number;
  entityCount: number;
}

export interface ProgramEntitiesResponse {
  programId: string;
  programName: string;
  programCode?: string | null;
  description?: string | null;
  totalCourses: number;
  totalEngrams: number;
  totalEntities: number;
  totalOccurrences: number;
  courses: ProgramCourseSummary[];
  entities: ProgramEntityItem[];
}

export async function getProgramEntities(programId: string): Promise<ProgramEntitiesResponse> {
  return apiGet<ProgramEntitiesResponse>(`/programs/${programId}/entities`);
}

