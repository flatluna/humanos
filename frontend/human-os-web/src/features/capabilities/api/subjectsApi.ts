import { apiGet } from './httpClient';

type Language = 'en' | 'es';

/**
 * Subject — real backend entity as of 2026-07-21 (`dbo.Subject` /
 * `dbo.SubjectTranslation`, see /memories/repo/student-graph-ui-redesign-final-design.md).
 * `Capability.SubjectId` is a nullable FK; `GET /capabilities?subject=<code>`
 * filters server-side (see SubjectCapabilitiesPage.tsx).
 */
export interface Subject {
  code: string;
  name: string;
  iconKey: string;
  description: string;
}

/** Backend response shape (PascalCase) for GET /subjects. */
interface BackendSubject {
  SubjectId: string;
  Code: string;
  Name: string;
  Description?: string;
}

export async function getSubjects(language: Language = 'es'): Promise<Subject[]> {
  const subjects = await apiGet<BackendSubject[]>(`/subjects?language=${language}`);
  return subjects.map((subject) => ({
    code: subject.Code,
    iconKey: subject.Code,
    name: subject.Name,
    description: subject.Description ?? '',
  }));
}

