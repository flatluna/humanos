import { apiGet } from './httpClient';

/** See CapabilityCostResponses.cs — CapabilityCostSummaryResponse. */
export interface BackendCapabilityCostSummary {
  CapabilityId: string;
  CapabilityName: string;
  InputTokens: number;
  OutputTokens: number;
  CachedInputTokens: number;
  TotalTokens: number;
  ImagesGeneratedCount: number;
  EstimatedCostUsd: number;
  IsEstimate: boolean;
  /** UTC ISO timestamp of the capability's earliest generation-usage row, or null if none yet. */
  GeneratedDate: string | null;
}

/** See CapabilityCostResponses.cs — CapabilityCostSectionResponse. */
export interface BackendCapabilityCostSection {
  SectionLabel: string;
  Agents: string;
  Models: string;
  InputTokens: number;
  OutputTokens: number;
  CachedInputTokens: number;
  TotalTokens: number;
  EstimatedCostUsd: number;
}

/** See CapabilityCostResponses.cs — CapabilityCostDetailResponse. */
export interface BackendCapabilityCostDetail {
  CapabilityId: string;
  CapabilityName: string;
  Sections: BackendCapabilityCostSection[];
  InputTokens: number;
  OutputTokens: number;
  CachedInputTokens: number;
  TotalTokens: number;
  ImagesGeneratedCount: number;
  EstimatedCostUsd: number;
  IsEstimate: boolean;
}

export function getCapabilityCosts(date?: string): Promise<BackendCapabilityCostSummary[]> {
  const query = date ? `?date=${encodeURIComponent(date)}` : '';
  return apiGet<BackendCapabilityCostSummary[]>(`/studio/capability-costs${query}`);
}

export function getCapabilityCostDetail(capabilityId: string): Promise<BackendCapabilityCostDetail> {
  return apiGet<BackendCapabilityCostDetail>(`/studio/capability-costs/${capabilityId}`);
}
