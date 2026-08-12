import { apiPost, apiGet, apiPatch, apiDelete } from './httpClient';
import type { CapabilityOutlineChapter } from './pdfPipelineApi';

/** Studio's THIRD course-creation method (2026-08-10), Step 2 only so
 * far: deterministic (no-LLM-generation) Chapter -> Section extraction
 * with a semantic embedding per section — see backend's
 * StructuredLearningExtractionService. Reuses the SAME "Learning path
 * design" outline-approval step (previewCapabilityOutline) the Graph
 * wizard already uses — request/response shapes mirror
 * traditionalCourseApi.ts's conventions. */

export interface StartStructuredLearningExtractionRequest {
  tenantId: string;
  documentName: string;
  /** Required for the PDF entry point. Mutually exclusive with `description`. */
  fileName?: string | null;
  contentBase64?: string | null;
  /** Required for the "Texto/idea" entry point. Mutually exclusive with `fileName`/`contentBase64`. */
  description?: string | null;
  languageCode?: 'es' | 'en';
  strictSyllabusMode?: boolean;
  /** The designer's reviewed/approved (possibly hand-edited) chapter/section
   * outline, as returned by previewCapabilityOutline — REQUIRED for Step 2
   * (unlike the Graph/Traditional pipelines, where it's optional). */
  approvedOutline: CapabilityOutlineChapter[];
  /** Only used together with approvedOutline from the "Texto/idea" entry
   * point — see pdfPipelineApi.ts's identical field for the full rationale. */
  preExpandedDocumentText?: string | null;
}

export type StructuredLearningExtractionStage = 'Running' | 'Completed' | 'Failed';

export interface StructuredLearningExtractionResult {
  StructuredLearningDocumentId: string;
  DocumentName: string;
  SectionCount: number;
  TotalCharacters: number;
  TotalTokens: number;
}

export interface StructuredLearningExtractionRunStatus {
  RunId: string;
  Stage: StructuredLearningExtractionStage;
  CurrentStepDescription: string;
  TotalSections: number | null;
  CompletedSections: number | null;
  ErrorMessage: string | null;
  Result: StructuredLearningExtractionResult | null;
}

/** Starts a new Structured Learning Document extraction run (Step 2).
 * Returns immediately (Stage: 'Running') — poll
 * getStructuredLearningExtractionStatus for progress. */
export function startStructuredLearningExtraction(
  request: StartStructuredLearningExtractionRequest
): Promise<StructuredLearningExtractionRunStatus> {
  return apiPost<StructuredLearningExtractionRunStatus>('/studio/structured-learning/extract', request);
}

export function getStructuredLearningExtractionStatus(runId: string): Promise<StructuredLearningExtractionRunStatus> {
  return apiGet<StructuredLearningExtractionRunStatus>(`/studio/structured-learning/${runId}/status`);
}

// ==================== Step 3: pedagogy configuration + generation ====================

export type StructuredLearningAssessmentQuestionType = 'MultipleChoice' | 'OpenEnded';

export interface StructuredLearningPedagogyConfig {
  pedagogicalAudience?: string | null;
  maxDeepDiveCardsPerNode?: number | null;
  includeProductionStep: boolean;
  includeRecallStep: boolean;
  recallItemsRequired?: number | null;
  assessmentQuestionType: StructuredLearningAssessmentQuestionType;
  assessmentQuestionsPerRound?: number | null;
}

export function updateStructuredLearningPedagogyConfig(
  documentId: string,
  config: StructuredLearningPedagogyConfig
): Promise<{ ok: boolean }> {
  return apiPatch<{ ok: boolean }>(`/studio/structured-learning/${documentId}/config`, config);
}

export type StructuredLearningPedagogyStage = 'Running' | 'Completed' | 'Failed';

export interface StructuredLearningPedagogyResult {
  StructuredLearningDocumentId: string;
  SectionsCompleted: number;
  SectionsFailed: number;
  TotalInputTokens: number;
  TotalOutputTokens: number;
}

export interface StructuredLearningPedagogyRunStatus {
  RunId: string;
  Stage: StructuredLearningPedagogyStage;
  CurrentStepDescription: string;
  TotalSections: number | null;
  CompletedSections: number | null;
  ActiveSectionTitles: string[] | null;
  ErrorMessage: string | null;
  Result: StructuredLearningPedagogyResult | null;
}

export function startStructuredLearningPedagogy(
  structuredLearningDocumentId: string,
  languageCode: 'es' | 'en' = 'es',
  forceRegenerateAll = false,
  uploadedImageBase64?: string | null,
  uploadedImageContentType?: string | null,
  referenceImageUsageInstruction?: string | null
): Promise<StructuredLearningPedagogyRunStatus> {
  return apiPost<StructuredLearningPedagogyRunStatus>('/studio/structured-learning/pedagogy/start', {
    structuredLearningDocumentId,
    languageCode,
    forceRegenerateAll,
    uploadedImageBase64,
    uploadedImageContentType,
    referenceImageUsageInstruction,
  });
}

export function getStructuredLearningPedagogyStatus(runId: string): Promise<StructuredLearningPedagogyRunStatus> {
  return apiGet<StructuredLearningPedagogyRunStatus>(`/studio/structured-learning/pedagogy/${runId}/status`);
}

export interface StructuredLearningStepDetail {
  StructuredLearningSectionStepId: string;
  StepType: string;
  Content: string;
  StepDataJson: string | null;
  PromptVersion?: string | null;
  ProcessVersion?: string | null;
  ModelName?: string | null;
  PromptHash?: string | null;
  ConfigSnapshotJson?: string | null;
  IsHumanApproved: boolean;
  HumanApprovedAtUtc: string | null;
}

// ==================== "Edit mode" review tools (2026-08-10) ====================
// Mirrors Graph's editNodeBlueprintStep/addDeepDiveCard/editDeepDiveCard/
// deleteDeepDiveCard — free-text-instruction AI editing, pre-publish review
// only. Only Hypothesis/Teaching/Production have a static Content to edit
// this way (DeepDive is card-by-card; Recall/Assessment are generated live).

export function editStructuredLearningStep(
  sectionId: string,
  stepType: string,
  instruction: string
): Promise<StructuredLearningStepDetail> {
  return apiPost<StructuredLearningStepDetail>(`/studio/structured-learning/sections/${sectionId}/steps/${stepType}/edit`, { instruction });
}

/** Direct manual save (2026-08-11) — no AI involved, overwrites Content
 * with the reviewer's own hand-edited text. `confirmed` MUST be true (also
 * enforced server-side) — the reviewer's explicit sign-off that they read
 * this content and take responsibility for it, since the AI is not perfect
 * and can alter or invent content. */
export function setStructuredLearningStepContent(
  sectionId: string,
  stepType: string,
  content: string,
  confirmed: boolean
): Promise<StructuredLearningStepDetail> {
  return apiPost<StructuredLearningStepDetail>(`/studio/structured-learning/sections/${sectionId}/steps/${stepType}/content`, { content, confirmed });
}

export function addStructuredLearningDeepDiveCard(sectionId: string, instruction: string): Promise<StructuredLearningStepDetail> {
  return apiPost<StructuredLearningStepDetail>(`/studio/structured-learning/sections/${sectionId}/deepdive/cards/add`, { instruction });
}

export function editStructuredLearningDeepDiveCard(
  sectionId: string,
  cardIndex: number,
  instruction: string
): Promise<StructuredLearningStepDetail> {
  return apiPost<StructuredLearningStepDetail>(`/studio/structured-learning/sections/${sectionId}/deepdive/cards/${cardIndex}/edit`, { instruction });
}

export function deleteStructuredLearningDeepDiveCard(sectionId: string, cardIndex: number): Promise<StructuredLearningStepDetail> {
  return apiDelete<StructuredLearningStepDetail>(`/studio/structured-learning/sections/${sectionId}/deepdive/cards/${cardIndex}`);
}

/** "Edit mode" add-section feature (2026-08-11) — mirrors Graph's
 * addCapabilityGraphNode: generates AND persists a brand-new section
 * (with its 6 pedagogical steps) synchronously in one call. See backend's
 * StructuredLearningSectionAdditionService.
 *
 * requiresSectionIds/requiredBySectionIds mirror Graph's Requires/
 * RequiredBy checklists, adapted to this pipeline's linear chapter
 * sequencing (they control WHERE in the chapter the new section lands,
 * not a prerequisite graph). illustrationMode/uploadedImageBase64/
 * uploadedImageContentType mirror Graph's 4-way illustration mode choice.
 * referenceImageUsageInstruction is a SEPARATE instruction (only meaningful
 * for illustrationMode 'AiGeneratedFromUpload') describing HOW the
 * reference image should appear in the generated scenes (e.g. "put our
 * logo on the volunteers' t-shirts", "use this exact mug in the scene"). */
export function addStructuredLearningSection(
  documentId: string,
  chapterTitle: string,
  instruction: string,
  languageCode: 'es' | 'en' = 'es',
  requiresSectionIds: string[] = [],
  requiredBySectionIds: string[] = [],
  illustrationMode: string = 'AiGenerated',
  uploadedImageBase64?: string | null,
  uploadedImageContentType?: string | null,
  referenceImageUsageInstruction?: string | null
): Promise<StructuredLearningSectionDetail> {
  return apiPost<StructuredLearningSectionDetail>(`/studio/structured-learning/${documentId}/sections/add`, {
    chapterTitle,
    instruction,
    languageCode,
    requiresSectionIds,
    requiredBySectionIds,
    illustrationMode,
    uploadedImageBase64,
    uploadedImageContentType,
    referenceImageUsageInstruction,
  });
}

/** "Edit mode" delete-section feature (2026-08-11) — mirrors Graph's
 * deleteCapabilityGraphNode. Permanently removes the section and all of
 * its steps/illustrations/recall/assessment progress. Irreversible — the
 * caller must have already confirmed with the user (see
 * DeleteStructuredLearningSectionModal). */
export function deleteStructuredLearningSection(documentId: string, sectionId: string): Promise<void> {
  return apiDelete(`/studio/structured-learning/${documentId}/sections/${sectionId}`);
}

export interface StructuredLearningSectionDetail {
  StructuredLearningDocumentSectionId: string;
  SortOrder: number;
  ChapterTitle: string;
  SubchapterTitle: string;
  TotalCharacters: number;
  TotalTokens: number;
  PromptVersion?: string | null;
  ProcessVersion?: string | null;
  ModelName?: string | null;
  PromptHash?: string | null;
  ConfigSnapshotJson?: string | null;
  PedagogyStatus: string;
  PedagogyErrorMessage: string | null;
  Steps: StructuredLearningStepDetail[];
}

export interface StructuredLearningDocumentDetail {
  StructuredLearningDocumentId: string;
  DocumentName: string;
  PedagogicalAudience: string | null;
  PromptVersion?: string | null;
  ProcessVersion?: string | null;
  ModelName?: string | null;
  PromptHash?: string | null;
  ConfigSnapshotJson?: string | null;
  MaxDeepDiveCardsPerNode: number | null;
  IncludeProductionStep: boolean;
  IncludeRecallStep: boolean;
  RecallItemsRequired: number | null;
  AssessmentQuestionType: StructuredLearningAssessmentQuestionType;
  AssessmentQuestionsPerRound: number | null;
  Sections: StructuredLearningSectionDetail[];
}

export function getStructuredLearningDocument(documentId: string): Promise<StructuredLearningDocumentDetail> {
  return apiGet<StructuredLearningDocumentDetail>(`/studio/structured-learning/${documentId}`);
}

export function deleteStructuredLearningDocument(documentId: string): Promise<void> {
  return apiDelete(`/studio/structured-learning/${documentId}`);
}

/** Lightweight read-only listing (2026-08-10) — lets the catalog page show
 * these as cards without the full Capability-model integration. */
export interface StructuredLearningDocumentSummary {
  StructuredLearningDocumentId: string;
  DocumentName: string;
  SectionCount: number;
  CreatedDate: string;
  CoverIllustrationId: string | null;
}

export function getStructuredLearningDocuments(): Promise<StructuredLearningDocumentSummary[]> {
  return apiGet<StructuredLearningDocumentSummary[]>('/studio/structured-learning');
}

export interface StructuredLearningDocumentProgramMembership {
  ProgramId: string;
  ProgramCode: string;
  ProgramName: string;
  SortOrder: number;
  IsRequired: boolean;
  PhaseLabel: string | null;
  Objectives: string | null;
  Requirements: string | null;
}

export function getProgramsForStructuredLearningDocument(documentId: string): Promise<StructuredLearningDocumentProgramMembership[]> {
  return apiGet<StructuredLearningDocumentProgramMembership[]>(`/studio/structured-learning/${documentId}/programs`);
}

export function addStructuredLearningDocumentToProgram(documentId: string, programId: string): Promise<void> {
  return apiPost<void>(`/studio/structured-learning/${documentId}/programs/${programId}`, {});
}

export function removeStructuredLearningDocumentFromProgram(documentId: string, programId: string): Promise<void> {
  return apiDelete(`/studio/structured-learning/${documentId}/programs/${programId}`);
}

// ==================== Real-time Recall/Assessment (2026-08-10) ====================
// Replaces the OLD design where Recall items/Assessment questions were baked
// once at generation time — questions are now generated live, one at a time,
// so practice never repeats. See StructuredLearningAssessmentEngine.cs/
// StructuredLearningRecallEngine.cs on the backend.

export interface StructuredLearningAssessmentQuestion {
  StructuredLearningAssessmentQuestionAttemptId: string;
  QuestionIndex: number;
  QuestionText: string;
  Options: string[] | null;
}

export interface StructuredLearningAssessmentRoundState {
  StructuredLearningAssessmentRoundId: string;
  RoundNumber: number;
  Status: 'InProgress' | 'Passed' | 'Failed';
  FinalScore: number | null;
  TotalQuestions: number;
  CurrentQuestion: StructuredLearningAssessmentQuestion | null;
}

export function startStructuredLearningAssessmentRound(sectionId: string): Promise<StructuredLearningAssessmentRoundState> {
  return apiPost<StructuredLearningAssessmentRoundState>(`/studio/structured-learning/sections/${sectionId}/assessment/start-round`);
}

export function getActiveStructuredLearningAssessmentRound(sectionId: string): Promise<StructuredLearningAssessmentRoundState | null> {
  return apiGet<StructuredLearningAssessmentRoundState | null>(`/studio/structured-learning/sections/${sectionId}/assessment/active-round`);
}

export interface StructuredLearningAssessmentAnswerGrade {
  Correctness: 'Pending' | 'Correct' | 'PartiallyCorrect' | 'Incorrect';
  Score: number;
  Feedback: string;
}

export interface SubmitStructuredLearningAssessmentAnswerResult {
  Grade: StructuredLearningAssessmentAnswerGrade;
  RoundComplete: boolean;
  Passed: boolean | null;
  FinalScore: number | null;
  NextQuestion: StructuredLearningAssessmentQuestion | null;
  NewRoundNumber: number | null;
  NewStructuredLearningAssessmentRoundId: string | null;
}

export function submitStructuredLearningAssessmentAnswer(
  questionAttemptId: string,
  studentAnswer: string
): Promise<SubmitStructuredLearningAssessmentAnswerResult> {
  return apiPost<SubmitStructuredLearningAssessmentAnswerResult>(
    `/studio/structured-learning/assessment/questions/${questionAttemptId}/answer`,
    { studentAnswer }
  );
}

export interface SubmitStructuredLearningRecallAttemptResult {
  Started: boolean;
  NextQuestion: string;
  Score: number | null;
  AttemptsUsedForItem: number;
  ItemsMastered: number;
  ItemsRequired: number;
  MasteredThisAttempt: boolean;
  Complete: boolean;
  ItemFailed: boolean;
}

/** Leave studentResponse undefined ONLY for the very first call for a
 * section (nothing to grade yet — just fetches the first question). */
export function submitStructuredLearningRecallAttempt(
  sectionId: string,
  studentResponse?: string
): Promise<SubmitStructuredLearningRecallAttemptResult> {
  return apiPost<SubmitStructuredLearningRecallAttemptResult>(
    `/studio/structured-learning/sections/${sectionId}/recall/attempt`,
    { studentResponse: studentResponse ?? null }
  );
}

/** Bootstrap-safe GET (2026-08-10) — fixes a real bug where a page
 * remount always POSTed .../recall/attempt with no StudentResponse
 * assuming it was the section's very first turn, which 400'd once a
 * question had already been asked. Never grades anything — safe to call
 * on every mount/reload. */
export function getStructuredLearningRecallState(sectionId: string): Promise<SubmitStructuredLearningRecallAttemptResult> {
  return apiGet<SubmitStructuredLearningRecallAttemptResult>(`/studio/structured-learning/sections/${sectionId}/recall/state`);
}

// ==================== Real-time Deep Dive (2026-08-10) ====================
// Card-by-card SEQUENTIAL and MANDATORY-ALL — every card must be mastered in
// order before the step is Complete, mirroring Graph's DeepDiveLoopGate. See
// backend's StructuredLearningDeepDiveEngine.cs.

export interface StructuredLearningDeepDiveStateResult {
  NextQuestion: string;
  CardIndex: number;
  TotalCards: number;
  Complete: boolean;
}

/** Bootstrap-safe GET — never grades anything, safe on every mount. */
export function getStructuredLearningDeepDiveState(sectionId: string): Promise<StructuredLearningDeepDiveStateResult> {
  return apiGet<StructuredLearningDeepDiveStateResult>(`/studio/structured-learning/sections/${sectionId}/deepdive/state`);
}

export interface SubmitStructuredLearningDeepDiveAttemptResult {
  Message: string;
  CardIndex: number;
  TotalCards: number;
  Mastered: boolean;
  NextCardIndex: number;
  Complete: boolean;
}

export function submitStructuredLearningDeepDiveAttempt(
  sectionId: string,
  studentResponse: string,
  tutorPromptShown?: string
): Promise<SubmitStructuredLearningDeepDiveAttemptResult> {
  return apiPost<SubmitStructuredLearningDeepDiveAttemptResult>(
    `/studio/structured-learning/sections/${sectionId}/deepdive/attempt`,
    { studentResponse, tutorPromptShown: tutorPromptShown ?? null }
  );
}


