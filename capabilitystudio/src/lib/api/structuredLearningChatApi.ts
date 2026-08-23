import { apiPost } from './httpClient';

/** AI Agents chat window (2026-08-18) — /ai-agents/:subject/:topic free-form
 * Q&A tutor. Backend: StructuredLearningChatService, grounded in the
 * Knowledge Graph retrieval package (StructuredLearningKnowledgeRetrievalService). */

export interface StructuredLearningChatHistoryEntry {
  Role: 'Student' | 'Agent';
  Text: string;
}

export interface PostStructuredLearningChatRequest {
  tenantId: string;
  message: string;
  history: { role: string; text: string }[];
}

export interface EngramReference {
  EngramId: string;
  DocumentId: string;
  Title: string;
  CourseName: string;
  Text: string;
}

export interface PostStructuredLearningChatResponse {
  Message: string;
  UsedEngrams: EngramReference[];
}

/** scope: pass subjectId(+topicId) for a Topic-level agent (all courses of
 * that Topic), or documentId for a Course-level agent (one course only). */
export interface StructuredLearningChatScope {
  subjectId?: string;
  topicId?: string;
  documentId?: string;
}

export async function postStructuredLearningChat(
  tenantId: string,
  message: string,
  history: StructuredLearningChatHistoryEntry[],
  scope: StructuredLearningChatScope = {}
): Promise<PostStructuredLearningChatResponse> {
  return apiPost<PostStructuredLearningChatResponse>('/studio/structured-learning/chat', {
    tenantId,
    message,
    history: history.map((h) => ({ role: h.Role, text: h.Text })),
    subjectId: scope.subjectId,
    topicId: scope.topicId,
    documentId: scope.documentId,
  });
}
