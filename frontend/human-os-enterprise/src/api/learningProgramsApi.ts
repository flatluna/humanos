import { apiClient } from './index';

export interface LearningProgram {
  programId: string;
  code: string;
  name: string;
  description: string | null;
  objectives: string | null;
  requirements: string | null;
  capabilityCount: number;
  isActive: boolean;
  createdDate: string;
  updatedDate: string;
}

export interface LearningProgramCourse {
  programCapabilityId: string;
  capabilityId: string;
  capabilityCode: string;
  capabilityName: string;
  subjectCode: string | null;
  sortOrder: number;
  isRequired: boolean;
  courseType?: string | null;
}

export interface LearningProgramDetail extends LearningProgram {
  capabilities: LearningProgramCourse[];
}

export interface LearningCapability {
  capabilityId: string;
  code: string;
  name: string;
  description: string | null;
  category: string | null;
  targetJobRoleId: string | null;
  targetSkill: string | null;
  learningMode: string;
  contentLanguageCode: string;
  isActive: boolean;
  structuredLearningDocumentId?: string;
}

export interface StructuredLearningCourse {
  structuredLearningDocumentId: string;
  documentName: string;
  executiveSummary: string | null;
  sectionCount: number;
  language: string;
}

export async function getLearningPrograms(): Promise<LearningProgram[]> {
  const { data } = await apiClient.get<LearningProgram[]>('/programs');
  return data;
}

export async function getLearningProgram(programId: string): Promise<LearningProgramDetail> {
  const { data } = await apiClient.get<LearningProgramDetail>(`/programs/${programId}`);
  return data;
}

export async function createLearningProgram(name: string): Promise<LearningProgram> {
  const { data } = await apiClient.post<LearningProgram>('/programs', { name });
  return data;
}

export async function updateLearningProgramMetadata(
  programId: string,
  body: { name: string; requirements: string },
): Promise<LearningProgram> {
  const { data } = await apiClient.put<LearningProgram>(`/programs/${programId}`, body);
  return data;
}

export async function updateLearningProgramCourses(
  programId: string,
  capabilities: Array<{ capabilityId: string; sortOrder: number }>,
): Promise<LearningProgramDetail> {
  const { data } = await apiClient.put<LearningProgramDetail>(`/programs/${programId}/capabilities`, {
    capabilities: capabilities.map((course) => ({ ...course, isRequired: true })),
  });
  return data;
}

export async function getLearningCapabilities(): Promise<LearningCapability[]> {
  const { data } = await apiClient.get<LearningCapability[]>('/capabilities', {
    params: { language: 'en' },
  });
  return data;
}

export async function getStructuredLearningCourses(): Promise<StructuredLearningCourse[]> {
  const { data } = await apiClient.get<StructuredLearningCourse[]>('/catalog/structured-learning');
  return data;
}

export async function addStructuredCourseToProgram(documentId: string, programId: string): Promise<void> {
  await apiClient.post(`/studio/structured-learning/${documentId}/programs/${programId}`);
}

export async function removeStructuredCourseFromProgram(documentId: string, programId: string): Promise<void> {
  await apiClient.delete(`/studio/structured-learning/${documentId}/programs/${programId}`);
}
