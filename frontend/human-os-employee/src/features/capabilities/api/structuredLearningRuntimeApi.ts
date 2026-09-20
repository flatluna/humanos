import { apiGet, apiPost } from './httpClient';
import type { GamificationRewardBundleDto } from '../../gamification/types';

/**
 * Real per-student Structured Learning runtime client —
 * mirrors capabilitystudio/src/lib/api/structuredLearningApi.ts's DTOs,
 * appending the real signed-in student's personId as a query param
 * so answers and progress are saved under their own identity.
 */

function toPascalCaseDeep(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(toPascalCaseDeep);
  }
  if (value !== null && typeof value === 'object') {
    const result: Record<string, unknown> = {};
    for (const [key, val] of Object.entries(value as Record<string, unknown>)) {
      const pascalKey = key.length > 0 ? key[0].toUpperCase() + key.slice(1) : key;
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

export interface StructuredLearningStepDetail {
  StructuredLearningSectionStepId: string;
  StepType: string;
  SortOrder: number;
  Content: string;
  StepDataJson: string | null;
  TimelineJson?: string | null;
  VideoWizardStateJson?: string | null;
}

export interface StructuredLearningSectionVideo {
  IllustrationId: string;
  Purpose: string;
  CreatedDate: string;
  Prompt: string;
  ImageModel: string;
  IsPublished: boolean;
}

export function listStructuredLearningSectionVideos(sectionId: string): Promise<StructuredLearningSectionVideo[]> {
  return rGet<StructuredLearningSectionVideo[]>(`/studio/structured-learning/sections/${sectionId}/videos`);
}

export interface StructuredLearningSectionDetail {
  StructuredLearningDocumentSectionId: string;
  SortOrder: number;
  ChapterTitle: string;
  SubchapterTitle: string;
  Text?: string;
  KnowledgeGraphRootEntityName?: string | null;
  Steps: StructuredLearningStepDetail[];
}

export interface StructuredLearningDocumentDetail {
  StructuredLearningDocumentId: string;
  DocumentName: string;
  ExecutiveSummary?: string | null;
  AssessmentQuestionType: string;
  Sections: StructuredLearningSectionDetail[];
}

export interface StructuredLearningDocumentSummary {
  structuredLearningDocumentId: string;
  documentName: string;
  executiveSummary?: string | null;
  sectionCount: number;
  createdDate: string;
  coverIllustrationId?: string | null;
  category?: string | null;
  targetSkill?: string | null;
}

export function getStructuredLearningDocuments(): Promise<StructuredLearningDocumentSummary[]> {
  return apiGet<StructuredLearningDocumentSummary[]>('/catalog/structured-learning');
}

export interface StructuredLearningSectionSummaryDto {
  StructuredLearningDocumentSectionId: string;
  StructuredLearningDocumentId: string;
  DocumentName?: string | null;
  SectionTitle?: string | null;
  ChapterTitle?: string | null;
  Capability?: string | null;
  AttemptCount: number;
  FirstCompletedDate?: string | null;
  LastCompletedDate?: string | null;
  FinalScore?: number | null;
  Memory?: {
    RecallPercent: number;
    MemoryState: 'Strong' | 'Fading' | 'AlmostForgotten' | string;
    ReviewCount: number;
    LastReviewDate?: string | null;
    IsAutomatic: boolean;
    ReviewsUntilAutomatic: number;
    NextReviewDate?: string | null;
  } | null;
  Reward?: {
    PointsAwarded: number;
    WasSurprise: boolean;
    SurpriseType: 'NONE' | 'BONUS' | 'DOUBLE' | 'JACKPOT' | string;
    PointsBase: number;
    DifficultyMultiplier: number;
    MemoryEffortMultiplier: number;
    SurpriseMultiplier: number;
    EarnedAt?: string | null;
  } | null;
  Impact?: {
    Score: number;
    Tier: 'Critical' | 'Important' | 'Foundational' | 'Refresher' | string;
    PrimarySignal?: string | null;
    Reason?: string | null;
  } | null;
}

export function getStructuredLearningSectionSummary(
  sectionId: string,
  personId?: string
): Promise<StructuredLearningSectionSummaryDto> {
  const query = personId ? `?personId=${encodeURIComponent(personId)}` : '';
  return rGet<StructuredLearningSectionSummaryDto>(
    `/studio/structured-learning/sections/${sectionId}/summary${query}`
  );
}

export function getStructuredLearningDocument(documentId: string): Promise<StructuredLearningDocumentDetail> {
  return rGet<StructuredLearningDocumentDetail>(`/studio/structured-learning/${documentId}`);
}

// ==================== Hypothesis pre-check answers ====================

export interface StructuredLearningHypothesisResponseDto {
  QuestionIndex: number;
  SelectedOptionIndex: number;
  IsCorrect: boolean | null;
}

export function getStructuredLearningHypothesisResponses(
  sectionId: string,
  personId: string
): Promise<StructuredLearningHypothesisResponseDto[]> {
  return rGet<StructuredLearningHypothesisResponseDto[]>(
    `/studio/structured-learning/sections/${sectionId}/hypothesis/responses?personId=${personId}`
  );
}

export function submitStructuredLearningHypothesisResponses(
  sectionId: string,
  personId: string,
  answers: { questionIndex: number; selectedOptionIndex: number }[]
): Promise<StructuredLearningHypothesisResponseDto[]> {
  return rPost<StructuredLearningHypothesisResponseDto[]>(
    `/studio/structured-learning/sections/${sectionId}/hypothesis/responses?personId=${personId}`,
    { answers: answers.map((a) => ({ QuestionIndex: a.questionIndex, SelectedOptionIndex: a.selectedOptionIndex })) }
  );
}

// ==================== Real-time Recall ====================

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

export function getStructuredLearningRecallState(
  sectionId: string,
  personId: string
): Promise<SubmitStructuredLearningRecallAttemptResult> {
  return rGet<SubmitStructuredLearningRecallAttemptResult>(
    `/studio/structured-learning/sections/${sectionId}/recall/state?personId=${personId}`
  );
}

export function submitStructuredLearningRecallAttempt(
  sectionId: string,
  personId: string,
  studentResponse?: string
): Promise<SubmitStructuredLearningRecallAttemptResult> {
  return rPost<SubmitStructuredLearningRecallAttemptResult>(
    `/studio/structured-learning/sections/${sectionId}/recall/attempt?personId=${personId}`,
    { studentResponse: studentResponse ?? null }
  );
}

// ==================== Real-time Deep Dive ====================

export interface StructuredLearningDeepDiveStateResult {
  NextQuestion: string;
  CardIndex: number;
  TotalCards: number;
  Complete: boolean;
}

export function getStructuredLearningDeepDiveState(sectionId: string): Promise<StructuredLearningDeepDiveStateResult> {
  return rGet<StructuredLearningDeepDiveStateResult>(`/studio/structured-learning/sections/${sectionId}/deepdive/state`);
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
  return rPost<SubmitStructuredLearningDeepDiveAttemptResult>(`/studio/structured-learning/sections/${sectionId}/deepdive/attempt`, {
    studentResponse,
    tutorPromptShown: tutorPromptShown ?? null,
  });
}

// ==================== Real-time Assessment ====================

export interface StructuredLearningAssessmentQuestion {
  StructuredLearningAssessmentQuestionAttemptId: string;
  QuestionIndex: number;
  QuestionText: string;
  Options?: string[] | null;
}

export interface StructuredLearningAssessmentRoundState {
  StructuredLearningAssessmentRoundId: string;
  RoundNumber: number;
  Status: 'InProgress' | 'Passed' | 'Failed';
  FinalScore: number | null;
  TotalQuestions: number;
  /** Set only once the round is Passed/Failed. */
  CompletedDate?: string | null;
  CurrentQuestion: StructuredLearningAssessmentQuestion | null;
}

export function startStructuredLearningAssessmentRound(
  sectionId: string,
  personId: string
): Promise<StructuredLearningAssessmentRoundState> {
  return rPost<StructuredLearningAssessmentRoundState>(
    `/studio/structured-learning/sections/${sectionId}/assessment/start-round?personId=${personId}`
  );
}

export function getActiveStructuredLearningAssessmentRound(
  sectionId: string,
  personId: string
): Promise<StructuredLearningAssessmentRoundState | null> {
  return rGet<StructuredLearningAssessmentRoundState | null>(
    `/studio/structured-learning/sections/${sectionId}/assessment/active-round?personId=${personId}`
  );
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
  Gamification?: GamificationRewardBundleDto | null;
  gamification?: GamificationRewardBundleDto | null;
}

export function submitStructuredLearningAssessmentAnswer(
  questionAttemptId: string,
  studentAnswer: string
): Promise<SubmitStructuredLearningAssessmentAnswerResult> {
  return rPost<SubmitStructuredLearningAssessmentAnswerResult>(
    `/studio/structured-learning/assessment/questions/${questionAttemptId}/answer`,
    { studentAnswer }
  );
}

// ==================== Knowledge Graph & Entities ====================

export interface StructuredLearningEntityOccurrence {
  EngramId: string;
  SourceText: string;
}

export interface StructuredLearningDocumentEntity {
  EntityId: string;
  CanonicalName: string;
  EntityType: string;
  Description: string | null;
  OntologicalClass: string | null;
  Aliases: string[];
  Occurrences: StructuredLearningEntityOccurrence[];
}

export function getStructuredLearningDocumentEntities(documentId: string): Promise<StructuredLearningDocumentEntity[]> {
  return rGet<StructuredLearningDocumentEntity[]>(`/studio/structured-learning/${documentId}/entities`);
}

export type StructuredLearningConnectionCategory = 'Sequence' | 'Requires' | 'PartOf' | 'IsRoleOf' | 'Generic';

export interface StructuredLearningConnection {
  ConnectionId: string;
  SourceEntityId: string;
  SourceEntityName: string;
  TargetEntityId: string;
  TargetEntityName: string;
  RelationshipCategory: StructuredLearningConnectionCategory;
  RelationshipLabel: string;
  Evidence: string | null;
  SourceSectionId: string | null;
  EpistemicStatus: 'explicit' | 'inferred' | 'referenced' | 'attributed' | 'hypothetical';
  Confidence: number | null;
}

export function getStructuredLearningDocumentConnections(documentId: string): Promise<StructuredLearningConnection[]> {
  return rGet<StructuredLearningConnection[]>(`/studio/structured-learning/${documentId}/connections`);
}

export interface StructuredLearningKnowledgeFactPath {
  ConnectionId: string;
  StepOrder: number;
  SourceEntityId: string;
  SourceEntityName: string;
  RelationshipCategory: string;
  RelationshipLabel: string;
  TargetEntityId: string;
  TargetEntityName: string;
  Evidence: string | null;
  EpistemicStatus: string;
  Confidence: number | null;
}

export interface StructuredLearningKnowledgeFact {
  KnowledgeFactId: string;
  Statement: string;
  FactType: string;
  Importance: string;
  DerivationType: string;
  EpistemicStatus: string;
  Confidence: number;
  GraphPath: StructuredLearningKnowledgeFactPath[];
}

export interface StructuredLearningKnowledgeFactsResponse {
  EngramId: string;
  TotalFacts: number;
  CoreFacts: number;
  SupportingFacts: number;
  AtomicFacts: number;
  CompositeFacts: number;
  KnowledgeFacts: StructuredLearningKnowledgeFact[];
}

export function getStructuredLearningKnowledgeFacts(sectionId: string): Promise<StructuredLearningKnowledgeFactsResponse> {
  return rGet<StructuredLearningKnowledgeFactsResponse>(`/studio/structured-learning/sections/${sectionId}/knowledge-facts`);
}

export interface GenerateStructuredLearningKnowledgeFactsResponse {
  EngramId: string;
  KnowledgeFactsCreated: number;
  MissingGraphFactsCount: number;
}

export function generateStructuredLearningKnowledgeFacts(sectionId: string): Promise<GenerateStructuredLearningKnowledgeFactsResponse> {
  return rPost<GenerateStructuredLearningKnowledgeFactsResponse>(`/studio/structured-learning/sections/${sectionId}/knowledge-facts/generate`, {});
}

export interface GenerateStructuredLearningConnectionsResponse {
  SectionId: string;
  ConnectionsCreated: number;
}

export function generateStructuredLearningConnections(sectionId: string): Promise<GenerateStructuredLearningConnectionsResponse> {
  return rPost<GenerateStructuredLearningConnectionsResponse>(`/studio/structured-learning/sections/${sectionId}/knowledge-graph/generate`, {});
}

// ==================== Workplace Application (Production / "In your role") ====================

export interface WorkplaceApplicationDto {
  SectionId: string;
  PersonId: string;
  StudentName: string;
  RoleTitle: string;
  Tools: string[];
  TargetObjective?: string | null;
  ScenarioTitle: string;
  ScenarioText: string;
  PracticalActions: string[];
  AudioUrl: string;
  LanguageCode: string;
}

export function getStructuredLearningWorkplaceApplication(
  sectionId: string,
  personId?: string,
  languageCode?: string,
  force = false
): Promise<WorkplaceApplicationDto> {
  const query = new URLSearchParams();
  if (personId) query.set('personId', personId);
  if (languageCode) query.set('languageCode', languageCode);
  if (force) query.set('force', 'true');
  const qs = query.toString() ? `?${query.toString()}` : '';
  return rGet<WorkplaceApplicationDto>(`/studio/structured-learning/sections/${sectionId}/workplace-application${qs}`);
}

export interface WorkplaceMentorChatResponse {
  Reply: string;
  SessionId: string;
}

export function postStructuredLearningWorkplaceChat(
  sectionId: string,
  message: string,
  sessionId?: string,
  personId?: string,
  languageCode?: string
): Promise<WorkplaceMentorChatResponse> {
  return rPost<WorkplaceMentorChatResponse>(
    `/studio/structured-learning/sections/${sectionId}/workplace-application/chat`,
    {
      personId: personId || null,
      message,
      sessionId: sessionId || null,
      languageCode: languageCode || null,
    }
  );
}

