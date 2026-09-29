import { getIdentityHeaders } from '../../auth';

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

/** HumanOS.SimulationLabs.Api runs as its own Azure Functions app (separate
 * from the main HumanOS backend on :7071), so it needs its own base URL
 * rather than sharing httpClient's API_BASE_URL. */
const LABS_API_BASE_URL = import.meta.env.VITE_SIMULATION_LABS_API_URL ?? 'http://localhost:7072/api';

async function labsApiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${LABS_API_BASE_URL}${path}`, {
    method: 'GET',
    headers: { Accept: 'application/json', ...getIdentityHeaders() },
  });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  const text = await response.text();
  return (text ? toPascalCaseDeep(JSON.parse(text)) : undefined) as T;
}

async function labsApiPost<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${LABS_API_BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...getIdentityHeaders() },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  const text = await response.text();
  return (text ? toPascalCaseDeep(JSON.parse(text)) : undefined) as T;
}

/** Recursively converts every camelCase object key to PascalCase (first letter uppercased,
 * rest untouched) so HumanOS.SimulationLabs.Api's camelCase JSON (its ApiResponses.JsonOptions
 * uses JsonSerializerDefaults.Web) matches this app's PascalCase DTO types — same fix already
 * used in httpClient.ts for the main HumanOS backend. Without this, every field silently reads
 * as undefined (e.g. result.Warnings.length crashing the AI Lab Builder result panel). */
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

/** Reads the backend's ProblemDetails JSON body (see HumanOS.SimulationLabs.Api's
 * ApiResponses.ProblemAsync) and surfaces its Title/Errors instead of a bare status code. */
async function extractErrorMessage(response: Response): Promise<string> {
  try {
    const raw = (await response.json()) as Record<string, unknown>;
    const title = (raw.Title ?? raw.title) as string | undefined;
    const detail = (raw.Detail ?? raw.detail) as string | undefined;
    const errors = (raw.Errors ?? raw.errors) as string[] | undefined;
    const parts = [title, detail, ...(errors ?? [])].filter(Boolean);
    if (parts.length > 0) return parts.join(' — ');
  } catch {
    // Body wasn't JSON — fall back to the generic message below.
  }
  return `Request failed with status ${response.status}`;
}

/** HumanOS.SimulationLabs.Api — separate Azure Functions app from the main
 * HumanOS backend; see VITE_SIMULATION_LABS_API_URL in .env. */
export async function listLabs(): Promise<PagedResponse<LabListItem>> {
  return labsApiGet<PagedResponse<LabListItem>>('/labs');
}

/** Permanently deletes a Lab and everything related to it (see Lab_Delete). Irreversible. */
export async function deleteLab(idLab: string): Promise<void> {
  const response = await fetch(`${LABS_API_BASE_URL}/labs/${encodeURIComponent(idLab)}`, {
    method: 'DELETE',
    headers: { Accept: 'application/json', ...getIdentityHeaders() },
  });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
}

export type LabDetail = LabListItem & {
  CreadoPor: string;
  ActualizadoPor?: string | null;
  /** One of LAB_ARCHETYPES codes (e.g. 'JOB_INTERVIEW'), or null for Labs created before this
   * concept existed (treated as CLIENT_NEGOTIATION at runtime). Only present via Lab_Get. */
  Arquetipo?: string | null;
};

export interface LabVersionDetail {
  IdVersion: string;
  NumeroVersion: number;
  ObjetivoGeneral: string;
  InstruccionesParticipante: string;
  BriefOculto?: string | null;
  DuracionMinutos: number;
  ScoreMinimo: number;
  Estatus: string;
}

export interface LabContentItem {
  IdStage?: string;
  IdObjective?: string;
  IdExpectedMoment?: string;
  IdScenario?: string;
  IdActor?: string;
  Codigo?: string;
  Nombre?: string;
  Descripcion?: string;
  Trigger?: string;
  IntencionEsperada?: string;
  RespuestaEjemplar?: string | null;
  ErrorFrecuente?: string | null;
  Recomendacion?: string | null;
  Orden?: number;
  [key: string]: unknown;
}

export interface LabContent {
  Version: LabVersionDetail | null;
  Stages: LabContentItem[];
  Objectives: LabContentItem[];
  Moments: LabContentItem[];
  Scenarios: LabContentItem[];
  Actors: LabContentItem[];
  Rubric: LabContentItem | null;
  Criteria: LabContentItem[];
  /** TestedSkill details keyed by skill name (lowercased), parsed from Scenario.BriefOculto.
   * Each moment's own Nombre is already set to its evaluated skill's name by LabDraftSaveService,
   * so questions look up their skill here by name match instead of the LLM's less reliable
   * per-dialogue "#N" order refs (TestedInMoments), which don't always line up with persisted order. */
  SkillsByName: Record<string, TestedSkill>;
  /** First-class LAB_TestedSkill rows (see TestedSkill_ListByVersion) — what the AI Lab Builder
   * actually generated per skill, including RelevanceToRole/FeedbackGuidance which the legacy
   * BriefOculto-derived SkillsByName above never carried. */
  TestedSkills: TestedSkillEntity[];
}

/** See HumanOS.SimulationLabs.Api GenerateLabDraftResponse.TestedSkillDraft — same shape persisted
 * verbatim as JSON in LAB_LabVersion.LAB_BriefOculto by LabDraftSaveService. */
export interface TestedSkill {
  SkillName: string;
  SkillType?: 'TECHNICAL' | 'SOFT';
  RelevanceToRole?: string;
  DemonstrationStandard?: string;
  TestedInMoments?: string[];
  RubricCriterionRef?: string;
  FeedbackGuidance?: string;
}

/** See HumanOS.SimulationLabs.Api TestedSkillListItem (TestedSkill_ListByVersion) — first-class
 * LAB_TestedSkill row, unlike the JSON-blob-derived TestedSkill type above. */
export interface TestedSkillEntity {
  IdSkill: string;
  Name: string;
  Type: string;
  RelevanceToRole: string;
  DemonstrationStandard: string;
  FeedbackGuidance?: string | null;
  CriterionId?: string | null;
}

export async function getLab(idLab: string): Promise<LabDetail> {
  return labsApiGet<LabDetail>(`/labs/${encodeURIComponent(idLab)}`);
}

/** "Probar simulación" (2026-09-14) — Studio-only preview: mints an ephemeral Azure OpenAI
 * Realtime session so an admin can experience the Lab's AI actor by voice before publishing.
 * Never creates a LAB_Attempt/LAB_ConversationTurn — read-only preview, see
 * LabSimulationVoiceSessionFunction.cs. */
export interface LabSimulationVoiceSessionDto {
  ClientSecret: string;
  RealtimeCallsUrl: string;
  Model: string;
  Voice: string;
  ExpiresAtUnixSeconds?: number | null;
  ActorNombre?: string | null;
  ActorRol?: string | null;
}

export async function requestLabSimulationVoiceSession(idLab: string): Promise<LabSimulationVoiceSessionDto> {
  return labsApiPost<LabSimulationVoiceSessionDto>(`/labs/${encodeURIComponent(idLab)}/simulate-voice-session`, {});
}

/** Requires LabVersions.Manage/Read permission — exposes LAB_BriefOculto, unlike the plain list endpoint.
 * Most studio users won't have LabVersions.Manage, so prefer getScenarioBriefOculto below instead. */
export async function getLabVersionDetail(idVersion: string): Promise<LabVersionDetail> {
  return labsApiGet<LabVersionDetail>(`/lab-versions/${idVersion}`);
}

/** Scenario_GetById only requires Scenarios.Read (already used successfully for the Actors list),
 * and carries the same TestedSkills JSON copied into SCN_BriefOculto by LabDraftSaveService — use
 * this instead of getLabVersionDetail to avoid the LabVersions.Manage permission wall. */
async function getScenarioBriefOculto(idScenario: string): Promise<string | null> {
  const scenario = await labsApiGet<{ BriefOculto?: string | null }>(`/scenarios/${idScenario}`);
  return scenario.BriefOculto ?? null;
}

/** Builds { lowercased skillName: TestedSkill }. BriefOculto is raw JSON text serialized
 * server-side with JsonSerializerDefaults.Web (camelCase keys: "skillName", "testedInMoments"),
 * unlike the rest of the HTTP response which labsApiGet already converts to PascalCase — so the
 * parsed skills need the same conversion applied here. */
function buildSkillsByName(rawTestedSkills: unknown[]): Record<string, TestedSkill> {
  const testedSkills = toPascalCaseDeep(rawTestedSkills) as TestedSkill[];
  const result: Record<string, TestedSkill> = {};
  for (const skill of testedSkills) {
    if (skill.SkillName) result[skill.SkillName.trim().toLowerCase()] = skill;
  }
  return result;
}

function items<T>(value: T[] | PagedResponse<T>): T[] {
  return Array.isArray(value) ? value : value.Items;
}

function emptyPage<T>(): PagedResponse<T> {
  return { Items: [], Page: 1, PageSize: 0, TotalItems: 0, TotalPages: 0 };
}

export async function getLabContent(idLab: string): Promise<LabContent> {
  const versions = await labsApiGet<PagedResponse<LabVersionDetail>>(`/labs/${idLab}/versions`);
  const version = items(versions)[0] ?? null;
  if (!version) return { Version: null, Stages: [], Objectives: [], Moments: [], Scenarios: [], Actors: [], Rubric: null, Criteria: [], SkillsByName: {}, TestedSkills: [] };

  const [stages, objectives, moments, scenarios, rubric, testedSkills] = await Promise.all([
    labsApiGet<PagedResponse<LabContentItem>>(`/lab-versions/${version.IdVersion}/stages`).catch(() => emptyPage<LabContentItem>()),
    labsApiGet<PagedResponse<LabContentItem>>(`/lab-versions/${version.IdVersion}/objectives`).catch(() => emptyPage<LabContentItem>()),
    labsApiGet<PagedResponse<LabContentItem>>(`/lab-versions/${version.IdVersion}/expected-moments`).catch(() => emptyPage<LabContentItem>()),
    labsApiGet<PagedResponse<LabContentItem>>(`/lab-versions/${version.IdVersion}/scenarios`).catch(() => emptyPage<LabContentItem>()),
    labsApiGet<LabContentItem>(`/lab-versions/${version.IdVersion}/rubric`).catch(() => null),
    labsApiGet<PagedResponse<TestedSkillEntity>>(`/lab-versions/${version.IdVersion}/tested-skills`).catch(() => emptyPage<TestedSkillEntity>()),
  ]);

  const scenarioItems = items(scenarios);
  const actorResults = await Promise.all(scenarioItems.map((scenario) =>
    labsApiGet<PagedResponse<LabContentItem>>(`/scenarios/${scenario.IdScenario}/actors`).catch(() => emptyPage<LabContentItem>())));

  let skillsByName: Record<string, TestedSkill> = {};
  const firstScenarioId = scenarioItems[0]?.IdScenario as string | undefined;
  let briefOculto: string | null = null;
  if (firstScenarioId) {
    try {
      briefOculto = await getScenarioBriefOculto(firstScenarioId);
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error('[labsApi] getScenarioBriefOculto failed — skills won\'t be shown per question:', err);
    }
  }
  if (briefOculto) {
    try {
      const testedSkills = JSON.parse(briefOculto) as unknown[];
      if (Array.isArray(testedSkills)) skillsByName = buildSkillsByName(testedSkills);
      else console.warn('[labsApi] BriefOculto parsed but is not an array:', testedSkills);
    } catch (err) {
      // BriefOculto isn't TestedSkills JSON (e.g. a plain-text brief from an older/manual lab).
      console.warn('[labsApi] BriefOculto is not valid TestedSkills JSON:', briefOculto, err);
    }
  } else {
    console.warn('[labsApi] No BriefOculto returned for scenario', firstScenarioId);
  }
  const criteria = rubric
    ? await labsApiGet<PagedResponse<LabContentItem>>(`/rubrics/${rubric.IdRubric}/criteria`).catch(() => emptyPage<LabContentItem>())
    : emptyPage<LabContentItem>();

  console.debug('[labsApi] SkillsByName keys:', Object.keys(skillsByName));
  console.debug('[labsApi] Moment Nombre values:', items(moments).map((m) => m.Nombre));

  return {
    Version: version,
    Stages: items(stages),
    Objectives: items(objectives),
    Moments: items(moments),
    Scenarios: scenarioItems,
    Actors: actorResults.flatMap(items),
    Rubric: rubric,
    Criteria: items(criteria),
    SkillsByName: skillsByName,
    TestedSkills: items(testedSkills),
  };
}

/** PATCH with an If-Match header — required by ExpectedMoment_Update (optimistic concurrency). */
async function labsApiPatch<T>(path: string, ifMatch: string, body: unknown): Promise<T> {
  const response = await fetch(`${LABS_API_BASE_URL}${path}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', 'If-Match': ifMatch, ...getIdentityHeaders() },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  const text = await response.text();
  return (text ? toPascalCaseDeep(JSON.parse(text)) : undefined) as T;
}

/** POST with an If-Match header — used by the Scenario/Rubric/LabVersion Approve/Publish/Retire
 * status-transition endpoints, which are ETag-gated the same way as PATCH updates. */
async function labsApiPostIfMatch<T>(path: string, ifMatch: string, body: unknown = {}): Promise<T> {
  const response = await fetch(`${LABS_API_BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', 'If-Match': ifMatch, ...getIdentityHeaders() },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  const text = await response.text();
  return (text ? toPascalCaseDeep(JSON.parse(text)) : undefined) as T;
}

export interface ScenarioResponse {
  IdScenario: string;
  Estatus: string;
  RowVersion: string;
}

/** DRAFT -> APPROVED. See Scenario_Approve. */
export async function approveScenario(idScenario: string, rowVersion: string): Promise<ScenarioResponse> {
  return labsApiPostIfMatch<ScenarioResponse>(`/scenarios/${encodeURIComponent(idScenario)}/approve`, rowVersion);
}

/** APPROVED -> PUBLISHED — makes the scenario runnable by real employees (Attempt_Start only
 * checks the Scenario's own status). See Scenario_Publish. */
export async function publishScenario(idScenario: string, rowVersion: string): Promise<ScenarioResponse> {
  return labsApiPostIfMatch<ScenarioResponse>(`/scenarios/${encodeURIComponent(idScenario)}/publish`, rowVersion);
}

/** Approve then publish in one call — the two-step transition Scenario_Approve/Scenario_Publish
 * require, using the fresh RowVersion each step returns from the previous one. */
export async function publishLabScenario(idScenario: string, rowVersion: string): Promise<ScenarioResponse> {
  const approved = await approveScenario(idScenario, rowVersion);
  return publishScenario(idScenario, approved.RowVersion);
}

export interface UpdateExpectedMomentRequest {
  StageId?: string;
  Nombre?: string;
  Trigger?: string;
  IntencionEsperada?: string;
  RespuestaEjemplar?: string;
  ErrorFrecuente?: string;
  Recomendacion?: string;
  EsCritico?: boolean;
}

/** Edits one moment's text/flags in place. See ExpectedMomentUpdateFunction (PATCH /expected-moments/{id}). */
export async function updateExpectedMoment(idExpectedMoment: string, rowVersion: string, request: UpdateExpectedMomentRequest): Promise<LabContentItem> {
  return labsApiPatch<LabContentItem>(`/expected-moments/${encodeURIComponent(idExpectedMoment)}`, rowVersion, request);
}

export interface ReorderExpectedMomentItem {
  IdExpectedMoment: string;
  Orden: number;
  RowVersion: string;
}

export interface ReorderExpectedMomentResponseItem {
  IdExpectedMoment: string;
  Orden: number;
  RowVersion: string;
}

/** Reorders all moments within one Stage in a single atomic call. See ExpectedMomentReorderFunction
 * (POST /stages/{idStage}/expected-moments/reorder) — all moments of that stage must be included.
 * Returns each moment's new Orden + RowVersion so the caller can update local state without refetching. */
export async function reorderExpectedMoments(idStage: string, moments: ReorderExpectedMomentItem[]): Promise<{ Items: ReorderExpectedMomentResponseItem[] }> {
  return labsApiPost<{ Items: ReorderExpectedMomentResponseItem[] }>(`/stages/${encodeURIComponent(idStage)}/expected-moments/reorder`, { moments });
}

export interface UpdateObjectiveRequest {
  Descripcion?: string;
  TipoEvidencia?: string;
  EsCritico?: boolean;
  Peso?: number;
  CondicionExito?: string;
}

/** Edits one objective's text/flags in place. See Objective_Update (PATCH /objectives/{id}). */
export async function updateObjective(idObjective: string, rowVersion: string, request: UpdateObjectiveRequest): Promise<LabContentItem> {
  return labsApiPatch<LabContentItem>(`/objectives/${encodeURIComponent(idObjective)}`, rowVersion, request);
}

/** Only the fields relevant to "who's hiring / what's the job" content — the server accepts the
 * full LAB_Scenario field set (see UpdateScenarioRequest), this is the subset the Studio UI edits.
 * ALL of these fields are read by LabSimulationPromptBuilder and sent straight into the LIVE AI
 * agent's own instructions (not just shown to the participant) — this is exactly where full job
 * description / hiring context belongs so the agent actually knows about it during the call. */
export interface UpdateScenarioContentRequest {
  Descripcion?: string;
  ContextoParticipante?: string;
  ProblemaCentral?: string;
  ResultadoEsperado?: string;
  Supuestos?: string;
  InformacionNoRevelarAutomaticamente?: string;
  MensajeInicial?: string;
}

/** Edits the Scenario's own text (job description / situation framing). DRAFT-only — see
 * ScenarioService.UpdateAsync (throws ScenarioNotEditableException once approved/published).
 * See Scenario_Update (PATCH /scenarios/{id}). */
export async function updateScenario(idScenario: string, rowVersion: string, request: UpdateScenarioContentRequest): Promise<LabContentItem> {
  return labsApiPatch<LabContentItem>(`/scenarios/${encodeURIComponent(idScenario)}`, rowVersion, request);
}

/** The Scenarios LIST endpoint (Scenario_ListByVersion) only returns a trimmed
 * ScenarioListItemResponse (Nombre/Codigo/Estatus/...) — Descripcion, ContextoParticipante,
 * ProblemaCentral etc. are NEVER included there. Must fetch the full ScenarioResponse via
 * Scenario_GetById before populating an edit form. */
export async function getScenarioDetail(idScenario: string): Promise<LabContentItem> {
  return labsApiGet<LabContentItem>(`/scenarios/${encodeURIComponent(idScenario)}`);
}

/** APPROVED/PUBLISHED -> DRAFT — lets a Studio admin edit an already-published Scenario's
 * content again (e.g. job description fields) without recreating the Lab. Must be re-approved
 * and re-published afterward (see approveScenario/publishLabScenario). See Scenario_RevertToDraft
 * (POST /scenarios/{id}/revert-to-draft). */
export async function revertScenarioToDraft(idScenario: string, rowVersion: string): Promise<ScenarioResponse> {
  return labsApiPostIfMatch<ScenarioResponse>(`/scenarios/${encodeURIComponent(idScenario)}/revert-to-draft`, rowVersion);
}

/** Subset of UpdateSimulatedActorRequest the Studio UI edits — the actor's persona/company/what
 * they know, i.e. exactly where "who is hiring, for what role" content lives. Every one of these
 * fields is read verbatim by LabSimulationPromptBuilder and injected into the LIVE AI agent's own
 * instructions (not just displayed to the participant) — this is the agent's actual knowledge. */
export interface UpdateSimulatedActorContentRequest {
  Rol?: string;
  Descripcion?: string;
  Objetivo?: string;
  ContextoConocido?: string;
  BriefOculto?: string;
  InformacionPuedeRevelar?: string;
  InformacionNoRevelarAutomaticamente?: string;
  Objeciones?: string;
  Contradicciones?: string;
}

/** Edits one Simulated Actor's persona/context. DRAFT-only (same ETag/status rule as Scenario).
 * See SimulatedActor_Update (PATCH /simulated-actors/{id}). */
export async function updateSimulatedActor(idActor: string, rowVersion: string, request: UpdateSimulatedActorContentRequest): Promise<LabContentItem> {
  return labsApiPatch<LabContentItem>(`/simulated-actors/${encodeURIComponent(idActor)}`, rowVersion, request);
}

/** Same trimmed-list problem as getScenarioDetail — SimulatedActor_ListByScenario never returns
 * Rol/Descripcion/Objetivo/ContextoConocido/BriefOculto/etc., only fetch via GetById. */
export async function getSimulatedActorDetail(idActor: string): Promise<LabContentItem> {
  return labsApiGet<LabContentItem>(`/simulated-actors/${encodeURIComponent(idActor)}`);
}

// ── AI Lab Builder (AGE-LAB-001) ────────────────────────────────────────────
// See HumanOS.SimulationLabs.Api Features/AiLabBuilder/Contracts. Generates a
// reusable Lab draft — nothing is persisted by this call.

/** Mirrors HumanOS.SimulationLabs.Entities.LabArquetipos / LabArchetypes (server-side). */
export interface LabArchetypeOption {
  Code: string;
  LabelEn: string;
  LabelEs: string;
  DescriptionEn: string;
  DescriptionEs: string;
}

export const LAB_ARCHETYPES: LabArchetypeOption[] = [
  { Code: 'JOB_INTERVIEW', LabelEn: 'Job Interview', LabelEs: 'Entrevista de trabajo', DescriptionEn: 'You play the interviewer assessing a candidate.', DescriptionEs: 'Interpretas al entrevistador evaluando a un candidato.' },
  { Code: 'CLIENT_NEGOTIATION', LabelEn: 'Client Negotiation', LabelEs: 'Negociación con cliente', DescriptionEn: 'You play a client/counterpart negotiating terms, price, or scope.', DescriptionEs: 'Interpretas a un cliente/contraparte negociando términos, precio o alcance.' },
  { Code: 'PERFORMANCE_REVIEW', LabelEn: 'Performance Review', LabelEs: 'Evaluación de desempeño', DescriptionEn: 'You play an employee receiving a performance review from the student.', DescriptionEs: 'Interpretas a un empleado recibiendo una evaluación de desempeño del estudiante.' },
  { Code: 'CONFLICT_RESOLUTION', LabelEn: 'Conflict Resolution', LabelEs: 'Resolución de conflictos', DescriptionEn: 'You play one party in a workplace conflict the student is mediating.', DescriptionEs: 'Interpretas a una de las partes en un conflicto laboral que el estudiante está mediando.' },
  { Code: 'SALES_DISCOVERY', LabelEn: 'Sales Discovery', DescriptionEn: 'You play a prospect being interviewed by the student to uncover needs.', LabelEs: 'Descubrimiento de ventas', DescriptionEs: 'Interpretas a un prospecto siendo entrevistado por el estudiante para descubrir necesidades.' },
  { Code: 'DIFFICULT_FEEDBACK', LabelEn: 'Difficult Feedback', LabelEs: 'Retroalimentación difícil', DescriptionEn: 'You play someone receiving hard/critical feedback from the student.', DescriptionEs: 'Interpretas a alguien recibiendo retroalimentación dura/crítica del estudiante.' },
  { Code: 'ONBOARDING', LabelEn: 'Onboarding', LabelEs: 'Incorporación (onboarding)', DescriptionEn: 'You play a new hire being onboarded/trained by the student.', DescriptionEs: 'Interpretas a un nuevo empleado siendo incorporado/entrenado por el estudiante.' },
  { Code: 'ANGRY_CUSTOMER', LabelEn: 'Angry Customer', LabelEs: 'Cliente enojado', DescriptionEn: 'You play an upset customer the student must de-escalate and help.', DescriptionEs: 'Interpretas a un cliente molesto que el estudiante debe calmar y ayudar.' },
  { Code: 'EXECUTIVE_PITCH', LabelEn: 'Executive Pitch', LabelEs: 'Pitch ejecutivo', DescriptionEn: 'You play a skeptical executive/stakeholder the student is pitching to.', DescriptionEs: 'Interpretas a un ejecutivo/stakeholder escéptico a quien el estudiante presenta un pitch.' },
  { Code: 'CAREER_MENTORING', LabelEn: 'Career Mentoring', LabelEs: 'Mentoría de carrera', DescriptionEn: 'You play a mentee seeking career guidance from the student.', DescriptionEs: 'Interpretas a un mentoreado buscando orientación de carrera del estudiante.' },
  { Code: 'CUSTOMER_SUPPORT', LabelEn: 'Customer Support', LabelEs: 'Atención al cliente', DescriptionEn: 'You play a customer contacting support with any kind of issue for the student to resolve.', DescriptionEs: 'Interpretas a un cliente que contacta a soporte con cualquier tipo de problema para que el estudiante lo resuelva.' },
  { Code: 'REQUIREMENTS_GATHERING', LabelEn: 'Requirements Gathering', LabelEs: 'Levantamiento de requisitos', DescriptionEn: 'You play a stakeholder/client whose needs the student must uncover through questions.', DescriptionEs: 'Interpretas a un stakeholder/cliente cuyas necesidades el estudiante debe descubrir mediante preguntas.' },
  { Code: 'BUSINESS_REPORT_UPDATE', LabelEn: 'Business Update / Report to Manager', LabelEs: 'Reporte / actualización al jefe', DescriptionEn: 'You play the student\'s manager/boss receiving a status update or report the student is presenting.', DescriptionEs: 'Interpretas al jefe/gerente del estudiante recibiendo una actualización o reporte que el estudiante presenta.' },
  { Code: 'TEACHING_PROCESS', LabelEn: 'Teaching Process', LabelEs: 'Proceso de enseñanza', DescriptionEn: 'You play a learner being taught a topic, process, or skill by the student (acting as instructor/trainer).', DescriptionEs: 'Interpretas a un alumno al que el estudiante (actuando como instructor/capacitador) le enseña un tema, proceso o habilidad.' },
  { Code: 'ACADEMIC_DEFENSE', LabelEn: 'Academic Defense (Thesis/Exam)', LabelEs: 'Defensa académica (tesis/examen)', DescriptionEn: 'You play an academic examiner/committee member questioning the student about a document they submitted (thesis, report, project).', DescriptionEs: 'Interpretas a un sinodal/examinador académico cuestionando al estudiante sobre un documento que entregó (tesis, reporte, proyecto).' },
];

export interface SkillToEvaluate {
  SkillName: string;
  SkillType?: 'TECHNICAL' | 'SOFT';
  /** Optional — when omitted, the AI Lab Builder agent itself writes the client's line/question
   * and the exemplary response for this skill's dialogue moment, and derives the rubric from that. */
  WhatGoodLooksLike?: string;
}

export interface GenerateLabDraftRequest {
  LabName: string;
  TargetRole: string;
  LabObjective: string;
  /** One of LAB_ARCHETYPES codes (e.g. 'JOB_INTERVIEW'). Optional — server falls back to CLIENT_NEGOTIATION. */
  Archetype?: string;
  ProcessName: string;
  ProcessContent: string;
  /** Original file name of an uploaded process PDF; mutually exclusive with a manually typed ProcessContent. */
  ProcessFileName?: string;
  /** Base64-encoded bytes of the uploaded process PDF — the server extracts its text and fills ProcessContent. */
  ProcessContentBase64?: string;
  SkillsToEvaluate: SkillToEvaluate[];
  ScenarioDescription: string;
  /** Optional human-authored context about the company/organization behind the scenario — saved verbatim, never rewritten by the agent. */
  CompanyContext?: string;
  LabLanguage: string;
  Difficulty: 'BEGINNER' | 'INTERMEDIATE' | 'ADVANCED';
  DurationMinutes: number;
  DialogueCount: number;
}

export interface ActorDraft {
  Ref: string;
  Role: string;
  CommunicationStyle: string;
  IsPrincipal: boolean;
  Description: string;
}

export interface StageDraft {
  Ref: string;
  Order: number;
  Name: string;
  Description: string;
}

export interface ObjectiveDraft {
  Ref: string;
  StageRef: string;
  Description: string;
  Weight: number;
  IsCritical: boolean;
}

export interface DialogueDraft {
  Ref: string;
  StageRef: string;
  ObjectiveRef: string;
  ActorRef: string;
  Order: number;
  ActorSays: string;
  ExpectedIntent: string;
  ExemplaryResponse: string;
  FrequentError: string;
  Recommendation: string;
  SkillEvaluated: string;
  IsCritical: boolean;
}

export interface CriterionDraft {
  Ref: string;
  ObjectiveRef: string;
  MomentRef: string | null;
  Name: string;
  Description: string;
  Weight: number;
  MinScore: number;
  PositiveIndicators: string;
  NegativeIndicators: string;
  IsCritical: boolean;
}

export interface TestedSkillDraft {
  SkillName: string;
  SkillType: 'TECHNICAL' | 'SOFT';
  RelevanceToRole: string;
  DemonstrationStandard: string;
  TestedInMoments: string[];
  RubricCriterionRef?: string;
  FeedbackGuidance: string;
}

export interface LabDraftContent {
  /** Only set by the agent when the caller left LabName empty in the request. */
  SuggestedLabName?: string;
  LabDescription: string;
  ScenarioProblemaCentral: string;
  ScenarioResultadoEsperado: string;
  TestedSkills?: TestedSkillDraft[];
  Actors: ActorDraft[];
  Stages: StageDraft[];
  Objectives: ObjectiveDraft[];
  Dialogues: DialogueDraft[];
  Criteria: CriterionDraft[];
}

export interface GenerateLabDraftResponse {
  Status: 'DRAFT_READY_FOR_REVIEW' | 'NEEDS_INFORMATION';
  LabName?: string;
  TargetRole?: string;
  Language?: string;
  Difficulty?: string;
  ScoreMinimo?: number;
  Draft?: LabDraftContent;
  Questions: string[];
  Warnings: string[];
  HumanReviewRequired: boolean;
  CorrelationId: string;
}

export async function generateLabDraft(request: GenerateLabDraftRequest): Promise<GenerateLabDraftResponse> {
  return labsApiPost<GenerateLabDraftResponse>('/ai-lab-builder/generate-draft', request);
}

export interface SaveLabDraftRequest {
  LabName?: string;
  LabCodigo?: string;
  TargetRole?: string;
  /** One of LAB_ARCHETYPES codes (e.g. 'JOB_INTERVIEW'). Optional — server falls back to CLIENT_NEGOTIATION. */
  Archetype?: string;
  ScenarioDescription?: string;
  CompanyContext?: string;
  Dominio?: string;
  Difficulty?: string;
  DurationMinutes?: number;
  ScoreMinimo?: number;
  Language?: string;
  Draft: LabDraftContent;
}

export interface SaveLabDraftResponse {
  IdLab: string;
  IdVersion: string;
  Codigo: string;
  Nombre: string;
  Estatus: string;
  StagesSaved: number;
  ObjectivesSaved: number;
  MomentsSaved: number;
  CriteriaSaved: number;
  ActorsSaved: number;
  SkillsRegistered: number;
  Message: string;
}

export async function saveLabDraft(request: SaveLabDraftRequest): Promise<SaveLabDraftResponse> {
  return labsApiPost<SaveLabDraftResponse>('/ai-lab-builder/save-draft', request);
}
