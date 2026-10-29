// NOTE: This module's DTOs are deliberately typed camelCase to match the
// backend's wire format 1:1 — it uses the "Raw" client variants (no
// PascalCase conversion), unlike most other api/ modules in this app.
import { apiGetRaw, apiPostRaw, apiDeleteRaw } from './httpClient';

export interface AcademicCourseGenerationRequestDto {
  courseCode: string;
  titleEs: string;
  titleEn: string;
  subject: string;
  hoursToMaster: number;
  gradeLevel: string;
  description: string;
  whyItMatters: string;
  microSkills: string[];
  customTeacherPrompt?: string;
  language?: string;
  optionalPdfFileName?: string;
  optionalPdfContentBase64?: string;
  optionalPdfContentText?: string;
}

export interface EngramStepsDto {
  hypothesis: string;
  learn: string;
  deepDive: string;
  inYourRole: string;
  recall: string;
  assessment: string;
}

export interface AcademicLessonDto {
  lessonNumber: number;
  lessonTitle: string;
  durationMinutes: string;
  teacherDialogue: string;
  suggestedImagePrompt: string;
  classroomActivity: string;
  materialsNeeded?: string;
  wonderQuestion?: string;
  engramSteps?: EngramStepsDto;
}

export interface AcademicUnitDto {
  unitNumber: number;
  unitTitle: string;
  objective: string;
  suggestedVisualPrompt: string;
  lessons: AcademicLessonDto[];
}

export interface ArchitectureSummaryDto {
  courseIntent: string;
  researchStandards: string;
  sourceEvaluation: string;
  livePerplexityResearch?: string;
  perplexityCitations?: string[];
  knowledgeExtracted: string;
  knowledgeGraphHierarchy: string;
  courseHierarchy: string;
  engramPipelineApplied: string;
  groundednessScore: string;
  humanReviewStatus: string;
}

export interface GroundednessValidationDto {
  isFactuallySupported: boolean;
  confidenceScore: number;
  groundednessExplanation: string;
  verifiedPrerequisites: string[];
}

export interface AcademicCourseGenerationResultDto {
  courseCode: string;
  titleEs: string;
  titleEn: string;
  subject: string;
  targetAudience: string;
  estimatedHours: number;
  courseDocumentMarkdown?: string;
  teacherIntroduction: string;
  pedagogicalApproach: string;
  units: AcademicUnitDto[];
  architectureSummary: ArchitectureSummaryDto;
  reportValidation: GroundednessValidationDto;
}

export async function generateAcademicCourseWithProfesorExpert(
  request: AcademicCourseGenerationRequestDto
): Promise<AcademicCourseGenerationResultDto> {
  return await apiPostRaw<AcademicCourseGenerationResultDto>('/family/academic/generate-course', request);
}

// Two-step generation (2026-10-08) — replaces the single whole-course call above for new
// UI flows: Step 1 (fast, lightweight) returns only the course's title/intro/chapter+lesson
// titles for review; Step 2 is called ONCE PER approved chapter, sequentially, to get that
// chapter's full written-out content. Fixes multi-minute timeouts on large courses — the
// bottleneck was always the sheer size of a single whole-course response, not model speed.

export interface AcademicCourseIndexRequestDto {
  courseCode: string;
  titleEs: string;
  titleEn: string;
  subject: string;
  hoursToMaster: number;
  gradeLevel: string;
  description: string;
  whyItMatters: string;
  microSkills: string[];
  customTeacherPrompt?: string;
  language?: string;
  optionalPdfFileName?: string;
  optionalPdfContentBase64?: string;
  optionalPdfContentText?: string;
  /** Prerequisites/prior-knowledge notes carried over from the Family capability
   * recommendation that was handed off to Studio (optional). */
  requirements?: string;
  /** Short table-of-contents preview (4-8 chapter titles) already proposed by the
   * EngramsIQ Family capability recommender — Studio uses it as a seed to build its
   * own full course index, not as the final answer. */
  suggestedOutline?: string[];
}

export interface AcademicChapterOutlineDto {
  chapterNumber: number;
  chapterTitle: string;
  lessonTitles: string[];
}

export interface AcademicCourseIndexResultDto {
  courseTitle: string;
  teacherIntroduction: string;
  pedagogicalApproach: string;
  chapters: AcademicChapterOutlineDto[];
  architectureSummary: ArchitectureSummaryDto;
}

export async function generateAcademicCourseIndex(
  request: AcademicCourseIndexRequestDto
): Promise<AcademicCourseIndexResultDto> {
  return await apiPostRaw<AcademicCourseIndexResultDto>('/family/academic/generate-course-index', request);
}

export interface AcademicChapterContentRequestDto {
  courseTitle: string;
  subject: string;
  gradeLevel: string;
  language?: string;
  customTeacherPrompt?: string;
  researchContext?: string;
  chapter: AcademicChapterOutlineDto;
}

export interface AcademicChapterContentResultDto {
  chapterNumber: number;
  chapterTitle: string;
  chapterMarkdown: string;
}

export async function generateAcademicCourseChapter(
  request: AcademicChapterContentRequestDto
): Promise<AcademicChapterContentResultDto> {
  return await apiPostRaw<AcademicChapterContentResultDto>('/family/academic/generate-course-chapter', request);
}

export interface SaveAcademicCourseDocumentRequestDto {
  courseCode: string;
  titleEs: string;
  titleEn: string;
  subject: string;
  gradeLevel: string;
  language: string;
  estimatedHours: number;
  courseDocumentMarkdown: string;
  teacherIntroduction?: string;
  pedagogicalApproach?: string;
  architectureSummaryJson?: string;
  isApprovedByEducator: boolean;
  educatorReviewNotes?: string;
}

export interface SaveAcademicCourseDocumentResponseDto {
  documentId: string;
  courseCode: string;
  updatedDate: string;
}

/** Persists (upsert, keyed by courseCode) the complete academic course document
 * authored by ProfesorExpert — see SaveAcademicCourseDocumentFunction.cs. */
export async function saveAcademicCourseDocument(
  request: SaveAcademicCourseDocumentRequestDto
): Promise<SaveAcademicCourseDocumentResponseDto> {
  return await apiPostRaw<SaveAcademicCourseDocumentResponseDto>('/family/academic/courses', request);
}

export interface AcademicCourseDocumentResponseDto {
  documentId: string;
  courseCode: string;
  titleEs: string;
  titleEn: string;
  subject: string;
  gradeLevel: string;
  language: string;
  estimatedHours: number;
  courseDocumentMarkdown: string;
  teacherIntroduction?: string;
  pedagogicalApproach?: string;
  architectureSummaryJson?: string;
  isApprovedByEducator: boolean;
  educatorReviewNotes?: string;
  createdDate: string;
  updatedDate: string;
}

/** Retrieves a previously saved academic course document (if any) for a
 * given courseCode — lets the Academic Course Generator page restore a
 * saved draft on load instead of forcing a full regeneration. Returns null
 * (never throws) when nothing has been saved yet for this code. */
export async function getAcademicCourseDocument(
  courseCode: string
): Promise<AcademicCourseDocumentResponseDto | null> {
  try {
    return await apiGetRaw<AcademicCourseDocumentResponseDto>(`/family/academic/courses/${encodeURIComponent(courseCode)}`);
  } catch {
    return null;
  }
}

export interface AcademicCourseDocumentSummaryDto {
  courseCode: string;
  titleEs: string;
  titleEn: string;
  subject: string;
  gradeLevel: string;
  language: string;
  estimatedHours: number;
  courseDocumentMarkdownLength: number;
  isApprovedByEducator: boolean;
  createdDate: string;
  updatedDate: string;
}

/** Lists every saved academic course document (lightweight summary, no full
 * markdown) — powers the "Catálogo Académico" page. */
export async function listAcademicCourseDocuments(): Promise<AcademicCourseDocumentSummaryDto[]> {
  return await apiGetRaw<AcademicCourseDocumentSummaryDto[]>('/family/academic/courses');
}

/** Deletes a saved academic course document by courseCode — lets the "Catálogo
 * Académico" page clean up test/broken drafts. */
export async function deleteAcademicCourseDocument(courseCode: string): Promise<void> {
  await apiDeleteRaw(`/family/academic/courses/${encodeURIComponent(courseCode)}`);
}
