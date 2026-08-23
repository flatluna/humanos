import { apiGet } from './httpClient';

/** Backend response shape (camelCase — FunctionResponseFactory serializes
 * with JsonSerializerDefaults.Web, same convention as capabilitiesApi.ts). */
export interface StructuredLearningDocumentSummary {
  structuredLearningDocumentId: string;
  documentName: string;
  executiveSummary?: string | null;
  subjectId?: string | null;
  topicId?: string | null;
  domainName?: string | null;
  subdomainName?: string | null;
  sectionCount: number;
  createdDate: string;
  coverIllustrationId?: string | null;
}

export function getStructuredLearningDocuments(): Promise<StructuredLearningDocumentSummary[]> {
  return apiGet<StructuredLearningDocumentSummary[]>('/catalog/structured-learning');
}
