import { apiGet, apiPost } from './httpClient';
import { apiClient } from '@/api';

/**
 * Client for the Runtime V1 "instructor-runtime" HTTP API — drives a
 * node's 5-step Memory Paradox experience (Hypothesis/Teaching/Recall/
 * Production/Assessment). DTO shapes below mirror
 * backend/HumanOS/AzureFunctions/Api/RuntimeGraphApiModels.cs and
 * TutorApiModels.cs's C# property names (PascalCase), but the ACTUAL wire
 * format is camelCase — every response goes through
 * `FunctionResponseFactory`'s global `JsonSerializerDefaults.Web` policy
 * (confirmed 2026-07-24: e.g. `{"learningSessionId":...}`, not
 * `{"LearningSessionId":...}`). Rather than rewrite every DTO/consumer in
 * this file to camelCase, `rGet`/`rPost` (below) recursively convert the
 * incoming camelCase keys to PascalCase before returning — same technique
 * capabilitystudio/src/lib/api/httpClient.ts uses app-wide. This is scoped
 * to THIS file only (not the shared httpClient.ts) because
 * capabilityGraphApi.ts/capabilitiesApi.ts/subjectsApi.ts in this app
 * already use correctly-typed camelCase DTOs against the same shared
 * httpClient — converting there too would break them.
 *
 * Per the backend file's own doc comment, the UI never treats
 * LearningSessionId/NodeId/StepId as its own source of truth — they're
 * just opaque IDs passed straight through to the next call.
 */

/** Recursively converts every camelCase object key to PascalCase (first
 * letter uppercased, rest untouched) so the backend's camelCase JSON
 * matches this file's PascalCase DTO types. Arrays/nested objects are
 * walked; primitives (including date strings) pass through untouched. */
function toPascalCaseDeep(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(toPascalCaseDeep);
  }
  if (value !== null && typeof value === 'object') {
    const result: Record<string, unknown> = {};
    for (const [key, val] of Object.entries(value as Record<string, unknown>)) {
      const pascalKey = key.charAt(0).toUpperCase() + key.slice(1);
      result[pascalKey] = toPascalCaseDeep(val);
    }
    return result;
  }
  return value;
}

async function rGet<T>(path: string): Promise<T> {
  const data = await apiGet<unknown>(path);
  return toPascalCaseDeep(data) as T;
}

async function rPost<T>(path: string, body?: unknown): Promise<T> {
  const data = await apiPost<unknown>(path, body);
  return toPascalCaseDeep(data) as T;
}

export type ExperienceStepType = 'Hypothesis' | 'Teaching' | 'DeepDive' | 'Recall' | 'Production' | 'Assessment';

export interface ExampleCardDto {
  Title: string;
  Text: string;
  Emoji?: string;
  IllustrationId?: string | null;
}

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
  /** Only populated when StepType === 'DeepDive'. */
  ExampleCards?: ExampleCardDto[];
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

export function getActiveSession(
  personId: string,
  capabilityId: string,
  programId?: string | null
): Promise<BackendRuntimeSessionInfo | null> {
  const programParam = programId ? `&programId=${programId}` : '';
  return rGet<BackendRuntimeSessionInfo | null>(
    `/instructor-runtime/sessions/active?personId=${personId}&capabilityId=${capabilityId}${programParam}`
  );
}

export function startSession(
  personId: string,
  capabilityId: string,
  capabilityGraphNodeId: string,
  programId?: string | null
): Promise<BackendRuntimeSessionInfo> {
  return rPost<BackendRuntimeSessionInfo>('/instructor-runtime/sessions/start', {
    PersonId: personId,
    CapabilityId: capabilityId,
    CapabilityGraphNodeId: capabilityGraphNodeId,
    ProgramId: programId ?? null,
  });
}

export function submitStepResponse(
  learningSessionStepId: string,
  response: string
): Promise<{ success: boolean; learningEvidenceId: string }> {
  return rPost('/instructor-runtime/steps/respond', {
    LearningSessionStepId: learningSessionStepId,
    Response: response,
  });
}

export function advanceStep(learningSessionNodeId: string): Promise<BackendRuntimeStep> {
  return rPost<BackendRuntimeStep>('/instructor-runtime/steps/advance', {
    LearningSessionNodeId: learningSessionNodeId,
  });
}

// === "Aprobar (demo HR)" (2026-08-02, ported from human-os-employee) —
// HRAdmin-only, real per-caller server-side check (see
// HrForceAdvanceStepFunction.cs). Lets an HRAdmin force-complete the
// currently Active gated step (DeepDive/Recall/Production/Assessment) of
// their own or an employee's session, to demo/advance a course live
// without waiting for real mastery. The UI only renders this for
// user.orgRole === 'HRAdmin' (NodeWorkflowPage.tsx), and the backend
// independently re-checks the SAME thing from the real signed-in
// identity, not just the UI's say-so. ===

export interface ForceAdvanceStepResponseDto {
  NextStep?: BackendRuntimeStep | null;
  NodeCompleted: boolean;
}

// This ONE call deliberately bypasses rPost/apiPost (the bare fetch
// wrapper this file otherwise uses, which sends NO auth headers at all)
// — HrForceAdvanceStepFunction.cs re-verifies the REAL signed-in identity
// server-side via the X-Azure-OID/X-Azure-TID headers, so it must go
// through the shared axios `apiClient` (see src/api/index.ts), whose
// interceptor actually attaches those headers from the live MSAL session.
export async function forceAdvanceStep(learningSessionNodeId: string): Promise<ForceAdvanceStepResponseDto> {
  const { data } = await apiClient.post(`/instructor-runtime/nodes/${learningSessionNodeId}/hr-force-advance`);
  return toPascalCaseDeep(data) as ForceAdvanceStepResponseDto;
}

export interface TutorTurnDto {
  Message: string;
  RecallScore?: number;
  Illustrations: BackendIllustration[];
}

export type TutorMode = 'Teaching' | 'Production' | 'AssessmentFeedback';

// === Voice Tutor (Azure OpenAI Realtime, ported from Capability Studio
// 2026-07-22) === Mints a short-lived ephemeral session so the BROWSER can
// negotiate WebRTC directly against Azure (see VoiceTutorAgent.tsx) — this
// backend call never carries audio itself, only the one-time token. Scope:
// Hypothesis/Teaching steps only in the real student app (see
// VoiceTutorAgent.tsx's own doc comment for why Recall never gets a voice
// agent for an actual student).

export interface VoiceTutorSessionDto {
  ClientSecret: string;
  RealtimeCallsUrl: string;
  Model: string;
  Voice: string;
  ExpiresAtUnixSeconds?: number | null;
  /** Only populated by requestEngramReviewSession — this node's already-generated illustrations. */
  Illustrations?: { Id: string; Caption?: string | null }[] | null;
}

export function requestVoiceTutorSession(
  target: { learningSessionStepId: string; promptText?: string; exampleCardIndex?: number }
): Promise<VoiceTutorSessionDto> {
  return rPost<VoiceTutorSessionDto>('/instructor-runtime/tutor/voice-session', {
    LearningSessionStepId: target.learningSessionStepId,
    CurrentPromptText: target.promptText,
    ExampleCardIndex: target.exampleCardIndex,
  });
}

// === "Reactivar Engrama" (2026-07-24) === A second, distinct Agent
// presence shown alongside "Practicar de nuevo" on an already-Mastered
// node's summary screen — a full-lesson active-recall STUDY conversation
// (never an exam/quiz, never grades anything), grounded in the student's
// own most recent completed attempt (blueprint content + their own past
// written answers). Reuses the exact same ephemeral-session response shape
// as the Voice Tutor (VoiceTutorSessionDto) since it's the same kind of
// Azure OpenAI Realtime session, just with a different system prompt.
export function requestEngramReviewSession(
  target: { personId: string; capabilityGraphNodeId: string; exampleCardIndex?: number }
): Promise<VoiceTutorSessionDto> {
  return rPost<VoiceTutorSessionDto>('/instructor-runtime/tutor/engram-review-session', {
    PersonId: target.personId,
    CapabilityGraphNodeId: target.capabilityGraphNodeId,
    ExampleCardIndex: target.exampleCardIndex,
  });
}

// On-demand image generation (2026-07-24) for the show_visual_aid tool's
// kind="generated_image" — used when the student asks to SEE a picture of
// something that isn't one of the lesson's existing illustrations. Never
// persisted server-side; just base64 bytes for this one ephemeral overlay.
export interface GeneratedVisualAidImageDto {
  ImageBase64: string;
  ContentType: string;
}

export function requestGeneratedVisualAidImage(prompt: string): Promise<GeneratedVisualAidImageDto> {
  return rPost<GeneratedVisualAidImageDto>('/instructor-runtime/tutor/visual-aid/generate-image', {
    Prompt: prompt,
  });
}

export function askTutor(
  learningSessionStepId: string,
  mode: TutorMode,
  studentMessage: string,
  rawAssessmentFeedback?: string
): Promise<TutorTurnDto> {
  return rPost<TutorTurnDto>('/instructor-runtime/tutor/ask', {
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
  return rPost<RecallAttemptOutcomeDto>('/instructor-runtime/tutor/recall-attempts', {
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
  return rPost<DeepDiveAttemptOutcomeDto>('/instructor-runtime/tutor/deep-dive-attempts', {
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
  /** Only populated when StepType === 'DeepDive'. */
  ExampleCards?: ExampleCardDto[];
}

export function getStepReview(
  learningSessionNodeId: string,
  stepType: ExperienceStepType
): Promise<StepReviewDto> {
  return rGet<StepReviewDto>(
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
  return rPost<KnowledgeExpansionDto>(`/capability-graph-nodes/${capabilityGraphNodeId}/knowledge-expansion`, {});
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
  return rGet<NodeSummaryDto>(
    `/instructor-runtime/nodes/summary?personId=${personId}&capabilityGraphNodeId=${capabilityGraphNodeId}`
  );
}

export interface AssessmentResultDto {
  Score: number;
  Passed: boolean;
  Feedback: string;
}

export function evaluateAssessment(learningSessionNodeId: string): Promise<AssessmentResultDto> {
  return rPost<AssessmentResultDto>('/instructor-runtime/nodes/evaluate', {
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
  return rPost<EvaluateProductionResponseDto>('/instructor-runtime/production/evaluate', {
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
  return rPost('/instructor-runtime/nodes/complete', {
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
  return rGet<AssessmentRoundStateDto | null>(
    `/instructor-runtime/assessment/active?learningSessionNodeId=${learningSessionNodeId}`
  );
}

export function startAssessmentRound(learningSessionNodeId: string): Promise<AssessmentRoundStateDto> {
  return rPost<AssessmentRoundStateDto>('/instructor-runtime/assessment/start-round', {
    LearningSessionNodeId: learningSessionNodeId,
  });
}

export function submitAssessmentAnswer(
  assessmentQuestionId: string,
  studentAnswer: string
): Promise<SubmitAssessmentAnswerResponse> {
  return rPost<SubmitAssessmentAnswerResponse>('/instructor-runtime/assessment/answer', {
    AssessmentQuestionId: assessmentQuestionId,
    StudentAnswer: studentAnswer,
  });
}
