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

// ==================== Structured Learning cost/time dashboard (2026-08-14) ====================
// See StructuredLearningCostResponses.cs on the backend.

export interface StructuredLearningCostSummary {
  StructuredLearningDocumentId: string;
  DocumentName: string;
  InputTokens: number;
  OutputTokens: number;
  CachedInputTokens: number;
  TotalTokens: number;
  TotalElapsedMilliseconds: number;
  ImagesGeneratedCount: number;
  ImageCostUsd: number;
  EstimatedCostUsd: number;
  IsEstimate: boolean;
  GeneratedDate: string | null;
}

export interface StructuredLearningCostStep {
  StepName: string;
  Agents: string;
  Models: string;
  InputTokens: number;
  OutputTokens: number;
  CachedInputTokens: number;
  TotalTokens: number;
  TotalElapsedMilliseconds: number;
  CallCount: number;
  ImagesGeneratedCount: number;
  ImageCostUsd: number;
  EstimatedCostUsd: number;
}

export interface StructuredLearningCostDetail {
  StructuredLearningDocumentId: string;
  DocumentName: string;
  Steps: StructuredLearningCostStep[];
  InputTokens: number;
  OutputTokens: number;
  CachedInputTokens: number;
  TotalTokens: number;
  TotalElapsedMilliseconds: number;
  ImagesGeneratedCount: number;
  ImageCostUsd: number;
  EstimatedCostUsd: number;
  IsEstimate: boolean;
}

export function getStructuredLearningCosts(): Promise<StructuredLearningCostSummary[]> {
  return apiGet<StructuredLearningCostSummary[]>('/studio/structured-learning-costs');
}

export function getStructuredLearningCostDetail(documentId: string): Promise<StructuredLearningCostDetail> {
  return apiGet<StructuredLearningCostDetail>(`/studio/structured-learning-costs/${documentId}`);
}
