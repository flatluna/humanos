import { apiGet, apiPost, apiPut } from '@/features/capabilities/api/httpClient';

/**
 * Read-only client for the employee's own résumé/CV — GET /people/{personId}/resume.
 * Goes through FunctionResponseFactory (see GetPersonResumeFunction), so unlike
 * personProfileApi.ts this comes back camelCase. Always returns a (possibly all-empty)
 * object, never null/404 — see PersonResumeService.GetByPersonIdAsync.
 */
export interface PersonResumeDto {
  personId: string;
  address: string | null;
  linkedInUrl: string | null;
  professionalObjective: string | null;
  professionalSummary: string | null;
  skills: string[];
  workExperience: string[];
  education: string[];
  certifications: string[];
  languages: string[];
  rawResumeText: string | null;
  studentAcademicProfile: StudentAcademicProfile | null;
  updatedDate: string | null;
}

export interface StudentAcademicProfile {
  institution: string | null;
  degree: string | null;
  graduationYear: number | null;
  academicStage: string | null;
  cumulativeAverage: number | null;
  averageScale: string | null;
  distinctions: string[];
  semesters: { name: string | null; average: number | null; subjects: { name: string | null; grade: number | null }[] }[];
}

export interface SavePersonResumeRequest {
  address?: string | null;
  linkedInUrl?: string | null;
  professionalObjective?: string | null;
  professionalSummary?: string | null;
  skills?: string[];
  workExperience?: string[];
  education?: string[];
  certifications?: string[];
  languages?: string[];
  rawResumeText?: string | null;
  studentAcademicProfile?: StudentAcademicProfile | null;
}

export interface ExtractPersonResumeResponse extends SavePersonResumeRequest {
  modelName?: string;
  inputTokens?: number;
  outputTokens?: number;
}

export function getMyResume(personId: string): Promise<PersonResumeDto> {
  return apiGet<PersonResumeDto>(`/people/${personId}/resume`);
}

export function extractMyResume(personId: string, resumeText: string): Promise<ExtractPersonResumeResponse> {
  return apiPost<ExtractPersonResumeResponse>(`/people/${personId}/resume/extract`, { resumeText });
}

export function saveMyResume(personId: string, body: SavePersonResumeRequest): Promise<PersonResumeDto> {
  return apiPut<PersonResumeDto>(`/people/${personId}/resume`, body);
}
