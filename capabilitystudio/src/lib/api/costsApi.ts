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
  VideosGeneratedCount: number;
  VideoCostUsd: number;
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
  VideosGeneratedCount?: number;
  VideoCostUsd?: number;
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
  VideosGeneratedCount: number;
  VideoCostUsd: number;
  EstimatedCostUsd: number;
  IsEstimate: boolean;
}

export function getStructuredLearningCosts(): Promise<StructuredLearningCostSummary[]> {
  return apiGet<StructuredLearningCostSummary[]>('/studio/structured-learning-costs');
}

export function getStructuredLearningCostDetail(documentId: string): Promise<StructuredLearningCostDetail> {
  return apiGet<StructuredLearningCostDetail>(`/studio/structured-learning-costs/${documentId}`);
}

/** Per-engram cost breakdown (2026-09-26) — everything billed to ONE
 * section: initial design generation AND every later edit (manual or AI),
 * plus that engram's own images/videos. See StructuredLearningCostService
 * .GetPerEngramAsync. */
export interface StructuredLearningEngramModelCost {
  ModelName: string;
  CallCount: number;
  InputTokens: number;
  CachedInputTokens: number;
  OutputTokens: number;
  InputRatePerMillionTokens: number;
  CachedInputRatePerMillionTokens: number;
  OutputRatePerMillionTokens: number;
  InputCostUsd: number;
  CachedInputCostUsd: number;
  OutputCostUsd: number;
  TotalCostUsd: number;
}

export interface StructuredLearningEngramVideoModelCost {
  ModelName: string;
  VideoCount: number;
  RatePerVideoUsd: number;
  TotalCostUsd: number;
}

export interface StructuredLearningEngramCost {
  StructuredLearningDocumentSectionId: string;
  ChapterTitle: string;
  SubchapterTitle: string;
  Steps: string;
  CallCount: number;
  InputTokens: number;
  OutputTokens: number;
  CachedInputTokens: number;
  TotalTokens: number;
  ModelBreakdown: StructuredLearningEngramModelCost[];
  LlmCostUsd: number;
  ImagesGeneratedCount: number;
  ImageRatePerImageUsd: number;
  ImageCostUsd: number;
  VideosGeneratedCount: number;
  VideoModelBreakdown: StructuredLearningEngramVideoModelCost[];
  VideoCostUsd: number;
  TotalCostUsd: number;
  IsEstimate: boolean;
}

export function getStructuredLearningCostPerEngram(documentId: string, sectionId?: string): Promise<StructuredLearningEngramCost[]> {
  const query = sectionId ? `?sectionId=${sectionId}` : '';
  return apiGet<StructuredLearningEngramCost[]>(`/studio/structured-learning-costs/${documentId}/per-engram${query}`);
}

