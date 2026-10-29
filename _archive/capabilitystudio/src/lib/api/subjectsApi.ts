import { apiGet } from './httpClient';
import i18n from '../../i18n';

/** Backend response shape (PascalCase) — see SubjectResponse.cs. The
 * student-facing topical browsing axis (Matematicas, Finanzas, Cocina...),
 * distinct from CapabilityDomain. */
export interface Subject {
  SubjectId: string;
  Code: string;
  Name: string;
  Description: string | null;
}

/** BUG FIX (2026-08-23): this used to default to 'es' unconditionally, so
 * every caller that omitted `language` (almost all of them) always got
 * Spanish subject names back from the backend regardless of the Studio
 * UI's own selected language (EN/ES toggle in the header). Default now
 * follows i18n's CURRENT language at call time instead. */
export function getSubjects(language: string = i18n.language): Promise<Subject[]> {
  return apiGet<Subject[]>(`/subjects?language=${language}`);
}

