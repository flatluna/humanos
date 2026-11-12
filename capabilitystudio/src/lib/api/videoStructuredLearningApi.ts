import { apiGet, apiPost, apiDelete } from './httpClient';

/** "New Learning From Video" pipeline (2026-11-06) — Phase 3: ingest the
 * LLM-grouped chapter/Engram outline (built by LearnWitVideos +
 * LlmChapterPlanningService) directly into a Structured Learning Document,
 * then run the video-specific pedagogy step which reuses the real
 * extracted frames as each Teaching step's illustration instead of
 * generating new AI images. Mirrors structuredLearningApi.ts's
 * conventions but talks to the video-only backend endpoints under
 * `studio/structured-learning/video/*`. */

export interface VideoEngramFrameInput {
  TimestampSeconds: number;
  SceneText: string;
  ImageBase64?: string | null;
  ImageContentType?: string;
}

export interface VideoEngramInput {
  ChapterTitle: string;
  SubchapterTitle: string;
  Text: string;
  StartSeconds?: number | null;
  EndSeconds?: number | null;
  /** When true, this Engram is purely informational (e.g. course welcome)
   * — pedagogy generation will only produce its Teaching/Learn step
   * (2026-11-07). */
  IsInformationalOnly?: boolean;
  Frames: VideoEngramFrameInput[];
}

export interface VideoIngestionRequest {
  tenantId: string;
  documentName: string;
  sessionId?: string | null;
  createdByOid?: string | null;
  createdByEmail?: string | null;
  isGlobal?: boolean;
  includeKnowledgeGraphEntities?: boolean;
  includeKnowledgeGraphConnections?: boolean;
  executiveSummary?: string | null;
  sourceVideoId?: string | null;
  /** "SoftSkill" or "TechnicalSkill". */
  skillType?: string | null;
  authorName?: string | null;
  authorCourseName?: string | null;
  presentedDate?: string | null;
  presentedEntityType?: string | null;
  presentedEntityName?: string | null;
  /** Language the course TEXT/pedagogy should be generated in (e.g. "es"/"en"). */
  courseLanguageCode?: string | null;
  engrams: VideoEngramInput[];
}

export interface VideoIngestionSectionResult {
  StructuredLearningDocumentSectionId: string;
  ChapterTitle: string;
  SubchapterTitle: string;
  StructuredLearningIllustrationId: string | null;
}

export interface VideoIngestionResult {
  StructuredLearningDocumentId: string;
  Sections: VideoIngestionSectionResult[];
}

/** Step 2 (video) — creates the full Structured Learning Document
 * directly from already-synchronized video Engrams. Synchronous (no
 * polling needed): returns once the document/sections/illustrations are
 * persisted. */
export function createStructuredLearningDocumentFromVideo(
  request: VideoIngestionRequest
): Promise<VideoIngestionResult> {
  return apiPost<VideoIngestionResult>('/studio/structured-learning/video/create', request);
}

export type StructuredLearningPedagogyStage = 'Running' | 'Completed' | 'Failed';

export interface VideoStructuredLearningPedagogyRunStatus {
  RunId: string;
  DocumentId: string;
  DocumentName: string | null;
  Stage: StructuredLearningPedagogyStage;
  CurrentStepDescription: string;
  TotalSections: number | null;
  CompletedSections: number | null;
  ActiveSectionTitles: string[] | null;
  ErrorMessage: string | null;
  Result: { StructuredLearningDocumentId: string; SectionsCompleted: number; SectionsFailed: number } | null;
  FromEngramNumber?: number | null;
  ToEngramNumber?: number | null;
}

/** Step 3 (video) — same role as structuredLearningApi.ts's
 * startStructuredLearningPedagogy, but delegates to
 * VideoStructuredLearningPedagogyOrchestrator so real extracted video
 * frames are reused as each step's illustration instead of generating new
 * AI images. Returns immediately (Stage: 'Running') — poll
 * getVideoStructuredLearningPedagogyStatus for progress. */
export function startVideoStructuredLearningPedagogy(
  structuredLearningDocumentId: string,
  languageCode: 'es' | 'en' = 'es',
  forceRegenerateAll = false,
  usePerplexitySearch?: boolean | null,
  fromEngramNumber?: number | null,
  toEngramNumber?: number | null
): Promise<VideoStructuredLearningPedagogyRunStatus> {
  return apiPost<VideoStructuredLearningPedagogyRunStatus>('/studio/structured-learning/video/pedagogy/start', {
    structuredLearningDocumentId,
    languageCode,
    forceRegenerateAll,
    usePerplexitySearch,
    fromEngramNumber,
    toEngramNumber,
  });
}

export function getVideoStructuredLearningPedagogyStatus(
  runId: string
): Promise<VideoStructuredLearningPedagogyRunStatus> {
  return apiGet<VideoStructuredLearningPedagogyRunStatus>(`/studio/structured-learning/video/pedagogy/${runId}/status`);
}

/** Recovers an already-started run's RunId for a document after the browser
 * lost track of it (reload/navigation away) — same recovery pattern as
 * structuredLearningApi.ts's getActiveStructuredLearningPedagogyRun.
 * Returns null if no run is currently in progress for this document. */
export function getActiveVideoStructuredLearningPedagogyRun(
  documentId: string
): Promise<VideoStructuredLearningPedagogyRunStatus | null> {
  return apiGet<VideoStructuredLearningPedagogyRunStatus | null>(
    `/studio/structured-learning/video/pedagogy/active/${documentId}`
  );
}

/** Lets a designer remove a raw extracted video frame (shown under "Real
 * images from the source video" in edit mode) before re-running "Restart
 * engrams" (2026-11-06) — trims how many frames the per-frame narration
 * pipeline has to process, speeding up the run. Soft delete only: the
 * backend flips IsExcludedFromGeneration, it never removes the row/blob. */
export function deleteVideoStructuredLearningSourceFrame(
  sectionId: string,
  illustrationId: string
): Promise<void> {
  return apiDelete<void>(`/studio/structured-learning/sections/${sectionId}/source-frames/${illustrationId}`);
}

/** Counterpart to deleteVideoStructuredLearningSourceFrame (2026-11-07) —
 * lets a designer ADD a new real image to a section in edit mode, saved
 * ALONGSIDE the existing "Real images from the source video" frames (never
 * replacing them), so it is used the next time "Restart engrams" runs. */
export function addVideoStructuredLearningSourceFrame(
  sectionId: string,
  options: { contentBase64: string; contentType?: string; caption?: string | null; videoTimestampSeconds?: number | null }
): Promise<{ IllustrationId: string }> {
  return apiPost<{ IllustrationId: string }>(`/studio/structured-learning/sections/${sectionId}/source-frames`, {
    ContentBase64: options.contentBase64,
    ContentType: options.contentType ?? 'image/png',
    Caption: options.caption ?? null,
    VideoTimestampSeconds: options.videoTimestampSeconds ?? null,
  });
}
