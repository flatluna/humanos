import { apiGet, apiPost } from '@/features/capabilities/api/httpClient';

/**
 * "Practica y refuerza" (Tec Contigo, 2026-09-28) — client for
 * TecPracticeFunctions.cs. On-demand AI practice for a course the student
 * already completed: generated once (cached), then each exercise is
 * answered and graded independently.
 */

export interface TecPracticeExerciseView {
  index: number;
  title: string;
  instructions: string;
  studentAnswer?: string | null;
  score?: number | null;
  feedback?: string | null;
}

export interface TecPracticeSessionView {
  practiceSessionId: string;
  capabilityId: string;
  capabilityName: string;
  createdDate: string;
  exercises: TecPracticeExerciseView[];
}

export function getTecPracticeSession(personId: string, capabilityId: string): Promise<TecPracticeSessionView | null> {
  return apiGet<TecPracticeSessionView | null>(`/people/${personId}/capabilities/${capabilityId}/practice`);
}

export function generateTecPracticeSession(personId: string, capabilityId: string): Promise<TecPracticeSessionView> {
  return apiPost<TecPracticeSessionView>(`/people/${personId}/capabilities/${capabilityId}/practice/generate`);
}

export function submitTecPracticeAnswer(
  personId: string,
  practiceSessionId: string,
  exerciseIndex: number,
  studentAnswer: string,
): Promise<TecPracticeExerciseView> {
  return apiPost<TecPracticeExerciseView>(
    `/people/${personId}/practice/${practiceSessionId}/exercises/${exerciseIndex}/answer`,
    { studentAnswer },
  );
}
