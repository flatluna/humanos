import { apiClient } from './index';

export interface ProcessDocumentUploadResponse {
  document_id: string;
  file_name: string;
  process_name: string;
  status: 'completed' | 'accepted' | 'failed';
  source_storage_path: string;
  structured_result_path: string;
  chapters_identified: number;
  steps_identified: number;
  tables_extracted: number;
  warnings: string[];
  status_url?: string;
}

export interface ProcessStepDto {
  step_id: string;
  step_number: string;
  step_name: string;
  objective: string;
  required_activities: string[];
  required_skills: string[];
  deliverables: string[];
  completion_condition: string;
  substeps: Array<{
    substep_id: string;
    substep_number: string;
    name: string;
    description: string;
  }>;
  checklists: string[];
  source_pages: number[];
}

export interface ProcessChapterDto {
  chapter_id: string;
  chapter_number: string;
  title: string;
  description: string;
  modules: Array<{
    module_id: string;
    module_number: string;
    title: string;
    description: string;
    phases: Array<{
      phase_id: string;
      phase_number: string;
      name: string;
      description: string;
    }>;
  }>;
  steps: ProcessStepDto[];
}

export interface ProcessTableDto {
  table_id: string;
  title: string;
  headers: string[];
  rows: string[][];
  source_page: number;
}

export interface ProcessDocumentStructureDto {
  document_id: string;
  process_name: string;
  document_title: string;
  source_file: {
    file_name: string;
    content_type: string;
    storage_path: string;
  };
  chapters: ProcessChapterDto[];
  tables: ProcessTableDto[];
  appendices: Array<{
    appendix_id: string;
    title: string;
    content: string;
    source_pages: number[];
  }>;
  warnings: string[];
  processing_status: string;
}

export interface SkillItemDto {
  skillId?: string;
  name: string;
  reason: string;
  category?: 'soft-skills' | 'job-skills' | 'department-processes' | 'hr-processes' | string;
  isAiSkill?: boolean;
}

export interface RequiredToolItemDto {
  name: string;
  useInStep: string;
}

export interface WorkStepRequirementsAnalysisResultDto {
  documentId: string;
  stepId: string;
  stepNumber: string;
  workStep: string;
  employeeMustBeAbleTo: string[];
  requiredSkillAreas: {
    softSkills: SkillItemDto[];
    businessSkills: SkillItemDto[];
    technicalSkills: SkillItemDto[];
    departmentProcesses?: SkillItemDto[];
    hrProcesses?: SkillItemDto[];
    aiSkills?: SkillItemDto[];
  };
  requiredTools: RequiredToolItemDto[];
  completionEvidence: string[];
  assumptions: string[];
  warnings: string[];
  sourcePages: number[];
}

export interface UploadProcessDocumentOptions {
  processName?: string;
  processDescription?: string;
  organizationId?: string;
  isAsync?: boolean;
  onUploadProgress?: (progressEvent: { loaded: number; total?: number }) => void;
}

export async function uploadProcessDocument(
  file: File,
  options?: UploadProcessDocumentOptions,
): Promise<ProcessDocumentUploadResponse> {
  const formData = new FormData();
  formData.append('file', file, file.name);

  if (options?.processName) {
    formData.append('process_name', options.processName);
  }
  if (options?.processDescription) {
    formData.append('process_description', options.processDescription);
  }
  if (options?.isAsync) {
    formData.append('is_async', 'true');
  }

  const params = options?.organizationId ? { organizationId: options.organizationId } : undefined;

  const response = await apiClient.post<ProcessDocumentUploadResponse>('/process-documents', formData, {
    params,
    headers: {
      'Content-Type': 'multipart/form-data',
    },
    onUploadProgress: (progress) => {
      if (options?.onUploadProgress && progress.total) {
        options.onUploadProgress({ loaded: progress.loaded, total: progress.total });
      }
    },
  });

  return response.data;
}

export async function getProcessDocumentStatus(documentId: string): Promise<any> {
  const response = await apiClient.get(`/process-documents/${encodeURIComponent(documentId)}/status`);
  return response.data;
}

export async function getProcessDocumentStructure(
  documentId: string,
  organizationId?: string,
): Promise<ProcessDocumentStructureDto> {
  const params = organizationId ? { organizationId } : undefined;
  const response = await apiClient.get<ProcessDocumentStructureDto>(
    `/process-documents/${encodeURIComponent(documentId)}`,
    { params },
  );
  return response.data;
}

export interface ProcessDocumentListItemDto {
  processDocumentId: string;
  title: string;
  code?: string;
  version: string;
  status: string;
  totalStepsCount: number;
  uniqueSkillsCount: number;
  createdDate: string;
  updatedDate?: string;
}

export async function getProcessDocuments(organizationId?: string): Promise<ProcessDocumentListItemDto[]> {
  const params = organizationId ? { organizationId } : undefined;
  const response = await apiClient.get<ProcessDocumentListItemDto[]>('/process-documents', { params });
  return response.data;
}

export interface CandidateClassDto {
  learningPathId: string;
  learningPathName: string;
  classId: string;
  className: string;
  classDescription: string;
  publicationStatus: string;
  language: string;
  matchedAbilities: string[];
  matchedSkillRequirements: Array<{
    category: string;
    name: string;
  }>;
  relevanceReason: string;
  relevanceScore: number;
  limitations: string[];
}

export interface CandidateClassSearchResponseDto {
  documentId: string;
  stepId: string;
  stepNumber: string;
  workStep: string;
  candidateClasses: CandidateClassDto[];
  requirementsWithoutCandidateClass: string[];
  warnings: string[];
  sourcePages: number[];
}

export async function analyzeWorkStepRequirements(
  documentId: string,
  stepId: string,
  organizationId?: string,
  previouslyIdentifiedSkills?: SkillItemDto[],
): Promise<WorkStepRequirementsAnalysisResultDto> {
  const params = organizationId ? { organizationId } : undefined;
  const response = await apiClient.post<WorkStepRequirementsAnalysisResultDto>(
    `/process-documents/${encodeURIComponent(documentId)}/steps/${encodeURIComponent(stepId)}/analyze-requirements`,
    previouslyIdentifiedSkills && previouslyIdentifiedSkills.length > 0 ? previouslyIdentifiedSkills : null,
    { params },
  );
  return response.data;
}

export async function searchCandidateClasses(
  documentId: string,
  stepId: string,
  organizationId?: string,
  language?: string,
  requirements?: WorkStepRequirementsAnalysisResultDto | null,
): Promise<CandidateClassSearchResponseDto> {
  const params: Record<string, string> = {};
  if (organizationId) params.organizationId = organizationId;
  if (language) params.language = language;

  const response = await apiClient.post<CandidateClassSearchResponseDto>(
    `/process-documents/${encodeURIComponent(documentId)}/steps/${encodeURIComponent(stepId)}/candidate-classes`,
    requirements || null,
    { params },
  );
  return response.data;
}

export interface SelectedEngramDto {
  engramId: string;
  engramName: string;
  engramDescription: string;
  supportedRequirements: string[];
  relevanceReason: string;
  relevanceScore: number;
  criticality: 'critical' | 'supporting';
}

export interface SelectedClassDto {
  learningPathId: string;
  learningPathName: string;
  classId: string;
  className: string;
  classDescription: string;
  selectedEngrams: SelectedEngramDto[];
}

export interface RequirementCoverageItemDto {
  requirement: string;
  requirementType: 'ability' | 'soft_skill' | 'business_skill' | 'technical_skill' | 'tool' | 'completion_evidence';
  coverageStatus: 'covered' | 'partially_covered' | 'not_covered';
  supportingEngramIds: string[];
  notes: string;
}

export interface RelevantEngramsResponseDto {
  documentId: string;
  stepId: string;
  stepNumber: string;
  workStep: string;
  selectedClasses: SelectedClassDto[];
  requirementCoverage: RequirementCoverageItemDto[];
  uncoveredRequirements: string[];
  assumptions: string[];
  warnings: string[];
  sourcePages: number[];
}

export async function selectRelevantEngrams(
  documentId: string,
  stepId: string,
  organizationId?: string,
  language?: string,
): Promise<RelevantEngramsResponseDto> {
  const params: Record<string, string> = {};
  if (organizationId) params.organizationId = organizationId;
  if (language) params.language = language;

  const response = await apiClient.post<RelevantEngramsResponseDto>(
    `/process-documents/${encodeURIComponent(documentId)}/steps/${encodeURIComponent(stepId)}/relevant-engrams`,
    null,
    { params },
  );
  return response.data;
}

export interface CoveredRequirementItemDto {
  requirement: string;
  criticality: 'critical' | 'supporting';
  category?: string;
  skillId?: string;
  isAiSkill?: boolean;
  matchedCapabilityName?: string;
  supportingEngramIds: string[];
}

export interface ContentGapItemDto {
  requirement: string;
  requirementType: 'ability' | 'soft_skill' | 'business_skill' | 'technical_skill' | 'tool' | 'completion_evidence' | string;
  criticality: 'critical' | 'supporting';
  currentCoverage: 'partially_covered' | 'not_covered';
  gapReason: string;
  recommendedGapType: 'none' | 'new_engram_in_existing_class' | 'new_class_required' | 'new_skill_required' | 'skill_in_catalog_course_pending' | string;
  existingLearningPathId?: string;
  existingClassId?: string;
  proposedEngramTitle?: string;
  targetClassName?: string;
  targetClassType?: 'existing_class' | 'proposed_class';
  category?: 'soft-skills' | 'job-skills' | 'department-processes' | 'hr-processes' | string;
  skillId?: string;
  isAiSkill?: boolean;
  isInCatalog?: boolean;
  hasCourse?: boolean;
  matchedCapabilityName?: string;
}

export interface LearningContentCoverageResponseDto {
  documentId: string;
  stepId: string;
  stepNumber: string;
  workStep: string;
  overallCoverageStatus: 'covered' | 'partially_covered' | 'content_gap';
  coverageSummary: string;
  coveredRequirements: CoveredRequirementItemDto[];
  contentGaps: ContentGapItemDto[];
  assumptions: string[];
  warnings: string[];
  sourcePages: number[];
}

export async function analyzeLearningContentCoverage(
  documentId: string,
  stepId: string,
  organizationId?: string,
): Promise<LearningContentCoverageResponseDto> {
  const params: Record<string, string> = {};
  if (organizationId) params.organizationId = organizationId;

  const response = await apiClient.post<LearningContentCoverageResponseDto>(
    `/process-documents/${encodeURIComponent(documentId)}/steps/${encodeURIComponent(stepId)}/content-coverage`,
    null,
    { params },
  );
  return response.data;
}

export interface EngramDraftDto {
  draftReference: string;
  proposedEngramTitle: string;
  proposedEngramDescription: string;
  capabilitySupported: string;
  workStepRequirementSupported: string;
  reasonNeeded: string;
  parentLearningPathId: string;
  parentLearningPathName: string;
  parentClassId: string;
  parentClassName: string;
  status: string;
}

export interface ProposedEngramIndexItemDto {
  proposedEngramTitle: string;
  proposedEngramDescription: string;
  capabilitySupported: string;
  workStepRequirementSupported: string;
  suggestedOrder: number;
}

export interface ClassDraftDto {
  draftReference: string;
  proposedClassTitle: string;
  proposedClassDescription: string;
  capabilityDeveloped: string;
  workStepRequirementsSupported: string[];
  recommendedExistingLearningPathId?: string;
  recommendedExistingLearningPathName?: string;
  proposedLearningPathName?: string;
  reasonNewClassIsRequired: string;
  proposedEngramIndex: ProposedEngramIndexItemDto[];
  status: string;
}

export interface CatalogItemCheckedDto {
  itemType: string;
  itemId: string;
  itemName: string;
}

export interface LearningContentGapDraftResponseDto {
  documentId: string;
  stepId: string;
  stepNumber: string;
  workStep: string;
  overallCoverageStatus: string;
  draftCreated: boolean;
  engramDrafts: EngramDraftDto[];
  classDrafts: ClassDraftDto[];
  catalogItemsChecked: CatalogItemCheckedDto[];
  assumptions: string[];
  warnings: string[];
  sourcePages: number[];
}

export async function generateContentGapDraft(
  documentId: string,
  stepId: string,
  organizationId?: string,
  language?: string,
): Promise<LearningContentGapDraftResponseDto> {
  const params: Record<string, string> = {};
  if (organizationId) params.organizationId = organizationId;
  if (language) params.language = language;

  const response = await apiClient.post<LearningContentGapDraftResponseDto>(
    `/process-documents/${encodeURIComponent(documentId)}/steps/${encodeURIComponent(stepId)}/content-gap-draft`,
    null,
    { params },
  );
  return response.data;
}

export interface StepLearningAssignmentResponseDto {
  documentId: string;
  stepId: string;
  stepNumber: string;
  workStep: string;
  assignmentStatus: 'ready' | 'ready_with_content_gaps' | 'blocked_by_content_gap';
  assignmentMarkdown: string;
  learningPathIds: string[];
  classIds: string[];
  engramIds: string[];
  coverageStatus: 'covered' | 'partially_covered' | 'content_gap';
  contentGapDraftReferences: string[];
  blockingContentGap: boolean;
  assumptions: string[];
  warnings: string[];
  sourcePages: number[];
}

export async function generateStepLearningAssignment(
  documentId: string,
  stepId: string,
  organizationId?: string,
  language?: string,
): Promise<StepLearningAssignmentResponseDto> {
  const params: Record<string, string> = {};
  if (organizationId) params.organizationId = organizationId;
  if (language) params.language = language;

  const response = await apiClient.post<StepLearningAssignmentResponseDto>(
    `/process-documents/${encodeURIComponent(documentId)}/steps/${encodeURIComponent(stepId)}/learning-assignment`,
    null,
    { params },
  );
  return response.data;
}

export interface AssignedEngramDto {
  engramId: string;
  engramName: string;
  assignmentStatus: 'required' | 'recommended' | 'reference_only' | 'previously_completed' | 'unavailable_content_gap';
  assignmentReason: string;
  employeeEvidenceLevel: 'unknown' | 'reported' | 'completed' | 'demonstrated';
  employeeEvidenceSummary: string;
}

export interface AssignedClassDto {
  learningPathId: string;
  learningPathName: string;
  classId: string;
  className: string;
  classAssignmentStatus: 'required' | 'recommended' | 'reference_only';
  assignedEngrams: AssignedEngramDto[];
}

export interface AssignmentContentGapDto {
  missingCapability: string;
  contentRequired: string;
  draftReference: string;
  status: string;
  blocking: boolean;
}

export interface EmployeeStepLearningAssignmentResponseDto {
  assignmentId: string;
  employeeId: string;
  documentId: string;
  stepId: string;
  stepNumber: string;
  workStep: string;
  employeeAssignmentStatus: 'assigned' | 'assigned_with_content_gaps' | 'blocked_by_content_gap';
  employeeMustBeAbleTo: string[];
  reportedExperienceSummary: string[];
  assignedClasses: AssignedClassDto[];
  expectedWorkEvidence: string[];
  contentGaps: AssignmentContentGapDto[];
  assignmentMarkdown: string;
  assumptions: string[];
  warnings: string[];
  createdAt: string;
}

export async function createEmployeeStepLearningAssignment(
  documentId: string,
  stepId: string,
  employeeId: string,
  organizationId?: string,
  language?: string,
): Promise<EmployeeStepLearningAssignmentResponseDto> {
  const params: Record<string, string> = {};
  if (organizationId) params.organizationId = organizationId;
  if (language) params.language = language;

  const response = await apiClient.post<EmployeeStepLearningAssignmentResponseDto>(
    `/process-documents/${encodeURIComponent(documentId)}/steps/${encodeURIComponent(stepId)}/employee-assignments`,
    { employeeId },
    { params },
  );
  return response.data;
}

export async function getEmployeeStepLearningAssignment(
  assignmentId: string,
  organizationId?: string,
): Promise<EmployeeStepLearningAssignmentResponseDto> {
  const params: Record<string, string> = {};
  if (organizationId) params.organizationId = organizationId;

  const response = await apiClient.get<EmployeeStepLearningAssignmentResponseDto>(
    `/employee-step-learning-assignments/${encodeURIComponent(assignmentId)}`,
    { params },
  );
  return response.data;
}

export interface SaveProcessAnalysisRequestDto {
  title?: string;
  code?: string;
  description?: string;
  departmentId?: string | null;
  jobRoleId?: string | null;
  structure?: ProcessDocumentStructureDto | null;
  stepCoverages?: Record<string, LearningContentCoverageResponseDto>;
  stepRequirements?: Record<string, WorkStepRequirementsAnalysisResultDto>;
}

export interface SaveProcessAnalysisResponseDto {
  success: boolean;
  processDocumentId: string;
  title: string;
  totalSteps: number;
  totalSkills: number;
  storageContainer: string;
  storageBlobPath: string;
  message: string;
}

export async function saveProcessAnalysis(
  documentId: string,
  request: SaveProcessAnalysisRequestDto,
  organizationId?: string,
  userId?: string,
): Promise<SaveProcessAnalysisResponseDto> {
  const params: Record<string, string> = {};
  if (organizationId) params.organizationId = organizationId;
  if (userId) params.userId = userId;

  const response = await apiClient.post<SaveProcessAnalysisResponseDto>(
    `/process-documents/${encodeURIComponent(documentId)}/save-analysis`,
    request,
    { params },
  );
  return response.data;
}

export interface ProcessDocumentDetailDto {
  processDocumentId: string;
  title: string;
  code?: string;
  version: string;
  description?: string;
  departmentId?: string | null;
  jobRoleId?: string | null;
  status: string;
  totalStepsCount: number;
  uniqueSkillsCount: number;
  createdDate: string;
  updatedDate?: string;
  structure: ProcessDocumentStructureDto;
  stepRequirements: Record<string, WorkStepRequirementsAnalysisResultDto>;
  stepCoverages: Record<string, LearningContentCoverageResponseDto>;
}

export async function getProcessDocumentDetail(
  documentId: string,
  organizationId?: string,
): Promise<ProcessDocumentDetailDto> {
  const params = organizationId ? { organizationId } : undefined;
  const response = await apiClient.get<ProcessDocumentDetailDto>(
    `/process-documents/${encodeURIComponent(documentId)}/detail`,
    { params },
  );
  return response.data;
}

export async function deleteProcessDocument(
  documentId: string,
  organizationId?: string,
): Promise<{ success: boolean; message: string }> {
  const params = organizationId ? { organizationId } : undefined;
  const response = await apiClient.delete<{ success: boolean; message: string }>(
    `/process-documents/${encodeURIComponent(documentId)}`,
    { params },
  );
  return response.data;
}
