import { apiClient } from '@/api/index';

// ── Types (mirrors backend Contracts/OrgStructure/SkillResponse + SaveSkillRequest) ──

/// The 4 career-plan tracks — same categories used throughout JobRoleDto's
/// technicalSkills/softSkills/departmentProcesses/hrProcesses fields.
export type SkillCategory = 'job-skills' | 'soft-skills' | 'department-processes' | 'hr-processes';
export type SkillAssignmentScope =
  | 'COMPANY_WIDE_ESSENTIAL'
  | 'DEPARTMENT_ESSENTIAL'
  | 'JOB_ROLE_ESSENTIAL'
  | 'RECOMMENDED_DEVELOPMENT'
  | 'EXPLORATORY';

export interface SkillJobRoleLinkDto {
  jobRoleId: string;
  jobRoleTitle: string;
  departmentName: string;
  /** "L100" | "L200" | "L300", or null — content depth this role
   * requires for this skill (same skill can be L100 for Jr, L300 for Sr). */
  requiredLevel: string | null;
}

export interface SkillDto {
  skillId: string;
  tenantId: string;
  category: SkillCategory;
  name: string;
  description: string | null;
  assignmentScope?: SkillAssignmentScope;
  applicableDepartmentIds: string[];
  isActive: boolean;
  documentStoragePath?: string | null;
  documentFileName?: string | null;
  documentUrl?: string | null;
  jobRoles: SkillJobRoleLinkDto[];
  createdDate: string;
  updatedDate: string;
  /** Set when an active Capability already satisfies this skill (see
   * backend's SkillService.GetMatchedCapabilitiesAsync) — null means no
   * course exists yet. Powers the "Curso creado" vs "Generar curso"
   * indicator. */
  matchedCapabilityId: string | null;
  matchedCapabilityName: string | null;
  matchedCourseType?: 'structured-learning' | 'graph' | null;
  matchedCourses?: Array<{
    courseId: string;
    courseName: string;
    courseType: 'structured-learning' | 'graph';
    languageCode: 'es' | 'en';
  }>;
  /** Set only for skills created from a real internet-sourced course
   * recommendation (Step 4 "Link to Process" on an internet gap). */
  sourceCourseTitle?: string | null;
  sourceCourseProvider?: string | null;
  sourceCourseUrl?: string | null;
  sourceOutlineModules?: Array<{ title: string; lessons: string[] }> | null;
  /** "scraped" | "ai-suggested" */
  sourceOutlineSource?: string | null;
  /** Set only when this Skill represents an ENTIRE business process saved as a
   * "department-processes" capability ("Guardar en Catálogo de Capabilities"), not an
   * individual skill gap. */
  sourceProcessDocumentId?: string | null;
  /** The department that authored the source process (e.g. "Finanzas"). */
  sourceProcessDepartmentName?: string | null;
}

export interface SkillJobRoleLinkRequest {
  jobRoleId: string;
  requiredLevel?: string | null;
}

export interface SaveSkillRequest {
  category: SkillCategory;
  name: string;
  description?: string | null;
  assignmentScope?: SkillAssignmentScope;
  applicableDepartmentIds?: string[];
  jobRoleLinks?: SkillJobRoleLinkRequest[];
}

export async function getSkills(tenantId: string, category?: SkillCategory): Promise<SkillDto[]> {
  const { data } = await apiClient.get<SkillDto[]>(`/tenants/${tenantId}/skills`, {
    params: category ? { category } : undefined,
  });
  return data;
}

export async function createSkill(tenantId: string, body: SaveSkillRequest): Promise<SkillDto> {
  const { data } = await apiClient.post<SkillDto>(`/tenants/${tenantId}/skills`, body);
  return data;
}

export async function updateSkill(skillId: string, body: SaveSkillRequest): Promise<SkillDto> {
  const { data } = await apiClient.put<SkillDto>(`/skills/${skillId}`, body);
  return data;
}

export async function deleteSkill(skillId: string): Promise<void> {
  await apiClient.delete(`/skills/${skillId}`);
}

export async function uploadSkillDocument(skillId: string, file: File): Promise<SkillDto> {
  const { data } = await apiClient.post<SkillDto>(`/skills/${skillId}/document`, file, {
    headers: {
      'Content-Type': 'application/pdf',
      'X-File-Name': file.name,
    },
    params: {
      fileName: file.name,
    },
  });
  return data;
}

export async function deleteSkillDocument(skillId: string): Promise<SkillDto> {
  const { data } = await apiClient.delete<SkillDto>(`/skills/${skillId}/document`);
  return data;
}

/// One process this Skill is linked to via a saved ProcessSkillRequirement
/// (Step 4 "Link to Process" of the whole-process Learning Recommendation
/// wizard) — the reverse of MatchedSkillId. See GetSkillLinkedProcessesFunction.
export interface SkillLinkedProcessDto {
  processDocumentId: string;
  processSkillRequirementId: string;
  processTitle: string;
  isRequired: boolean;
  appliesToAllDepartments: boolean;
  departmentCount: number;
  jobRoleCount: number;
}

export async function getSkillLinkedProcesses(skillId: string): Promise<SkillLinkedProcessDto[]> {
  const { data } = await apiClient.get<SkillLinkedProcessDto[]>(`/skills/${skillId}/linked-processes`);
  return data;
}

/// One-time/on-demand backfill: reconciles every active Job Role's
/// free-text skill lists into the shared catalog (needed for roles that
/// existed before this feature shipped and haven't been re-saved since).
/// Idempotent — safe to call again any time.
export async function syncSkillsFromJobRoles(tenantId: string): Promise<{ syncedJobRoleCount: number }> {
  const { data } = await apiClient.post<{ syncedJobRoleCount: number }>(`/tenants/${tenantId}/skills/sync-from-roles`);
  return data;
}
