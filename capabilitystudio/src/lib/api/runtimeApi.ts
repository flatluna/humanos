import { apiGet, apiPost } from './httpClient';

/**
 * Client for the real Human OS Learning Runtime API, ported from
 * humanlearn/src/lib/api/{capabilityGraphApi,runtimeSessionApi}.ts so the
 * "Memory Paradox" student experience (Hypothesis → Teaching → Recall →
 * Production → Assessment) can be previewed directly inside Capability
 * Studio's own UI instead of opening the separate student app.
 */

// === Graph ===

export type CapabilityGraphNodeState = 'Locked' | 'Available' | 'Mastered';

export interface BackendCapabilityGraphNode {
  CapabilityGraphNodeId: string;
  Name: string;
  Description?: string;
  SortOrder: number;
  State: CapabilityGraphNodeState;
  IllustrationId?: string;
}

export type BackendRelationshipType = 0 | 1;

export interface BackendCapabilityGraphEdge {
  SourceNodeId: string;
  TargetNodeId: string;
  RelationshipType: BackendRelationshipType;
}

export interface BackendCapabilityGraph {
  CapabilityGraphId: string;
  Name: string;
  Description?: string;
  Nodes: BackendCapabilityGraphNode[];
  Edges: BackendCapabilityGraphEdge[];
  ExecutiveSummary?: string;
}

export function getCapabilityGraph(capabilityId: string, personId: string): Promise<BackendCapabilityGraph> {
  return apiGet<BackendCapabilityGraph>(`/capabilities/${capabilityId}/graph?personId=${personId}`);
}

// === Runtime session (5-step Memory Paradox) ===

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

export function getActiveSession(personId: string, capabilityId: string): Promise<BackendRuntimeSessionInfo | null> {
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
  AttemptsUsedForItem: number;
  ItemsMastered: number;
  ItemsRequired: number;
  Mastered: boolean;
  Advanced: boolean;
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

// === Voice Tutor (Azure OpenAI Realtime, 2026-07-21) ===
// Mints a short-lived ephemeral session so the BROWSER can negotiate
// WebRTC directly against Azure (see VoiceTutorAgent.tsx) — this backend
// call never carries audio itself, only the one-time token. Scope:
// Hypothesis/Teaching/Recall steps are accepted (see VoiceTutorSessionFunction.cs).

export interface VoiceTutorSessionDto {
  ClientSecret: string;
  RealtimeCallsUrl: string;
  Model: string;
  Voice: string;
  ExpiresAtUnixSeconds?: number | null;
}

export function requestVoiceTutorSession(
  target:
    | { learningSessionStepId: string; promptText?: string }
    | { capabilityGraphNodeId: string; stepType: ExperienceStepType }
): Promise<VoiceTutorSessionDto> {
  return apiPost<VoiceTutorSessionDto>('/instructor-runtime/tutor/voice-session', {
    LearningSessionStepId: 'learningSessionStepId' in target ? target.learningSessionStepId : undefined,
    CurrentPromptText: 'learningSessionStepId' in target ? target.promptText : undefined,
    CapabilityGraphNodeId: 'capabilityGraphNodeId' in target ? target.capabilityGraphNodeId : undefined,
    StepType: 'capabilityGraphNodeId' in target ? target.stepType : undefined,
  });
}

// === Adaptive Assessment ===

export type AssessmentRoundStatus = 'InProgress' | 'Passed' | 'Failed';
export type AssessmentQuestionCorrectness = 'Correct' | 'PartiallyCorrect' | 'Incorrect';

export interface AssessmentQuestionDto {
  AssessmentQuestionId: string;
  QuestionIndex: number;
  QuestionType: string;
  QuestionText: string;
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

export function getActiveAssessmentRound(learningSessionNodeId: string): Promise<AssessmentRoundStateDto | null> {
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

/**
 * Fixed test Person used to preview the student experience from inside
 * Capability Studio (there's no real student auth here) — same seeded
 * dev Person as humanlearn/src/components/layout/AppShell.tsx's
 * MOCK_USER, required because runtime endpoints reject Guid.Empty and
 * LearningSessions has a real FK to Person.
 */
export const PREVIEW_PERSON_ID = '22e52050-2b03-475f-93b9-880b81e50663';

// === Blueprint review (Studio "Demo"/"Edición" preview modes) ===
// Reads/edits a node's Memory Paradox blueprint DIRECTLY, bypassing any
// LearningSession/progression gating — powers Studio's free step-jump
// (Demo) and prompt-based content editing (Edición) review tools. Never
// touches a real student's LearningSession/progress.

export interface BlueprintIllustrationDto {
  IllustrationId: string;
  StoragePath: string;
  Caption?: string;
}

export interface BlueprintStepDto {
  StepType: ExperienceStepType;
  Content: string;
  Illustrations: BlueprintIllustrationDto[];
}

export interface NodeBlueprintDto {
  CapabilityGraphNodeId: string;
  NodeExperienceBlueprintId: string;
  Steps: BlueprintStepDto[];
}

export function getNodeBlueprint(capabilityGraphNodeId: string): Promise<NodeBlueprintDto> {
  return apiGet<NodeBlueprintDto>(`/studio/nodes/${capabilityGraphNodeId}/blueprint`);
}

export function editNodeBlueprintStep(
  capabilityGraphNodeId: string,
  stepType: ExperienceStepType,
  instruction: string
): Promise<BlueprintStepDto> {
  return apiPost<BlueprintStepDto>(`/studio/nodes/${capabilityGraphNodeId}/blueprint/steps/${stepType}/edit`, {
    Instruction: instruction,
  });
}
