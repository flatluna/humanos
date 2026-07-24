import { apiClient } from './index';
import type {
  TenantResponse,
  PersonResponse,
  PersonProfileResponse,
  HumanProfileResponse,
  UploadRoleDocumentRequest,
  UploadRoleDocumentResponse,
  GoalResponse,
  PersonGoalResponse,
  MotivationResponse,
  PersonMotivationResponse,
  PersonCapabilityResponse,
  RecommendGrowthPathRequest,
  RecommendGrowthPathResponse,
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

// ── Growth Plan — Step 2 "Where You Want to Go" (Goals + Motivations) ──────

export async function getGoals(language: string): Promise<GoalResponse[]> {
  const { data } = await apiClient.get<GoalResponse[]>('/goals', { params: { language } });
  return data;
}

export async function getPersonGoals(personId: string, language: string): Promise<PersonGoalResponse[]> {
  const { data } = await apiClient.get<PersonGoalResponse[]>(`/people/${personId}/goals`, { params: { language } });
  return data;
}

export async function adoptGoal(personId: string, goalId: string): Promise<PersonGoalResponse> {
  const { data } = await apiClient.post<PersonGoalResponse>(`/people/${personId}/goals/${goalId}`, {});
  return data;
}

export async function abandonPersonGoal(personId: string, personGoalId: string): Promise<PersonGoalResponse> {
  const { data } = await apiClient.post<PersonGoalResponse>(`/people/${personId}/goals/${personGoalId}/abandon`);
  return data;
}

export async function getMotivations(language: string): Promise<MotivationResponse[]> {
  const { data } = await apiClient.get<MotivationResponse[]>('/motivations', { params: { language } });
  return data;
}

export async function setPersonMotivations(
  personId: string,
  motivationCodes: string[],
): Promise<PersonMotivationResponse[]> {
  const { data } = await apiClient.post<PersonMotivationResponse[]>(`/people/${personId}/motivations`, {
    motivationCodes,
  });
  return data;
}

// ── Growth Plan — Step 3 "Your Starting Point" (Capabilities) ─────────────

export async function getPersonCapabilities(personId: string): Promise<PersonCapabilityResponse[]> {
  const { data } = await apiClient.get<PersonCapabilityResponse[]>(`/people/${personId}/capabilities`);
  return data;
}

/** Starts development of a real Capability for this person, optionally
 *  seeded with a self-assessed starting level (see
 *  StartCapabilityDevelopmentRequest.SelfAssessedLevel on the backend).
 *  Returns 409/Conflict (thrown by apiClient) if already started — the
 *  caller should check getPersonCapabilities first to avoid that on
 *  repeat visits, same pattern as FutureDirectionPage's goal diffing.
 */
export async function startCapabilityDevelopment(
  personId: string,
  capabilityId: string,
  selfAssessedLevel?: 'Beginner' | 'Intermediate' | 'Advanced',
): Promise<PersonCapabilityResponse> {
  const { data } = await apiClient.post<PersonCapabilityResponse>(`/people/${personId}/capabilities/${capabilityId}`, {
    targetLevel: 5,
    selfAssessedLevel,
  });
  return data;
}

/** Real LLM recommendation (GrowthPathRecommenderAgent) for the
 *  GoalPlanner UI on Step 3 — replaces the earlier frontend-only
 *  keyword-matching mock (mockLearningPrograms.ts's recommendPath).
 *  Anonymous/stateless endpoint, no personId in the route.
 */
export async function recommendGrowthPath(
  request: RecommendGrowthPathRequest,
): Promise<RecommendGrowthPathResponse> {
  const { data } = await apiClient.post<RecommendGrowthPathResponse>('/growth-plan/starting-point/recommend', request);
  return data;
}
