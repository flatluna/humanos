import { apiPost, apiGet } from './httpClient';

/** Human OS's only seeded Tenant ("Human OS") — same prototype default used
 * by humanstudio/src/lib/api/studioApi.ts until real auth/tenant context
 * exists. */
export const DEFAULT_STUDIO_TENANT_ID = '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b';

export interface StartPdfCapabilityGraphRequest {
  tenantId: string;
  capabilityDomainId: string;
  subjectId?: string | null;
  capabilityName: string;
  /** Optional: the user's own description of what this Capability teaches.
   * Saved directly to the Capability row in the database. */
  capabilityDescription?: string | null;
  fileName: string;
  contentBase64: string;
  enableWebEnrichment?: boolean;
  /** Optional: an existing Program to attach this new Capability to
   * (appended to the end of its sequence) as soon as it's created —
   * top-down flow: Programs are created empty first, Capabilities are
   * connected to them afterward, here or later from the Capability's
   * own detail page. */
  programId?: string | null;
  /** Optional: the designer's own explicit choice of sequence position
   * (1-based, e.g. "#8") within programId's sequence — gaps are allowed,
   * never forced to be contiguous. Ignored when programId is null. */
  programSequenceNumber?: number | null;
  /** Optional: what this Capability is meant to accomplish specifically
   * within programId's sequence. Ignored when programId is null. */
  capabilityObjectives?: string | null;
  /** Optional: prerequisites a learner needs before starting this
   * Capability within programId's sequence. Ignored when programId is
   * null. */
  capabilityRequirements?: string | null;
  /** "es" or "en" — the language ALL generated content for this
   * capability (graph node names/descriptions, blueprint step text,
   * illustration prompts) will be authored in, regardless of what
   * language the uploaded PDF itself is written in. Defaults to "es"
   * when omitted. */
  languageCode?: 'es' | 'en';
  /** Optional tone/persona directive for ALL generated learner-facing
   * content on this capability. Omit/undefined means "Standard" — zero
   * behavior change. The only other supported value today is
   * "YoungLearner" (K-12 early-childhood courses, e.g. a Grade 1
   * phonics/reading course) — appends a warm/playful, direct-to-the-child
   * persona to the generation prompts. */
  pedagogicalAudience?: 'YoungLearner' | null;
  /** Formal/school-track opt-in (2026-07-29 feature): when true, the
   * generated blueprint for every node includes an OPTIONAL 6th step,
   * Examples, with 3+ example cards illustrated with emoji. False/omitted
   * (the default) leaves every generated blueprint identical to before
   * this field existed. */
  includeExamplesStep?: boolean;
  /** Optional (2026-08-01 "cursos de empresa" feature): which employer
   * training track this capability belongs to — "job-skills" |
   * "soft-skills" | "department-processes" | "hr-processes". Omit for
   * capabilities not tied to any employer training track. */
  category?: string | null;
  /** Optional: together with targetSkill, explicitly connects this new
   * Capability to ONE specific required skill/process gap on a JobRole. */
  targetJobRoleId?: string | null;
  /** The exact required-skill/process string (from the target JobRole's
   * list) this capability is meant to satisfy. Ignored when
   * targetJobRoleId is null. */
  targetSkill?: string | null;
}

export type PdfCapabilityGraphStage = 'Running' | 'Completed' | 'Failed';

export interface AgentTokenUsage {
  AgentName: string;
  ModuleId: string | null;
  InputTokens: number;
  OutputTokens: number;
  CachedInputTokens: number;
  TotalTokens: number;
}

export interface PdfCapabilityCostEstimate {
  BillableInputTokens: number;
  CachedInputTokens: number;
  OutputTokens: number;
  InputCostUsd: number;
  CachedInputCostUsd: number;
  OutputCostUsd: number;
  ImageCostUsd: number;
  TotalCostUsd: number;
  IsEstimate: boolean;
}

export interface PdfCapabilityGraphResult {
  CapabilityId: string;
  CapabilityGraphId: string;
  ProgramId: string | null;
  GraphName: string;
  PageCount: number;
  ChapterCount: number;
  NodeCount: number;
  EdgeCount: number;
  NodesWithBlueprintCount: number;
  UnreferencedChunkTags: string[];
  TokenUsage: AgentTokenUsage[];
  IllustrationsGeneratedCount: number;
  EstimatedCost: PdfCapabilityCostEstimate | null;
}

export interface PdfCapabilityGraphRunStatus {
  RunId: string;
  Stage: PdfCapabilityGraphStage;
  CurrentStepDescription: string;
  ErrorMessage: string | null;
  Result: PdfCapabilityGraphResult | null;
}

/** Reads a File as a base64 string (no data: URL prefix), for the
 * JSON+base64-body convention the backend expects (no multipart parsing
 * exists anywhere in this codebase). */
export function readFileAsBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      const commaIndex = result.indexOf(',');
      resolve(commaIndex >= 0 ? result.slice(commaIndex + 1) : result);
    };
    reader.onerror = () => reject(reader.error ?? new Error('Failed to read the file.'));
    reader.readAsDataURL(file);
  });
}

/** Starts a V2 "PDF → CapabilityGraph" run. Returns immediately
 * (Stage: 'Running') — poll getPdfCapabilityGraphStatus for progress. */
export function startPdfCapabilityGraph(
  request: StartPdfCapabilityGraphRequest
): Promise<PdfCapabilityGraphRunStatus> {
  return apiPost<PdfCapabilityGraphRunStatus>('/studio/capability-graph/create-from-pdf', request);
}

export function getPdfCapabilityGraphStatus(runId: string): Promise<PdfCapabilityGraphRunStatus> {
  return apiGet<PdfCapabilityGraphRunStatus>(`/studio/capability-graph/${runId}/status`);
}

/** The single currently in-progress run, if any — only one capability
 * generation is allowed at a time (backend enforces this; starting a
 * second one while another is Running fails with a 409). Lets the
 * frontend show/link back to a live run even if the user navigated away
 * from its /runs/:runId page or reloaded. Returns null when nothing is
 * currently running. */
export function getActiveCapabilityGraphRun(): Promise<PdfCapabilityGraphRunStatus | null> {
  return apiGet<PdfCapabilityGraphRunStatus | null>('/studio/capability-graph/active');
}

export interface StartCapabilityGraphFromDescriptionRequest {
  tenantId: string;
  capabilityDomainId: string;
  subjectId?: string | null;
  capabilityName: string;
  description: string;
  /** Optional: the user's own description of what this Capability teaches.
   * Saved directly to the Capability row in the database. If not provided,
   * uses description as both the idea expansion input and the stored description. */
  capabilityDescription?: string | null;
  enableWebEnrichment?: boolean;
  /** See StartPdfCapabilityGraphRequest.programId for the full rationale. */
  programId?: string | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  programSequenceNumber?: number | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  capabilityObjectives?: string | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  capabilityRequirements?: string | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  languageCode?: 'es' | 'en';
  /** See StartPdfCapabilityGraphRequest's identical field. */
  pedagogicalAudience?: 'YoungLearner' | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  includeExamplesStep?: boolean;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  category?: string | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  targetJobRoleId?: string | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  targetSkill?: string | null;
}

/** Starts a V2 run from a short idea/goal description instead of a PDF —
 * IdeaToDocumentAgent expands it into source material first, then the
 * same Curador → GraphArchitect pipeline runs. Same run store as
 * startPdfCapabilityGraph — poll with getPdfCapabilityGraphStatus. */
export function startCapabilityGraphFromDescription(
  request: StartCapabilityGraphFromDescriptionRequest
): Promise<PdfCapabilityGraphRunStatus> {
  return apiPost<PdfCapabilityGraphRunStatus>('/studio/capability-graph/create-from-description', request);
}
