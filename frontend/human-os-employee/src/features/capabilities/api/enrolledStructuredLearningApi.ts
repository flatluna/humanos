import { apiDelete, apiGet, apiPost } from './httpClient';

/**
 * "Mis Engramas" (structured learning) — client for the Structured
 * Learning document enrollment endpoints. Mirrors
 * enrolledCapabilitiesApi.ts's shape/DTOs
 * (Contracts/Capabilities/EnrolledStructuredLearningDocumentContracts.cs).
 */

export interface EnrolledStructuredLearningDocument {
  structuredLearningDocumentId: string;
  documentName: string;
  sectionCount: number;
  /** Distinct sections this person has a Passed assessment round for. */
  completedSectionCount?: number;
  coverIllustrationId?: string | null;
  enrolledDate: string;
  /** "es"/"en" — see StructuredLearningLanguageHeuristic (server-side). */
  language?: string;
}

export interface StructuredLearningDocumentEnrollmentAction {
  structuredLearningDocumentId: string;
  enrolled: boolean;
  enrolledDate?: string | null;
}

export function getEnrolledStructuredLearningDocuments(personId: string): Promise<EnrolledStructuredLearningDocument[]> {
  return apiGet<EnrolledStructuredLearningDocument[]>(`/people/${personId}/enrolled-structured-learning`);
}

export function enrollStructuredLearningDocument(
  personId: string,
  documentId: string,
): Promise<StructuredLearningDocumentEnrollmentAction> {
  return apiPost<StructuredLearningDocumentEnrollmentAction>(`/people/${personId}/structured-learning/${documentId}/enroll`);
}

export function unenrollStructuredLearningDocument(
  personId: string,
  documentId: string,
): Promise<StructuredLearningDocumentEnrollmentAction> {
  return apiDelete<StructuredLearningDocumentEnrollmentAction>(`/people/${personId}/structured-learning/${documentId}/enroll`);
}
