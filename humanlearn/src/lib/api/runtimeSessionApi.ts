import { apiGet, apiPost } from './httpClient';

/**
 * Client for the Runtime V1 "instructor-runtime" HTTP API — drives a
 * node's 5-step Memory Paradox experience (Hypothesis/Teaching/Recall/
 * Production/Assessment). Mirrors the backend DTOs in
 * backend/HumanOS/AzureFunctions/Api/RuntimeGraphApiModels.cs and
 * TutorApiModels.cs exactly (PascalCase). Per that file's own doc comment,
 * the UI never treats LearningSessionId/NodeId/StepId as its own source of
 * truth — they're just opaque IDs passed straight through to the next call.
 */

export type ExperienceStepType = 'Hypothesis' | 'Teaching' | 'Recall' | 'Production' | 'Assessment';

export interface BackendIllustration {
  IllustrationId: string;
  StoragePath: string;
  Caption?: string;
}

export interface BackendRuntimeStep {
  LearningSessionStepId: string;
  StepType: ExperienceStepType;
  Content: string;
  Illustrations: BackendIllustration[];
}

export interface BackendRuntimeSessionInfo {
  LearningSessionId: string;
  LearningSessionNodeId: string;
  CapabilityGraphNodeId: string;
  CurrentStepType: ExperienceStepType;
  CurrentStep: BackendRuntimeStep;
}

export function getActiveSession(
  personId: string,
  capabilityId: string
): Promise<BackendRuntimeSessionInfo | null> {
  return apiGet<BackendRuntimeSessionInfo | null>(
    `/instructor-runtime/sessions/active?personId=${personId}&capabilityId=${capabilityId}`
  );
}

export function startSession(
  personId: string,
  capabilityId: string,
  capabilityGraphNodeId: string
): Promise<BackendRuntimeSessionInfo> {
  return apiPost<BackendRuntimeSessionInfo>('/instructor-runtime/sessions/start', {
    PersonId: personId,
    CapabilityId: capabilityId,
    CapabilityGraphNodeId: capabilityGraphNodeId,
  });
}

export function submitStepResponse(
  learningSessionStepId: string,
  response: string
): Promise<{ success: boolean; learningEvidenceId: string }> {
  return apiPost('/instructor-runtime/steps/respond', {
    LearningSessionStepId: learningSessionStepId,
    Response: response,
  });
}

export function advanceStep(learningSessionNodeId: string): Promise<BackendRuntimeStep> {
  return apiPost<BackendRuntimeStep>('/instructor-runtime/steps/advance', {
    LearningSessionNodeId: learningSessionNodeId,
  });
}

export interface TutorTurnDto {
  Message: string;
  RecallScore?: number;
  Illustrations: BackendIllustration[];
}

export type TutorMode = 'Teaching' | 'Production' | 'AssessmentFeedback';

export function askTutor(
  learningSessionStepId: string,
  mode: TutorMode,
  studentMessage: string,
  rawAssessmentFeedback?: string
): Promise<TutorTurnDto> {
  return apiPost<TutorTurnDto>('/instructor-runtime/tutor/ask', {
    LearningSessionStepId: learningSessionStepId,
    Mode: mode,
    StudentMessage: studentMessage,
    RawAssessmentFeedback: rawAssessmentFeedback,
  });
}

export interface RecallAttemptOutcomeDto {
  TutorTurn: TutorTurnDto;
  LearningEvidenceId: string;
  /** Attempts used on the CURRENT item (resets after each mastered item, and after a regression-to-Teaching cycle). */
  AttemptsUsedForItem: number;
  /** How many of ItemsRequired distinct items the student has mastered so far on this Recall step. */
  ItemsMastered: number;
  ItemsRequired: number;
  Mastered: boolean;
  /** True only when all ItemsRequired items are now mastered and the step advanced to Production. */
  Advanced: boolean;
  /** True when the student exhausted their attempt budget on one item and the node regressed back to Teaching. */
  RegressedToTeaching: boolean;
  NextStep?: BackendRuntimeStep | null;
}

export function submitRecallAttempt(
  learningSessionStepId: string,
  studentResponse: string,
  tutorPromptShown?: string
): Promise<RecallAttemptOutcomeDto> {
  return apiPost<RecallAttemptOutcomeDto>('/instructor-runtime/tutor/recall-attempts', {
    LearningSessionStepId: learningSessionStepId,
    StudentResponse: studentResponse,
    TutorPromptShown: tutorPromptShown,
  });
}

export interface EvidenceEntryDto {
  StudentResponse: string;
  TutorPrompt?: string;
  TutorScore?: number;
  CreatedDate: string;
}

/** Read-only recap of a step the student already started — never mutates anything (no reactivation, no new evidence). */
export interface StepReviewDto {
  StepType: ExperienceStepType;
  Status: 'NotStarted' | 'Active' | 'Completed';
  StartedDate?: string;
  CompletedDate?: string;
  Content: string;
  Illustrations: BackendIllustration[];
  Evidence: EvidenceEntryDto[];
}

export function getStepReview(
  learningSessionNodeId: string,
  stepType: ExperienceStepType
): Promise<StepReviewDto> {
  return apiGet<StepReviewDto>(
    `/instructor-runtime/steps/review?learningSessionNodeId=${learningSessionNodeId}&stepType=${stepType}`
  );
}

/**
 * "Profundizar" (Knowledge Expansion) — learner-triggered, never automatic
 * (see /memories/repo/adaptive-learning-engine-design.md). First call for a
 * node generates it (LLM knowledge + live Bing Grounding search + optional
 * diagram); later calls (any learner) return the same cached result. The
 * diagram, if present, is served via the existing
 * `/illustrations/{id}/image` endpoint — same as any other node illustration.
 */
export interface KnowledgeExpansionDto {
  CapabilityGraphNodeId: string;
  Content: string;
  DiagramIllustrationId?: string;
}

export function expandNodeKnowledge(capabilityGraphNodeId: string): Promise<KnowledgeExpansionDto> {
  return apiPost<KnowledgeExpansionDto>(`/capability-graph-nodes/${capabilityGraphNodeId}/knowledge-expansion`, {});
}

/** One past completed attempt at a node — see NodeSummaryDto.PastAttempts. */
export interface NodeAttemptSummaryDto {
  LearningSessionNodeId: string;
  StartedDate?: string;
  CompletedDate?: string;
  FinalScore?: number;
  Passed: boolean;
}

/**
 * Read-only "what happened the last time I completed this node" recap —
 * shown when opening a node that is already Mastered on the map, instead
 * of silently starting a brand-new attempt from scratch.
 */
export interface NodeSummaryDto {
  CapabilityGraphNodeId: string;
  /** Which attempt (row in PastAttempts) the top-level Steps belongs to — always the most recent one. */
  LearningSessionNodeId: string;
  AttemptCount: number;
  FirstCompletedDate?: string;
  LastCompletedDate?: string;
  FinalScore?: number;
  Steps: StepReviewDto[];
  PastAttempts: NodeAttemptSummaryDto[];
}

export function getNodeSummary(personId: string, capabilityGraphNodeId: string): Promise<NodeSummaryDto> {
  return apiGet<NodeSummaryDto>(
    `/instructor-runtime/nodes/summary?personId=${personId}&capabilityGraphNodeId=${capabilityGraphNodeId}`
  );
}

export interface AssessmentResultDto {
  Score: number;
  Passed: boolean;
  Feedback: string;
}

export function evaluateAssessment(learningSessionNodeId: string): Promise<AssessmentResultDto> {
  return apiPost<AssessmentResultDto>('/instructor-runtime/nodes/evaluate', {
    LearningSessionNodeId: learningSessionNodeId,
  });
}

// === Production ("Aplícalo") formative evaluation (2026-07-18) ===
// Purely formative — never affects node mastery/unlocking (that stays
// Assessment's exclusive domain). No attempt cap: the student may resubmit
// as many times as they want after an IsCorrect=false verdict.

export interface EvaluateProductionResponseDto {
  IsCorrect: boolean;
  Score: number;
  Feedback: string;
  LearningEvidenceId: string;
}

export function evaluateProduction(
  learningSessionStepId: string,
  studentSubmission: string
): Promise<EvaluateProductionResponseDto> {
  return apiPost<EvaluateProductionResponseDto>('/instructor-runtime/production/evaluate', {
    LearningSessionStepId: learningSessionStepId,
    StudentSubmission: studentSubmission,
  });
}

export interface GraphNodeInfoDto {
  CapabilityGraphNodeId: string;
  Name: string;
  SortOrder: number;
}

export function completeNode(
  learningSessionNodeId: string
): Promise<{ success: boolean; newlyUnlockedNodes: GraphNodeInfoDto[] }> {
  return apiPost('/instructor-runtime/nodes/complete', {
    LearningSessionNodeId: learningSessionNodeId,
  });
}

// === Adaptive Assessment (2026-07-18) ===
// Dynamic, one-question-at-a-time Assessment redesign — replaces the old
// single-free-text-then-evaluate flow (evaluateAssessment above, which
// stays wired to the legacy endpoint but is no longer called by the UI).
// A round always has exactly 5 questions; a Failed round auto-starts a
// brand-new round with 5 new questions in the SAME response.

export type AssessmentQuestionType =
  | 'ActiveRecall'
  | 'Comprehension'
  | 'Application'
  | 'ErrorDetection'
  | 'Transfer'
  | 'Production'
  | 'MultipleChoice';

export type AssessmentRoundStatus = 'InProgress' | 'Passed' | 'Failed';

export type AssessmentQuestionCorrectness = 'Correct' | 'PartiallyCorrect' | 'Incorrect';

export interface AssessmentQuestionDto {
  AssessmentQuestionId: string;
  QuestionIndex: number;
  QuestionType: AssessmentQuestionType;
  QuestionText: string;
  /** Set only when the question genuinely benefits from a visual scenario —
   * servable via the existing `/illustrations/{id}/image` endpoint, same as
   * any other node illustration. */
  IllustrationId?: string | null;
}

export interface AssessmentRoundStateDto {
  AssessmentRoundId: string;
  RoundNumber: number;
  TotalQuestions: number;
  Status: AssessmentRoundStatus;
  FinalScore?: number | null;
  CurrentQuestion?: AssessmentQuestionDto | null;
}

export interface AssessmentAnswerGradeDto {
  Correctness: AssessmentQuestionCorrectness;
  Score: number;
  Feedback: string;
}

export interface SubmitAssessmentAnswerResponse {
  Grade: AssessmentAnswerGradeDto;
  RoundComplete: boolean;
  Passed?: boolean | null;
  FinalScore?: number | null;
  NextQuestion?: AssessmentQuestionDto | null;
  NewRoundNumber?: number | null;
  NewAssessmentRoundId?: string | null;
}

export function getActiveAssessmentRound(
  learningSessionNodeId: string
): Promise<AssessmentRoundStateDto | null> {
  return apiGet<AssessmentRoundStateDto | null>(
    `/instructor-runtime/assessment/active?learningSessionNodeId=${learningSessionNodeId}`
  );
}

export function startAssessmentRound(learningSessionNodeId: string): Promise<AssessmentRoundStateDto> {
  return apiPost<AssessmentRoundStateDto>('/instructor-runtime/assessment/start-round', {
    LearningSessionNodeId: learningSessionNodeId,
  });
}

export function submitAssessmentAnswer(
  assessmentQuestionId: string,
  studentAnswer: string
): Promise<SubmitAssessmentAnswerResponse> {
  return apiPost<SubmitAssessmentAnswerResponse>('/instructor-runtime/assessment/answer', {
    AssessmentQuestionId: assessmentQuestionId,
    StudentAnswer: studentAnswer,
  });
}
