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

export interface ProcessActivityDto {
  activity_id: string;
  activity_sequence: number;
  activity_name: string;
  activity_description: string;
  activity_type: string;
  activity_status: string;
  id_source: string;
  primary_responsible_role: string;
  systems_and_tools: string[];
  human_judgment_required: boolean;
  ai_contribution: string;
  human_verification_required: boolean;
  learning_workflow_decision: string;
  required_capabilities: string[];
  suggested_learning_method: string;
  source_page: number;
  required_inputs: string[];
  outputs: string[];
  business_rules: string[];
  readiness_criteria: string[];
  source_text: string;
  missing_information: string[];
}

export interface ProcessStepImageDto {
  source_page: number;
  storage_path: string;
  content_type: string;
  /** Base64 data URL, ready to use directly as an <img src>. */
  data_url: string;
}

export interface ProcessStepDto {
  step_id: string;
  step_code: string;
  step_number: string;
  step_name: string;
  objective: string;
  primary_responsible_role: string;
  supporting_roles: string[];
  systems_and_tools: string[];
  trigger: string;
  required_inputs: string[];
  required_activities: string[];
  required_skills: string[];
  deliverables: string[];
  decisions_and_human_judgment: string[];
  business_rules_and_controls: string[];
  exceptions_and_escalations: string[];
  evidence_produced_or_required: string[];
  completion_condition: string;
  missing_information: string[];
  activities: ProcessActivityDto[];
  substeps: Array<{
    substep_id: string;
    substep_number: string;
    name: string;
    description: string;
  }>;
  checklists: string[];
  source_pages: number[];
  /** Verbatim excerpt captured during the fast Phase A index extraction; input for the per-step deep analysis. */
  source_content: string;
  /** Real images (screenshots/diagrams/figures) embedded in the source document pages covering this step —
   * extracted verbatim (never AI-generated); see ProcessStepImage on the backend. */
  step_images?: ProcessStepImageDto[];
  /** True once AnalyzeStepBusinessRequirements has populated role/systems/trigger/business-rules fields for this step. */
  business_requirements_analyzed: boolean;

  // ===== Wizard Step 3 "Clasificar Proceso" =====
  owner_role: string;
  /** "Identified" (role explicitly stated in the source) or "ToBeConfirmed" (needs human resolution). */
  owner_status: string;
  classified_inputs: CodedListItemDto[];
  classified_systems: CodedListItemDto[];
  classified_dependencies: CodedListItemDto[];
  classified_business_rules: CodedListItemDto[];
  classified_exceptions: CodedListItemDto[];
  classified_outputs: CodedListItemDto[];
  process_classified: boolean;
}

export interface CodedListItemDto {
  id: string;
  label: string;
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
  process_code: string;
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
  executive_summary: string;
  purpose: string;
  process_owner: string;
  trigger: string;
  scope: string;
  inputs: string[];
  final_output: string;
  participating_roles: string[];
  systems_used: string[];
  /** Whether this process applies to the whole organization (ignores department_ids/job_role_ids when true). */
  applies_to_all_departments: boolean;
  /** Department IDs (from the tenant's Department catalog) this process applies to. */
  department_ids: string[];
  /** Job Role IDs (from the tenant's JobRole catalog) this process applies to. */
  job_role_ids: string[];
  applicable_policies: string[];
  metrics: string[];
  version: string;
  status: string;
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

export interface RecommendedCourseDto {
  title: string;
  provider?: string;
  url?: string;
}

export interface ProcessSkillGapDto {
  name: string;
  category?: 'soft-skills' | 'technology-skills' | string;
  isAiSkill?: boolean;
  reason: string;
  affectedSteps: string[];
  recommendedCourses: RecommendedCourseDto[];
  /** True when this gap is already covered by an existing dbo.Skill catalog entry — no internet
   * search was run for it; the UI should offer "assign to roles/depts" only, not "create". */
  isFromCatalog?: boolean;
  matchedSkillId?: string | null;
  matchedSkillName?: string | null;
  /** True when the user manually assigned this skill from the catalog (instead of the LLM
   * identifying it). Preserved across re-analysis so manual choices are not discarded. */
  isManual?: boolean;
}

export interface ProcessWideLearningRecommendationResultDto {
  documentId: string;
  processName: string;
  overallSummary: string;
  skillGaps: ProcessSkillGapDto[];
  assumptions: string[];
  warnings: string[];
  /** UTC timestamp of the last time this recommendation was produced by the LLM. */
  analyzedAt?: string | null;
}

export interface ProcessSkillRequirementDto {
  processSkillRequirementId: string;
  processDocumentId: string;
  skillName: string;
  category: string;
  isAiSkill: boolean;
  reason?: string | null;
  appliesToAllDepartments: boolean;
  isRequired: boolean;
  departmentIds: string[];
  jobRoleIds: string[];
  matchedSkillId?: string | null;
}

export interface SaveProcessSkillRequirementRequest {
  skillName: string;
  category: string;
  isAiSkill: boolean;
  reason?: string;
  appliesToAllDepartments: boolean;
  isRequired: boolean;
  departmentIds: string[];
  jobRoleIds: string[];
  /** Case (a) catalog match: reuse this existing dbo.Skill, nothing new is created. */
  matchedSkillId?: string;
  /** Case (b) new internet course: create a new dbo.Skill from the fields below. */
  isNewFromInternet?: boolean;
  sourceCourseTitle?: string;
  sourceCourseProvider?: string;
  sourceCourseUrl?: string;
  sourceOutlineModules?: CourseOutlineModuleDto[];
  sourceOutlineSource?: string;
}

export async function getProcessSkillRequirements(documentId: string): Promise<ProcessSkillRequirementDto[]> {
  const response = await apiClient.get<ProcessSkillRequirementDto[]>(
    `/process-documents/${encodeURIComponent(documentId)}/skill-requirements`,
  );
  return response.data;
}

export async function saveProcessSkillRequirement(
  documentId: string,
  payload: SaveProcessSkillRequirementRequest,
  processSkillRequirementId?: string,
): Promise<ProcessSkillRequirementDto> {
  const params = processSkillRequirementId ? { processSkillRequirementId } : undefined;
  const response = await apiClient.post<ProcessSkillRequirementDto>(
    `/process-documents/${encodeURIComponent(documentId)}/skill-requirements`,
    payload,
    { params },
  );
  return response.data;
}

export async function deleteProcessSkillRequirement(documentId: string, processSkillRequirementId: string): Promise<void> {
  await apiClient.delete(
    `/process-documents/${encodeURIComponent(documentId)}/skill-requirements/${encodeURIComponent(processSkillRequirementId)}`,
  );
}

export interface SaveProcessAsCapabilityResponseDto {
  skillId: string;
  alreadyExisted: boolean;
}

/** Saves the WHOLE process (not an individual skill gap) as a "department-processes"
 * Capability Catalog entry, reusing its original PDF + chapter/step outline, so it shows up
 * in /skills and can be opened in Studio to generate its course. `departmentId` records who
 * AUTHORED the process (e.g. HR, Finance), not who must follow it. */
export async function saveProcessAsCapability(
  documentId: string,
  departmentId: string,
): Promise<SaveProcessAsCapabilityResponseDto> {
  const response = await apiClient.post<SaveProcessAsCapabilityResponseDto>(
    `/process-documents/${encodeURIComponent(documentId)}/save-as-capability`,
    { departmentId },
  );
  return response.data;
}

export async function analyzeProcessLearningNeeds(
  documentId: string,
  organizationId?: string,
): Promise<ProcessWideLearningRecommendationResultDto> {
  const params = organizationId ? { organizationId } : undefined;
  const response = await apiClient.post<ProcessWideLearningRecommendationResultDto>(
    `/process-documents/${encodeURIComponent(documentId)}/analyze-all-requirements`,
    null,
    { params },
  );
  return response.data;
}

export async function saveProcessWideLearningRecommendation(
  documentId: string,
  recommendation: ProcessWideLearningRecommendationResultDto,
  organizationId?: string,
): Promise<{ success: boolean; analyzedAt?: string | null }> {
  const params = organizationId ? { organizationId } : undefined;
  const response = await apiClient.put<{ success: boolean; analyzedAt?: string | null }>(
    `/process-documents/${encodeURIComponent(documentId)}/process-wide-recommendation`,
    recommendation,
    { params },
  );
  return response.data;
}

export interface CourseOutlineModuleDto {
  title: string;
  lessons: string[];
}

export interface CourseOutlineRequest {
  skillName: string;
  courseTitle: string;
  provider?: string;
  url?: string;
  processName?: string;
}

export interface CourseOutlineResultDto {
  courseTitle: string;
  url?: string;
  source: 'scraped' | 'ai-suggested' | string;
  modules: CourseOutlineModuleDto[];
  warnings: string[];
}

export async function getCourseOutline(payload: CourseOutlineRequest): Promise<CourseOutlineResultDto> {
  const response = await apiClient.post<CourseOutlineResultDto>('/courses/outline', payload);
  return response.data;
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

/**
 * PHASE B — deep business-requirements analysis for ONE step at a time (role, systems, trigger, business
 * rules, exceptions), using only that step's own SourceContent captured by the fast Phase A index extraction.
 * Call this once per step, as the user reviews each step — never for the whole document at once.
 */
export async function analyzeStepBusinessRequirements(
  documentId: string,
  stepId: string,
  organizationId?: string,
): Promise<ProcessStepDto> {
  const params = organizationId ? { organizationId } : undefined;
  const response = await apiClient.post<ProcessStepDto>(
    `/process-documents/${encodeURIComponent(documentId)}/steps/${encodeURIComponent(stepId)}/analyze-business-requirements`,
    null,
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
  processWideLearningRecommendation?: ProcessWideLearningRecommendationResultDto | null;
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
