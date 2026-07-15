export interface User {
  name: string;
  email: string;
  oid: string;
  avatarUrl?: string;
}

export interface TopBarProps {
  user: User;
  onSearch?: (query: string) => void;
  onOpenSettings?: () => void;
  onSignOut?: () => void;
}

export type HumanEvolutionLevel =
  | 'Foundation'
  | 'Exploration'
  | 'Mastery'
  | 'Professional'
  | 'Frontier'
  | 'Creator';

export interface EvolutionProgress {
  currentLevel: HumanEvolutionLevel;
  nextLevel: HumanEvolutionLevel;
  percentage: number;
}

export interface Course {
  id: string;
  title: string;
  level: HumanEvolutionLevel;
  completedModules: number;
  totalModules: number;
  progress: number;
}

export interface CourseCardProps {
  course: Course;
  onContinue?: (courseId: string) => void;
}

export type Intensity = 'modest' | 'serious' | 'transformative';

export interface StudioMaterial {
  id: string;
  fileName: string;
  fileType: string;
  fileSize: number;
  status: 'selected' | 'uploading' | 'uploaded' | 'error';
  errorMessage?: string;
}

export interface StudioObjectiveForm {
  objective: string;
  intensity: Intensity;
  materials: StudioMaterial[];
}

export type CapabilityMetric =
  | 'Knowledge'
  | 'Recall'
  | 'Application'
  | 'Confidence'
  | 'Independence'
  | 'Retention'
  | 'Fluency';

export type HumanEvolutionLevelName =
  | 'Foundation'
  | 'Exploration'
  | 'Mastery'
  | 'Professional'
  | 'Frontier'
  | 'Creator';

export interface Module {
  id: string;
  number: number;
  title: string;
  description: string;
  moduleType: string;
  targetMetric: CapabilityMetric;
}

export interface BlueprintLevel {
  levelName: HumanEvolutionLevelName;
  description: string;
  modules: Module[];
}

export interface ScopeDeclaration {
  objective: string;
  intensity: Intensity;
  description: string;
}

export interface Blueprint {
  id: string;
  scopeDeclaration: ScopeDeclaration;
  levels: BlueprintLevel[];
  createdAt: string;
}

export interface ApproveBlueprintRequest {
  blueprintId: string;
}

export interface ApproveBlueprintResponse {
  runId: string;
  status: 'Generating';
  totalModules: number;
  approvedAt: string;
}

export interface GenerationRun {
  runId: string;
  status: 'Generating' | 'Completed' | 'Failed';
  totalModules: number;
  completedModules: number;
  approvedAt: string;
}

export type ModuleGenerationState =
  | 'Pending'
  | 'GeneratingScript'
  | 'ScriptCompleted'
  | 'VerifyingMetric'
  | 'Verified'
  | 'RequiresReview'
  | 'Failed';

export type InstructorStatus = 'Pending' | 'Generating' | 'Completed' | 'Error';

export interface ModuleGenerationStatus {
  id: string;
  order: number;
  title: string;
  level: HumanEvolutionLevelName;
  targetMetric: CapabilityMetric;
  instructorStatus: InstructorStatus;
  moduleState: ModuleGenerationState;
  errorMessage?: string;
}

export type GenerationStatus = 'Generating' | 'AwaitingFinalApproval' | 'Failed';

export interface GenerationRunStatus {
  runId: string;
  status: GenerationStatus;
  totalModules: number;
  verifiedModules: number;
  currentModuleId?: string;
  modules: ModuleGenerationStatus[];
  updatedAt: string;
}

// ============ GATE 2 — FINAL REVIEW TYPES ============

export type PrincipleCode = 'P1' | 'P2' | 'P3' | 'P4' | 'P5' | 'P6' | 'P7';
export type PrincipleStatus = 'Pass' | 'Warning' | 'Fail' | 'NotApplicable';

export interface PrincipleVerification {
  principle: PrincipleCode;
  status: PrincipleStatus;
  explanation: string;
}

export type VerificationStatus = 'Verified' | 'Warning' | 'Failed';

export interface MetricVerification {
  status: VerificationStatus;
  summary: string;
  principles: PrincipleVerification[];
}

export interface FinalReviewModule {
  id: string;
  order: number;
  title: string;
  description: string;
  script: string;
  moduleType: string;
  targetMetric: CapabilityMetric;
  verification: MetricVerification;
}

export interface FinalReviewLevel {
  id: string;
  level: HumanEvolutionLevelName;
  transformation: string;
  modules: FinalReviewModule[];
}

export type TutorKnowledgeBaseStatus = 'Prepared' | 'Incomplete' | 'Failed';

export interface TutorKnowledgeBaseReview {
  status: TutorKnowledgeBaseStatus;
  content: string;
  sourceCount: number;
  moduleCount: number;
  sectionCount: number;
}

export interface QualitySummary {
  modulesCompleted: number;
  totalModules: number;
  metricsVerified: number;
  totalMetrics: number;
  metricsInScope: number;
  studentProduction: number;
  tutorKnowledgeBaseStatus: TutorKnowledgeBaseStatus;
  warningCount: number;
  blockingWarningCount: number;
}

export type ReviewWarningsSeverity = 'Warning' | 'Blocking';

export interface ReviewWarning {
  id: string;
  severity: ReviewWarningsSeverity;
  moduleId?: string;
  title: string;
  description: string;
}

export interface FinalReviewPackage {
  runId: string;
  capabilityPackageId: string;
  title: string;
  description: string;
  objective: string;
  intensity: Intensity;
  scopeDeclaration: string;
  levels: FinalReviewLevel[];
  quality: QualitySummary;
  tutorKnowledgeBase: TutorKnowledgeBaseReview;
  warnings: ReviewWarning[];
  status: 'AwaitingFinalApproval';
}

export type RunStatus =
  | 'Generating'
  | 'AwaitingFinalApproval'
  | 'Reviewing'
  | 'Publishing'
  | 'Published'
  | 'RequiresChanges'
  | 'Failed';

export interface Gate2RejectionPayload {
  scope: 'Capability' | 'Module';
  moduleId?: string;
  reason: string;
}

export interface Gate2Rejection {
  runId: string;
  status: 'RequiresChanges';
  rejectionReason: string;
  rejectedAt: string;
  rejectedBy: string; // TODO: from session in production
}

export interface ApproveFinalResponse {
  runId: string;
  publicationId: string;
  status: 'Publishing';
  approvedAt: string;
  approvedBy: string; // TODO: from session in production
}

// ============ PASO 11 — PUBLICATION TYPES ============

export type PublicationTaskKey =
  | 'Capability'
  | 'Levels'
  | 'Modules'
  | 'Metrics'
  | 'KnowledgeChunks'
  | 'Embeddings';

export type PublicationTaskStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed';

export interface PublicationTask {
  key: PublicationTaskKey;
  label: string;
  status: PublicationTaskStatus;
}

export type PublicationOverallStatus = 'Publishing' | 'Published' | 'Failed';

export interface PublicationError {
  code: string;
  message: string;
  retryable: boolean;
}

export interface PublicationRunStatus {
  runId: string;
  publicationId: string;
  capabilityId?: string;
  status: PublicationOverallStatus;
  progress: number;
  tasks: PublicationTask[];
  error?: PublicationError;
}

export interface PublishedCapabilityResult {
  runId: string;
  publicationId: string;
  capabilityId: string;
  status: 'Published';
  title: string;
  levelCount: number;
  moduleCount: number;
  metricCount: number;
  knowledgeChunkCount: number;
  publishedAt: string;
}

// ============ PASO 12 — CAPABILITY LIBRARY (DESIGNER VIEW) TYPES ============
//
// IMPORTANT: HumanStudio is the capability-AUTHORING app (for designers/
// instructors), NOT the student-facing learning app. "Capability Library"
// here lists capabilities the designer has authored — it must NEVER include
// student/learner data (progress, completion %, "Continue" state, etc.).
// That belongs to a separate student app. Nav item "Progress" is unrelated
// to this concept and is defined elsewhere.

/** A level a capability can include (a design-time declaration, not a student's evolution level). */
export type CapabilityLevelTag = HumanEvolutionLevelName;

/** Design/authoring lifecycle status of a capability — NOT a student progress status.
 * Derived from the real backend's Capability.IsActive (true=Published,
 * false=Archived) — there is no Draft/InReview concept in the real
 * pipeline (a capability only exists once GATE2 + Publish succeed). */
export type CapabilityStatus = 'Published' | 'Archived';

/**
 * Card-level summary shown in the /capabilities grid (designer's capability list).
 * Backed 100% by the real backend (GET /capabilities) — see
 * src/lib/api/capabilitiesApi.ts. No mock/demo data.
 */
export interface CapabilitySummary {
  capabilityId: string;
  title: string;
  description: string;
  domain: string;
  levels: CapabilityLevelTag[]; // levels included (for card + level filter)
  moduleCount: number;
  status: CapabilityStatus;
  createdAt: string;
  updatedAt: string;
}

