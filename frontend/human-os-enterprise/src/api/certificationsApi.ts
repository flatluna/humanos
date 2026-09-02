import { apiClient } from '@/api/index';

export interface CertificationJobRoleLinkDto {
  jobRoleId: string;
  jobRoleTitle: string;
  departmentName: string;
}

export interface CertificationDto {
  certificationId: string;
  tenantId: string;
  name: string;
  issuer: string | null;
  description: string | null;
  isActive: boolean;
  jobRoles: CertificationJobRoleLinkDto[];
  createdDate: string;
  updatedDate: string;
}

export interface SaveCertificationRequest {
  name: string;
  issuer?: string | null;
  description?: string | null;
  jobRoleIds?: string[];
}

export async function getCertifications(tenantId: string): Promise<CertificationDto[]> {
  const { data } = await apiClient.get<CertificationDto[]>(`/tenants/${tenantId}/certifications`);
  return data;
}

export async function createCertification(
  tenantId: string,
  body: SaveCertificationRequest,
): Promise<CertificationDto> {
  const { data } = await apiClient.post<CertificationDto>(`/tenants/${tenantId}/certifications`, body);
  return data;
}

export async function updateCertification(
  certificationId: string,
  body: SaveCertificationRequest,
): Promise<CertificationDto> {
  const { data } = await apiClient.put<CertificationDto>(`/certifications/${certificationId}`, body);
  return data;
}

export async function deleteCertification(certificationId: string): Promise<void> {
  await apiClient.delete(`/certifications/${certificationId}`);
}
