import { getIdentityHeaders } from '@/auth';

/** HumanOS.SimulationLabs.Api runs as its own Azure Functions app (separate
 * from the main HumanOS backend on :7071) — same pattern as Capability
 * Studio's labsApi.ts. Authenticates via the same X-Azure-OID/TID/Email
 * headers already used against the main backend (see
 * HeaderIdentityAuthenticationMiddleware on the SimulationLabs.Api side). */
const LABS_API_BASE_URL = import.meta.env.VITE_SIMULATION_LABS_API_URL ?? 'http://localhost:7072/api';

export type LabType = 'CONVERSATIONAL' | 'DESKTOP' | 'HYBRID';
export type LabStatus = 'DRAFT' | 'PUBLISHED' | 'RETIRED';

/** See HumanOS.SimulationLabs.Api LabListItemResponse. */
export interface LabListItem {
  IdLab: string;
  Codigo: string;
  Nombre: string;
  Descripcion: string;
  Tipo: LabType;
  Dominio: string;
  Estatus: LabStatus;
  OwnerId: string;
  FechaCreacion: string;
  FechaActualizacion: string | null;
  RowVersion: string;
}

export interface PagedResponse<T> {
  Items: T[];
  Page: number;
  PageSize: number;
  TotalItems: number;
  TotalPages: number;
}

/** SimulationLabs.Api's SEG_IdTenant is the org tenant resolved by the main
 * backend's /api/me (Person.TenantId) — NOT the raw CIAM directory `tid`
 * claim that getIdentityHeaders() sends by default. Every call here must
 * override X-Azure-TID with that resolved org tenant id or Lab_List/Enroll
 * will silently scope to the wrong (empty) tenant. */
function identityHeaders(orgTenantId: string): Record<string, string> {
  return { ...getIdentityHeaders(), 'X-Azure-TID': orgTenantId };
}

async function labsApiGet<T>(path: string, orgTenantId: string): Promise<T> {
  const response = await fetch(`${LABS_API_BASE_URL}${path}`, {
    method: 'GET',
    headers: { Accept: 'application/json', ...identityHeaders(orgTenantId) },
  });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  const text = await response.text();
  return (text ? toPascalCaseDeep(JSON.parse(text)) : undefined) as T;
}

/** Recursively converts every camelCase object key to PascalCase — SimulationLabs.Api's JSON
 * (ApiResponses.JsonOptions uses JsonSerializerDefaults.Web) comes back camelCase (idLab,
 * nombre...), but every type here is PascalCase (same fix Studio's labsApi.ts already needed). */
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

async function extractErrorMessage(response: Response): Promise<string> {
  try {
    const raw = (await response.json()) as Record<string, unknown>;
    const title = (raw.Title ?? raw.title) as string | undefined;
    const detail = (raw.Detail ?? raw.detail) as string | undefined;
    if (title || detail) return [title, detail].filter(Boolean).join(' — ');
  } catch {
    // Body wasn't JSON — fall back to the generic message below.
  }
  return `Request failed with status ${response.status}`;
}

async function labsApiPost<T>(path: string, orgTenantId: string, body?: unknown, extraHeaders?: Record<string, string>): Promise<T> {
  const response = await fetch(`${LABS_API_BASE_URL}${path}`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...identityHeaders(orgTenantId),
      ...extraHeaders,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  const text = await response.text();
  return (text ? toPascalCaseDeep(JSON.parse(text)) : undefined) as T;
}

async function labsApiDelete<T>(path: string, orgTenantId: string): Promise<T> {
  const response = await fetch(`${LABS_API_BASE_URL}${path}`, {
    method: 'DELETE',
    headers: { Accept: 'application/json', ...identityHeaders(orgTenantId) },
  });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  const text = await response.text();
  return (text ? toPascalCaseDeep(JSON.parse(text)) : undefined) as T;
}

/** Lists every Lab visible to the caller's tenant. Deliberately NOT filtered by
 * estatus=PUBLISHED for now — most labs created during early testing are still
 * DRAFT, and the ask right now is just "show me the labs that exist" (same as
 * Studio's own unfiltered LabsPage). Tighten this back to PUBLISHED-only once
 * real publishing is in place. */
export async function listPublishedLabs(orgTenantId: string): Promise<LabListItem[]> {
  const result = await labsApiGet<PagedResponse<LabListItem>>('/labs?pageSize=100', orgTenantId);
  return result.Items ?? [];
}

/** See HumanOS.SimulationLabs.Api EnrollmentResponse. */
export interface EnrollmentItem {
  IdEnrollment: string;
  IdLab: string;
  Estatus: 'ENROLLED' | 'CANCELLED';
  FechaInscripcion: string;
  FechaCancelacion: string | null;
  RowVersion: string;
}

/** Persists the employee's enrollment in a Lab for running later (LAB_Enrollment table) —
 * separate from actually starting/running an attempt. */
export function enrollInLab(idLab: string, orgTenantId: string): Promise<EnrollmentItem> {
  return labsApiPost<EnrollmentItem>(`/labs/${encodeURIComponent(idLab)}/enroll`, orgTenantId);
}

export function unenrollFromLab(idLab: string, orgTenantId: string): Promise<EnrollmentItem> {
  return labsApiDelete<EnrollmentItem>(`/labs/${encodeURIComponent(idLab)}/enroll`, orgTenantId);
}

// ─── Running a Lab (real attempt, not the Studio preview) ──────────────────

interface LabVersionListItem {
  IdVersion: string;
}

interface ScenarioListItem {
  IdScenario: string;
}

/** Finds the (version, scenario) pair to start a real Attempt against — the latest LabVersion's
 * first Scenario. Both list endpoints already require only read-level participant permissions
 * (see HeaderIdentityAuthenticationMiddleware). */
export async function getRunnableScenario(idLab: string, orgTenantId: string): Promise<{ idVersion: string; idScenario: string } | null> {
  const versions = await labsApiGet<PagedResponse<LabVersionListItem>>(
    `/labs/${encodeURIComponent(idLab)}/versions?sortDirection=DESC&pageSize=1`,
    orgTenantId,
  );
  const idVersion = versions.Items?.[0]?.IdVersion;
  if (!idVersion) return null;

  const scenarios = await labsApiGet<PagedResponse<ScenarioListItem>>(
    `/lab-versions/${encodeURIComponent(idVersion)}/scenarios?pageSize=1`,
    orgTenantId,
  );
  const idScenario = scenarios.Items?.[0]?.IdScenario;
  if (!idScenario) return null;

  return { idVersion, idScenario };
}

/** See HumanOS.SimulationLabs.Api AttemptResponse (only the fields this app needs). */
export interface AttemptItem {
  IdAttempt: string;
  IdVersion: string;
  IdScenario: string;
  Estatus: string;
  RowVersion: string;
}

/** `idempotencyKey`, when provided, is sent as the `Idempotency-Key` header — Attempt_Start
 * already caches its result by that key server-side (see InMemoryIdempotencyService), so a
 * duplicate call (e.g. React StrictMode's dev-only double-invoke of an effect, or a real
 * double-click) replays the SAME attempt instead of racing a second INSERT and hitting the
 * (tenant, scenario, participant, attempt#) unique-index violation. */
export function startAttempt(idScenario: string, orgTenantId: string, idioma: string, idempotencyKey?: string): Promise<AttemptItem> {
  return labsApiPost<AttemptItem>(
    `/scenarios/${encodeURIComponent(idScenario)}/attempts`,
    orgTenantId,
    { idioma, modalidad: 'VOICE' },
    idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined,
  );
}

export function completeAttempt(idAttempt: string, orgTenantId: string, rowVersion: string): Promise<AttemptItem> {
  return labsApiPostWithIfMatch<AttemptItem>(`/attempts/${encodeURIComponent(idAttempt)}/complete`, orgTenantId, rowVersion);
}

/** Freezes the Attempt (IN_PROGRESS -> PAUSED) without completing/evaluating it — lets the
 * employee stop the live conversation mid-way and come back later instead of being forced to
 * either keep going or end (which immediately moves to the evaluation step). */
export function pauseAttempt(idAttempt: string, orgTenantId: string, rowVersion: string): Promise<AttemptItem> {
  return labsApiPostWithIfMatch<AttemptItem>(`/attempts/${encodeURIComponent(idAttempt)}/pause`, orgTenantId, rowVersion);
}

/** PAUSED -> IN_PROGRESS — see pauseAttempt. */
export function resumeAttempt(idAttempt: string, orgTenantId: string, rowVersion: string): Promise<AttemptItem> {
  return labsApiPostWithIfMatch<AttemptItem>(`/attempts/${encodeURIComponent(idAttempt)}/resume`, orgTenantId, rowVersion);
}

export function getAttempt(idAttempt: string, orgTenantId: string): Promise<AttemptItem> {
  return labsApiGet<AttemptItem>(`/attempts/${encodeURIComponent(idAttempt)}`, orgTenantId);
}

/** Permanently deletes an Attempt and all of its related rows (conversation turns, user
 * actions, artifact submissions, evaluation) — the backend removes everything in one
 * transaction (see AttemptService.DeleteAsync), so this either fully succeeds or leaves
 * no partial state behind. */
export function deleteAttempt(idAttempt: string, orgTenantId: string): Promise<void> {
  return labsApiDelete<void>(`/attempts/${encodeURIComponent(idAttempt)}`, orgTenantId);
}

async function labsApiPostWithIfMatch<T>(path: string, orgTenantId: string, rowVersion: string): Promise<T> {
  const response = await fetch(`${LABS_API_BASE_URL}${path}`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'If-Match': rowVersion, ...identityHeaders(orgTenantId) },
  });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  const text = await response.text();
  return (text ? toPascalCaseDeep(JSON.parse(text)) : undefined) as T;
}

/** See HumanOS.SimulationLabs.Api AttemptVoiceSessionResponse. Real (non-preview) voice session
 * for a real, participant-owned Attempt — the caller must persist turns itself as the realtime
 * data channel emits transcript events (see EmployeeLabVoiceModal). */
export interface AttemptVoiceSession {
  ClientSecret: string;
  RealtimeCallsUrl: string;
  Model: string;
  Voice: string;
  ExpiresAtUnixSeconds: number | null;
  ActorNombre: string | null;
  ActorRol: string | null;
  ActorId: string | null;
}

export function getAttemptVoiceSession(
  idAttempt: string,
  orgTenantId: string,
  employeeProfile?: {
    EmployeeName?: string;
    CompanyName?: string;
    JobRoleTitle?: string;
    JobRoleSummary?: string;
    RequiredTechnicalSkills?: string[];
    RequiredSoftSkills?: string[];
    ResumeSummary?: string;
  },
): Promise<AttemptVoiceSession> {
  return labsApiPost<AttemptVoiceSession>(`/attempts/${encodeURIComponent(idAttempt)}/voice-session`, orgTenantId, employeeProfile);
}

export type ConversationTurnSpeakerType = 'PARTICIPANT' | 'SIMULATED_ACTOR';

/** Persists one live turn as the realtime data channel emits transcript events — see
 * LAB_ConversationTurn. `actorId` is required (backend-enforced) when speakerType is
 * SIMULATED_ACTOR. */
export function createConversationTurn(
  idAttempt: string,
  orgTenantId: string,
  speakerType: ConversationTurnSpeakerType,
  texto: string,
  actorId?: string,
): Promise<void> {
  return labsApiPost(`/attempts/${encodeURIComponent(idAttempt)}/conversation-turns`, orgTenantId, {
    speakerType,
    texto,
    tipoEntrada: 'VOICE',
    actorId: speakerType === 'SIMULATED_ACTOR' ? actorId : undefined,
  });
}

export function listMyEnrollments(orgTenantId: string): Promise<EnrollmentItem[]> {
  return labsApiGet<EnrollmentItem[]>('/me/lab-enrollments', orgTenantId);
}

/** See HumanOS.SimulationLabs.Api ConversationTurnResponse (only the fields this app needs). */
export interface ConversationTurnItem {
  IdTurn: string;
  NumeroTurno: number;
  SpeakerType: ConversationTurnSpeakerType;
  Texto: string;
}

/** Full ordered transcript for an attempt — used to let the employee verify the conversation
 * was captured correctly alongside the AI evaluator's per-answer feedback. */
export async function listConversationTurns(idAttempt: string, orgTenantId: string): Promise<ConversationTurnItem[]> {
  const result = await labsApiGet<PagedResponse<ConversationTurnItem>>(
    `/attempts/${encodeURIComponent(idAttempt)}/conversation-turns?pageSize=100`,
    orgTenantId,
  );
  return result.Items ?? [];
}

// ─── Post-attempt AI evaluation (skills-aware, any lab kind) ───────────────

/** See HumanOS.SimulationLabs.Api EvaluateAttemptRequest — the employee's real, required job-role
 * skills (never invented) so the evaluator's recommendations are personalized. Any list left empty
 * is simply skipped by the evaluation agent. */
export interface EvaluateAttemptRequest {
  EmployeeName?: string;
  JobRoleTitle?: string;
  RequiredTechnicalSkills: string[];
  RequiredSoftSkills: string[];
  RequiredDepartmentProcesses: string[];
  RequiredHrProcesses: string[];
}

export type RecommendedSkillCategory = 'TECHNICAL' | 'SOFT' | 'DEPARTMENT_PROCESS' | 'HR_PROCESS';

export interface RecommendedSkillDto {
  SkillName: string;
  SkillCategory: RecommendedSkillCategory;
  Reason: string;
}

export interface CriterionEvaluationDto {
  CriterionName: string;
  Score: number;
  Evidence: string;
}

/** One participant answer paired with the question that preceded it, scored individually with
 * a model/ideal answer grounded in best practices, technical knowledge, and soft skills. */
export interface TurnEvaluationDto {
  QuestionTurnNumber: number;
  AnswerTurnNumber: number;
  Question: string;
  Answer: string;
  SkillArea: 'TECHNICAL' | 'SOFT';
  Score: number;
  Assessment: string;
  RecommendedAnswer: string;
}

/** See HumanOS.SimulationLabs.Api AttemptEvaluationResponse. */
export interface AttemptEvaluationResponse {
  IdEvaluation: string;
  IdAttempt: string;
  ScoreFinal: number;
  Resultado: string;
  Feedback: string;
  Strengths: string[];
  Gaps: string[];
  RecommendedSkills: RecommendedSkillDto[];
  CriteriaScores: CriterionEvaluationDto[];
  TurnEvaluations: TurnEvaluationDto[];
  FechaCreacion: string;
}

/** Triggers the AI evaluation agent for a COMPLETED attempt, passing the employee's real required
 * job-role skills so gap-driven recommendations are personalized. Works for any lab kind. */
export function evaluateAttempt(
  idAttempt: string,
  orgTenantId: string,
  request: EvaluateAttemptRequest,
): Promise<AttemptEvaluationResponse> {
  return labsApiPost<AttemptEvaluationResponse>(`/attempts/${encodeURIComponent(idAttempt)}/evaluate`, orgTenantId, request);
}

export function getAttemptEvaluation(idAttempt: string, orgTenantId: string): Promise<AttemptEvaluationResponse> {
  return labsApiGet<AttemptEvaluationResponse>(`/attempts/${encodeURIComponent(idAttempt)}/evaluation`, orgTenantId);
}

/** One past attempt across any Lab, for the "My Attempts" history list. */
export interface AttemptHistoryItem {
  IdAttempt: string;
  IdScenario: string;
  NumeroIntento: number;
  Estatus: string;
  Resultado: string | null;
  ScoreFinal: number | null;
  FechaInicio: string;
  FechaFin: string | null;
  IdLab: string | null;
  LabNombre: string | null;
  ScenarioNombre: string | null;
}

export async function listMyAttempts(orgTenantId: string): Promise<AttemptHistoryItem[]> {
  const result = await labsApiGet<PagedResponse<AttemptHistoryItem>>('/me/attempts?pageSize=100', orgTenantId);
  return result.Items ?? [];
}
