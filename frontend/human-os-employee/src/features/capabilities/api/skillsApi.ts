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
}

export async function getSkills(tenantId: string): Promise<SkillDto[]> {
  return apiGet<SkillDto[]>(`/tenants/${tenantId}/skills`);
}
