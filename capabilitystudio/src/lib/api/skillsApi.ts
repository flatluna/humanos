import { apiGet } from './httpClient';

/**
 * Read-only client for the tenant's reusable Skill catalog (2026-08-01
 * "cierra el ciclo" feature) — GET /tenants/{tenantId}/skills is
 * Anonymous (see backend's GetSkillsFunction) and shared across all
 * Human OS frontends, so Capability Studio can call it directly against
 * the same backend (localhost:7071/api) without needing its own auth/
 * tenant context. Powers the "target skill" combobox in
 * NewCapabilityPage.tsx: once HR picks a career category (e.g. "Soft
 * Skills"), this lists every reusable skill/process HR has already
 * authored for that category — across ALL job roles, not just one —
 * so HR can pick exactly which one this new course covers.
 *
 * Response fields mirror backend Contracts/OrgStructure/SkillResponse.cs,
 * but PascalCase — see httpClient.ts's own doc comment: every response
 * body is recursively converted from the backend's camelCase to
 * PascalCase before reaching consumers in this app.
 */
export interface SkillJobRoleLinkDto {
  JobRoleId: string;
  JobRoleTitle: string;
  DepartmentName: string;
}

export interface SkillDto {
  SkillId: string;
  TenantId: string;
  Category: string;
  Name: string;
  Description: string | null;
  IsActive: boolean;
  JobRoles: SkillJobRoleLinkDto[];
  CreatedDate: string;
  UpdatedDate: string;
}

export function getSkills(tenantId: string, category?: string): Promise<SkillDto[]> {
  const query = category ? `?category=${encodeURIComponent(category)}` : '';
  return apiGet<SkillDto[]>(`/tenants/${tenantId}/skills${query}`);
}
