import { apiGet, apiPost } from '@/features/capabilities/api/httpClient';

/**
 * "Recomienda contenido relevante" (2026-09-29) — client for the content
 * recommendation agent: POST /people/{personId}/content-recommendations
 * generates+saves a report, GET lists/reopens saved searches, and
 * POST .../send-email re-sends a saved report to the student's own inbox
 * so they can keep a copy. Serialized camelCase by FunctionResponseFactory.
 */

export interface RecommendedContentItem {
  title: string;
  type: string;
  reason: string;
  url?: string | null;
}

export interface ContentRecommendationProposal {
  title: string;
  summary: string;
  recommendedItems: RecommendedContentItem[];
  trendsSummary: string;
}

/** Generate response also carries the newly created saved-search id (null if the
 * best-effort save failed) so the UI can enable "send by email" immediately. */
export interface GeneratedContentRecommendation extends ContentRecommendationProposal {
  savedSearchId: string | null;
}

export interface ContentRecommendationSavedSearchSummary {
  contentRecommendationSavedSearchId: string;
  topic: string;
  createdDate: string;
}

export function generateContentRecommendations(
  personId: string,
  prompt?: string,
  languageCode?: string,
  sendEmail?: boolean,
): Promise<GeneratedContentRecommendation> {
  return apiPost<GeneratedContentRecommendation>(`/people/${personId}/content-recommendations`, {
    prompt,
    languageCode,
    sendEmail,
  });
}

export function listContentRecommendations(personId: string): Promise<ContentRecommendationSavedSearchSummary[]> {
  return apiGet<ContentRecommendationSavedSearchSummary[]>(`/people/${personId}/content-recommendations`);
}

export function getContentRecommendation(personId: string, savedSearchId: string): Promise<ContentRecommendationProposal> {
  return apiGet<ContentRecommendationProposal>(`/people/${personId}/content-recommendations/${savedSearchId}`);
}

export function sendContentRecommendationEmail(personId: string, savedSearchId: string): Promise<{ sent: boolean }> {
  return apiPost<{ sent: boolean }>(`/people/${personId}/content-recommendations/${savedSearchId}/send-email`, {});
}
