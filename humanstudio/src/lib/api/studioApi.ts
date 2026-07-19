import { apiGet, apiPost } from './httpClient';

/**
 * Real backend types for the Human OS Studio capability-creation pipeline
 * (backend/HumanOS/Agentic/Studio + Agents/Studio). Response bodies come
 * back PascalCase (see httpClient.ts) — these interfaces mirror that
 * exactly; mapping into the app's existing (camelCase, mock-shaped) UI
 * types happens in src/lib/studioRunStore.ts and the pages that consume it.
 */

export type BackendHumanEvolutionLayer =
  | 'Foundation' | 'Exploration' | 'Mastery' | 'Professional' | 'Frontier' | 'Creator';

export type BackendCapabilityMetric =
  | 'Knowledge' | 'Recall' | 'Application' | 'Independence' | 'Confidence' | 'Retention' | 'Fluency';

export type BackendModuleType = 'Lectura' | 'Video' | 'Practica' | 'SimuladorIA' | 'Mentoria';

export type BackendRawMaterialType = 'Pdf' | 'VideoTranscript' | 'WebLink' | 'UserNote';

export interface BackendRawMaterialItem {
  type: BackendRawMaterialType;
  label: string;
  content: string;
}

export interface BackendCapabilityLevelBlueprint {
  Layer: BackendHumanEvolutionLayer;
  Title: string;
  HumanTransformation: string;
  Modules: BackendModuleSkeletonResponse[];
}

export interface BackendModuleSkeletonResponse {
  Title: string;
  Description: string;
  Type: BackendModuleType;
  TargetMetric: BackendCapabilityMetric;
}

export interface BackendCapabilityBlueprint {
  CapabilityName: string;
  Goal: string;
  ScopeDeclaration: string;
  Levels: BackendCapabilityLevelBlueprint[];
}

export interface BackendModuleChapter {
  Title: string;
  TeachingContent: string;
  IsPrimaryWeight: boolean;
  RecallPrompt: string;
  IsCumulativeRecall: boolean;
  PredictionPrompt: string | null;
  MiniPracticePrompt: string | null;
}

export interface BackendModuleScript {
  Script: string;
  Chapters: BackendModuleChapter[];
  ReflectionPrompt: string;
}

export interface BackendModuleMetricAssignment {
  Metrics: BackendCapabilityMetric[];
  Rationale: string;
}

export interface BackendCompletedModule {
  Module: BackendModuleSkeletonResponse;
  Script: BackendModuleScript;
  Metrics: BackendModuleMetricAssignment;
}

export interface BackendCapabilityPackage {
  PackageId: string;
  BlueprintId: string;
  CapabilityName: string;
  CapabilityId: string | null;
  TutorKnowledgeBase: string;
  Modules: BackendCompletedModule[];
}

/** ModuleProcessingStatus enum (backend/HumanOS/Agents/Studio/StudioSharedTypes.cs):
 * 0=Pending, 1=GeneratingScript, 2=ScriptGenerated, 3=VerifyingMetric,
 * 4=Verified, 5=RequiresRevision, 6=Failed. */
export type ModuleProcessingStatusCode = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export interface BackendOutcomeModule extends BackendCompletedModule {
  Status: ModuleProcessingStatusCode;
  FailureReason: string | null;
}

/** Terminal payload when the run finishes but FEWER than the required
 * ratio of modules reached Verified (ModuleRevisionRequiredExecutor) —
 * Stage is 'Completed' but there is no PackageId/CapabilityId, unlike a
 * real Gate-2-ready CapabilityPackage. */
export interface BackendModuleGenerationOutcome {
  BlueprintId: string;
  Modules: BackendOutcomeModule[];
}

export type RunStage = 'Running' | 'PendingGate' | 'Completed' | 'Failed' | 'Idle';

export interface PublishTaskStatus {
  TaskKey: string;
  Status: 'Pending' | 'Processing' | 'Completed' | 'Failed';
}

export interface RunProgress {
  TotalModules: number | null;
  CompletedModules: number | null;
  CurrentModuleTitle: string | null;
  /** Titles of every module CURRENTLY being processed at once (module
   * generation is bounded-concurrency parallel, not one-at-a-time — see
   * ParallelModuleGenerationExecutor). Use this instead of
   * CurrentModuleTitle (which only ever shows the single most-recently-
   * started module) to mark ALL in-flight modules as in-progress. */
  ActiveModuleTitles: string[] | null;
  /** Titles of every module whose outcome is FINAL (Verified, or retries
   * exhausted). Authoritative — with parallel generation, modules can
   * finish in a DIFFERENT order than the blueprint's own order, so
   * "the first CompletedModules modules in blueprint order are done" is
   * NOT a safe assumption from the count alone. */
  CompletedModuleTitles: string[] | null;
  PublishTasks: PublishTaskStatus[] | null;
}

export interface CapabilityCreationRunStatus {
  RunId: string;
  Stage: RunStage;
  PendingSubjectId: string | null;
  /** CapabilityBlueprint (Gate 1) | CapabilityPackage (Gate 2) | BackendModuleGenerationOutcome (Completed, below verification threshold) | string (rejection message) */
  Payload: BackendCapabilityBlueprint | BackendCapabilityPackage | BackendModuleGenerationOutcome | string | null;
  Progress: RunProgress | null;
  ErrorMessage: string | null;
}

export interface StartCapabilityCreationRequest {
  capabilityDomainId: string;
  capabilityGoal: string;
  rawMaterials: BackendRawMaterialItem[];
}

export function startCapabilityCreation(
  request: StartCapabilityCreationRequest
): Promise<CapabilityCreationRunStatus> {
  return apiPost<CapabilityCreationRunStatus>('/studio/capability-creation/start', request);
}

export interface ExtractPdfTextResponse {
  FileName: string;
  Text: string;
  StoragePath: string | null;
}

/** Human OS's only seeded Tenant ("Human OS", TenantId
 * 81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b) — humanstudio has no real
 * auth/tenant context wired up yet (see AppShell.tsx's mockUser), so this
 * is used as the prototype default until Studio gets real sign-in
 * (matches the DEMO_PERSON_ID convention used by the Deep Learning Runtime
 * frontend). Replace with the signed-in user's real tenantId once
 * humanstudio has auth. */
export const DEFAULT_STUDIO_TENANT_ID = '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b';

/** Extracts real plain text from a PDF attached as capability-creation raw
 * material, and persists the original file to its own tenant-scoped Data
 * Lake container (backend/HumanOS/AzureFunctions/Api/
 * ExtractCapabilityMaterialPdfFunction.cs + CapabilityMaterialStorageService,
 * reusing PdfTextExtractor). */
export function extractCapabilityMaterialPdfText(
  fileName: string,
  contentBase64: string,
  tenantId: string = DEFAULT_STUDIO_TENANT_ID
): Promise<ExtractPdfTextResponse> {
  return apiPost<ExtractPdfTextResponse>('/studio/capability-creation/materials/extract-pdf', {
    tenantId,
    fileName,
    contentBase64,
  });
}

export interface RespondToGateRequest {
  subjectId: string;
  approved: boolean;
  comments?: string;
  revisedBlueprint?: BackendCapabilityBlueprint;
}

export function respondToCapabilityCreationGate(
  runId: string,
  request: RespondToGateRequest
): Promise<CapabilityCreationRunStatus> {
  return apiPost<CapabilityCreationRunStatus>(
    `/studio/capability-creation/${runId}/respond`,
    request
  );
}

export function getCapabilityCreationStatus(runId: string): Promise<CapabilityCreationRunStatus> {
  return apiGet<CapabilityCreationRunStatus>(`/studio/capability-creation/${runId}/status`);
}
