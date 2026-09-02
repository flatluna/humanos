import { apiClient } from '@/api/index';

export interface ResponsibilityJobRoleLinkDto {
  jobRoleId: string;
  jobRoleTitle: string;
  departmentName: string;
}

export interface ResponsibilityDto {
  responsibilityId: string;
  tenantId: string;
  name: string;
  description: string | null;
  isActive: boolean;
  jobRoles: ResponsibilityJobRoleLinkDto[];
  createdDate: string;
  updatedDate: string;
}

export interface SaveResponsibilityRequest {
  name: string;
  description?: string | null;
  jobRoleIds?: string[];
}

export async function getResponsibilities(tenantId: string): Promise<ResponsibilityDto[]> {
  const { data } = await apiClient.get<ResponsibilityDto[]>(`/tenants/${tenantId}/responsibilities`);
  return data;
}

export async function createResponsibility(
  tenantId: string,
  body: SaveResponsibilityRequest,
): Promise<ResponsibilityDto> {
  const { data } = await apiClient.post<ResponsibilityDto>(`/tenants/${tenantId}/responsibilities`, body);
  return data;
}

export async function updateResponsibility(
  responsibilityId: string,
  body: SaveResponsibilityRequest,
): Promise<ResponsibilityDto> {
  const { data } = await apiClient.put<ResponsibilityDto>(`/responsibilities/${responsibilityId}`, body);
  return data;
}

export async function deleteResponsibility(responsibilityId: string): Promise<void> {
  await apiClient.delete(`/responsibilities/${responsibilityId}`);
}
