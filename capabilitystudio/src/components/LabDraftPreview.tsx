import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import {
  CheckCircle2,
  AlertCircle,
  Users,
  MessageSquare,
  Target,
  FileCode,
  Copy,
  Check,
  ChevronDown,
  ChevronUp,
  Award,
  AlertTriangle,
  Sparkles,
  Save,
  Loader2,
  ExternalLink,
  Flame,
  Wrench,
  Brain,
  ShieldCheck,
  Layers,
} from 'lucide-react';
import {
  type GenerateLabDraftResponse,
  type GenerateLabDraftRequest,
  type TestedSkillDraft,
  type ActorDraft,
  saveLabDraft,
  type SaveLabDraftResponse,
} from '../lib/api/labsApi';

interface LabDraftPreviewProps {
  response: GenerateLabDraftResponse;
  requestContext?: GenerateLabDraftRequest;
}

type TabType = 'skills' | 'dialogues' | 'scenario' | 'rubric' | 'raw';

export default function LabDraftPreview({ response, requestContext }: LabDraftPreviewProps) {
  const { i18n } = useTranslation();
  const isEs = i18n.language.startsWith('es');
  const [activeTab, setActiveTab] = useState<TabType>('skills');
  const [copied, setCopied] = useState(false);
  const [expandedDialogues, setExpandedDialogues] = useState<Record<string, boolean>>({
    '0': true,
    '1': true,
  });

  // Modal de guardado atómico
  const [isSaveModalOpen, setIsSaveModalOpen] = useState(false);
  const [customLabName, setCustomLabName] = useState(
    response.LabName || requestContext?.LabName || 'Negociación y Cierre de Contratos B2B'
  );
  const [customLabCode, setCustomLabCode] = useState('');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState<SaveLabDraftResponse | null>(null);

  const { Status, Draft, Questions, Warnings, LabName, TargetRole, Difficulty, ScoreMinimo, Language } = response;

  // Editable copy of the generated dialogues ("preguntas") — the admin can rewrite any field
  // here before saving; everything else in Draft stays as the agent generated it.
  const [dialogues, setDialogues] = useState(Draft?.Dialogues ?? []);
  const [editingDialogueIdx, setEditingDialogueIdx] = useState<number | null>(null);
  const [dialogueDraft, setDialogueDraft] = useState({ ActorSays: '', ExpectedIntent: '', ExemplaryResponse: '', FrequentError: '', Recommendation: '' });

  // Editable copy of the generated Actors — the admin can edit or fully replace any
  // LLM-invented actor, or add new ones by hand, before saving.
  const [actors, setActors] = useState<ActorDraft[]>(Draft?.Actors ?? []);
  const [editingActorIdx, setEditingActorIdx] = useState<number | null>(null);
  const [actorDraft, setActorDraft] = useState<ActorDraft>({ Ref: '', Role: '', CommunicationStyle: '', IsPrincipal: false, Description: '' });

  function startEditActor(idx: number) {
    setEditingActorIdx(idx);
    setActorDraft({ ...actors[idx] });
  }

  function saveEditActor(idx: number) {
    setActors((prev) => prev.map((a, i) => (i === idx ? { ...actorDraft } : a)));
    setEditingActorIdx(null);
  }

  function removeActor(idx: number) {
    setActors((prev) => prev.filter((_, i) => i !== idx));
  }

  function addActor() {
    const nextRef = `ACT_${actors.length + 1}`;
    const newActor: ActorDraft = { Ref: nextRef, Role: '', CommunicationStyle: 'DIRECT', IsPrincipal: actors.length === 0, Description: '' };
    setActors((prev) => [...prev, newActor]);
    setEditingActorIdx(actors.length);
    setActorDraft(newActor);
  }

  function startEditDialogue(idx: number) {
    const dlg = dialogues[idx];
    setEditingDialogueIdx(idx);
    setDialogueDraft({
      ActorSays: dlg.ActorSays,
      ExpectedIntent: dlg.ExpectedIntent,
      ExemplaryResponse: dlg.ExemplaryResponse,
      FrequentError: dlg.FrequentError,
      Recommendation: dlg.Recommendation,
    });
  }

  function saveEditDialogue(idx: number) {
    setDialogues((prev) => prev.map((d, i) => (i === idx ? { ...d, ...dialogueDraft } : d)));
    setEditingDialogueIdx(null);
  }

  function toggleDialogue(idx: number) {
    setExpandedDialogues((prev) => ({
      ...prev,
      [idx]: !prev[idx],
    }));
  }

  function handleCopyJson() {
    navigator.clipboard.writeText(JSON.stringify(response, null, 2));
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  async function handleConfirmSave() {
    if (!Draft) return;
    setSaving(true);
    setSaveError(null);
    try {
      const result = await saveLabDraft({
        LabName: customLabName,
        LabCodigo: customLabCode || undefined,
        TargetRole: TargetRole || requestContext?.TargetRole,
        Archetype: requestContext?.Archetype,
        ScenarioDescription: requestContext?.ScenarioDescription,
        CompanyContext: requestContext?.CompanyContext,
        Dominio: 'SIMULATION',
        Difficulty: Difficulty || requestContext?.Difficulty || 'INTERMEDIATE',
        DurationMinutes: requestContext?.DurationMinutes || 30,
        ScoreMinimo: ScoreMinimo ?? 7.00,
        Language: Language || requestContext?.LabLanguage || 'es-MX',
        Draft: { ...Draft, Dialogues: dialogues, Actors: actors },
      });
      setSaveSuccess(result);
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'Error al guardar el Lab atómicamente.');
    } finally {
      setSaving(false);
    }
  }

  const actorsMap = new Map(actors.map((a) => [a.Ref, a]));

  // The human-authored ScenarioDescription (if any) always wins over the agent's own
  // LabDescription — shown here so preview matches exactly what gets saved (see save fix).
  const effectiveLabDescription = requestContext?.ScenarioDescription?.trim() || Draft?.LabDescription || '';

  // Infiere TestedSkills si por alguna razón no viniera directamente
  const effectiveTestedSkills: TestedSkillDraft[] =
    Draft?.TestedSkills && Draft.TestedSkills.length > 0
      ? Draft.TestedSkills
      : (requestContext?.SkillsToEvaluate ?? []).map((s, idx) => ({
          SkillName: s.SkillName,
          SkillType: s.SkillType ?? (idx % 2 === 0 ? 'TECHNICAL' : 'SOFT'),
          RelevanceToRole: `Demostración indispensable para el rol de ${TargetRole || requestContext?.TargetRole || 'ejecutivo'}.`,
          DemonstrationStandard: s.WhatGoodLooksLike || 'Aplica la metodología establecida sin ceder bajo presión.',
          TestedInMoments: [`#${idx + 1}`],
          RubricCriterionRef: `CRT_${idx + 1}`,
          FeedbackGuidance: 'Evaluar el apego al proceso y la claridad de la respuesta observada en la simulación.',
        }));

  return (
    <div id="lab-builder-results" className="mt-8 space-y-6">
      {Draft && (
        <button
          onClick={() => {
            setCustomLabName(LabName || requestContext?.LabName || 'Simulación Práctica');
            setSaveSuccess(null);
            setSaveError(null);
            setIsSaveModalOpen(true);
          }}
          className="flex w-full items-center justify-center gap-2 rounded-2xl border-2 border-emerald-400/50 bg-emerald-500 px-6 py-4 text-lg font-bold text-white shadow-xl transition hover:bg-emerald-400"
        >
          <Save className="h-6 w-6" />
          {isEs ? '💾 Guardar Lab en Base de Datos' : '💾 Save Lab (Atomic)'}
        </button>
      )}

      {/* Header status bar */}
      <div className="rounded-2xl border border-teal-500/30 bg-gradient-to-r from-teal-950/40 via-slate-900/60 to-slate-900/60 p-5 shadow-xl">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-teal-500/20 text-teal-300 ring-1 ring-teal-500/30">
              <Sparkles className="h-5 w-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/20 px-2.5 py-0.5 text-xs font-semibold text-emerald-300 ring-1 ring-emerald-500/30">
                  <CheckCircle2 className="h-3.5 w-3.5" />
                  {Status === 'DRAFT_READY_FOR_REVIEW'
                    ? isEs
                      ? 'Borrador generado listo para revisión'
                      : 'Draft ready for human review'
                    : Status}
                </span>
                {Difficulty && (
                  <span className="rounded-full bg-slate-800 px-2.5 py-0.5 text-xs font-medium text-slate-300">
                    {Difficulty}
                  </span>
                )}
              </div>
              <h2 className="mt-1 text-lg font-bold text-white">
                {LabName || effectiveLabDescription || (isEs ? 'Simulación Práctica' : 'Practice Simulation')}
              </h2>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <button
              onClick={handleCopyJson}
              className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/5 px-3 py-1.5 text-xs font-medium text-slate-300 transition hover:bg-white/10 hover:text-white"
            >
              {copied ? <Check className="h-3.5 w-3.5 text-emerald-400" /> : <Copy className="h-3.5 w-3.5" />}
              {copied ? (isEs ? '¡Copiado!' : 'Copied!') : (isEs ? 'Copiar JSON' : 'Copy JSON')}
            </button>
          </div>
        </div>

        {/* Quick metrics */}
        <div className="mt-4 grid grid-cols-2 gap-3 border-t border-white/10 pt-4 sm:grid-cols-4">
          <div className="rounded-xl border border-white/5 bg-white/[0.02] p-2.5">
            <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
              {isEs ? 'Rol Evaluado' : 'Target Role'}
            </p>
            <p className="mt-0.5 truncate text-sm font-semibold text-teal-200">
              {TargetRole || requestContext?.TargetRole || '—'}
            </p>
          </div>
          <div className="rounded-xl border border-white/5 bg-white/[0.02] p-2.5">
            <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
              {isEs ? 'Skills Probadas' : 'Tested Skills'}
            </p>
            <p className="mt-0.5 text-sm font-semibold text-purple-300">
              {effectiveTestedSkills.length} {isEs ? 'habilidades' : 'skills'}
            </p>
          </div>
          <div className="rounded-xl border border-white/5 bg-white/[0.02] p-2.5">
            <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
              {isEs ? 'Diálogos / Momentos' : 'Dialogues / Moments'}
            </p>
            <p className="mt-0.5 text-sm font-semibold text-white">
              {Draft?.Dialogues?.length ?? 0} {isEs ? 'interacciones' : 'interactions'}
            </p>
          </div>
          <div className="rounded-xl border border-white/5 bg-white/[0.02] p-2.5">
            <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
              {isEs ? 'Score Mínimo' : 'Min Score'}
            </p>
            <p className="mt-0.5 text-sm font-semibold text-emerald-300">
              {ScoreMinimo ? `${ScoreMinimo} / 10` : '8.0 / 10'}
            </p>
          </div>
        </div>

        {/* Warnings if any */}
        {Warnings && Warnings.length > 0 && (
          <div className="mt-4 rounded-xl border border-amber-500/30 bg-amber-500/10 p-3 text-xs text-amber-300">
            <div className="flex items-center gap-2 font-semibold">
              <AlertTriangle className="h-4 w-4 shrink-0" />
              {isEs ? 'Observaciones del agente' : 'Agent warnings'}
            </div>
            <ul className="mt-1 list-disc pl-5 space-y-0.5">
              {Warnings.map((w, idx) => (
                <li key={idx}>{w}</li>
              ))}
            </ul>
          </div>
        )}

        {/* Missing information questions if any */}
        {Status === 'NEEDS_INFORMATION' && Questions && Questions.length > 0 && (
          <div className="mt-4 rounded-xl border border-rose-500/30 bg-rose-500/10 p-3 text-xs text-rose-300">
            <div className="flex items-center gap-2 font-semibold">
              <AlertCircle className="h-4 w-4 shrink-0" />
              {isEs ? 'Se requiere información adicional:' : 'Additional information needed:'}
            </div>
            <ul className="mt-1 list-disc pl-5 space-y-0.5">
              {Questions.map((q, idx) => (
                <li key={idx}>{q}</li>
              ))}
            </ul>
          </div>
        )}
      </div>

      {Draft && (
        <>
          {/* Navigation Tabs — solid pill-style so the active view is unmistakable */}
          <div className="flex flex-wrap gap-2 rounded-xl border border-white/10 bg-white/[0.02] p-2">
            <button
              onClick={() => setActiveTab('skills')}
              className={`flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm font-bold transition ${
                activeTab === 'skills'
                  ? 'bg-purple-500 text-white shadow-lg'
                  : 'text-slate-300 hover:bg-white/10 hover:text-white'
              }`}
            >
              <Brain className="h-4 w-4" />
              {isEs ? 'Skills Evaluadas (Puesto)' : 'Evaluated Skills (Role)'}
              <span className={`rounded-full px-2 py-0.5 text-xs ${activeTab === 'skills' ? 'bg-white/25 text-white' : 'bg-purple-500/20 text-purple-300'}`}>
                {effectiveTestedSkills.length}
              </span>
            </button>

            <button
              onClick={() => setActiveTab('dialogues')}
              className={`flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm font-bold transition ${
                activeTab === 'dialogues'
                  ? 'bg-teal-500 text-white shadow-lg'
                  : 'text-slate-300 hover:bg-white/10 hover:text-white'
              }`}
            >
              <MessageSquare className="h-4 w-4" />
              {isEs ? 'Diálogos y Momentos' : 'Dialogues & Moments'}
              <span className={`rounded-full px-2 py-0.5 text-xs ${activeTab === 'dialogues' ? 'bg-white/25 text-white' : 'bg-teal-500/20 text-teal-300'}`}>
                {dialogues.length}
              </span>
            </button>

            <button
              onClick={() => setActiveTab('scenario')}
              className={`flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm font-bold transition ${
                activeTab === 'scenario'
                  ? 'bg-brand-500 text-white shadow-lg'
                  : 'text-slate-300 hover:bg-white/10 hover:text-white'
              }`}
            >
              <Users className="h-4 w-4" />
              {isEs ? 'Escenario y Actores' : 'Scenario & Actors'}
              <span className={`rounded-full px-2 py-0.5 text-xs ${activeTab === 'scenario' ? 'bg-white/25 text-white' : 'bg-slate-800 text-slate-400'}`}>
                {actors.length}
              </span>
            </button>

            <button
              onClick={() => setActiveTab('rubric')}
              className={`flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm font-bold transition ${
                activeTab === 'rubric'
                  ? 'bg-brand-500 text-white shadow-lg'
                  : 'text-slate-300 hover:bg-white/10 hover:text-white'
              }`}
            >
              <Award className="h-4 w-4" />
              {isEs ? 'Rúbrica de Evaluación' : 'Evaluation Rubric'}
              <span className={`rounded-full px-2 py-0.5 text-xs ${activeTab === 'rubric' ? 'bg-white/25 text-white' : 'bg-slate-800 text-slate-400'}`}>
                {Draft.Criteria?.length ?? 0}
              </span>
            </button>

            <button
              onClick={() => setActiveTab('raw')}
              className={`flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm font-bold transition ${
                activeTab === 'raw'
                  ? 'bg-brand-500 text-white shadow-lg'
                  : 'text-slate-300 hover:bg-white/10 hover:text-white'
              }`}
            >
              <FileCode className="h-4 w-4" />
              JSON
            </button>
          </div>

          {/* TAB: TESTED SKILLS MATRIX (TÉCNICAS Y BLANDAS) */}
          {activeTab === 'skills' && (
            <div className="space-y-4">
              <div className="rounded-xl border border-purple-500/20 bg-purple-950/20 p-4">
                <h3 className="text-sm font-semibold text-purple-200 flex items-center gap-2">
                  <ShieldCheck className="h-4 w-4 text-purple-400" />
                  {isEs
                    ? 'Matriz de Habilidades Requeridas vs Simulación Real'
                    : 'Target Role Skills Matrix vs Simulation Practice'}
                </h3>
                <p className="mt-1 text-xs text-slate-300">
                  {isEs
                    ? 'Estas son las habilidades del puesto que el simulador evaluará durante la conversación. Al finalizar la simulación, el estudiante real recibirá retroalimentación formativa y calificación diferenciada por cada habilidad técnica y soft skill.'
                    : 'These are the role skills evaluated in the simulation. When a real student completes the practice, they will receive detailed formative feedback and scoring for each technical and soft skill.'}
                </p>
              </div>

              <div className="grid grid-cols-1 gap-4">
                {effectiveTestedSkills.map((skill, idx) => {
                  const isTech = skill.SkillType === 'TECHNICAL';
                  return (
                    <div
                      key={idx}
                      className="rounded-xl border border-white/10 bg-slate-900/70 p-5 shadow-sm space-y-4"
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-white/5 pb-3">
                        <div className="flex items-center gap-2">
                          <span
                            className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                              isTech
                                ? 'border border-teal-500/30 bg-teal-500/10 text-teal-300'
                                : 'border border-purple-500/30 bg-purple-500/10 text-purple-300'
                            }`}
                          >
                            {isTech ? <Wrench className="h-3 w-3" /> : <Brain className="h-3 w-3" />}
                            {isTech
                              ? isEs
                                ? 'Habilidad Técnica'
                                : 'Technical Skill'
                              : isEs
                              ? 'Soft Skill / Conductual'
                              : 'Soft Skill'}
                          </span>
                          <h4 className="font-bold text-white text-base">{skill.SkillName}</h4>
                        </div>

                        {skill.TestedInMoments && skill.TestedInMoments.length > 0 && (
                          <div className="flex items-center gap-1.5 text-xs text-slate-400">
                            <span>{isEs ? 'Probada en:' : 'Tested in:'}</span>
                            {skill.TestedInMoments.map((m, mIdx) => (
                              <span
                                key={mIdx}
                                className="rounded bg-teal-500/20 px-2 py-0.5 font-mono text-xs font-semibold text-teal-200"
                              >
                                {m}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>

                      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                        <div className="rounded-lg bg-white/[0.02] p-3 border border-white/5">
                          <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                            📌 {isEs ? 'Relevancia para el puesto' : 'Role relevance'}:
                          </p>
                          <p className="mt-1 text-xs text-slate-300 leading-relaxed">
                            {skill.RelevanceToRole || 'Competencia crítica para el desempeño efectivo del rol.'}
                          </p>
                        </div>

                        <div className="rounded-lg bg-emerald-500/5 p-3 border border-emerald-500/20">
                          <p className="text-[11px] font-semibold uppercase tracking-wider text-emerald-300">
                            ⭐ {isEs ? 'Estándar de demostración excelente' : 'Demonstration standard'}:
                          </p>
                          <p className="mt-1 text-xs text-slate-200 leading-relaxed">
                            {skill.DemonstrationStandard}
                          </p>
                        </div>
                      </div>

                      {skill.FeedbackGuidance && (
                        <div className="rounded-lg bg-blue-500/5 p-3 border border-blue-500/20">
                          <p className="text-[11px] font-semibold uppercase tracking-wider text-blue-300">
                            💡 {isEs ? 'Orientación de feedback para el estudiante real' : 'Student feedback guidance'}:
                          </p>
                          <p className="mt-1 text-xs text-slate-200 leading-relaxed font-medium">
                            {skill.FeedbackGuidance}
                          </p>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* TAB 1: DIALOGUES & MOMENTS */}
          {activeTab === 'dialogues' && (
            <div className="space-y-4">
              <div className="flex items-center justify-between text-xs text-slate-400">
                <span>
                  {isEs
                    ? 'Guión interactivo generado por el agente para evaluar en tiempo real al participante.'
                    : 'Interactive script generated by the agent to evaluate the participant in real time.'}
                </span>
                <button
                  type="button"
                  onClick={() => {
                    const allOpen = Object.keys(expandedDialogues).length === dialogues.length;
                    if (allOpen) {
                      setExpandedDialogues({});
                    } else {
                      const obj: Record<string, boolean> = {};
                      dialogues.forEach((_, i) => (obj[i] = true));
                      setExpandedDialogues(obj);
                    }
                  }}
                  className="text-teal-400 hover:text-teal-300"
                >
                  {Object.keys(expandedDialogues).length === dialogues.length
                    ? isEs
                      ? 'Colapsar todos'
                      : 'Collapse all'
                    : isEs
                    ? 'Expandir todos'
                    : 'Expand all'}
                </button>
              </div>

              <div className="space-y-3">
                {dialogues.map((dlg, idx) => {
                  const actor = actorsMap.get(dlg.ActorRef);
                  const isOpen = !!expandedDialogues[idx];
                  const isEditing = editingDialogueIdx === idx;
                  return (
                    <div
                      key={dlg.Ref || idx}
                      className="overflow-hidden rounded-xl border border-white/10 bg-slate-900/60 shadow-sm transition"
                    >
                      {/* Dialogue Accordion Header */}
                      <button
                        type="button"
                        onClick={() => toggleDialogue(idx)}
                        className="flex w-full items-start justify-between gap-3 p-4 text-left transition hover:bg-white/[0.02]"
                      >
                        <div className="flex items-start gap-3">
                          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-teal-500/20 text-xs font-bold text-teal-300">
                            #{dlg.Order || idx + 1}
                          </span>
                          <div>
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="font-semibold text-white">
                                {actor?.Role || dlg.ActorRef || (isEs ? 'Actor Simulador' : 'Simulated Actor')}
                              </span>
                              {dlg.SkillEvaluated && (
                                <span className="rounded-md border border-teal-500/30 bg-teal-500/10 px-2 py-0.5 text-[11px] font-medium text-teal-300">
                                  {dlg.SkillEvaluated}
                                </span>
                              )}
                              {dlg.IsCritical && (
                                <span className="inline-flex items-center gap-1 rounded-md bg-rose-500/20 px-1.5 py-0.5 text-[10px] font-bold text-rose-300">
                                  <Flame className="h-3 w-3" />
                                  {isEs ? 'CRÍTICO' : 'CRITICAL'}
                                </span>
                              )}
                            </div>
                            <p className="mt-1 line-clamp-1 text-xs text-slate-300 italic">
                              "{dlg.ActorSays}"
                            </p>
                          </div>
                        </div>

                        <div className="flex shrink-0 items-center gap-2 text-slate-400">
                          {isOpen && !isEditing && (
                            <span
                              role="button"
                              tabIndex={0}
                              onClick={(e) => { e.stopPropagation(); startEditDialogue(idx); }}
                              onKeyDown={(e) => { if (e.key === 'Enter') { e.stopPropagation(); startEditDialogue(idx); } }}
                              className="rounded border border-teal-400/30 px-2 py-1 text-xs text-teal-300 hover:bg-teal-400/10"
                            >
                              {isEs ? 'Editar' : 'Edit'}
                            </span>
                          )}
                          {isOpen ? <ChevronUp className="h-5 w-5" /> : <ChevronDown className="h-5 w-5" />}
                        </div>
                      </button>

                      {/* Dialogue Body */}
                      {isOpen && isEditing ? (
                        <div className="border-t border-white/10 bg-slate-950/40 p-4 space-y-3">
                          <label className="block text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                            {isEs ? 'Lo que dice el interlocutor' : 'What the actor says'}
                          </label>
                          <textarea className="input" rows={2} value={dialogueDraft.ActorSays} onChange={(e) => setDialogueDraft({ ...dialogueDraft, ActorSays: e.target.value })} />
                          <label className="block text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                            {isEs ? 'Intención esperada del participante' : 'Expected participant intent'}
                          </label>
                          <textarea className="input" rows={2} value={dialogueDraft.ExpectedIntent} onChange={(e) => setDialogueDraft({ ...dialogueDraft, ExpectedIntent: e.target.value })} />
                          <label className="block text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                            {isEs ? 'Respuesta ejemplar' : 'Exemplary response'}
                          </label>
                          <textarea className="input" rows={2} value={dialogueDraft.ExemplaryResponse} onChange={(e) => setDialogueDraft({ ...dialogueDraft, ExemplaryResponse: e.target.value })} />
                          <label className="block text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                            {isEs ? 'Error frecuente' : 'Frequent error'}
                          </label>
                          <textarea className="input" rows={2} value={dialogueDraft.FrequentError} onChange={(e) => setDialogueDraft({ ...dialogueDraft, FrequentError: e.target.value })} />
                          <label className="block text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                            {isEs ? 'Recomendación formativa' : 'Recommendation'}
                          </label>
                          <textarea className="input" rows={2} value={dialogueDraft.Recommendation} onChange={(e) => setDialogueDraft({ ...dialogueDraft, Recommendation: e.target.value })} />
                          <div className="flex justify-end gap-2 pt-1">
                            <button type="button" onClick={() => setEditingDialogueIdx(null)} className="rounded px-3 py-1.5 text-xs text-slate-400 hover:text-white">
                              {isEs ? 'Cancelar' : 'Cancel'}
                            </button>
                            <button type="button" onClick={() => saveEditDialogue(idx)} className="rounded bg-teal-500 px-3 py-1.5 text-xs font-medium text-slate-950 hover:bg-teal-400">
                              {isEs ? 'Guardar' : 'Save'}
                            </button>
                          </div>
                        </div>
                      ) : isOpen ? (
                        <div className="border-t border-white/10 bg-slate-950/40 p-4 space-y-4">
                          {/* Actor says bubble */}
                          <div className="rounded-xl border border-slate-700/60 bg-slate-800/60 p-3.5">
                            <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                              💬 {isEs ? 'Lo que dice el interlocutor' : 'What the actor says'}:
                            </p>
                            <p className="mt-1 text-sm font-medium text-white italic">
                              "{dlg.ActorSays}"
                            </p>
                          </div>

                          {/* Expected Intent */}
                          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                            <div className="rounded-xl border border-blue-500/20 bg-blue-500/5 p-3">
                              <p className="text-[11px] font-semibold uppercase tracking-wider text-blue-300">
                                🎯 {isEs ? 'Intención esperada del participante' : 'Expected participant intent'}:
                              </p>
                              <p className="mt-1 text-xs text-slate-300 leading-relaxed">
                                {dlg.ExpectedIntent}
                              </p>
                            </div>

                            <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3">
                              <p className="text-[11px] font-semibold uppercase tracking-wider text-emerald-300">
                                ⭐ {isEs ? 'Respuesta ejemplar (benchmark)' : 'Exemplary response'}:
                              </p>
                              <p className="mt-1 text-xs text-slate-200 leading-relaxed font-medium">
                                "{dlg.ExemplaryResponse}"
                              </p>
                            </div>
                          </div>

                          {/* Error & Recommendation */}
                          {(dlg.FrequentError || dlg.Recommendation) && (
                            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                              {dlg.FrequentError && (
                                <div className="rounded-xl border border-rose-500/20 bg-rose-500/5 p-3">
                                  <p className="text-[11px] font-semibold uppercase tracking-wider text-rose-300">
                                    ⚠️ {isEs ? 'Error frecuente a penalizar' : 'Frequent error'}:
                                  </p>
                                  <p className="mt-1 text-xs text-slate-300 leading-relaxed">
                                    {dlg.FrequentError}
                                  </p>
                                </div>
                              )}
                              {dlg.Recommendation && (
                                <div className="rounded-xl border border-amber-500/20 bg-amber-500/5 p-3">
                                  <p className="text-[11px] font-semibold uppercase tracking-wider text-amber-300">
                                    💡 {isEs ? 'Recomendación formativa' : 'Recommendation'}:
                                  </p>
                                  <p className="mt-1 text-xs text-slate-300 leading-relaxed">
                                    {dlg.Recommendation}
                                  </p>
                                </div>
                              )}
                            </div>
                          )}
                        </div>
                      ) : null}
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* TAB 2: SCENARIO & ACTORS */}
          {activeTab === 'scenario' && (
            <div className="space-y-6">
              {/* Scenario details */}
              <div className="rounded-xl border border-white/10 bg-slate-900/60 p-5 space-y-4">
                <h3 className="text-sm font-bold text-white flex items-center gap-2">
                  <Target className="h-4 w-4 text-teal-400" />
                  {isEs ? 'Definición del Escenario' : 'Scenario Definition'}
                </h3>

                <div>
                  <p className="text-xs font-semibold uppercase text-slate-400">
                    {isEs ? 'Descripción del Lab' : 'Lab Description'}
                  </p>
                  <p className="mt-1 text-sm text-slate-200 leading-relaxed">
                    {effectiveLabDescription}
                  </p>
                </div>

                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 pt-2 border-t border-white/5">
                  <div className="rounded-lg border border-amber-500/20 bg-amber-500/5 p-3">
                    <p className="text-xs font-semibold text-amber-300">
                      🚨 {isEs ? 'Problema Central' : 'Core Problem'}
                    </p>
                    <p className="mt-1 text-xs text-slate-300 leading-relaxed">
                      {Draft.ScenarioProblemaCentral}
                    </p>
                  </div>
                  <div className="rounded-lg border border-emerald-500/20 bg-emerald-500/5 p-3">
                    <p className="text-xs font-semibold text-emerald-300">
                      ✅ {isEs ? 'Resultado Esperado' : 'Expected Outcome'}
                    </p>
                    <p className="mt-1 text-xs text-slate-300 leading-relaxed">
                      {Draft.ScenarioResultadoEsperado}
                    </p>
                  </div>
                </div>
              </div>

              {/* Actors */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <h3 className="text-sm font-bold text-white flex items-center gap-2">
                    <Users className="h-4 w-4 text-teal-400" />
                    {isEs ? 'Actores de la Simulación' : 'Simulation Actors'}
                  </h3>
                  <button
                    type="button"
                    onClick={addActor}
                    className="rounded-lg border border-teal-400/30 px-3 py-1.5 text-xs font-medium text-teal-300 hover:bg-teal-400/10"
                  >
                    + {isEs ? 'Agregar actor' : 'Add actor'}
                  </button>
                </div>
                <p className="text-xs text-slate-500">
                  {isEs
                    ? 'Puedes editar, reemplazar o eliminar cualquier actor propuesto por el agente, o agregar uno nuevo manualmente, antes de guardar.'
                    : 'You can edit, replace, or remove any agent-proposed actor, or add a new one manually, before saving.'}
                </p>
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  {actors.map((actor, idx) => {
                    const isEditing = editingActorIdx === idx;
                    if (isEditing) {
                      return (
                        <div key={actor.Ref || idx} className="rounded-xl border border-teal-500/40 bg-teal-500/5 p-4 space-y-2 sm:col-span-2">
                          <input
                            className="input"
                            placeholder={isEs ? 'Puesto/Rol (ej. Director Financiero)' : 'Role/Title (e.g. CFO)'}
                            value={actorDraft.Role}
                            onChange={(e) => setActorDraft({ ...actorDraft, Role: e.target.value })}
                          />
                          <textarea
                            className="input"
                            rows={2}
                            placeholder={isEs ? 'Descripción del actor' : 'Actor description'}
                            value={actorDraft.Description}
                            onChange={(e) => setActorDraft({ ...actorDraft, Description: e.target.value })}
                          />
                          <div className="flex flex-wrap items-center gap-3">
                            <select
                              className="input w-auto"
                              value={actorDraft.CommunicationStyle}
                              onChange={(e) => setActorDraft({ ...actorDraft, CommunicationStyle: e.target.value })}
                            >
                              {['DIRECT', 'COLLABORATIVE', 'RESERVED', 'SKEPTICAL', 'IMPATIENT', 'DETAIL_ORIENTED', 'EXECUTIVE'].map((style) => (
                                <option key={style} value={style}>{style}</option>
                              ))}
                            </select>
                            <label className="flex items-center gap-1.5 text-xs text-slate-300">
                              <input
                                type="checkbox"
                                checked={actorDraft.IsPrincipal}
                                onChange={(e) => setActorDraft({ ...actorDraft, IsPrincipal: e.target.checked })}
                              />
                              {isEs ? 'Actor principal (interlocutor del participante)' : 'Principal actor (participant\'s counterpart)'}
                            </label>
                          </div>
                          <div className="flex justify-end gap-2 pt-1">
                            <button type="button" onClick={() => setEditingActorIdx(null)} className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:bg-white/5">
                              {isEs ? 'Cancelar' : 'Cancel'}
                            </button>
                            <button type="button" onClick={() => saveEditActor(idx)} className="rounded-lg border border-teal-400/30 bg-teal-500/20 px-3 py-1.5 text-xs font-semibold text-teal-100 hover:bg-teal-500/30">
                              {isEs ? 'Guardar' : 'Save'}
                            </button>
                          </div>
                        </div>
                      );
                    }
                    return (
                      <div
                        key={actor.Ref || idx}
                        className={`rounded-xl border p-4 ${
                          actor.IsPrincipal
                            ? 'border-teal-500/40 bg-teal-500/10'
                            : 'border-white/10 bg-slate-900/60'
                        }`}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <span className="font-semibold text-sm text-white">{actor.Role || (isEs ? '(sin definir)' : '(undefined)')}</span>
                          <div className="flex items-center gap-1.5">
                            {actor.IsPrincipal ? (
                              <span className="rounded-full bg-teal-500/30 px-2 py-0.5 text-[10px] font-bold text-teal-200">
                                {isEs ? 'PARTICIPANTE' : 'PARTICIPANT'}
                              </span>
                            ) : (
                              <span className="rounded-full bg-slate-800 px-2 py-0.5 text-[10px] font-medium text-slate-400">
                                {actor.Ref}
                              </span>
                            )}
                            <button type="button" onClick={() => startEditActor(idx)} className="rounded border border-teal-400/30 px-2 py-0.5 text-[10px] text-teal-300 hover:bg-teal-400/10">
                              {isEs ? 'Editar' : 'Edit'}
                            </button>
                            <button type="button" onClick={() => removeActor(idx)} className="rounded border border-red-400/30 px-2 py-0.5 text-[10px] text-red-300 hover:bg-red-400/10">
                              {isEs ? 'Eliminar' : 'Remove'}
                            </button>
                          </div>
                        </div>
                        <p className="mt-2 text-xs text-slate-300 leading-relaxed">{actor.Description}</p>
                        {actor.CommunicationStyle && (
                          <div className="mt-3 rounded-lg bg-black/20 p-2 text-[11px] text-slate-400">
                            <span className="font-semibold text-slate-300">
                              {isEs ? 'Estilo de comunicación' : 'Communication style'}:
                            </span>{' '}
                            {actor.CommunicationStyle}
                          </div>
                        )}
                      </div>
                    );
                  })}
                  {actors.length === 0 && (
                    <p className="text-sm text-slate-500 sm:col-span-2">{isEs ? 'Sin actores. Agrega uno manualmente.' : 'No actors. Add one manually.'}</p>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* TAB 3: RUBRIC & CRITERIA */}
          {activeTab === 'rubric' && (
            <div className="space-y-4">
              <div className="text-xs text-slate-400">
                {isEs
                  ? 'Criterios de evaluación automática configurados para medir el desempeño del participante durante el Lab.'
                  : 'Automated evaluation criteria configured to measure the participant performance.'}
              </div>

              <div className="space-y-3">
                {Draft.Criteria.map((crit, idx) => (
                  <div
                    key={crit.Ref || idx}
                    className="rounded-xl border border-white/10 bg-slate-900/60 p-4 space-y-3"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex items-center gap-2">
                        <span className="flex h-6 w-6 items-center justify-center rounded-md bg-teal-500/20 text-xs font-bold text-teal-300">
                          {idx + 1}
                        </span>
                        <span className="font-semibold text-sm text-white">{crit.Name}</span>
                      </div>
                      <div className="flex items-center gap-2">
                        <span className="rounded-md bg-slate-800 px-2 py-0.5 text-xs text-slate-300">
                          {isEs ? 'Peso' : 'Weight'}: <strong>{crit.Weight}%</strong>
                        </span>
                        {crit.IsCritical && (
                          <span className="rounded-md bg-rose-500/20 px-2 py-0.5 text-[10px] font-bold text-rose-300">
                            {isEs ? 'CRÍTICO' : 'CRITICAL'}
                          </span>
                        )}
                      </div>
                    </div>

                    <p className="text-xs text-slate-300">{crit.Description}</p>

                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 pt-2 border-t border-white/5">
                      <div className="rounded-lg bg-emerald-500/10 p-2.5 text-xs text-emerald-300">
                        <p className="font-semibold mb-1">
                          ✓ {isEs ? 'Indicadores Positivos' : 'Positive Indicators'}
                        </p>
                        <p className="text-[11px] text-slate-300">{crit.PositiveIndicators}</p>
                      </div>
                      <div className="rounded-lg bg-rose-500/10 p-2.5 text-xs text-rose-300">
                        <p className="font-semibold mb-1">
                          ✕ {isEs ? 'Indicadores Negativos' : 'Negative Indicators'}
                        </p>
                        <p className="text-[11px] text-slate-300">{crit.NegativeIndicators}</p>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* TAB 4: RAW JSON */}
          {activeTab === 'raw' && (
            <div className="relative rounded-xl border border-white/10 bg-black/40 p-4">
              <pre className="max-h-[32rem] overflow-auto whitespace-pre-wrap text-xs text-slate-300 font-mono">
                {JSON.stringify(Draft, null, 2)}
              </pre>
            </div>
          )}
        </>
      )}

      {/* MODAL DE GUARDADO ATÓMICO EN BASE DE DATOS */}
      {isSaveModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm">
          <div className="w-full max-w-xl rounded-2xl border border-teal-500/30 bg-slate-900 p-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-white/10 pb-4">
              <div className="flex items-center gap-2.5">
                <div className="rounded-lg bg-emerald-500/20 p-2 text-emerald-300">
                  <Save className="h-5 w-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-white">
                    {isEs ? 'Guardar Lab en Base de Datos' : 'Save Lab to Database'}
                  </h3>
                  <p className="text-xs text-slate-400">
                    {isEs
                      ? 'Operación atómica: persiste las 9 tablas relacionales en una sola transacción SQL.'
                      : 'Atomic operation: persists all 9 relational tables within one SQL transaction.'}
                  </p>
                </div>
              </div>
              <button
                onClick={() => setIsSaveModalOpen(false)}
                className="text-slate-400 hover:text-white text-sm"
              >
                ✕
              </button>
            </div>

            {saveSuccess ? (
              <div className="mt-5 space-y-4">
                <div className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-4 text-emerald-200">
                  <div className="flex items-center gap-2 font-semibold">
                    <CheckCircle2 className="h-5 w-5 text-emerald-400" />
                    {saveSuccess.Message}
                  </div>
                  <div className="mt-3 grid grid-cols-2 gap-2 text-xs text-slate-300">
                    <div>
                      <span className="text-slate-400">{isEs ? 'Código:' : 'Code:'}</span>{' '}
                      <strong className="text-white font-mono">{saveSuccess.Codigo}</strong>
                    </div>
                    <div>
                      <span className="text-slate-400">{isEs ? 'Etapas guardadas:' : 'Stages:'}</span>{' '}
                      <strong className="text-white">{saveSuccess.StagesSaved}</strong>
                    </div>
                    <div>
                      <span className="text-slate-400">{isEs ? 'Momentos / Diálogos:' : 'Moments:'}</span>{' '}
                      <strong className="text-white">{saveSuccess.MomentsSaved}</strong>
                    </div>
                    <div>
                      <span className="text-slate-400">{isEs ? 'Criterios de rúbrica:' : 'Criteria:'}</span>{' '}
                      <strong className="text-white">{saveSuccess.CriteriaSaved}</strong>
                    </div>
                  </div>
                </div>

                <div className="flex justify-end gap-3 pt-2">
                  <button
                    onClick={() => setIsSaveModalOpen(false)}
                    className="rounded-lg border border-white/10 px-4 py-2 text-xs font-medium text-slate-300 hover:bg-white/5"
                  >
                    {isEs ? 'Cerrar' : 'Close'}
                  </button>
                  <Link
                    to="/labs"
                    className="inline-flex items-center gap-1.5 rounded-lg bg-teal-500 px-4 py-2 text-xs font-semibold text-slate-950 hover:bg-teal-400"
                  >
                    <ExternalLink className="h-3.5 w-3.5" />
                    {isEs ? 'Ver en Labs Studio' : 'View in Labs Studio'}
                  </Link>
                </div>
              </div>
            ) : (
              <div className="mt-5 space-y-4">
                <div>
                  <label className="block text-xs font-semibold text-slate-300 mb-1">
                    {isEs ? 'Nombre del Lab' : 'Lab Name'}
                  </label>
                  <input
                    className="input w-full"
                    value={customLabName}
                    onChange={(e) => setCustomLabName(e.target.value)}
                    required
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-300 mb-1">
                    {isEs ? 'Código Único (opcional, se auto-genera si está vacío)' : 'Unique Code (optional)'}
                  </label>
                  <input
                    className="input w-full font-mono text-xs"
                    placeholder="ej. LAB_NEG_B2B_01"
                    value={customLabCode}
                    onChange={(e) => setCustomLabCode(e.target.value)}
                  />
                </div>

                {/* Resumen de entidades a guardar atómicamente */}
                <div className="rounded-xl border border-white/5 bg-slate-950/60 p-3.5">
                  <p className="text-[11px] font-semibold uppercase tracking-wider text-teal-300 flex items-center gap-1.5 mb-2">
                    <Layers className="h-3.5 w-3.5" />
                    {isEs ? 'Estructura relacional completa a insertar:' : 'Full relational structure to insert:'}
                  </p>
                  <div className="grid grid-cols-2 sm:grid-cols-3 gap-2 text-[11px] text-slate-300">
                    <div>• 1 LAB_Lab (Maestro)</div>
                    <div>• 1 LAB_LabVersion</div>
                    <div>• 1 LAB_Scenario</div>
                    <div>• {Draft?.Actors?.length ?? 0} LAB_SimulatedActor</div>
                    <div>• {Draft?.Stages?.length ?? 0} LAB_Stage</div>
                    <div>• {Draft?.Objectives?.length ?? 0} LAB_Objective</div>
                    <div>• {Draft?.Dialogues?.length ?? 0} LAB_ExpectedMoment</div>
                    <div>• 1 LAB_Rubric</div>
                    <div>• {Draft?.Criteria?.length ?? 0} LAB_RubricCriterion</div>
                    <div>• {effectiveTestedSkills.length} LAB_TestedSkill</div>
                  </div>
                </div>

                {saveError && (
                  <p className="rounded-lg bg-red-500/10 p-3 text-xs text-red-300 border border-red-500/30">
                    {saveError}
                  </p>
                )}

                <div className="flex justify-end gap-3 pt-3 border-t border-white/10">
                  <button
                    type="button"
                    onClick={() => setIsSaveModalOpen(false)}
                    disabled={saving}
                    className="rounded-lg border border-white/10 px-4 py-2 text-xs font-medium text-slate-300 hover:bg-white/5 disabled:opacity-50"
                  >
                    {isEs ? 'Cancelar' : 'Cancel'}
                  </button>
                  <button
                    type="button"
                    onClick={handleConfirmSave}
                    disabled={saving || !customLabName.trim()}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-500 px-4 py-2 text-xs font-bold text-slate-950 hover:bg-emerald-400 disabled:opacity-50"
                  >
                    {saving ? (
                      <>
                        <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        {isEs ? 'Guardando atómicamente…' : 'Saving atomically…'}
                      </>
                    ) : (
                      <>
                        <Save className="h-3.5 w-3.5" />
                        {isEs ? 'Confirmar Guardado Atómico' : 'Confirm Atomic Save'}
                      </>
                    )}
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
