import { apiGet, API_BASE_URL } from './httpClient';

/** Backend response shape (PascalCase) for a Capability list item. */
export interface BackendCapabilitySummary {
  CapabilityId: string;
  Name: string;
  Description?: string;
  CapabilityDomainId?: string;
  SubjectId?: string;
  SubjectCode?: string;
  /** Total CapabilityGraphNode count (0 when no graph has been generated yet). */
  NodeCount?: number;
  /** True when GET /capabilities/{id}/cover-image will return a real image. */
  HasCoverImage?: boolean;
  /** Short "what you'll learn" teaser derived from the graph's executive
   * summary — use this on cards instead of Description, which for
   * PDF-generated capabilities is just an internal generation note. */
  LearningSummary?: string;
}

export function getCapabilities(subjectCode?: string): Promise<BackendCapabilitySummary[]> {
  const query = subjectCode ? `?subject=${encodeURIComponent(subjectCode)}` : '';
  return apiGet<BackendCapabilitySummary[]>(`/capabilities${query}`);
}

/** Direct <img src> URL for a capability's course-level cover image. */
export function getCapabilityCoverImageUrl(capabilityId: string): string {
  return `${API_BASE_URL}/capabilities/${capabilityId}/cover-image`;
}

