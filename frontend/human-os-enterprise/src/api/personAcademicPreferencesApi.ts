import { apiClient } from '@/api/index';

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

export interface UpsertPersonAcademicPreferencesRequest {
  academicInterests: string[];
  learningPreferences: string[];
  academicMotivations: string[];
  growthAreas: string[];
}

export async function getPersonAcademicPreferences(personId: string): Promise<PersonAcademicPreferencesDto | null> {
  try {
    const { data } = await apiClient.get<PersonAcademicPreferencesDto>(`/people/${personId}/academic-preferences`);
    return data;
  } catch (error: unknown) {
    if (error && typeof error === 'object' && 'response' in error && (error.response as { status?: number }).status === 404) return null;
    throw error;
  }
}

export function savePersonAcademicPreferences(
  personId: string,
  body: UpsertPersonAcademicPreferencesRequest,
): Promise<PersonAcademicPreferencesDto> {
  return apiClient.put<PersonAcademicPreferencesDto>(`/people/${personId}/academic-preferences`, body).then((response) => response.data);
}
