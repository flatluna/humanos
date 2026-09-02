import { apiClient } from './index';

export interface ToolDto {
  toolId: string;
  tenantId: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdDate: string;
  updatedDate: string;
}

export async function getTools(tenantId: string): Promise<ToolDto[]> {
  const { data } = await apiClient.get<ToolDto[]>(`/tenants/${tenantId}/tools`);
  return data;
}
