import { apiGet } from './httpClient';

/**
 * Read-only client for the enterprise console's Job Roles ("puestos")
 * catalog — GET /tenants/{tenantId}/job-roles is Anonymous (see backend's
 * GetJobRolesFunction) and shared across all Human OS frontends, so
 * Capability Studio can call it directly against the same backend
 * (localhost:7071/api) without needing its own auth/tenant context.
 * Powers the "target job role + skill" picker in NewCapabilityPage.tsx
 * (2026-08-01 "cursos de empresa" feature) — lets HR explicitly connect a
 * newly-generated Capability to the exact required skill/process gap it's
 * meant to close on a JobRole.
 *
 * Response fields mirror backend Contracts/OrgStructure/JobRoleResponse.cs,
 * but PascalCase — see httpClient.ts's own doc comment: every response
 * body is recursively converted from the backend's camelCase to
 * PascalCase before reaching consumers in this app.
 */
export interface JobRoleDto {
  JobRoleId: string;
  TenantId: string;
  DepartmentId: string;
  DepartmentName: string;
  Title: string;
  LevelLabel: string | null;
  TechnicalSkills: string[];
  SoftSkills: string[];
  DepartmentProcesses: string[];
  HrProcesses: string[];
  IsActive: boolean;
}

export function getJobRoles(tenantId: string): Promise<JobRoleDto[]> {
  return apiGet<JobRoleDto[]>(`/tenants/${tenantId}/job-roles`);
}

