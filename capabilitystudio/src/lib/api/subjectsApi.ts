import { apiGet } from './httpClient';

/** Backend response shape (PascalCase) — see SubjectResponse.cs. The
 * student-facing topical browsing axis (Matematicas, Finanzas, Cocina...),
 * distinct from CapabilityDomain. */
export interface Subject {
  SubjectId: string;
  Code: string;
  Name: string;
  Description: string | null;
}

export function getSubjects(language = 'es'): Promise<Subject[]> {
  return apiGet<Subject[]>(`/subjects?language=${language}`);
}
