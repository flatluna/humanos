import { apiGet, apiPost } from '@/features/capabilities/api/httpClient';
import type { GrowWithUsPlan } from './growWithUsPlanApi';

/**
 * "Grow With Us" saved-searches history (2026-09-28) — client for the
 * left-side panel on /advance-your-knowledge: list past searches, reopen
 * one's full plan, and semantic-search across them. See
 * ListGrowWithUsSavedSearchesFunction / GetGrowWithUsSavedSearchFunction /
 * SearchGrowWithUsSavedSearchesFunction + GrowWithUsSavedSearchService.
 */

export interface GrowWithUsSavedSearchSummary {
  growWithUsSavedSearchId: string;
  topic: string;
  createdDate: string;
}

export interface GrowWithUsSavedSearchDetail {
  growWithUsSavedSearchId: string;
  topic: string;
  createdDate: string;
  plan: GrowWithUsPlan;
}

export function listGrowWithUsSavedSearches(personId: string): Promise<GrowWithUsSavedSearchSummary[]> {
  return apiGet<GrowWithUsSavedSearchSummary[]>(`/people/${personId}/grow-with-us-searches`);
}

export function getGrowWithUsSavedSearch(personId: string, savedSearchId: string): Promise<GrowWithUsSavedSearchDetail> {
  return apiGet<GrowWithUsSavedSearchDetail>(`/people/${personId}/grow-with-us-searches/${savedSearchId}`);
}

export function searchGrowWithUsSavedSearches(personId: string, query: string): Promise<GrowWithUsSavedSearchSummary[]> {
  return apiPost<GrowWithUsSavedSearchSummary[]>(`/people/${personId}/grow-with-us-searches/search`, { query });
}
