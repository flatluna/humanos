import { apiGet, apiPost, apiPut, apiDelete } from './httpClient';

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
  /** 0-based index of the source document chapter/module this node belongs
   * to (2026-08-04) — what groupIntoLevels now groups nodes by. Null/undefined
   * for nodes whose chapter couldn't be traced, or capabilities created
   * before this field existed — falls back to edge-tier grouping. */
  ChapterIndex?: number | null;
}

export type BackendRelationshipType = 0 | 1;

export interface BackendCapabilityGraphEdge {
  SourceNodeId: string;
  TargetNodeId: string;
  RelationshipType: BackendRelationshipType;
}

/** Professor-given name for one computed "climb level"/tier (2026-07-30) —
 * see PreviewGraphPage.tsx's groupIntoLevels. Only present for levels the
 * professor has explicitly named; any level missing here falls back to the
 * generic "Level N" label. */
export interface BackendCapabilityGraphLevel {
  LevelIndex: number;
  Name: string;
}

export interface BackendCapabilityGraph {
  CapabilityGraphId: string;
  Name: string;
  Description?: string;
  Nodes: BackendCapabilityGraphNode[];
  Edges: BackendCapabilityGraphEdge[];
  Levels?: BackendCapabilityGraphLevel[];
  ExecutiveSummary?: string;
}

/** Names (or renames, or clears when name is empty) one computed graph
 * level for the given capability — Studio's "professor" tool. */
export function setCapabilityGraphLevelName(
  capabilityId: string,
  levelIndex: number,
  name: string
): Promise<{ LevelIndex: number; Name: string | null }> {
  return apiPut<{ LevelIndex: number; Name: string | null }>(
    `/capabilities/${capabilityId}/graph/levels/${levelIndex}`,
    { Name: name }
  );
}

export function getCapabilityGraph(capabilityId: string, personId: string): Promise<BackendCapabilityGraph> {
  return apiGet<BackendCapabilityGraph>(`/capabilities/${capabilityId}/graph?personId=${personId}`);
}

// === Studio "professor" tool: add a node to an existing graph ===

export interface AddGraphNodeRequest {
  Prompt: string;
  RequiresNodeIds: string[];
  RequiredByNodeIds: string[];
  /** "None" | "AiGenerated" (default) | "UploadDirect" | "AiGeneratedFromUpload" —
   * see AddGraphNodeModal.tsx for the professor-facing meaning of each. */
  IllustrationMode: string;
  /** Base64-encoded image (no data: URL prefix), required when
   * IllustrationMode is "UploadDirect" or "AiGeneratedFromUpload". */
  UploadedImageBase64?: string | null;
  UploadedImageContentType?: string | null;
  /** 0-based ChapterIndex to place the new node in (2026-08-06 fix for nodes
   * silently landing at the end of chapter-graded graphs) — null for graphs
   * still using the older edge-depth tiering. See AddGraphNodeModal.tsx. */
  TargetChapterIndex?: number | null;
}

export interface AddGraphNodeResponse {
  CapabilityGraphNodeId: string;
  Name: string;
  Description?: string;
  SortOrder: number;
  IllustrationsGenerated: number;
}

export function addCapabilityGraphNode(
  capabilityId: string,
  request: AddGraphNodeRequest
): Promise<AddGraphNodeResponse> {
  return apiPost<AddGraphNodeResponse>(`/capabilities/${capabilityId}/graph/nodes`, request);
}

/**
 * Permanently deletes ONE node from a capability's graph (edges,
 * illustrations, blueprint(s), knowledge cache, per-node progress — see
 * CapabilityGraphPersistenceService.DeleteNodeAsync). Irreversible — the
 * caller must have already confirmed with the user (see
 * DeleteGraphNodeModal).
 */
export function deleteCapabilityGraphNode(capabilityId: string, nodeId: string): Promise<void> {
  return apiDelete(`/capabilities/${capabilityId}/graph/nodes/${nodeId}`);
}

// === Runtime session (5-step Memory Paradox) ===

export type ExperienceStepType = 'Hypothesis' | 'Teaching' | 'DeepDive' | 'Recall' | 'Production' | 'Assessment';

export interface ExampleCardDto {
  Title: string;
  Text: string;
  Emoji?: string | null;
  IllustrationId?: string | null;
}

export interface BackendIllustration {
  IllustrationId: string;
  StoragePath: string;
  Caption?: string;
  /** Raw generation-model tag ("deterministic-skiasharp-v1" or a generative image model name like "gpt-image-2"). */
  ImageModel: string;
  /** Human-readable provenance label for a small caption in the UI. */
  GeneratedByLabel: string;
}

export interface BackendRuntimeStep {
  LearningSessionStepId: string;
  StepType: ExperienceStepType;
  Content: string;
  Illustrations: BackendIllustration[];
  /** Only populated when StepType === 'DeepDive'. */
  ExampleCards?: ExampleCardDto[] | null;
  /** Only populated when StepType === 'DeepDive' — index (0-based) of the card currently being asked about. */
  CurrentDeepDiveCardIndex?: number;
  /** Only populated when StepType === 'DeepDive' — total number of cards that must be sequentially mastered. */
  DeepDiveTotalCards?: number;
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

/**
 * Deep Dive is card-by-card SEQUENTIAL and MANDATORY-ALL — unlike Recall's
 * "master N distinct items out of an open pool", every single example
 * card must be mastered in order before the step can advance (see
 * backend/HumanOS/Agentic/Runtime/DeepDiveLoopGate.cs). There is no
 * attempt cap and no regression path: the student simply keeps retrying
 * the SAME card (CardIndex) until Mastered flips true, at which point
 * NextCardIndex increments and the loop continues until every card is done.
 */
export interface DeepDiveAttemptOutcomeDto {
  TutorTurn: TutorTurnDto;
  LearningEvidenceId: string;
  /** 0-based index of the card this attempt was scored against. */
  CardIndex: number;
  TotalCards: number;
  /** True if THIS attempt scored high enough to master the current card. */
  Mastered: boolean;
  /** Index of the card to ask about next (unchanged from CardIndex if not yet mastered). */
  NextCardIndex: number;
  /** True only when every card has been mastered and the step advanced to Recall. */
  Advanced: boolean;
  NextStep?: BackendRuntimeStep | null;
}

export function submitDeepDiveAttempt(
  learningSessionStepId: string,
  studentResponse: string,
  tutorPromptShown?: string
): Promise<DeepDiveAttemptOutcomeDto> {
  return apiPost<DeepDiveAttemptOutcomeDto>('/instructor-runtime/tutor/deep-dive-attempts', {
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
): Promise<{ Success: boolean; NewlyUnlockedNodes: GraphNodeInfoDto[] }> {
  return apiPost('/instructor-runtime/nodes/complete', {
    LearningSessionNodeId: learningSessionNodeId,
  });
}

// === Dev/demo-only "Aprobar (temporal)" (2026-08-02) — Capability Studio
// ONLY, see previewMode.ts's useAutoApprove and
// StudioForceAdvanceStepFunction.cs. Force-completes whatever gated step
// (DeepDive/Recall/Production/Assessment) is currently Active, WITHOUT
// requiring a real mastered answer — purely for demo/pilot walkthroughs.
// The backend endpoint itself is also gated behind AllowStudioAutoApprove;
// this call 403s in any environment where that isn't explicitly enabled. ===

export interface ForceAdvanceStepResponseDto {
  NextStep?: BackendRuntimeStep | null;
  NodeCompleted: boolean;
}

export function forceAdvanceStep(learningSessionNodeId: string): Promise<ForceAdvanceStepResponseDto> {
  return apiPost<ForceAdvanceStepResponseDto>(`/studio/runtime/nodes/${learningSessionNodeId}/force-advance`);
}

// === Read-only node/step review (2026-07-30) — ported from
// frontend/human-os-web/src/features/capabilities/api/runtimeSessionApi.ts
// so Capability Studio's "Probar como estudiante" (real mode) shows the
// same read-only recap when reopening an already-Mastered node, instead of
// silently starting a brand-new attempt. Never mutates anything — pure GETs. ===

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
  /** Only populated when StepType === 'DeepDive'. */
  ExampleCards?: ExampleCardDto[];
}

export function getStepReview(learningSessionNodeId: string, stepType: ExperienceStepType): Promise<StepReviewDto> {
  return apiGet<StepReviewDto>(
    `/instructor-runtime/steps/review?learningSessionNodeId=${learningSessionNodeId}&stepType=${stepType}`
  );
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
 * of silently starting a brand-new attempt.
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
    | { learningSessionStepId: string; promptText?: string; personName?: string; personId?: string; exampleCardIndex?: number }
    | {
        capabilityGraphNodeId: string;
        stepType: ExperienceStepType;
        personName?: string;
        personId?: string;
        exampleCardIndex?: number;
        /** DeepDive only (2026-07-30) — revisiting an already-mastered card
         * via the read-only card index. Switches the voice Agent to a
         * plain explain-and-invite-questions conversation, never a quiz. */
        isReviewOnly?: boolean;
      }
    | {
        /** Traditional Course path (2026-08-09) — a single CapabilityLesson
         * (a "subchapter", NOT its whole parent chapter — matches Graph
         * mode's per-node granularity). See
         * VoiceTutorSessionFunction.RunTraditionalLessonAsync. */
        traditionalCourseLessonId: string;
        personName?: string;
        personId?: string;
      }
    | {
        /** Structured Learning path (2026-08-10) — a single
         * StructuredLearningSectionStep id (Hypothesis/Teaching/DeepDive).
         * See VoiceTutorSessionFunction.RunStructuredLearningStepAsync. */
        structuredLearningSectionStepId: string;
        personName?: string;
        personId?: string;
        /** DeepDive only — which ExampleCard (see StepDataJson) to quiz on. */
        exampleCardIndex?: number;
        /** DeepDive only — read-only card review, no grading tool offered. */
        isReviewOnly?: boolean;
        /** When true, configures the Voice Tutor in Q&A-only mode to answer questions about the lecture. */
        isQaMode?: boolean;
      }
): Promise<VoiceTutorSessionDto> {
  return apiPost<VoiceTutorSessionDto>('/instructor-runtime/tutor/voice-session', {
    LearningSessionStepId: 'learningSessionStepId' in target ? target.learningSessionStepId : undefined,
    CurrentPromptText: 'learningSessionStepId' in target ? target.promptText : undefined,
    CapabilityGraphNodeId: 'capabilityGraphNodeId' in target ? target.capabilityGraphNodeId : undefined,
    StepType: 'capabilityGraphNodeId' in target ? target.stepType : undefined,
    TraditionalCourseLessonId: 'traditionalCourseLessonId' in target ? target.traditionalCourseLessonId : undefined,
    StructuredLearningSectionStepId: 'structuredLearningSectionStepId' in target ? target.structuredLearningSectionStepId : undefined,
    PersonId: 'personId' in target ? target.personId : undefined,
    PersonName: target.personName,
    ExampleCardIndex: 'exampleCardIndex' in target ? target.exampleCardIndex : undefined,
    IsReviewOnly:
      'capabilityGraphNodeId' in target
        ? target.isReviewOnly ?? false
        : 'structuredLearningSectionStepId' in target
        ? target.isReviewOnly ?? false
        : false,
    IsQaMode: 'isQaMode' in target ? target.isQaMode ?? false : false,
  });
}

// === "Reactivar Engrama" / voice lesson review (2026-07-30, ported from
// frontend/human-os-web's EngramReviewAgent.tsx) === A full-lesson, spoken
// STUDY conversation for an already-Mastered node's read-only recap screen
// — deliberately NEVER an exam/quiz, never grades anything. The Agent
// reads/explains the lesson back to the student (grounded in the
// blueprint content + the student's own past answers, via the exact same
// InstructorRuntimeOrchestrator.GetNodeSummaryAsync data as the recap
// screen itself) and invites questions. Reuses the same ephemeral-session
// response shape as the regular Voice Tutor (VoiceTutorSessionDto), just
// with a different system prompt built server-side.
export function requestEngramReviewSession(
  personId: string,
  capabilityGraphNodeId: string
): Promise<VoiceTutorSessionDto> {
  return apiPost<VoiceTutorSessionDto>('/instructor-runtime/tutor/engram-review-session', {
    PersonId: personId,
    CapabilityGraphNodeId: capabilityGraphNodeId,
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
  /** Raw generation-model tag ("deterministic-skiasharp-v1" or a generative image model name like "gpt-image-2"). */
  ImageModel: string;
  /** Human-readable provenance label for a small caption in the UI, e.g. "Generado con .NET (determinístico)". */
  GeneratedByLabel: string;
}

export interface BlueprintStepDto {
  StepType: ExperienceStepType;
  Content: string;
  Illustrations: BlueprintIllustrationDto[];
  /** Only populated when StepType === 'DeepDive'. */
  ExampleCards?: ExampleCardDto[] | null;
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

// === Deep Dive card-by-card editing (2026-07-30) ===
// The generic editNodeBlueprintStep above only ever rewrites a step's plain
// Content HTML — it has no concept of ExampleCards at all, so using it on
// the DeepDive step never actually edits a specific card. These two calls
// target ONE card (or append a brand-new one) without ever touching any of
// the step's other cards.

export function editDeepDiveCard(
  capabilityGraphNodeId: string,
  cardIndex: number,
  instruction: string
): Promise<BlueprintStepDto> {
  return apiPost<BlueprintStepDto>(
    `/studio/nodes/${capabilityGraphNodeId}/blueprint/deepdive/cards/${cardIndex}/edit`,
    { Instruction: instruction }
  );
}

export function addDeepDiveCard(capabilityGraphNodeId: string, instruction: string): Promise<BlueprintStepDto> {
  return apiPost<BlueprintStepDto>(`/studio/nodes/${capabilityGraphNodeId}/blueprint/deepdive/cards/add`, {
    Instruction: instruction,
  });
}

export function deleteDeepDiveCard(capabilityGraphNodeId: string, cardIndex: number): Promise<BlueprintStepDto> {
  return apiDelete<BlueprintStepDto>(`/studio/nodes/${capabilityGraphNodeId}/blueprint/deepdive/cards/${cardIndex}`);
}
