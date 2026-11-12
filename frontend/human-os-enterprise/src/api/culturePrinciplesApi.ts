import { apiClient } from '@/api/index';

export type CulturePrincipleStatus = 'Draft' | 'Approved' | 'Superseded' | 'Retired';

export interface CulturePrincipleDto {
  culturePrincipleId: string;
  tenantId: string | null;
  sourceCulturePrincipleId: string | null;
  principleCode: string;
  nameEn: string;
  nameEs: string | null;
  descriptionEn: string;
  descriptionEs: string | null;
  ownerPersonId: string | null;
  versionNumber: number;
  status: CulturePrincipleStatus;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  approvedByPersonId: string | null;
  approvedAt: string | null;
  createdAt: string;
  createdByPersonId: string | null;
  updatedAt: string;
}

export interface SaveCulturePrincipleRequest {
  tenantId?: string | null;
  sourceCulturePrincipleId?: string | null;
  principleCode: string;
  nameEn: string;
  nameEs?: string | null;
  descriptionEn: string;
  descriptionEs?: string | null;
  ownerPersonId?: string | null;
  versionNumber?: number;
  status?: CulturePrincipleStatus;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  approvedByPersonId?: string | null;
  approvedAt?: string | null;
  createdByPersonId?: string | null;
}

export async function getCulturePrinciples(tenantId?: string): Promise<CulturePrincipleDto[]> {
  const { data } = await apiClient.get<{ principles: CulturePrincipleDto[] }>(
    '/enterprise-evolution/culture/principles',
    { params: tenantId ? { tenantId } : undefined },
  );
  return data.principles;
}

export async function getCulturePrincipleById(principleId: string): Promise<CulturePrincipleDto> {
  const { data } = await apiClient.get<CulturePrincipleDto>(
    `/enterprise-evolution/culture/principles/${principleId}`,
  );
  return data;
}

export async function createCulturePrinciple(body: SaveCulturePrincipleRequest): Promise<CulturePrincipleDto> {
  const { data } = await apiClient.post<CulturePrincipleDto>('/enterprise-evolution/culture/principles', body);
  return data;
}

export async function updateCulturePrinciple(
  principleId: string,
  body: SaveCulturePrincipleRequest,
): Promise<CulturePrincipleDto> {
  const { data } = await apiClient.put<CulturePrincipleDto>(
    `/enterprise-evolution/culture/principles/${principleId}`,
    body,
  );
  return data;
}

export async function deleteCulturePrinciple(principleId: string): Promise<void> {
  await apiClient.delete(`/enterprise-evolution/culture/principles/${principleId}`);
}

export type CulturePrincipleBehaviorType =
  | 'ExpectedBehavior'
  | 'BehaviorToAvoid'
  | 'LeadershipExpectation'
  | 'EmployeeExpectation'
  | 'AITransformationImplication';

export interface CulturePrincipleBehaviorDto {
  culturePrincipleBehaviorId: string;
  culturePrincipleId: string;
  behaviorCode: string | null;
  behaviorType: CulturePrincipleBehaviorType;
  descriptionEn: string;
  descriptionEs: string | null;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  createdByPersonId: string | null;
  updatedAt: string;
}

export interface SaveCulturePrincipleBehaviorRequest {
  behaviorCode?: string | null;
  behaviorType: CulturePrincipleBehaviorType;
  descriptionEn: string;
  descriptionEs?: string | null;
  sortOrder?: number;
  isActive?: boolean;
  createdByPersonId?: string | null;
}

export async function getCulturePrincipleBehaviors(principleId: string): Promise<CulturePrincipleBehaviorDto[]> {
  const { data } = await apiClient.get<{ behaviors: CulturePrincipleBehaviorDto[] }>(
    `/enterprise-evolution/culture/principles/${principleId}/behaviors`,
  );
  return data.behaviors;
}

export async function createCulturePrincipleBehavior(
  principleId: string,
  body: SaveCulturePrincipleBehaviorRequest,
): Promise<CulturePrincipleBehaviorDto> {
  const { data } = await apiClient.post<CulturePrincipleBehaviorDto>(
    `/enterprise-evolution/culture/principles/${principleId}/behaviors`,
    body,
  );
  return data;
}

export async function updateCulturePrincipleBehavior(
  principleId: string,
  behaviorId: string,
  body: SaveCulturePrincipleBehaviorRequest,
): Promise<CulturePrincipleBehaviorDto> {
  const { data } = await apiClient.put<CulturePrincipleBehaviorDto>(
    `/enterprise-evolution/culture/principles/${principleId}/behaviors/${behaviorId}`,
    body,
  );
  return data;
}

export async function deleteCulturePrincipleBehavior(principleId: string, behaviorId: string): Promise<void> {
  await apiClient.delete(`/enterprise-evolution/culture/principles/${principleId}/behaviors/${behaviorId}`);
}
