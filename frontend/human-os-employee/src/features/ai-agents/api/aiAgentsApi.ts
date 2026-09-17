import { apiGet, apiPost } from '@/features/capabilities/api/httpClient';
import { getEnrolledStructuredLearningDocuments } from '@/features/capabilities/api/enrolledStructuredLearningApi';
import { getCapabilities } from '@/features/capabilities/api/capabilitiesApi';
import { getSkills } from '@/features/capabilities/api/skillsApi';
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

interface StructuredLearningDocumentSummary {
  structuredLearningDocumentId: string;
  documentName: string;
  sectionCount: number;
  coverIllustrationId?: string | null;
}

function getPublishedStructuredLearningDocuments(): Promise<StructuredLearningDocumentSummary[]> {
  return apiGet<StructuredLearningDocumentSummary[]>('/catalog/structured-learning');
}

/** One card per published Structured Learning course. Enrolled courses keep
 * their real progress; published courses without enrollment remain available
 * with zero progress so every agent can be discovered from this page. */
export async function listAiAgentCourses(personId: string, tenantId: string): Promise<AiAgentCourseCard[]> {
  const enrolled = await getEnrolledStructuredLearningDocuments(personId);
  const published = await getPublishedStructuredLearningDocuments();
  const capabilities = await getCapabilities();
  const skills = tenantId ? await getSkills(tenantId) : [];
  const enrolledById = new Map(enrolled.map((doc) => [doc.structuredLearningDocumentId, doc]));
  const structuredLearningCards = await Promise.all(published.map(async (doc) => {
      const enrolledDoc = enrolledById.get(doc.structuredLearningDocumentId);
      if (!enrolledDoc) {
        return {
          documentId: doc.structuredLearningDocumentId,
          documentName: doc.documentName,
          totalEngrams: doc.sectionCount,
          masteredEngrams: 0,
          coverIllustrationId: doc.coverIllustrationId,
        };
      }
      try {
        const detail = await getStructuredLearningDocument(doc.structuredLearningDocumentId);
        const rounds = await Promise.all(
          detail.Sections.map((section) =>
            getActiveStructuredLearningAssessmentRound(section.StructuredLearningDocumentSectionId, personId).catch(() => null),
          ),
        );
        return {
          documentId: enrolledDoc.structuredLearningDocumentId,
          documentName: enrolledDoc.documentName,
          totalEngrams: detail.Sections.length,
          masteredEngrams: rounds.filter((r) => r?.Status === 'Passed').length,
          coverIllustrationId: enrolledDoc.coverIllustrationId,
        };
      } catch {
        return {
          documentId: enrolledDoc.structuredLearningDocumentId,
          documentName: enrolledDoc.documentName,
          totalEngrams: enrolledDoc.sectionCount,
          masteredEngrams: 0,
          coverIllustrationId: enrolledDoc.coverIllustrationId,
        };
      }
    }));

  const structuredLearningIds = new Set(structuredLearningCards.map((course) => course.documentId));
  const capabilityCards = capabilities
    .filter((capability) => !structuredLearningIds.has(capability.capabilityId))
    .map((capability) => ({
      documentId: capability.capabilityId,
      documentName: capability.name,
      totalEngrams: capability.nodeCount ?? 0,
      masteredEngrams: 0,
      coverIllustrationId: null,
    }));

  const knownIds = new Set([...structuredLearningIds, ...capabilityCards.map((course) => course.documentId)]);
  const skillCards = skills
    .filter((skill) => skill.matchedCapabilityId && !knownIds.has(skill.matchedCapabilityId))
    .map((skill) => ({
      documentId: skill.matchedCapabilityId as string,
      documentName: skill.matchedCapabilityName || skill.name,
      totalEngrams: 0,
      masteredEngrams: 0,
      coverIllustrationId: null,
    }));

  return [...structuredLearningCards, ...capabilityCards, ...skillCards];
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

/** One turn of the per-program agent chat — grounds answers in ALL
 * courses and engrams of the program across the Knowledge Graph. */
export function postAiProgramAgentChat(
  tenantId: string,
  personId: string,
  programId: string,
  message: string,
  history: AiAgentChatHistoryEntry[],
): Promise<AiAgentChatResponse> {
  return apiPost<AiAgentChatResponse>('/studio/structured-learning/chat', {
    tenantId,
    personId,
    programId,
    message,
    history: history.map((h) => ({ role: h.role, text: h.text })),
  });
}
