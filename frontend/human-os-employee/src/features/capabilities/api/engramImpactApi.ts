import { apiGet, apiPost } from './httpClient';

export interface EngramImpactScoreDto {
  engramId: string;
  score: number;
  tier: string;
  primarySignal: string;
  reason: string;
  generatedAt: string;
}

export interface LearnerProfileSummaryDto {
  employeeId: string;
  employeeName: string;
  roleTitle: string;
  rolePurpose?: string;
  yearsOfExperience?: number;
  experienceLevel: string;
  primaryTools: string[];
  topSkills: string[];
  activeObjectiveTitles: string[];
}

/**
 * Retrieves lightweight learner profile summary for UI personalization.
 */
export async function getLearnerProfile(personId?: string): Promise<LearnerProfileSummaryDto> {
  const query = personId ? `?personId=${encodeURIComponent(personId)}` : '';
  return apiGet<LearnerProfileSummaryDto>(`/learner-profile${query}`);
}

/**
 * Retrieves personalized Engram impact scores for the authenticated employee and course.
 */
export async function getEngramImpactScores(courseId: string, personId?: string): Promise<EngramImpactScoreDto[]> {
  const query = personId ? `?personId=${encodeURIComponent(personId)}` : '';
  return apiGet<EngramImpactScoreDto[]>(`/engram-impact-scores/${courseId}${query}`);
}

/**
 * Forces AI re-scoring and returns updated impact scores for the course.
 */
export async function refreshEngramImpactScores(courseId: string, personId?: string): Promise<EngramImpactScoreDto[]> {
  const query = personId ? `?personId=${encodeURIComponent(personId)}` : '';
  return apiPost<EngramImpactScoreDto[]>(`/engram-impact-scores/${courseId}/refresh${query}`);
}
