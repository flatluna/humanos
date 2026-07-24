import { apiGet, API_BASE_URL } from './httpClient';

/** Backend response shape (camelCase — FunctionResponseFactory serializes
 * with JsonSerializerDefaults.Web, confirmed via curl against GET
 * /capabilities: {"capabilityId":...,"name":...}) for a Capability list
 * item. */
export interface BackendCapabilitySummary {
  capabilityId: string;
  name: string;
  description?: string;
  capabilityDomainId?: string;
  subjectId?: string;
  subjectCode?: string;
  /** Total CapabilityGraphNode count (0 when no graph has been generated yet). */
  nodeCount?: number;
  /** True when GET /capabilities/{id}/cover-image will return a real image. */
  hasCoverImage?: boolean;
  /** Short "what you'll learn" teaser derived from the graph's executive
   * summary — use this on cards instead of description, which for
   * PDF-generated capabilities is just an internal generation note. */
  learningSummary?: string;
}

export function getCapabilities(subjectCode?: string): Promise<BackendCapabilitySummary[]> {
  const query = subjectCode ? `?subject=${encodeURIComponent(subjectCode)}` : '';
  return apiGet<BackendCapabilitySummary[]>(`/capabilities${query}`);
}

/** Direct <img src> URL for a capability's course-level cover image. */
export function getCapabilityCoverImageUrl(capabilityId: string): string {
  return `${API_BASE_URL}/capabilities/${capabilityId}/cover-image`;
}

