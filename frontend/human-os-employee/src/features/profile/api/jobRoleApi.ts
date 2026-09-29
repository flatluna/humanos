import { apiGet } from '@/features/capabilities/api/httpClient';

/**
 * Read-only client for a single Job Role ("puesto") — GET /job-roles/{id}.
 * Powers "Mi Perfil"'s "Mi Puesto y Desarrollo" section (2026-08-02): the
 * employee's own real job description/required skills, already captured
 * by HR in human-os-enterprise, surfaced here so they can see exactly
 * what's expected of them and where to grow — no separate editing here,
 * this stays HR-managed (see EmployeeDetailPage/RoleDetailPage in
 * human-os-enterprise). Response is camelCase (goes through
 * FunctionResponseFactory, unlike the PersonProfile endpoints).
 */
export interface JobRoleDetailDto {
  jobRoleId: string;
  tenantId: string;
  departmentId: string;
  departmentName: string;
  title: string;
  levelLabel: string | null;
  purpose: string | null;
  summary: string | null;
  responsibilities: string[];
  requiredExperience: string | null;
  requiredEducation: string | null;
  requiredCertifications: string[];
  requiredLanguages: string[];
  technicalSkills: string[];
  softSkills: string[];
  departmentProcesses: string[];
  hrProcesses: string[];
  tools: string[];
  dailyActivities: string[];
  performanceIndicators: string[];
  expectedOutcomes: string[];
  workSchedule: string | null;
  workModality: string | null;
  requiresTravel: boolean;
  workLocation: string | null;
  availabilityNotes: string | null;
  employmentType: string | null;
  startInYourRoleTechnicalSkills?: string[];
  startInYourRoleSoftSkills?: string[];
  startInYourRoleDepartmentProcesses?: string[];
  startInYourRoleHrProcesses?: string[];
  inYourRoleTechnicalSkills?: string[];
  inYourRoleSoftSkills?: string[];
  inYourRoleDepartmentProcesses?: string[];
  inYourRoleHrProcesses?: string[];
}

export function getJobRole(jobRoleId: string): Promise<JobRoleDetailDto> {
  return apiGet<JobRoleDetailDto>(`/job-roles/${jobRoleId}`);
}
