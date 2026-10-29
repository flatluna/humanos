import { apiGet, apiPost, apiPut, apiDelete } from './httpClient';

/** Knowledge Graph — Catálogo de Tipos/Temas (2026-08-16). One Subject
 * (domain) has many Topics (e.g. "Leyes" -> "Ley del IVA", "Ley del ISR").
 * See backend StructuredLearningTopic's own doc comment. */
export interface StructuredLearningTopic {
  StructuredLearningTopicId: string;
  TenantId: string;
  SubjectId: string;
  Name: string;
  Description: string | null;
  CreatedDate: string;
  UpdatedDate: string | null;
}

export function getStructuredLearningTopics(tenantId: string, subjectId?: string): Promise<StructuredLearningTopic[]> {
  const subjectQuery = subjectId ? `&subjectId=${subjectId}` : '';
  return apiGet<StructuredLearningTopic[]>(`/studio/structured-learning/topics?tenantId=${tenantId}${subjectQuery}`);
}

export function createStructuredLearningTopic(
  tenantId: string,
  subjectId: string,
  name: string,
  description: string | null
): Promise<StructuredLearningTopic> {
  return apiPost<StructuredLearningTopic>('/studio/structured-learning/topics', { tenantId, subjectId, name, description });
}

export function updateStructuredLearningTopic(
  topicId: string,
  name: string,
  description: string | null
): Promise<StructuredLearningTopic> {
  return apiPut<StructuredLearningTopic>(`/studio/structured-learning/topics/${topicId}`, { name, description });
}

export function deleteStructuredLearningTopic(topicId: string): Promise<void> {
  return apiDelete(`/studio/structured-learning/topics/${topicId}`);
}
