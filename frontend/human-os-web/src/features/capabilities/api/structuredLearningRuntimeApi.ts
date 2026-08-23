import { apiGet, apiPost } from './httpClient';

/**
 * Real per-student Structured Learning runtime client (2026-08-14) —
 * mirrors capabilitystudio/src/lib/api/structuredLearningApi.ts's DTOs,
 * but every call appends the real signed-in student's personId as a query
 * param so answers are saved under their own identity instead of
 * capabilitystudio's PersonId=null author/reviewer preview rows.
 *
 * Backend responses for this route are camelCase (confirmed via a direct
 * fetch, e.g. `structuredLearningDocumentId` — FunctionResponseFactory's
 * global JsonSerializerDefaults.Web policy), unlike capabilitystudio's own
 * httpClient.ts which auto-converts camelCase->PascalCase for every call.
 * This app's shared httpClient.ts does NOT do that conversion, so it's
 * done locally here (same technique as runtimeSessionApi.ts's rGet/rPost)
 * to keep the PascalCase DTO types below matching capabilitystudio's.
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
}

export interface StructuredLearningSectionDetail {
  StructuredLearningDocumentSectionId: string;
  SortOrder: number;
  ChapterTitle: string;
  SubchapterTitle: string;
  Steps: StructuredLearningStepDetail[];
}

export interface StructuredLearningDocumentDetail {
  StructuredLearningDocumentId: string;
  DocumentName: string;
  ExecutiveSummary?: string | null;
  AssessmentQuestionType: string;
  Sections: StructuredLearningSectionDetail[];
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
