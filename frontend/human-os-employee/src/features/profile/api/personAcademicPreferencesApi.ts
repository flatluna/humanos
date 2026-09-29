import { API_BASE_URL } from '@/features/capabilities/api/httpClient';
import { getIdentityHeaders } from '@/auth';

/**
 * Read-only client for the employee's own academic preferences (2026-09-28) —
 * saved from the HR console's "Intereses" page (human-os-enterprise,
 * EmployeeAcademicInterestsPage.tsx / PUT /people/{personId}/academic-preferences).
 * The employee app only ever reads this (no edit UI here yet) to show a
 * "Tus preferencias" summary card on the Today/"Crece con nosotros" home page.
 * Fetched with a raw fetch() (not apiGet) so a 404 (no preferences saved
 * yet) can be told apart from a real error without depending on the
 * response body's message text.
 */
export interface PersonAcademicPreferencesDto {
  PersonAcademicPreferencesId: string;
  PersonId: string;
  AcademicInterests: string[];
  LearningPreferences: string[];
  AcademicMotivations: string[];
  GrowthAreas: string[];
  CreatedDate: string;
  UpdatedDate: string;
}

export async function getPersonAcademicPreferences(personId: string): Promise<PersonAcademicPreferencesDto | null> {
  const response = await fetch(`${API_BASE_URL}/people/${personId}/academic-preferences`, {
    method: 'GET',
    headers: { Accept: 'application/json', ...getIdentityHeaders() },
  });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Request failed with status ${response.status}`);
  return (await response.json()) as PersonAcademicPreferencesDto;
}

