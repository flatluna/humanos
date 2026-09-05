import { apiPost } from '@/features/capabilities/api/httpClient';
import { getEnrolledStructuredLearningDocuments } from '@/features/capabilities/api/enrolledStructuredLearningApi';
import {
  getStructuredLearningDocument,
  getActiveStructuredLearningAssessmentRound,
} from '@/features/capabilities/api/structuredLearningRuntimeApi';

export interface AiAgentCourseCard {
  documentId: string;
  documentName: string;
  totalEngrams: number;
  masteredEngrams: number;
  coverIllustrationId?: string | null;
}

/** One card per enrolled Structured Learning course, with real
 * total-engrams/mastered-engrams counts (reuses the SAME per-section
 * "active round" lookup useKnowledgeNetwork.ts already uses for the Brain
 * page — no new backend endpoint needed). */
export async function listAiAgentCourses(personId: string): Promise<AiAgentCourseCard[]> {
  const enrolled = await getEnrolledStructuredLearningDocuments(personId);

  return Promise.all(
    enrolled.map(async (doc) => {
      try {
        const detail = await getStructuredLearningDocument(doc.structuredLearningDocumentId);
        const rounds = await Promise.all(
          detail.Sections.map((section) =>
            getActiveStructuredLearningAssessmentRound(section.StructuredLearningDocumentSectionId, personId).catch(() => null),
          ),
        );
        return {
          documentId: doc.structuredLearningDocumentId,
          documentName: doc.documentName,
          totalEngrams: detail.Sections.length,
          masteredEngrams: rounds.filter((r) => r?.Status === 'Passed').length,
          coverIllustrationId: doc.coverIllustrationId,
        };
      } catch {
        return {
          documentId: doc.structuredLearningDocumentId,
          documentName: doc.documentName,
          totalEngrams: doc.sectionCount,
          masteredEngrams: 0,
          coverIllustrationId: doc.coverIllustrationId,
        };
      }
    }),
  );
}

export interface AiAgentChatHistoryEntry {
  role: 'Student' | 'Agent';
  text: string;
}

export interface AiAgentUsedEngram {
  engramId: string;
  documentId: string;
  title: string;
  courseName: string;
  text: string;
}

export interface AiAgentChatResponse {
  message: string;
  usedEngrams?: AiAgentUsedEngram[];
}

/** One turn of the per-course agent chat — same backend endpoint as
 * human-os-web's AiAgentCoursePage
 * (StructuredLearningChatService/PostStructuredLearningChatFunction).
 * Passing personId lets the backend ground answers in THIS student's own
 * objectives/skills/progress for this course (BuildStudentContextAsync +
 * the Knowledge Graph's semantic/vector entity resolution — never a raw
 * dump of every engram, see StructuredLearningKnowledgeRetrievalService). */
export function postAiAgentChat(
  tenantId: string,
  personId: string,
  documentId: string,
  message: string,
  history: AiAgentChatHistoryEntry[],
): Promise<AiAgentChatResponse> {
  return apiPost<AiAgentChatResponse>('/studio/structured-learning/chat', {
    tenantId,
    personId,
    documentId,
    message,
    history: history.map((h) => ({ role: h.role, text: h.text })),
  });
}
