import { apiClient } from './index';
import type {
  TenantResponse,
  PersonResponse,
  PersonProfileResponse,
  HumanProfileResponse,
  UploadRoleDocumentRequest,
  UploadRoleDocumentResponse,
} from './types';

/** Typed clients for the real Azure Functions already implemented in
 *  backend/HumanOS/AzureFunctions/Api. Each function name below matches
 *  the `[Function("...")]` attribute on the corresponding endpoint.
 */

export async function getTenant(tenantId: string): Promise<TenantResponse> {
  const { data } = await apiClient.get<TenantResponse>(`/tenants/${tenantId}`);
  return data;
}

export async function getPerson(personId: string): Promise<PersonResponse> {
  const { data } = await apiClient.get<PersonResponse>(`/people/${personId}`);
  return data;
}

export async function getPersonProfile(personId: string): Promise<PersonProfileResponse> {
  const { data } = await apiClient.get<PersonProfileResponse>(`/people/${personId}/profile`);
  return data;
}

export async function getHumanProfile(personId: string): Promise<HumanProfileResponse> {
  const { data } = await apiClient.get<HumanProfileResponse>(`/people/${personId}/human-profile`);
  return data;
}

/** Uploads a job description or résumé PDF/DOCX to Data Lake storage.
 *  Returns a 503 until the backend's "DataLakeStorage" setting is
 *  configured — see backend/HumanOS/Storage/RoleDocumentStorageService.cs.
 */
export async function uploadRoleDocument(
  personId: string,
  request: UploadRoleDocumentRequest,
): Promise<UploadRoleDocumentResponse> {
  const { data } = await apiClient.post<UploadRoleDocumentResponse>(`/people/${personId}/role-documents`, request);
  return data;
}
