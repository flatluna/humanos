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

export interface BackendModuleScript {
  Script: string;
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

export type RunStage = 'Running' | 'PendingGate' | 'Completed' | 'Failed' | 'Idle';

export interface PublishTaskStatus {
  TaskKey: string;
  Status: 'Pending' | 'Processing' | 'Completed' | 'Failed';
}

export interface RunProgress {
  TotalModules: number | null;
  CompletedModules: number | null;
  CurrentModuleTitle: string | null;
  PublishTasks: PublishTaskStatus[] | null;
}

export interface CapabilityCreationRunStatus {
  RunId: string;
  Stage: RunStage;
  PendingSubjectId: string | null;
  /** CapabilityBlueprint (Gate 1) | CapabilityPackage (Gate 2 or Completed) | string (rejection message) */
  Payload: BackendCapabilityBlueprint | BackendCapabilityPackage | string | null;
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
