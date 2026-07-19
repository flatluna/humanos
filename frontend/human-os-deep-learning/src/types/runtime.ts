/**
 * TypeScript interfaces generated from C# backend runtime contracts
 * Source: AzureFunctions/Api/ and Agents/Runtime/
 */

// ============================================================================
// ENUMS - Backend State Machine
// ============================================================================

export enum RuntimeStage {
  ModuleStarted = 'ModuleStarted',
  RecallRequired = 'RecallRequired',
  PredictionRequired = 'PredictionRequired',
  Instruction = 'Instruction',
  ChapterTeaching = 'ChapterTeaching',
  ChapterRecall = 'ChapterRecall',
  ChapterPrediction = 'ChapterPrediction',
  ChapterMiniPractice = 'ChapterMiniPractice',
  LearnerProduction = 'LearnerProduction',
  Assessment = 'Assessment',
  Reflection = 'Reflection',
  Completed = 'Completed',
  RequiresRevision = 'RequiresRevision',
}

export enum StudentEvidenceKind {
  Text = 'Text',
  Image = 'Image',
  Audio = 'Audio',
  Document = 'Document',
  Table = 'Table',
  Project = 'Project',
  Code = 'Code',
  Other = 'Other',
}

export enum EvidenceAssistanceLevel {
  Unaided = 'Unaided',
  WithRetrievalCues = 'WithRetrievalCues',
  WithGuidedHints = 'WithGuidedHints',
  WithAiAssistance = 'WithAiAssistance',
}

export enum CapabilityMetric {
  Recall = 'Recall',
  Application = 'Application',
  Independence = 'Independence',
  Confidence = 'Confidence',
  Knowledge = 'Knowledge',
  Retention = 'Retention',
  Fluency = 'Fluency',
}

export enum StudentEvidenceOrigin {
  Recall = 'Recall',
  Prediction = 'Prediction',
  Production = 'Production',
  Reflection = 'Reflection',
}

export enum MetricVerificationStatus {
  Verified = 'Verified',
  NotVerified = 'NotVerified',
  Failed = 'Failed',
}

// ============================================================================
// RESPONSE DTOS
// ============================================================================

/**
 * Core response from all runtime endpoints
 * Returned by:
 * - GET /sessions/{id}
 * - POST /sessions/{id}/evidence
 * - POST /sessions/{id}/instruction-ack
 * - POST /people/{personId}/modules/{moduleId}/sessions
 */
export interface RuntimeTurnResponse {
  runtimeSessionId: string; // Guid
  stage: RuntimeStage;
  message: string;
  requiresInstructionAcknowledgementOnly: boolean;
  isTerminal: boolean;
  capabilityTitle?: string; // Title of the capability being learned (e.g., "Creating Actionable Agendas")
  capabilityCode?: string; // Code of the capability (e.g., "MEETING-DESIGN")
  finalAssessment?: RuntimeAssessmentResult;
  /** 0-based index of the chapter currently being presented (fixed
   * 2026-07-16) — populated only when `stage` is one of the `Chapter*`
   * stages; undefined otherwise (legacy whole-script path). */
  chapterIndex?: number;
  /** Total chapter count for this module (fixed 2026-07-16) — lets the UI
   * render e.g. "Capítulo 2 de 5". */
  totalChapters?: number;
  /** The active chapter's title (fixed 2026-07-16). */
  chapterTitle?: string;
  /** 1-based attempt number for the current Recall retrieval-practice
   * turn (fixed 2026-07-17) — populated only for RecallRequired/
   * ChapterRecall, lets the UI show e.g. "Intento 2 de 5". */
  attemptNumber?: number;
  /** Total attempts allowed for this Recall (fixed 2026-07-17). */
  totalAttempts?: number;
  /** The PREVIOUS attempt's estimated accuracy 0-100 (fixed 2026-07-17) —
   * undefined on the first attempt or after a genuine clarifying
   * question. */
  lastAccuracyPercentage?: number;
  /** Every chapter's title for this module, in order (fixed 2026-07-16) —
   * always populated (even outside Chapter* stages) so the sidebar can
   * render the full phase sub-list for the current module. */
  allChapterTitles?: string[];
  /** The Capability this module belongs to (fixed 2026-07-16) — lets the
   * sidebar fetch the full course structure via GET
   * /capabilities/{id}/content. */
  capabilityId?: string;
  capabilityModuleId?: string;
}

/**
 * Assessment verdict returned when stage transitions to Assessment or Completed
 */
export interface RuntimeAssessmentResult {
  targetMetric: CapabilityMetric;
  status: MetricVerificationStatus;
  successCriteriaResults: SuccessCriterionAssessment[];
  explanation: string;
}

export interface SuccessCriterionAssessment {
  criterion: string;
  isSatisfied: boolean;
  evidence: string;
}

// ============================================================================
// REQUEST DTOS
// ============================================================================

/**
 * POST /sessions/{id}/evidence
 * Submits learner evidence (code, text, artifact)
 */
export interface SubmitRuntimeEvidenceRequest {
  parts: SubmitRuntimeEvidencePartRequest[];
  assistanceLevel: EvidenceAssistanceLevel;
  capturedBeforeAssistance: boolean;
  comparesToEvidenceId?: string; // Guid or null
  /** Learner explicitly chose to move on despite an insufficient
   * Recall/Prediction check, instead of taking another retry (fixed
   * 2026-07-17 — "continuar de todas formas"). Ignored by every stage
   * except the Recall-check loop. */
  forceAdvance?: boolean;
}

export interface SubmitRuntimeEvidencePartRequest {
  kind: StudentEvidenceKind;
  text?: string;
  storageUrl?: string;
  mimeType?: string;
}

/**
 * POST /sessions/{id}/instruction-ack
 * Empty body — just acknowledges reading instruction
 */
export interface AcknowledgeRuntimeInstructionRequest {
  // Empty — the POST itself is the acknowledgement
}

/**
 * POST /people/{personId}/modules/{moduleId}/sessions
 * Empty body — starts a new session
 */
export interface StartRuntimeSessionRequest {
  // Empty — the POST itself initiates the session
}

// ============================================================================
// SESSION PERSISTENCE MODEL
// ============================================================================

/**
 * Local component state for managing session
 * Maps RuntimeTurnResponse to UI concerns
 */
export interface SessionState {
  runtimeSessionId: string;
  currentStage: RuntimeStage;
  message: string;
  requiresInstructionAcknowledgementOnly: boolean;
  isTerminal: boolean;
  assessment?: RuntimeAssessmentResult;
  loadingError?: string;
  isLoading: boolean;
}

/**
 * User responses collected across the session
 * NOT persisted locally — only sent to backend via endpoints
 */
export interface SessionResponses {
  recall?: string; // RecallRequired stage
  prediction?: string; // PredictionRequired stage
  evidence?: string; // LearnerProduction stage (text portion)
  reflections?: string[]; // Reflection stage
}

// ============================================================================
// BACKEND RESPONSE NORMALIZATION
// ============================================================================

/**
 * The .NET Azure Functions Worker serializes JSON with PascalCase property
 * names by default (RuntimeSessionId, Stage, Message...) and serializes
 * MetricVerificationStatus as its raw numeric enum value (0/1/2) rather
 * than a string — NOT camelCase / string-enum like the rest of this file
 * assumes. Every fetch() call against the Runtime API must pipe its JSON
 * through this normalizer before use, or fields will silently be
 * `undefined` (confirmed live: session.stage/message/runtimeSessionId all
 * undefined until this was added).
 */
const METRIC_VERIFICATION_STATUS_BY_ORDINAL: MetricVerificationStatus[] = [
  MetricVerificationStatus.Verified,
  MetricVerificationStatus.NotVerified,
  MetricVerificationStatus.Failed,
];

function normalizeMetricVerificationStatus(raw: unknown): MetricVerificationStatus {
  if (typeof raw === 'number') {
    return METRIC_VERIFICATION_STATUS_BY_ORDINAL[raw] ?? MetricVerificationStatus.NotVerified;
  }
  return (raw as MetricVerificationStatus) ?? MetricVerificationStatus.NotVerified;
}

function normalizeSuccessCriterion(raw: any): SuccessCriterionAssessment {
  return {
    criterion: raw.criterion ?? raw.Criterion ?? '',
    isSatisfied: raw.isSatisfied ?? raw.IsSatisfied ?? false,
    evidence: raw.evidence ?? raw.Evidence ?? '',
  };
}

function normalizeAssessmentResult(raw: any): RuntimeAssessmentResult | undefined {
  if (!raw) return undefined;
  return {
    targetMetric: raw.targetMetric ?? raw.TargetMetric,
    status: normalizeMetricVerificationStatus(raw.status ?? raw.Status),
    successCriteriaResults: (raw.successCriteriaResults ?? raw.SuccessCriteriaResults ?? []).map(
      normalizeSuccessCriterion
    ),
    explanation: raw.explanation ?? raw.Explanation ?? '',
  };
}

/**
 * Normalizes a raw JSON response from any Runtime API endpoint (GET
 * /sessions/{id}, POST .../instruction-ack, POST .../evidence, POST
 * .../sessions) into the camelCase RuntimeTurnResponse shape this app
 * uses everywhere else.
 */
export function parseRuntimeTurnResponse(raw: any): RuntimeTurnResponse {
  return {
    runtimeSessionId: raw.runtimeSessionId ?? raw.RuntimeSessionId,
    stage: raw.stage ?? raw.Stage,
    message: raw.message ?? raw.Message ?? '',
    requiresInstructionAcknowledgementOnly:
      raw.requiresInstructionAcknowledgementOnly ?? raw.RequiresInstructionAcknowledgementOnly ?? false,
    isTerminal: raw.isTerminal ?? raw.IsTerminal ?? false,
    capabilityTitle: raw.capabilityTitle ?? raw.CapabilityTitle,
    capabilityCode: raw.capabilityCode ?? raw.CapabilityCode,
    finalAssessment: normalizeAssessmentResult(raw.finalAssessment ?? raw.FinalAssessment),
    chapterIndex: raw.chapterIndex ?? raw.ChapterIndex ?? undefined,
    totalChapters: raw.totalChapters ?? raw.TotalChapters ?? undefined,
    chapterTitle: raw.chapterTitle ?? raw.ChapterTitle ?? undefined,
    attemptNumber: raw.attemptNumber ?? raw.AttemptNumber ?? undefined,
    totalAttempts: raw.totalAttempts ?? raw.TotalAttempts ?? undefined,
    lastAccuracyPercentage: raw.lastAccuracyPercentage ?? raw.LastAccuracyPercentage ?? undefined,
    allChapterTitles: raw.allChapterTitles ?? raw.AllChapterTitles ?? undefined,
    capabilityId: raw.capabilityId ?? raw.CapabilityId ?? undefined,
    capabilityModuleId: raw.capabilityModuleId ?? raw.CapabilityModuleId ?? undefined,
  };
}

