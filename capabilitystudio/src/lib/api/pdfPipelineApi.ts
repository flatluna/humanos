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
  /** Optional: maximum number of Deep Dive example cards the AI may
   * generate per node for this course. Backend hard-caps to 10. */
  maxDeepDiveCardsPerNode?: number | null;
  /** Opt-in (2026-08-03): when true, and the source PDF has a real
   * detected chapter/subsection structure (a formal syllabus with
   * numbered "Unidades"/subsections), forces the graph designer to
   * create EXACTLY one node per detected chapter/subsection, in order,
   * none skipped or merged — for courses that must mirror an
   * externally-mandated curriculum exactly. */
  strictSyllabusMode?: boolean;
  /** Optional (2026-08-04 "Paso 5: Diseño de la Capability"): the
   * designer's reviewed/approved (possibly hand-edited) chapter/node
   * outline, as returned by previewCapabilityOutline. When provided, the
   * backend uses it VERBATIM instead of re-running detection — the
   * generation loop then respects exactly these chapter/node names. */
  approvedOutline?: CapabilityOutlineChapter[] | null;
  /** Opt-out (2026-08-04): when false, every generated node blueprint
   * OMITS the Production ("Aplícalo") step entirely. True/omitted (the
   * default) keeps the existing 5-step Memory Paradox order. */
  includeProductionStep?: boolean;
  /** Optional (2026-08-04): how many successful Recall items a learner
   * must complete before advancing past Recall, for every node in this
   * course. Must be between 1 and 10 when provided; omit/null keeps the
   * runtime default (3). */
  recallItemsRequired?: number | null;
  /** Optional (2026-08-04): how many questions make up one Assessment
   * round, for every node in this course. Must be between 1 and 10 when
   * provided; omit/null keeps the runtime default (5). */
  assessmentQuestionsPerRound?: number | null;
  /** Optional (2026-08-04 "logo en las ilustraciones" feature): the
   * company/brand's real logo image, base64-encoded (no data: URL
   * prefix — see readFileAsBase64), MUST be a PNG file. When provided,
   * it's used as a visual reference for EVERY illustration generated for
   * this capability (at creation time and later, e.g. Assessment/Tutor/
   * BlueprintReview/AddNode illustrations) so the AI can actually see and
   * reuse the real logo instead of guessing/hallucinating one. Omit/null
   * for capabilities with no logo. */
  logoImageBase64?: string | null;
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
  /** See StartPdfCapabilityGraphRequest's identical field. */
  maxDeepDiveCardsPerNode?: number | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  strictSyllabusMode?: boolean;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  approvedOutline?: CapabilityOutlineChapter[] | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  includeProductionStep?: boolean;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  recallItemsRequired?: number | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  assessmentQuestionsPerRound?: number | null;
  /** See StartPdfCapabilityGraphRequest's identical field. */
  logoImageBase64?: string | null;
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

/** One proposed NODE (fine-grained topic) under a chapter/level — see
 * {@link CapabilityOutlinePreviewResult}. `startMarker` is round-tripped
 * back to the backend unchanged (even if the designer edits `title`) so a
 * subsequent real run can locate this node's text without re-running
 * detection — never shown in the UI. */
export interface CapabilityOutlineNode {
  Title: string;
  StartMarker: string;
}

/** One proposed CHAPTER (becomes a graph "level") — see
 * {@link CapabilityOutlinePreviewResult}. */
export interface CapabilityOutlineChapter {
  Title: string;
  StartMarker: string;
  Nodes: CapabilityOutlineNode[];
}

/** How the outline was obtained (2026-08-04 "Modo A" vs "Modo B"):
 * - 'Detected': the document already has its own explicit chapter/section
 *   structure and it was located — low risk of inventing content.
 * - 'InferredByAI': the document has NO explicit index of its own — the
 *   model read the whole document and proposed a breakdown from scratch.
 *   Higher cost; review more carefully before approving.
 * - 'None': no usable structure could be detected or inferred at all. */
export type CapabilityOutlineSource = 'Detected' | 'InferredByAI' | 'None';

export interface CapabilityOutlinePreviewResult {
  Source: CapabilityOutlineSource;
  Chapters: CapabilityOutlineChapter[];
  RejectionReason: string | null;
}

/** Cheap, detection-only preview of a document's chapter/node structure —
 * runs ONLY text extraction + detection, WITHOUT the expensive
 * Curador/GraphArchitect/illustrations pipeline, so the designer can
 * review/approve the plan (Paso 5) before any real generation happens.
 * Pass either a PDF (fileName + contentBase64) or plain text. */
export function previewCapabilityOutline(
  request: { fileName: string; contentBase64: string } | { text: string }
): Promise<CapabilityOutlinePreviewResult> {
  return apiPost<CapabilityOutlinePreviewResult>('/studio/capability-graph/preview-outline', request);
}
