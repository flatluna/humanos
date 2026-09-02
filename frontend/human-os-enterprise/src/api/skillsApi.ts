import { apiClient } from '@/api/index';

// ── Types (mirrors backend Contracts/OrgStructure/SkillResponse + SaveSkillRequest) ──

/// The 4 career-plan tracks — same categories used throughout JobRoleDto's
/// technicalSkills/softSkills/departmentProcesses/hrProcesses fields.
export type SkillCategory = 'job-skills' | 'soft-skills' | 'department-processes' | 'hr-processes';

export interface SkillJobRoleLinkDto {
  jobRoleId: string;
  jobRoleTitle: string;
  departmentName: string;
}

export interface SkillDto {
  skillId: string;
  tenantId: string;
  category: SkillCategory;
  name: string;
  description: string | null;
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
}

export interface SaveSkillRequest {
  category: SkillCategory;
  name: string;
  description?: string | null;
  jobRoleIds?: string[];
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

/// One-time/on-demand backfill: reconciles every active Job Role's
/// free-text skill lists into the shared catalog (needed for roles that
/// existed before this feature shipped and haven't been re-saved since).
/// Idempotent — safe to call again any time.
export async function syncSkillsFromJobRoles(tenantId: string): Promise<{ syncedJobRoleCount: number }> {
  const { data } = await apiClient.post<{ syncedJobRoleCount: number }>(`/tenants/${tenantId}/skills/sync-from-roles`);
  return data;
}
