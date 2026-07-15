import { apiGet } from './httpClient';

export interface BackendCapabilityContentModule {
  CapabilityModuleId: string;
  SortOrder: number;
  Title: string;
  Description: string;
  Type: string;
  Script: string;
  MetricRationale: string;
  Metrics: string[];
}

export interface BackendCapabilityContentLevel {
  CapabilityLevelId: string;
  Layer: string;
  Title: string;
  HumanTransformation: string;
  Modules: BackendCapabilityContentModule[];
}

export interface BackendCapabilityContent {
  CapabilityId: string;
  Code: string;
  Name: string;
  Description: string | null;
  Levels: BackendCapabilityContentLevel[];
}

export function getCapabilityContent(capabilityId: string): Promise<BackendCapabilityContent> {
  return apiGet<BackendCapabilityContent>(`/capabilities/${capabilityId}/content`);
}
