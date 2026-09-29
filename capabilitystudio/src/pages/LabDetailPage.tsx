import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, ArrowDown, ArrowUp, FlaskConical, Headphones, Drama, Rocket, Loader2 } from 'lucide-react';
import {
  getLab,
  getLabContent,
  getScenarioDetail,
  getSimulatedActorDetail,
  reorderExpectedMoments,
  updateExpectedMoment,
  updateObjective,
  updateScenario,
  updateSimulatedActor,
  publishLabScenario,
  revertScenarioToDraft,
  LAB_ARCHETYPES,
  type LabContent,
  type LabContentItem,
  type LabDetail,
  type TestedSkill,
  type TestedSkillEntity,
} from '../lib/api/labsApi';
import LabSimulationVoiceModal from '../components/LabSimulationVoiceModal';

export default function LabDetailPage() {
  const { i18n } = useTranslation();
  const isEs = i18n.language.startsWith('es');
  const { labId } = useParams<{ labId: string }>();
  const [lab, setLab] = useState<LabDetail | null>(null);
  const [content, setContent] = useState<LabContent | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showSimulationModal, setShowSimulationModal] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);

  useEffect(() => {
    if (!labId) return;
    Promise.all([getLab(labId), getLabContent(labId)])
      .then(([detail, labContent]) => { setLab(detail); setContent(labContent); })
      .catch((err: Error) => setError(err.message));
  }, [labId]);

  async function handlePublish() {
    const scenario = content?.Scenarios[0];
    if (!scenario?.IdScenario || publishing) return;
    const confirmMessage = isEs
      ? 'Esto publicará el escenario y lo hará disponible para que empleados reales inicien intentos de examen. ¿Continuar?'
      : 'This will publish the scenario, making it available for real employees to start exam attempts. Continue?';
    if (!window.confirm(confirmMessage)) return;
    setPublishing(true);
    setPublishError(null);
    try {
      const published = await publishLabScenario(String(scenario.IdScenario), String(scenario.RowVersion ?? ''));
      setContent({
        ...content!,
        Scenarios: content!.Scenarios.map((s) => (s.IdScenario === scenario.IdScenario ? { ...s, Estatus: published.Estatus, RowVersion: published.RowVersion } : s)),
      });
    } catch (err) {
      setPublishError(err instanceof Error ? err.message : String(err));
    } finally {
      setPublishing(false);
    }
  }

  return (
    <div className="mx-auto max-w-3xl px-4 py-8 sm:px-6">
      <Link to="/labs" className="inline-flex items-center gap-2 text-sm text-slate-400 hover:text-white">
        <ArrowLeft className="h-4 w-4" /> {isEs ? 'Volver a Labs' : 'Back to Labs'}
      </Link>
      {error && <p className="mt-8 text-sm text-red-400">{error}</p>}
      {!error && !lab && <p className="mt-8 text-sm text-slate-400">{isEs ? 'Cargando lab...' : 'Loading lab...'}</p>}
      {lab && (
        <section className="mt-8 rounded-2xl border border-white/10 bg-slate-900/60 p-6">
          <div className="flex items-start justify-between gap-4">
            <div className="flex items-start gap-3">
              <FlaskConical className="mt-1 h-6 w-6 text-teal-400" />
              <div>
                <h1 className="text-xl font-semibold text-white">{lab.Nombre}</h1>
                <p className="mt-1 text-sm text-slate-400">{lab.Codigo} · {lab.Dominio}</p>
              </div>
            </div>
            <span className="rounded-full bg-slate-500/15 px-3 py-1 text-xs font-medium text-slate-300">{lab.Estatus}</span>
          </div>
          {(() => {
            const archetype = LAB_ARCHETYPES.find((a) => a.Code === lab.Arquetipo) ?? LAB_ARCHETYPES.find((a) => a.Code === 'CLIENT_NEGOTIATION');
            if (!archetype) return null;
            const label = isEs ? archetype.LabelEs : archetype.LabelEn;
            const description = isEs ? archetype.DescriptionEs : archetype.DescriptionEn;
            return (
              <div className="mt-4 flex items-start gap-2 rounded-xl border border-purple-400/20 bg-purple-400/5 p-3">
                <Drama className="mt-0.5 h-4 w-4 shrink-0 text-purple-300" />
                <div>
                  <p className="text-sm font-semibold text-purple-200">
                    {isEs ? 'Tipo de simulación' : 'Simulation type'}: {label}
                  </p>
                  <p className="mt-0.5 text-xs text-slate-400">{description}</p>
                </div>
              </div>
            );
          })()}
          {content?.Version && content.Actors.length > 0 && (
            <button
              type="button"
              onClick={() => setShowSimulationModal(true)}
              className="mt-4 inline-flex items-center gap-2 rounded-lg bg-teal-500 px-3 py-2 text-sm font-medium text-slate-950 hover:bg-teal-400"
            >
              <Headphones className="h-4 w-4" /> {isEs ? 'Probar simulación' : 'Test simulation'}
            </button>
          )}
          <p className="mt-6 text-sm text-slate-300">{lab.Descripcion}</p>
          {lab.Estatus === 'DRAFT' && (() => {
            const scenario = content?.Scenarios[0];
            const scenarioEstatus = String(scenario?.Estatus ?? 'DRAFT');
            if (scenarioEstatus === 'PUBLISHED') {
              return (
                <p className="mt-6 rounded-xl border border-emerald-400/20 bg-emerald-400/10 p-4 text-sm text-emerald-200">
                  {isEs
                    ? 'Este escenario ya está publicado — los empleados ya pueden iniciar intentos reales.'
                    : 'This scenario is already published — employees can already start real attempts.'}
                </p>
              );
            }
            return (
              <div className="mt-6 space-y-3 rounded-xl border border-amber-400/20 bg-amber-400/10 p-4">
                <p className="text-sm text-amber-200">
                  {isEs
                    ? 'Este lab está guardado como borrador. Publicálo para que los empleados puedan iniciar intentos reales del examen.'
                    : 'This lab is saved as a draft. Publish it so employees can start real exam attempts.'}
                </p>
                <button
                  type="button"
                  onClick={handlePublish}
                  disabled={publishing || !scenario}
                  className="inline-flex items-center gap-2 rounded-lg bg-amber-500 px-3 py-2 text-sm font-medium text-slate-950 hover:bg-amber-400 disabled:opacity-60"
                >
                  {publishing ? <Loader2 className="h-4 w-4 animate-spin" /> : <Rocket className="h-4 w-4" />}
                  {isEs ? 'Publicar lab' : 'Publish lab'}
                </button>
                {publishError && <p className="text-sm text-red-400">{publishError}</p>}
              </div>
            );
          })()}
          {content?.Version && (
            <div className="mt-8 space-y-6 border-t border-white/10 pt-6">
              <div>
                <h2 className="text-lg font-semibold text-white">{isEs ? 'Contenido del Lab' : 'Lab content'}</h2>
                <p className="mt-1 text-sm text-slate-400">
                  {isEs ? 'Versión' : 'Version'} {content.Version.NumeroVersion} · {content.Version.DuracionMinutos} {isEs ? 'minutos' : 'minutes'} · {isEs ? 'mínimo' : 'minimum'} {content.Version.ScoreMinimo}
                </p>
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <ContentSection title={isEs ? 'Objetivo general' : 'General objective'} text={content.Version.ObjetivoGeneral} isEs={isEs} />
                <ContentSection title={isEs ? 'Instrucciones para el participante' : 'Instructions for the participant'} text={content.Version.InstruccionesParticipante} isEs={isEs} />
              </div>
              <ContentList title={isEs ? 'Etapas' : 'Stages'} items={content.Stages} isEs={isEs} />
              {lab.Estatus === 'DRAFT' ? (
                <EditableObjectiveList
                  items={content.Objectives}
                  isEs={isEs}
                  onObjectivesChange={(updated) => setContent({ ...content, Objectives: updated })}
                />
              ) : (
                <ContentList title={isEs ? 'Objetivos evaluables' : 'Evaluable objectives'} items={content.Objectives} isEs={isEs} />
              )}
              {lab.Estatus === 'DRAFT' ? (
                <EditableMomentList
                  items={content.Moments}
                  stages={content.Stages}
                  skillsByName={content.SkillsByName}
                  isEs={isEs}
                  onMomentsChange={(updated) => setContent({ ...content, Moments: updated })}
                />
              ) : (
                <QuestionList items={content.Moments} skillsByName={content.SkillsByName} isEs={isEs} />
              )}
              {lab.Estatus === 'DRAFT' ? (
                <EditableScenarioList
                  items={content.Scenarios}
                  isEs={isEs}
                  onScenariosChange={(updated) => setContent({ ...content, Scenarios: updated })}
                />
              ) : (
                <ContentList title={isEs ? 'Escenarios' : 'Scenarios'} items={content.Scenarios} isEs={isEs} />
              )}
              {lab.Estatus === 'DRAFT' ? (
                <EditableActorList
                  items={content.Actors}
                  isEs={isEs}
                  onActorsChange={(updated) => setContent({ ...content, Actors: updated })}
                />
              ) : (
                <ContentList title={isEs ? 'Actores simulados' : 'Simulated actors'} items={content.Actors} isEs={isEs} />
              )}
              <TestedSkillsSection items={content.TestedSkills} isEs={isEs} />
              <ContentList title={isEs ? 'Criterios de rúbrica' : 'Rubric criteria'} items={content.Criteria} isEs={isEs} />
            </div>
          )}
        </section>
      )}
      {showSimulationModal && labId && content && (
        <LabSimulationVoiceModal labId={labId} content={content} onClose={() => setShowSimulationModal(false)} />
      )}
    </div>
  );
}

/** DRAFT-only editor for LAB_Objective descriptions/flags — previously read-only via ContentList. */
function EditableObjectiveList({
  items,
  isEs,
  onObjectivesChange,
}: {
  items: LabContentItem[];
  isEs: boolean;
  onObjectivesChange: (updated: LabContentItem[]) => void;
}) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState<{ Descripcion: string; CondicionExito: string }>({ Descripcion: '', CondicionExito: '' });
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  function startEdit(item: LabContentItem) {
    setEditingId(String(item.IdObjective));
    setDraft({
      Descripcion: String(item.Descripcion ?? ''),
      CondicionExito: String(item.CondicionExito ?? ''),
    });
  }

  async function saveEdit(item: LabContentItem) {
    setSaving(true);
    setSaveError(null);
    try {
      const updated = await updateObjective(String(item.IdObjective), String(item.RowVersion ?? ''), draft);
      onObjectivesChange(items.map((o) => (o.IdObjective === item.IdObjective ? { ...o, ...updated } : o)));
      setEditingId(null);
    } catch (err) {
      setSaveError((err as Error).message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <section>
      <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-400">{isEs ? 'Objetivos evaluables' : 'Evaluable objectives'} ({items.length})</h3>
      {saveError && <p className="mt-2 rounded-lg bg-red-500/10 p-2 text-xs text-red-300 border border-red-500/30">{saveError}</p>}
      <div className="mt-2 space-y-2">
        {items.length === 0 && <p className="text-sm text-slate-500">{isEs ? 'Sin registros.' : 'No records.'}</p>}
        {items.map((item, index) => {
          const isEditing = editingId === String(item.IdObjective);
          return (
            <div key={String(item.IdObjective ?? index)} className="rounded-lg border border-white/10 bg-slate-950/30 p-3">
              {isEditing ? (
                <div className="space-y-2">
                  <textarea
                    value={draft.Descripcion}
                    onChange={(e) => setDraft({ ...draft, Descripcion: e.target.value })}
                    rows={2}
                    className="w-full rounded border border-white/10 bg-slate-950/60 p-2 text-sm text-slate-200"
                    placeholder={isEs ? 'Descripción del objetivo' : 'Objective description'}
                  />
                  <textarea
                    value={draft.CondicionExito}
                    onChange={(e) => setDraft({ ...draft, CondicionExito: e.target.value })}
                    rows={2}
                    className="w-full rounded border border-white/10 bg-slate-950/60 p-2 text-sm text-slate-200"
                    placeholder={isEs ? 'Condición de éxito' : 'Success condition'}
                  />
                  <div className="flex justify-end gap-2">
                    <button type="button" disabled={saving} onClick={() => setEditingId(null)} className="rounded border border-white/10 px-2 py-1 text-xs text-slate-300 hover:bg-white/10">{isEs ? 'Cancelar' : 'Cancel'}</button>
                    <button type="button" disabled={saving} onClick={() => saveEdit(item)} className="rounded border border-teal-400/30 px-2 py-1 text-xs text-teal-300 hover:bg-teal-400/10">{isEs ? 'Guardar' : 'Save'}</button>
                  </div>
                </div>
              ) : (
                <>
                  <div className="flex items-start justify-between gap-3">
                    <p className="text-sm font-medium text-white">{String(item.Nombre ?? item.Codigo ?? `${isEs ? 'Objetivo' : 'Objective'} ${index + 1}`)}</p>
                    <button type="button" onClick={() => startEdit(item)} className="shrink-0 rounded border border-teal-400/30 px-2 py-1 text-xs text-teal-300 hover:bg-teal-400/10">{isEs ? 'Editar' : 'Edit'}</button>
                  </div>
                  {Boolean(item.Descripcion) && <p className="mt-1 whitespace-pre-wrap text-sm text-slate-400">{String(item.Descripcion)}</p>}
                  {Boolean(item.CondicionExito) && <p className="mt-1 whitespace-pre-wrap text-xs text-slate-500">{isEs ? 'Condición de éxito' : 'Success condition'}: {String(item.CondicionExito)}</p>}
                </>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
}

function ContentSection({ title, text, isEs }: { title: string; text: string; isEs: boolean }) {
  return <div className="rounded-xl border border-white/10 bg-slate-950/40 p-4"><h3 className="text-sm font-medium text-teal-300">{title}</h3><p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-slate-300">{text || (isEs ? 'Sin información.' : 'No information.')}</p></div>;
}

/** DRAFT-only editor for LAB_Scenario — this is where "job description / hiring situation"
 * content actually lives (Descripcion, ContextoParticipante, ProblemaCentral, ResultadoEsperado,
 * Supuestos = company context, InformacionNoRevelarAutomaticamente, MensajeInicial = opening
 * line). Previously read-only via ContentList. IMPORTANT: every one of these fields is read
 * verbatim by LabSimulationPromptBuilder.BuildInstructions and injected into the LIVE AI agent's
 * own system prompt — they are NOT participant-only text, this is the agent's actual knowledge
 * of the job/situation during the real-time conversation. */
function EditableScenarioList({
  items,
  isEs,
  onScenariosChange,
}: {
  items: LabContentItem[];
  isEs: boolean;
  onScenariosChange: (updated: LabContentItem[]) => void;
}) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState<{ Descripcion: string; ContextoParticipante: string; ProblemaCentral: string; ResultadoEsperado: string; Supuestos: string; InformacionNoRevelarAutomaticamente: string; MensajeInicial: string }>({
    Descripcion: '', ContextoParticipante: '', ProblemaCentral: '', ResultadoEsperado: '', Supuestos: '', InformacionNoRevelarAutomaticamente: '', MensajeInicial: '',
  });
  const [saving, setSaving] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [reverting, setReverting] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  /** Approved/Published scenarios reject Scenario_Update with 422 ("El escenario no está en
   * DRAFT") — move it back to DRAFT first so job-description edits can actually be saved. Must
   * be re-approved/re-published afterward (see the Publish button elsewhere on this page). */
  async function revertToDraft(item: LabContentItem) {
    if (!window.confirm(isEs
      ? 'Esto regresa el escenario a borrador. Los empleados no podrán iniciar nuevos intentos hasta que lo publiques de nuevo. ¿Continuar?'
      : 'This reverts the scenario to draft. Employees won\'t be able to start new attempts until you publish it again. Continue?')) return;
    setReverting(true);
    setSaveError(null);
    try {
      const updated = await revertScenarioToDraft(String(item.IdScenario), String(item.RowVersion ?? ''));
      onScenariosChange(items.map((s) => (s.IdScenario === item.IdScenario ? { ...s, Estatus: updated.Estatus, RowVersion: updated.RowVersion } : s)));
    } catch (err) {
      setSaveError((err as Error).message);
    } finally {
      setReverting(false);
    }
  }

  /** The list endpoint (Scenario_ListByVersion) never returns Descripcion/ContextoParticipante/
   * etc — fetch the full ScenarioResponse via GetById first, otherwise the form opens empty. */
  async function startEdit(item: LabContentItem) {
    setEditingId(String(item.IdScenario));
    setLoadingDetail(true);
    setSaveError(null);
    try {
      const full = await getScenarioDetail(String(item.IdScenario));
      setDraft({
        Descripcion: String(full.Descripcion ?? ''),
        ContextoParticipante: String(full.ContextoParticipante ?? ''),
        ProblemaCentral: String(full.ProblemaCentral ?? ''),
        ResultadoEsperado: String(full.ResultadoEsperado ?? ''),
        Supuestos: String(full.Supuestos ?? ''),
        InformacionNoRevelarAutomaticamente: String(full.InformacionNoRevelarAutomaticamente ?? ''),
        MensajeInicial: String(full.MensajeInicial ?? ''),
      });
      onScenariosChange(items.map((s) => (s.IdScenario === item.IdScenario ? { ...s, ...full } : s)));
    } catch (err) {
      setSaveError((err as Error).message);
    } finally {
      setLoadingDetail(false);
    }
  }

  async function saveEdit(item: LabContentItem) {
    setSaving(true);
    setSaveError(null);
    try {
      const updated = await updateScenario(String(item.IdScenario), String(item.RowVersion ?? ''), draft);
      onScenariosChange(items.map((s) => (s.IdScenario === item.IdScenario ? { ...s, ...updated } : s)));
      setEditingId(null);
    } catch (err) {
      setSaveError((err as Error).message);
    } finally {
      setSaving(false);
    }
  }

  const fields: Array<{ key: keyof typeof draft; labelEn: string; labelEs: string }> = [
    { key: 'Descripcion', labelEn: 'Scenario description (sent to the AI agent)', labelEs: 'Descripción del escenario (se envía al agente de IA)' },
    { key: 'ContextoParticipante', labelEn: 'Job description / hiring situation — shared context both the participant AND the AI agent use', labelEs: 'Descripción del puesto / situación de contratación — contexto compartido que usan tanto el participante COMO el agente de IA' },
    { key: 'ProblemaCentral', labelEn: 'Central problem / what is being hired for (sent to the AI agent)', labelEs: 'Problema central / para qué se está contratando (se envía al agente de IA)' },
    { key: 'ResultadoEsperado', labelEn: 'Expected outcome', labelEs: 'Resultado esperado' },
    { key: 'Supuestos', labelEn: 'Company context / assumptions (sent to the AI agent)', labelEs: 'Contexto de la empresa / supuestos (se envía al agente de IA)' },
    { key: 'InformacionNoRevelarAutomaticamente', labelEn: "Info the AI agent knows but must NOT reveal unless asked directly", labelEs: 'Información que el agente de IA sabe pero NO debe revelar salvo que se le pregunte directamente' },
    { key: 'MensajeInicial', labelEn: "Actor's opening line", labelEs: 'Frase de apertura del actor' },
  ];

  return (
    <section>
      <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-400">{isEs ? 'Escenarios' : 'Scenarios'} ({items.length})</h3>
      {saveError && <p className="mt-2 rounded-lg bg-red-500/10 p-2 text-xs text-red-300 border border-red-500/30">{saveError}</p>}
      <div className="mt-2 space-y-2">
        {items.length === 0 && <p className="text-sm text-slate-500">{isEs ? 'Sin registros.' : 'No records.'}</p>}
        {items.map((item, index) => {
          const isEditing = editingId === String(item.IdScenario);
          return (
            <div key={String(item.IdScenario ?? index)} className="rounded-lg border border-white/10 bg-slate-950/30 p-3">
              <div className="flex items-start justify-between gap-3">
                <p className="text-sm font-medium text-white">{String(item.Nombre ?? item.Codigo ?? `${isEs ? 'Escenario' : 'Scenario'} ${index + 1}`)}</p>
                {!isEditing && (
                  <div className="flex shrink-0 items-center gap-2">
                    {String(item.Estatus ?? '') !== 'DRAFT' && (
                      <button type="button" disabled={reverting} onClick={() => revertToDraft(item)} className="rounded border border-amber-400/30 px-2 py-1 text-xs text-amber-300 hover:bg-amber-400/10 disabled:opacity-60">{isEs ? 'Revertir a borrador' : 'Revert to draft'}</button>
                    )}
                    <button type="button" onClick={() => startEdit(item)} className="rounded border border-teal-400/30 px-2 py-1 text-xs text-teal-300 hover:bg-teal-400/10">{isEs ? 'Editar' : 'Edit'}</button>
                  </div>
                )}
              </div>
              {isEditing ? (
                loadingDetail ? (
                  <p className="mt-2 text-xs text-slate-500">{isEs ? 'Cargando…' : 'Loading…'}</p>
                ) : (
                <div className="mt-2 space-y-2">
                  {fields.map((f) => (
                    <div key={f.key}>
                      <label className="block text-[11px] font-semibold uppercase tracking-wider text-slate-400">{isEs ? f.labelEs : f.labelEn}</label>
                      <textarea
                        value={draft[f.key]}
                        onChange={(e) => setDraft({ ...draft, [f.key]: e.target.value })}
                        rows={2}
                        className="mt-1 w-full rounded border border-white/10 bg-slate-950/60 p-2 text-sm text-slate-200"
                      />
                    </div>
                  ))}
                  <div className="flex justify-end gap-2">
                    <button type="button" disabled={saving} onClick={() => setEditingId(null)} className="rounded border border-white/10 px-2 py-1 text-xs text-slate-300 hover:bg-white/10">{isEs ? 'Cancelar' : 'Cancel'}</button>
                    <button type="button" disabled={saving} onClick={() => saveEdit(item)} className="rounded border border-teal-400/30 px-2 py-1 text-xs text-teal-300 hover:bg-teal-400/10">{isEs ? 'Guardar' : 'Save'}</button>
                  </div>
                </div>
                )
              ) : (
                <>
                  {Boolean(item.Descripcion) && <p className="mt-1 whitespace-pre-wrap text-sm text-slate-400">{String(item.Descripcion)}</p>}
                  {Boolean(item.ContextoParticipante) && <p className="mt-1 whitespace-pre-wrap text-xs text-slate-500">{isEs ? 'Contexto del participante: ' : 'Participant context: '}{String(item.ContextoParticipante)}</p>}
                </>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
}

/** DRAFT-only editor for LAB_SimulatedActor — the hiring manager/interviewer persona: role,
 * company, what they already know about the job/candidate. Previously read-only via ContentList.
 * EVERY field below is read verbatim by LabSimulationPromptBuilder.BuildInstructions and injected
 * into the LIVE AI agent's own system prompt — this (plus the Scenario fields above) is the
 * agent's actual, complete knowledge of the job it's hiring for during the real-time call. */
function EditableActorList({
  items,
  isEs,
  onActorsChange,
}: {
  items: LabContentItem[];
  isEs: boolean;
  onActorsChange: (updated: LabContentItem[]) => void;
}) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState<{ Rol: string; Descripcion: string; Objetivo: string; ContextoConocido: string; BriefOculto: string; InformacionPuedeRevelar: string; InformacionNoRevelarAutomaticamente: string; Objeciones: string; Contradicciones: string }>({
    Rol: '', Descripcion: '', Objetivo: '', ContextoConocido: '', BriefOculto: '', InformacionPuedeRevelar: '', InformacionNoRevelarAutomaticamente: '', Objeciones: '', Contradicciones: '',
  });
  const [saving, setSaving] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  /** The list endpoint (SimulatedActor_ListByScenario) never returns Rol/Descripcion/Objetivo/
   * ContextoConocido/etc — fetch the full SimulatedActorResponse via GetById first, otherwise the
   * form opens empty. */
  async function startEdit(item: LabContentItem) {
    setEditingId(String(item.IdActor));
    setLoadingDetail(true);
    setSaveError(null);
    try {
      const full = await getSimulatedActorDetail(String(item.IdActor));
      setDraft({
        Rol: String(full.Rol ?? ''),
        Descripcion: String(full.Descripcion ?? ''),
        Objetivo: String(full.Objetivo ?? ''),
        ContextoConocido: String(full.ContextoConocido ?? ''),
        BriefOculto: String(full.BriefOculto ?? ''),
        InformacionPuedeRevelar: String(full.InformacionPuedeRevelar ?? ''),
        InformacionNoRevelarAutomaticamente: String(full.InformacionNoRevelarAutomaticamente ?? ''),
        Objeciones: String(full.Objeciones ?? ''),
        Contradicciones: String(full.Contradicciones ?? ''),
      });
      onActorsChange(items.map((a) => (a.IdActor === item.IdActor ? { ...a, ...full } : a)));
    } catch (err) {
      setSaveError((err as Error).message);
    } finally {
      setLoadingDetail(false);
    }
  }

  async function saveEdit(item: LabContentItem) {
    setSaving(true);
    setSaveError(null);
    try {
      const updated = await updateSimulatedActor(String(item.IdActor), String(item.RowVersion ?? ''), draft);
      onActorsChange(items.map((a) => (a.IdActor === item.IdActor ? { ...a, ...updated } : a)));
      setEditingId(null);
    } catch (err) {
      setSaveError((err as Error).message);
    } finally {
      setSaving(false);
    }
  }

  const fields: Array<{ key: keyof typeof draft; labelEn: string; labelEs: string }> = [
    { key: 'Rol', labelEn: 'Role (e.g. "Hiring Manager (Acme Corp)") — sent to the AI agent', labelEs: 'Rol (ej. "Gerente de Contratación (Acme Corp)") — se envía al agente de IA' },
    { key: 'Descripcion', labelEn: 'Description — sent to the AI agent', labelEs: 'Descripción — se envía al agente de IA' },
    { key: 'Objetivo', labelEn: 'Objective in this conversation — sent to the AI agent', labelEs: 'Objetivo en esta conversación — se envía al agente de IA' },
    { key: 'ContextoConocido', labelEn: 'Full job description / what the AI agent already knows (title, responsibilities, requirements, company — this drives the live conversation)', labelEs: 'Descripción completa del puesto / lo que el agente de IA ya sabe (título, responsabilidades, requisitos, empresa — esto guía la conversación en vivo)' },
    { key: 'BriefOculto', labelEn: "Hidden brief — only the agent knows this, won't reveal unless asked directly", labelEs: 'Brief oculto — solo el agente lo sabe, no lo revela salvo que se le pregunte directamente' },
    { key: 'InformacionPuedeRevelar', labelEn: 'Information they may reveal', labelEs: 'Información que puede revelar' },
    { key: 'InformacionNoRevelarAutomaticamente', labelEn: 'Info the agent must NOT reveal automatically, only if asked directly', labelEs: 'Información que el agente NO debe revelar automáticamente, solo si se le pregunta directamente' },
    { key: 'Objeciones', labelEn: 'Objections the agent can raise', labelEs: 'Objeciones que el agente puede plantear' },
    { key: 'Contradicciones', labelEn: 'Contradictions the agent can introduce if relevant', labelEs: 'Contradicciones que el agente puede introducir si es relevante' },
  ];

  return (
    <section>
      <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-400">{isEs ? 'Actores simulados' : 'Simulated actors'} ({items.length})</h3>
      {saveError && <p className="mt-2 rounded-lg bg-red-500/10 p-2 text-xs text-red-300 border border-red-500/30">{saveError}</p>}
      <div className="mt-2 space-y-2">
        {items.length === 0 && <p className="text-sm text-slate-500">{isEs ? 'Sin registros.' : 'No records.'}</p>}
        {items.map((item, index) => {
          const isEditing = editingId === String(item.IdActor);
          return (
            <div key={String(item.IdActor ?? index)} className="rounded-lg border border-white/10 bg-slate-950/30 p-3">
              <div className="flex items-start justify-between gap-3">
                <p className="text-sm font-medium text-white">{String(item.Nombre ?? item.Codigo ?? `${isEs ? 'Actor' : 'Actor'} ${index + 1}`)} {Boolean(item.Rol) && <span className="font-normal text-slate-400">— {String(item.Rol)}</span>}</p>
                {!isEditing && (
                  <button type="button" onClick={() => startEdit(item)} className="shrink-0 rounded border border-teal-400/30 px-2 py-1 text-xs text-teal-300 hover:bg-teal-400/10">{isEs ? 'Editar' : 'Edit'}</button>
                )}
              </div>
              {isEditing ? (
                loadingDetail ? (
                  <p className="mt-2 text-xs text-slate-500">{isEs ? 'Cargando…' : 'Loading…'}</p>
                ) : (
                <div className="mt-2 space-y-2">
                  {fields.map((f) => (
                    <div key={f.key}>
                      <label className="block text-[11px] font-semibold uppercase tracking-wider text-slate-400">{isEs ? f.labelEs : f.labelEn}</label>
                      <textarea
                        value={draft[f.key]}
                        onChange={(e) => setDraft({ ...draft, [f.key]: e.target.value })}
                        rows={2}
                        className="mt-1 w-full rounded border border-white/10 bg-slate-950/60 p-2 text-sm text-slate-200"
                      />
                    </div>
                  ))}
                  <div className="flex justify-end gap-2">
                    <button type="button" disabled={saving} onClick={() => setEditingId(null)} className="rounded border border-white/10 px-2 py-1 text-xs text-slate-300 hover:bg-white/10">{isEs ? 'Cancelar' : 'Cancel'}</button>
                    <button type="button" disabled={saving} onClick={() => saveEdit(item)} className="rounded border border-teal-400/30 px-2 py-1 text-xs text-teal-300 hover:bg-teal-400/10">{isEs ? 'Guardar' : 'Save'}</button>
                  </div>
                </div>
                )
              ) : (
                <>
                  {Boolean(item.Descripcion) && <p className="mt-1 whitespace-pre-wrap text-sm text-slate-400">{String(item.Descripcion)}</p>}
                  {Boolean(item.ContextoConocido) && <p className="mt-1 whitespace-pre-wrap text-xs text-slate-500">{isEs ? 'Contexto conocido: ' : 'Known context: '}{String(item.ContextoConocido)}</p>}
                </>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
}

/** What the AI Lab Builder actually generated per evaluated skill (LAB_TestedSkill) — previously
 * invisible in this edit view since it only lived in an unreadable JSON blob. */
function TestedSkillsSection({ items, isEs }: { items: TestedSkillEntity[]; isEs: boolean }) {
  return (
    <section>
      <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-400">{isEs ? 'Skills evaluadas por el AI' : 'Skills evaluated by the AI'} ({items.length})</h3>
      <div className="mt-2 space-y-2">
        {items.length === 0 && <p className="text-sm text-slate-500">{isEs ? 'Sin registros.' : 'No records.'}</p>}
        {items.map((skill) => (
          <div key={skill.IdSkill} className="rounded-lg border border-purple-400/20 bg-purple-400/5 p-3">
            <div className="flex items-center justify-between gap-2">
              <p className="text-sm font-medium text-white">{skill.Name}</p>
              <span className="rounded-full bg-purple-500/20 px-2 py-0.5 text-[10px] font-semibold text-purple-200">
                {skill.Type === 'SOFT' ? 'Soft Skill' : (isEs ? 'Técnica' : 'Technical')}
              </span>
            </div>
            {Boolean(skill.RelevanceToRole) && (
              <p className="mt-2 text-xs text-slate-400"><span className="font-semibold text-slate-300">{isEs ? 'Por qué es relevante: ' : 'Why it matters: '}</span>{skill.RelevanceToRole}</p>
            )}
            {Boolean(skill.DemonstrationStandard) && (
              <p className="mt-1 text-xs text-slate-400"><span className="font-semibold text-slate-300">{isEs ? 'Estándar esperado: ' : 'Expected standard: '}</span>{skill.DemonstrationStandard}</p>
            )}
            {Boolean(skill.FeedbackGuidance) && (
              <p className="mt-1 text-xs text-slate-400"><span className="font-semibold text-slate-300">{isEs ? 'Guía de feedback: ' : 'Feedback guidance: '}</span>{skill.FeedbackGuidance}</p>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}

function ContentList({ title, items, isEs }: { title: string; items: Array<Record<string, unknown>>; isEs: boolean }) {
  return <section><h3 className="text-sm font-semibold uppercase tracking-wide text-slate-400">{title} ({items.length})</h3><div className="mt-2 space-y-2">{items.length === 0 ? <p className="text-sm text-slate-500">{isEs ? 'Sin registros.' : 'No records.'}</p> : items.map((item, index) => <div key={String(item.IdStage ?? item.IdObjective ?? item.IdExpectedMoment ?? item.IdScenario ?? item.IdActor ?? index)} className="rounded-lg border border-white/10 bg-slate-950/30 p-3"><p className="text-sm font-medium text-white">{String(item.Nombre ?? item.Codigo ?? `${isEs ? 'Registro' : 'Record'} ${index + 1}`)}</p>{Boolean(item.Descripcion) && <p className="mt-1 whitespace-pre-wrap text-sm text-slate-400">{String(item.Descripcion)}</p>}</div>)}</div></section>;
}

function QuestionList({ items, skillsByName, isEs }: { items: Array<Record<string, unknown>>; skillsByName: Record<string, TestedSkill>; isEs: boolean }) {
  return <section><h3 className="text-sm font-semibold uppercase tracking-wide text-slate-400">{isEs ? 'Preguntas e intervenciones del AI' : 'AI questions and interventions'} ({items.length})</h3><div className="mt-2 space-y-3">{items.length === 0 ? <p className="text-sm text-slate-500">{isEs ? 'Sin preguntas generadas.' : 'No questions generated.'}</p> : items.map((item, index) => {
    const nombre = String(item.Nombre ?? '').trim();
    const skill = skillsByName[nombre.toLowerCase()];
    return <article key={String(item.IdExpectedMoment ?? index)} className="rounded-lg border border-teal-400/20 bg-teal-400/5 p-4"><div className="flex items-center justify-between gap-3"><p className="text-sm font-semibold text-white">{index + 1}. {String(item.Nombre ?? item.Codigo ?? (isEs ? 'Pregunta' : 'Question'))}</p>{Boolean(item.EsCritico) && <span className="text-xs text-amber-300">{isEs ? 'Crítica' : 'Critical'}</span>}</div>{skill && <div className="mt-2 flex flex-wrap items-center gap-1.5"><span className="rounded-full bg-purple-500/15 px-2.5 py-0.5 text-xs font-medium text-purple-200">{skill.SkillName}</span>{skill.SkillType && <span className="rounded-full bg-slate-500/15 px-2.5 py-0.5 text-xs font-medium text-slate-300">{skill.SkillType === 'TECHNICAL' ? (isEs ? 'Técnica' : 'Technical') : (isEs ? 'Blanda' : 'Soft')}</span>}</div>}<p className="mt-3 text-xs font-semibold uppercase tracking-wide text-teal-300">{isEs ? 'Pregunta del AI / intervención del actor' : "AI question / actor's line"}</p><p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-200">{String(item.Trigger ?? (isEs ? 'Sin pregunta registrada.' : 'No question registered.'))}</p><p className="mt-3 text-xs font-semibold uppercase tracking-wide text-teal-300">{isEs ? 'Qué debe lograr el participante' : 'What the participant must achieve'}</p><p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-300">{String(item.IntencionEsperada ?? (isEs ? 'Sin intención registrada.' : 'No intent registered.'))}</p>{Boolean(item.RespuestaEjemplar) && <><p className="mt-3 text-xs font-semibold uppercase tracking-wide text-teal-300">{isEs ? 'Respuesta ejemplar' : 'Exemplary response'}</p><p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-300">{String(item.RespuestaEjemplar)}</p></>}{skill?.DemonstrationStandard && <><p className="mt-3 text-xs font-semibold uppercase tracking-wide text-teal-300">{isEs ? 'Estándar de desempeño esperado para esta skill' : 'Expected performance standard for this skill'}</p><p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-300">{skill.DemonstrationStandard}</p></>}</article>;
  })}</div></section>;
}

/** DRAFT-only editor for LAB_ExpectedMoment. Shows ALL moments as ONE single global sequence
 * (numbered 1..N across every Stage/"caso"), matching how the AI actor actually plays them
 * back-to-back — the Stage grouping is an internal data-model detail the admin doesn't care
 * about here. Moving an item to a new global position reassigns its STG_IdStage to whichever
 * stage now neighbors it (see updateExpectedMoment's StageId param) and renumbers that stage's
 * MOM_OrdenSugerido values via reorderExpectedMoments (still stage-scoped in the backend). */
function EditableMomentList({
  items,
  stages,
  skillsByName,
  isEs,
  onMomentsChange,
}: {
  items: LabContentItem[];
  stages: LabContentItem[];
  skillsByName: Record<string, TestedSkill>;
  isEs: boolean;
  onMomentsChange: (updated: LabContentItem[]) => void;
}) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState<{ Nombre: string; Trigger: string; IntencionEsperada: string; RespuestaEjemplar: string }>({
    Nombre: '', Trigger: '', IntencionEsperada: '', RespuestaEjemplar: '',
  });
  const [saving, setSaving] = useState(false);
  const [moveError, setMoveError] = useState<string | null>(null);

  const stageOrder = new Map(stages.map((s) => [String(s.IdStage), Number(s.Orden ?? 0)]));
  const globalItems = [...items].sort((a, b) => {
    const stageDiff = (stageOrder.get(String(a.StageId)) ?? 0) - (stageOrder.get(String(b.StageId)) ?? 0);
    return stageDiff !== 0 ? stageDiff : Number(a.OrdenSugerido ?? 0) - Number(b.OrdenSugerido ?? 0);
  });

  function startEdit(item: LabContentItem) {
    setEditingId(String(item.IdExpectedMoment));
    setDraft({
      Nombre: String(item.Nombre ?? ''),
      Trigger: String(item.Trigger ?? ''),
      IntencionEsperada: String(item.IntencionEsperada ?? ''),
      RespuestaEjemplar: String(item.RespuestaEjemplar ?? ''),
    });
  }

  async function saveEdit(item: LabContentItem) {
    setSaving(true);
    setMoveError(null);
    try {
      const updated = await updateExpectedMoment(String(item.IdExpectedMoment), String(item.RowVersion ?? ''), draft);
      onMomentsChange(items.map((m) => (m.IdExpectedMoment === item.IdExpectedMoment ? { ...m, ...updated } : m)));
      setEditingId(null);
    } catch (err) {
      setMoveError((err as Error).message);
    } finally {
      setSaving(false);
    }
  }

  /** Moves one moment to 1-based `targetPos` in the GLOBAL sequence. If that crosses into a
   * different Stage, the moment is reassigned to that Stage first, then that Stage's whole
   * order is renumbered to match the new global sequence. */
  async function moveToGlobalPosition(itemId: string, targetPos: number) {
    const currentIndex = globalItems.findIndex((m) => String(m.IdExpectedMoment) === itemId);
    const clamped = Math.min(Math.max(Math.trunc(targetPos), 1), globalItems.length);
    if (currentIndex < 0 || clamped - 1 === currentIndex) return;

    setSaving(true);
    setMoveError(null);
    try {
      const moving = globalItems[currentIndex];
      const reordered = [...globalItems];
      reordered.splice(currentIndex, 1);
      reordered.splice(clamped - 1, 0, moving);

      const neighbor = reordered[clamped - 1 === 0 ? 1 : clamped - 2];
      const newStageId = neighbor ? String(neighbor.StageId) : String(moving.StageId);
      let movingRowVersion = String(moving.RowVersion ?? '');
      let movingStageId = String(moving.StageId);

      if (newStageId !== movingStageId) {
        const updated = await updateExpectedMoment(String(moving.IdExpectedMoment), movingRowVersion, { StageId: newStageId });
        movingRowVersion = String(updated.RowVersion ?? movingRowVersion);
        movingStageId = newStageId;
      }

      const stageMembers = reordered.filter((m) =>
        String(m.IdExpectedMoment) === String(moving.IdExpectedMoment) ? true : String(m.StageId) === newStageId);
      const payload = stageMembers.map((m, idx) => ({
        IdExpectedMoment: String(m.IdExpectedMoment),
        Orden: idx + 1,
        RowVersion: String(m.IdExpectedMoment) === String(moving.IdExpectedMoment) ? movingRowVersion : String(m.RowVersion ?? ''),
      }));
      const result = await reorderExpectedMoments(newStageId, payload);
      const byId = new Map(result.Items.map((r) => [r.IdExpectedMoment, r]));
      onMomentsChange(items.map((m) => {
        const r = byId.get(String(m.IdExpectedMoment));
        if (!r) return m;
        return String(m.IdExpectedMoment) === String(moving.IdExpectedMoment)
          ? { ...m, StageId: movingStageId, OrdenSugerido: r.Orden, RowVersion: r.RowVersion }
          : { ...m, OrdenSugerido: r.Orden, RowVersion: r.RowVersion };
      }));
    } catch (err) {
      setMoveError((err as Error).message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <section>
      <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-400">{isEs ? 'Preguntas e intervenciones del AI' : 'AI questions and interventions'} ({globalItems.length})</h3>
      <p className="mt-1 text-xs text-slate-500">{isEs ? 'Escribe la posición (1, 2, 3…) para mover una pregunta a ese lugar en la secuencia completa de la conversación.' : 'Type the position (1, 2, 3…) to move a question to that spot in the full conversation sequence.'}</p>
      {moveError && <p className="mt-2 rounded-lg bg-red-500/10 p-2 text-xs text-red-300 border border-red-500/30">{moveError}</p>}
      <div className="mt-2 space-y-3">
        {globalItems.length === 0 && <p className="text-sm text-slate-500">{isEs ? 'Sin preguntas generadas.' : 'No questions generated.'}</p>}
        {globalItems.map((item, index) => {
          const nombre = String(item.Nombre ?? '').trim();
          const skill = skillsByName[nombre.toLowerCase()];
          const isEditing = editingId === String(item.IdExpectedMoment);
          return (
            <article key={String(item.IdExpectedMoment)} className="rounded-lg border border-teal-400/20 bg-teal-400/5 p-4">
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-semibold text-white">{index + 1}. {String(item.Nombre ?? item.Codigo ?? (isEs ? 'Pregunta' : 'Question'))}</p>
                <div className="flex items-center gap-1.5">
                  {Boolean(item.EsCritico) && <span className="text-xs text-amber-300">{isEs ? 'Crítica' : 'Critical'}</span>}
                  <input
                    type="number"
                    min={1}
                    max={globalItems.length}
                    value={index + 1}
                    disabled={saving}
                    onChange={(e) => moveToGlobalPosition(String(item.IdExpectedMoment), Number(e.target.value))}
                    className="w-12 rounded border border-white/10 bg-slate-950/60 p-1 text-center text-xs text-slate-200"
                    title={isEs ? 'Posición en la secuencia completa' : 'Position in the full sequence'}
                  />
                  <button type="button" disabled={saving || index === 0} onClick={() => moveToGlobalPosition(String(item.IdExpectedMoment), index)} className="rounded border border-white/10 p-1 text-slate-300 hover:bg-white/10 disabled:opacity-30" title={isEs ? 'Subir' : 'Move up'}>
                    <ArrowUp className="h-3.5 w-3.5" />
                  </button>
                  <button type="button" disabled={saving || index === globalItems.length - 1} onClick={() => moveToGlobalPosition(String(item.IdExpectedMoment), index + 2)} className="rounded border border-white/10 p-1 text-slate-300 hover:bg-white/10 disabled:opacity-30" title={isEs ? 'Bajar' : 'Move down'}>
                    <ArrowDown className="h-3.5 w-3.5" />
                  </button>
                  {!isEditing && (
                    <button type="button" onClick={() => startEdit(item)} className="rounded border border-teal-400/30 px-2 py-1 text-xs text-teal-300 hover:bg-teal-400/10">
                      {isEs ? 'Editar' : 'Edit'}
                    </button>
                  )}
                </div>
              </div>
              {skill && <div className="mt-2 flex flex-wrap items-center gap-1.5"><span className="rounded-full bg-purple-500/15 px-2.5 py-0.5 text-xs font-medium text-purple-200">{skill.SkillName}</span></div>}
              {isEditing ? (
                <div className="mt-3 space-y-2">
                  <label className="block text-xs font-semibold uppercase tracking-wide text-teal-300">{isEs ? 'Pregunta del AI / intervención del actor' : "AI question / actor's line"}</label>
                  <textarea value={draft.Trigger} onChange={(e) => setDraft({ ...draft, Trigger: e.target.value })} rows={2} className="w-full rounded-lg border border-white/10 bg-slate-950/60 p-2 text-sm text-slate-200" />
                  <label className="block text-xs font-semibold uppercase tracking-wide text-teal-300">{isEs ? 'Qué debe lograr el participante' : 'What the participant must achieve'}</label>
                  <textarea value={draft.IntencionEsperada} onChange={(e) => setDraft({ ...draft, IntencionEsperada: e.target.value })} rows={2} className="w-full rounded-lg border border-white/10 bg-slate-950/60 p-2 text-sm text-slate-200" />
                  <label className="block text-xs font-semibold uppercase tracking-wide text-teal-300">{isEs ? 'Respuesta ejemplar' : 'Exemplary response'}</label>
                  <textarea value={draft.RespuestaEjemplar} onChange={(e) => setDraft({ ...draft, RespuestaEjemplar: e.target.value })} rows={2} className="w-full rounded-lg border border-white/10 bg-slate-950/60 p-2 text-sm text-slate-200" />
                  <div className="flex justify-end gap-2 pt-1">
                    <button type="button" onClick={() => setEditingId(null)} className="rounded px-3 py-1.5 text-xs text-slate-400 hover:text-white">{isEs ? 'Cancelar' : 'Cancel'}</button>
                    <button type="button" disabled={saving} onClick={() => saveEdit(item)} className="rounded bg-teal-500 px-3 py-1.5 text-xs font-medium text-slate-950 hover:bg-teal-400 disabled:opacity-50">
                      {saving ? (isEs ? 'Guardando…' : 'Saving…') : (isEs ? 'Guardar' : 'Save')}
                    </button>
                  </div>
                </div>
              ) : (
                <>
                  <p className="mt-3 text-xs font-semibold uppercase tracking-wide text-teal-300">{isEs ? 'Pregunta del AI / intervención del actor' : "AI question / actor's line"}</p>
                  <p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-200">{String(item.Trigger ?? (isEs ? 'Sin pregunta registrada.' : 'No question registered.'))}</p>
                  <p className="mt-3 text-xs font-semibold uppercase tracking-wide text-teal-300">{isEs ? 'Qué debe lograr el participante' : 'What the participant must achieve'}</p>
                  <p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-300">{String(item.IntencionEsperada ?? (isEs ? 'Sin intención registrada.' : 'No intent registered.'))}</p>
                  {Boolean(item.RespuestaEjemplar) && <><p className="mt-3 text-xs font-semibold uppercase tracking-wide text-teal-300">{isEs ? 'Respuesta ejemplar' : 'Exemplary response'}</p><p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-300">{String(item.RespuestaEjemplar)}</p></>}
                </>
              )}
            </article>
          );
        })}
      </div>
    </section>
  );
}