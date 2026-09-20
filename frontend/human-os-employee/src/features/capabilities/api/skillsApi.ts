import { apiGet } from './httpClient';

export interface SkillJobRoleLinkDto {
  jobRoleId: string;
  jobRoleTitle: string;
  departmentName: string;
}

export interface SkillDto {
  skillId: string;
  tenantId: string;
  category: 'job-skills' | 'soft-skills' | 'department-processes' | 'hr-processes';
  name: string;
  description: string | null;
  assignmentScope?: 'COMPANY_WIDE_ESSENTIAL' | 'DEPARTMENT_ESSENTIAL' | 'JOB_ROLE_ESSENTIAL' | 'RECOMMENDED_DEVELOPMENT' | 'EXPLORATORY';
  applicableDepartmentIds?: string[];
  isActive: boolean;
  documentStoragePath?: string | null;
  documentFileName?: string | null;
  documentUrl?: string | null;
  jobRoles: SkillJobRoleLinkDto[];
  createdDate: string;
  updatedDate: string;
  matchedCapabilityId: string | null;
  matchedCapabilityName: string | null;
  matchedCourseType?: 'structured-learning' | 'graph' | null;
  displayOrder?: number | null;
}

export async function getSkills(tenantId: string): Promise<SkillDto[]> {
  const skills = await apiGet<Array<Record<string, unknown>>>(`/tenants/${tenantId}/skills`);
  return skills.map((skill) => ({
    skillId: String(skill.SkillId ?? skill.skillId ?? ''),
    tenantId: String(skill.TenantId ?? skill.tenantId ?? ''),
    category: (skill.Category ?? skill.category) as SkillDto['category'],
    name: String(skill.Name ?? skill.name ?? ''),
    description: (skill.Description ?? skill.description ?? null) as string | null,
    assignmentScope: (skill.AssignmentScope ?? skill.assignmentScope) as SkillDto['assignmentScope'],
    applicableDepartmentIds: (skill.ApplicableDepartmentIds ?? skill.applicableDepartmentIds ?? []) as string[],
    isActive: Boolean(skill.IsActive ?? skill.isActive),
    documentStoragePath: (skill.DocumentStoragePath ?? skill.documentStoragePath ?? null) as string | null,
    documentFileName: (skill.DocumentFileName ?? skill.documentFileName ?? null) as string | null,
    documentUrl: (skill.DocumentUrl ?? skill.documentUrl ?? null) as string | null,
    jobRoles: (skill.JobRoles ?? skill.jobRoles ?? []) as SkillJobRoleLinkDto[],
    createdDate: String(skill.CreatedDate ?? skill.createdDate ?? ''),
    updatedDate: String(skill.UpdatedDate ?? skill.updatedDate ?? ''),
    matchedCapabilityId: (skill.MatchedCapabilityId ?? skill.matchedCapabilityId ?? null) as string | null,
    matchedCapabilityName: (skill.MatchedCapabilityName ?? skill.matchedCapabilityName ?? null) as string | null,
    matchedCourseType: (skill.MatchedCourseType ?? skill.matchedCourseType ?? null) as SkillDto['matchedCourseType'],
    displayOrder: (skill.DisplayOrder ?? skill.displayOrder ?? null) as number | null,
  }));
}
