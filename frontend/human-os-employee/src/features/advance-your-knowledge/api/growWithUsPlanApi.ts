import { apiPost } from '@/features/capabilities/api/httpClient';

/**
 * "Grow With Us" prompt-driven growth-plan agent (2026-09-27) — client for
 * POST /people/{personId}/grow-with-us-plan (GenerateGrowWithUsPlanFunction
 * / GrowWithUsPlanAgent). Serialized camelCase by FunctionResponseFactory,
 * same convention as every other api/*.ts client in this feature area.
 */

export interface GrowWithUsCourseSuggestion {
  name: string;
  objectives: string;
  suggestedIndex: string[];
}

export interface GrowWithUsPaperSuggestion {
  title: string;
  author?: string | null;
  summary: string;
  url?: string | null;
}

export interface GrowWithUsNewsLink {
  title: string;
  url: string;
  summary: string;
}

export interface GrowWithUsBestPractice {
  title: string;
  scope: 'Role' | 'Company';
  description: string;
  impact: string;
}

export interface GrowWithUsPlan {
  title?: string;
  overallSummary: string;
  suggestedCourses: GrowWithUsCourseSuggestion[];
  papers: GrowWithUsPaperSuggestion[];
  newsLinks: GrowWithUsNewsLink[];
  bestPractices?: GrowWithUsBestPractice[];
}

export function generateGrowWithUsPlan(personId: string, prompt: string, languageCode?: string): Promise<GrowWithUsPlan> {
  return apiPost<GrowWithUsPlan>(`/people/${personId}/grow-with-us-plan`, { prompt, languageCode });
}
