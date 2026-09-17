import { useState, useMemo } from 'react';
import {
  X,
  CheckCircle2,
  ArrowRight,
  Bot,
  Database,
  Layers,
  GraduationCap,
  Heart,
  Briefcase,
  Building2,
  TableIcon,
  Search,
} from 'lucide-react';
import type { ProcessDocumentStructureDto, WorkStepRequirementsAnalysisResultDto } from '@/api/processDocumentsApi';

export interface StepSummaryStats {
  stepId: string;
  stepNumber: string;
  stepName: string;
  chapterTitle: string;
  isExcluded: boolean;
  hasRequirements: boolean;
  softCount?: number;
  technicalCount?: number;
  processesCount?: number;
  aiCount?: number;
  totalSkillsCount?: number;
  toolsCount?: number;
  classesCount?: number;
  engramsCount?: number;
  coverageStatus?: string;
  assignmentStatus?: string;
  isAssigned?: boolean;
  assignedEmployeeName?: string;
  draftsCount?: number;
}

export interface ProcessLearningSummaryModalProps {
  isOpen: boolean;
  onClose: () => void;
  structure: ProcessDocumentStructureDto;
  stepSummaries: StepSummaryStats[];
  stepRequirements?: Record<string, WorkStepRequirementsAnalysisResultDto>;
  initialTab?: 'byType' | 'byStep';
  onOpenStep: (stepId: string, targetWizardStep?: number) => void;
  isSaved?: boolean;
}

interface GroupedSkillItem {
  name: string;
  reason: string;
  category: 'technical' | 'soft' | 'business' | 'dept' | 'hr' | 'ai';
  categoryLabel: string;
  isAi: boolean;
  sourceSteps: Array<{ stepId: string; stepNumber: string; stepName: string }>;
}

export function ProcessLearningSummaryModal({
  isOpen,
  onClose,
  structure,
  stepSummaries,
  stepRequirements = {},
  initialTab = 'byStep',
  onOpenStep,
  isSaved = false,
}: ProcessLearningSummaryModalProps) {
  const [activeTab, setActiveTab] = useState<'byStep' | 'byType'>(initialTab);
  const [categoryFilter, setCategoryFilter] = useState<'all' | 'technical' | 'ai' | 'soft' | 'business' | 'processes'>('all');
  const [skillSearch, setSkillSearch] = useState('');

  const totalSteps = stepSummaries.length;
  const analyzedSteps = stepSummaries.filter((s) => s.hasRequirements).length;
  const totalSkills = stepSummaries.reduce((acc, s) => acc + (s.totalSkillsCount || 0), 0);
  const totalAiSkills = stepSummaries.reduce((acc, s) => acc + (s.aiCount || 0), 0);
  const totalTools = stepSummaries.reduce((acc, s) => acc + (s.toolsCount || 0), 0);

  // Group all skills across the whole process by type/category
  const groupedSkills = useMemo(() => {
    const map = new Map<string, GroupedSkillItem>();

    for (const [stepId, reqs] of Object.entries(stepRequirements)) {
      if (!reqs?.requiredSkillAreas) continue;
      const stepMeta = {
        stepId,
        stepNumber: reqs.stepNumber || '',
        stepName: reqs.workStep || stepId,
      };

      const register = (
        name: string,
        reason: string,
        category: 'technical' | 'soft' | 'business' | 'dept' | 'hr' | 'ai',
        label: string,
        isAi: boolean,
      ) => {
        if (!name?.trim()) return;
        const key = `${category}:${name.trim().toLowerCase()}`;
        if (!map.has(key)) {
          map.set(key, {
            name: name.trim(),
            reason: reason || 'Requerimiento identificado para la operación',
            category,
            categoryLabel: label,
            isAi,
            sourceSteps: [stepMeta],
          });
        } else {
          const existing = map.get(key)!;
          if (!existing.sourceSteps.some((s) => s.stepId === stepId)) {
            existing.sourceSteps.push(stepMeta);
          }
        }
      };

      // Technical skills
      for (const s of reqs.requiredSkillAreas.technicalSkills || []) {
        register(s.name, s.reason, 'technical', 'Technical Skills (Habilidades Técnicas)', false);
      }
      // AI skills (Mandatory)
      for (const s of reqs.requiredSkillAreas.aiSkills || []) {
        register(s.name, s.reason, 'ai', 'AI Skills (Habilidades de IA)', true);
      }
      // Soft skills
      for (const s of reqs.requiredSkillAreas.softSkills || []) {
        register(s.name, s.reason, 'soft', 'Soft Skills (Habilidades Blandas)', false);
      }
      // Business skills
      for (const s of reqs.requiredSkillAreas.businessSkills || []) {
        register(s.name, s.reason, 'business', 'Business Skills (Negocio y Estrategia)', false);
      }
      // Department processes
      for (const s of reqs.requiredSkillAreas.departmentProcesses || []) {
        register(s.name, s.reason, 'dept', 'Department Processes (Procesos Operativos)', false);
      }
      // HR processes
      for (const s of reqs.requiredSkillAreas.hrProcesses || []) {
        register(s.name, s.reason, 'hr', 'HR Processes (Políticas y RRHH)', false);
      }
    }

    return Array.from(map.values());
  }, [stepRequirements]);

  // Counts per category
  const categoryCounts = useMemo(() => {
    return {
      total: groupedSkills.length,
      technical: groupedSkills.filter((s) => s.category === 'technical').length,
      ai: groupedSkills.filter((s) => s.category === 'ai' || s.isAi).length,
      soft: groupedSkills.filter((s) => s.category === 'soft').length,
      business: groupedSkills.filter((s) => s.category === 'business').length,
      processes: groupedSkills.filter((s) => s.category === 'dept' || s.category === 'hr').length,
    };
  }, [groupedSkills]);

  // Filter grouped skills by search and category filter
  const filteredGroupedSkills = useMemo(() => {
    return groupedSkills.filter((s) => {
      // Category filter
      if (categoryFilter === 'technical' && s.category !== 'technical') return false;
      if (categoryFilter === 'ai' && !s.isAi && s.category !== 'ai') return false;
      if (categoryFilter === 'soft' && s.category !== 'soft') return false;
      if (categoryFilter === 'business' && s.category !== 'business') return false;
      if (categoryFilter === 'processes' && s.category !== 'dept' && s.category !== 'hr') return false;

      // Text search
      if (skillSearch.trim()) {
        const q = skillSearch.toLowerCase();
        return (
          s.name.toLowerCase().includes(q) ||
          s.reason.toLowerCase().includes(q) ||
          s.sourceSteps.some((st) => st.stepName.toLowerCase().includes(q) || st.stepNumber.includes(q))
        );
      }

      return true;
    });
  }, [groupedSkills, categoryFilter, skillSearch]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="flex max-h-[90vh] w-full max-w-5xl flex-col rounded-2xl border border-slate-200 bg-white shadow-2xl dark:border-white/10 dark:bg-slate-900 overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-200 p-5 dark:border-white/10">
          <div className="space-y-0.5">
            <div className="flex items-center gap-2">
              <span className="rounded-full bg-emerald-100 p-1 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
                <CheckCircle2 className="h-4 w-4" />
              </span>
              <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                {isSaved ? '¡Proceso y Matriz Guardados en Azure SQL!' : 'Resumen del Proceso y Matriz de Capacitación'}
              </h2>
            </div>
            <p className="text-xs text-slate-500 dark:text-white/60">
              {structure.process_name || 'Manual de Proceso'} — Resumen consolidado de estructura y competencias.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-white/10 dark:hover:text-white"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Success Banner if Saved */}
        {isSaved && (
          <div className="flex items-center gap-2.5 bg-emerald-500/10 border-b border-emerald-500/20 px-5 py-3 text-xs text-emerald-800 dark:text-emerald-300">
            <Database className="h-4 w-4 text-emerald-600 shrink-0" />
            <p className="font-semibold">
              El proceso, sus capítulos, pasos de trabajo y la Matriz de Habilidades quedaron registrados en la base de datos de tu organización.
            </p>
          </div>
        )}

        {/* Global KPI stats */}
        <div className="grid grid-cols-2 gap-3 border-b border-slate-100 bg-slate-50/60 p-5 sm:grid-cols-3 lg:grid-cols-6 dark:border-white/5 dark:bg-white/[0.02]">
          <div className="rounded-xl border border-slate-200 bg-white p-3 text-center dark:border-white/10 dark:bg-white/5">
            <p className="text-xl font-bold text-slate-900 dark:text-white">{totalSteps}</p>
            <p className="text-[10px] uppercase font-semibold text-slate-400 mt-0.5">Pasos Totales</p>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-3 text-center dark:border-white/10 dark:bg-white/5">
            <p className="text-xl font-bold text-brand-600 dark:text-brand-400">{analyzedSteps}</p>
            <p className="text-[10px] uppercase font-semibold text-slate-400 mt-0.5">Analizados con IA</p>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-3 text-center dark:border-white/10 dark:bg-white/5">
            <p className="text-xl font-bold text-teal-600 dark:text-teal-400">{totalSkills}</p>
            <p className="text-[10px] uppercase font-semibold text-slate-400 mt-0.5">Skills en Matriz</p>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-3 text-center dark:border-white/10 dark:bg-white/5">
            <p className="text-xl font-bold text-indigo-600 dark:text-indigo-400">{totalAiSkills}</p>
            <p className="text-[10px] uppercase font-semibold text-slate-400 mt-0.5">Skills de IA</p>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-3 text-center dark:border-white/10 dark:bg-white/5">
            <p className="text-xl font-bold text-amber-600 dark:text-amber-400">{totalTools}</p>
            <p className="text-[10px] uppercase font-semibold text-slate-400 mt-0.5">Herramientas (Tools)</p>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-3 text-center dark:border-white/10 dark:bg-white/5">
            <p className="text-xl font-bold text-emerald-600 dark:text-emerald-400">100%</p>
            <p className="text-[10px] uppercase font-semibold text-slate-400 mt-0.5">Estado</p>
          </div>
        </div>

        {/* Navigation Tabs (Por Tipo de Skill vs Por Paso) */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 bg-slate-50/50 px-5 py-2.5 dark:border-white/10 dark:bg-white/[0.02]">
          <div className="inline-flex items-center rounded-xl border border-slate-200 bg-white p-1 text-xs font-semibold shadow-xs dark:border-white/10 dark:bg-white/5">
            <button
              type="button"
              onClick={() => setActiveTab('byType')}
              className={`inline-flex items-center gap-2 rounded-lg px-3 py-1.5 transition-colors ${
                activeTab === 'byType'
                  ? 'bg-brand-600 text-white font-bold shadow-xs'
                  : 'text-slate-600 hover:text-slate-900 dark:text-white/70 dark:hover:text-white'
              }`}
            >
              <GraduationCap className="h-4 w-4" />
              <span>Por Tipo de Skill ({groupedSkills.length})</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('byStep')}
              className={`inline-flex items-center gap-2 rounded-lg px-3 py-1.5 transition-colors ${
                activeTab === 'byStep'
                  ? 'bg-brand-600 text-white font-bold shadow-xs'
                  : 'text-slate-600 hover:text-slate-900 dark:text-white/70 dark:hover:text-white'
              }`}
            >
              <TableIcon className="h-4 w-4" />
              <span>Por Paso de Trabajo ({totalSteps})</span>
            </button>
          </div>

          {activeTab === 'byType' && (
            <div className="flex flex-wrap items-center gap-2">
              {/* Category Pills Filter */}
              <div className="flex items-center gap-1 overflow-x-auto text-[11px]">
                <button
                  type="button"
                  onClick={() => setCategoryFilter('all')}
                  className={`rounded-lg px-2.5 py-1 font-semibold transition ${
                    categoryFilter === 'all'
                      ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                      : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-100 dark:border-white/10 dark:bg-white/5 dark:text-white/70'
                  }`}
                >
                  Todas ({categoryCounts.total})
                </button>
                <button
                  type="button"
                  onClick={() => setCategoryFilter('technical')}
                  className={`rounded-lg px-2.5 py-1 font-semibold transition ${
                    categoryFilter === 'technical'
                      ? 'bg-teal-600 text-white'
                      : 'bg-teal-50 border border-teal-200 text-teal-700 hover:bg-teal-100 dark:bg-teal-500/10 dark:border-teal-500/20 dark:text-teal-300'
                  }`}
                >
                  Technical ({categoryCounts.technical})
                </button>
                <button
                  type="button"
                  onClick={() => setCategoryFilter('ai')}
                  className={`rounded-lg px-2.5 py-1 font-semibold transition ${
                    categoryFilter === 'ai'
                      ? 'bg-indigo-600 text-white'
                      : 'bg-indigo-50 border border-indigo-200 text-indigo-700 hover:bg-indigo-100 dark:bg-indigo-500/10 dark:border-indigo-500/20 dark:text-indigo-300'
                  }`}
                >
                  AI Skills ({categoryCounts.ai})
                </button>
                <button
                  type="button"
                  onClick={() => setCategoryFilter('soft')}
                  className={`rounded-lg px-2.5 py-1 font-semibold transition ${
                    categoryFilter === 'soft'
                      ? 'bg-violet-600 text-white'
                      : 'bg-violet-50 border border-violet-200 text-violet-700 hover:bg-violet-100 dark:bg-violet-500/10 dark:border-violet-500/20 dark:text-violet-300'
                  }`}
                >
                  Soft ({categoryCounts.soft})
                </button>
                <button
                  type="button"
                  onClick={() => setCategoryFilter('business')}
                  className={`rounded-lg px-2.5 py-1 font-semibold transition ${
                    categoryFilter === 'business'
                      ? 'bg-amber-600 text-white'
                      : 'bg-amber-50 border border-amber-200 text-amber-700 hover:bg-amber-100 dark:bg-amber-500/10 dark:border-amber-500/20 dark:text-amber-300'
                  }`}
                >
                  Business ({categoryCounts.business})
                </button>
                <button
                  type="button"
                  onClick={() => setCategoryFilter('processes')}
                  className={`rounded-lg px-2.5 py-1 font-semibold transition ${
                    categoryFilter === 'processes'
                      ? 'bg-blue-600 text-white'
                      : 'bg-blue-50 border border-blue-200 text-blue-700 hover:bg-blue-100 dark:bg-blue-500/10 dark:border-blue-500/20 dark:text-blue-300'
                  }`}
                >
                  Procesos ({categoryCounts.processes})
                </button>
              </div>

              {/* Text Search */}
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
                <input
                  type="text"
                  value={skillSearch}
                  onChange={(e) => setSkillSearch(e.target.value)}
                  placeholder="Filtrar skills..."
                  className="rounded-lg border border-slate-200 bg-white py-1 pl-7 pr-3 text-xs text-slate-800 placeholder-slate-400 focus:border-brand-500 focus:outline-none dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </div>
            </div>
          )}
        </div>

        {/* Content View: Tab 1 (Por Tipo de Skill) */}
        {activeTab === 'byType' && (
          <div className="flex-1 overflow-y-auto p-5 space-y-3">
            {filteredGroupedSkills.length === 0 ? (
              <div className="py-12 text-center">
                <p className="text-xs text-slate-400">
                  {skillSearch || categoryFilter !== 'all'
                    ? 'No se encontraron habilidades con los filtros seleccionados.'
                    : 'Aún no se han analizado habilidades para este proceso.'}
                </p>
              </div>
            ) : (
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                {filteredGroupedSkills.map((sk) => {
                  const isTech = sk.category === 'technical';
                  const isAi = sk.category === 'ai' || sk.isAi;
                  const isSoft = sk.category === 'soft';
                  const isBiz = sk.category === 'business';
                  const isProc = sk.category === 'dept' || sk.category === 'hr';

                  const badgeClass = isAi
                    ? 'bg-indigo-50 border-indigo-200 text-indigo-700 dark:bg-indigo-500/15 dark:border-indigo-500/20 dark:text-indigo-300'
                    : isTech
                    ? 'bg-teal-50 border-teal-200 text-teal-700 dark:bg-teal-500/15 dark:border-teal-500/20 dark:text-teal-300'
                    : isSoft
                    ? 'bg-violet-50 border-violet-200 text-violet-700 dark:bg-violet-500/15 dark:border-violet-500/20 dark:text-violet-300'
                    : isBiz
                    ? 'bg-amber-50 border-amber-200 text-amber-700 dark:bg-amber-500/15 dark:border-amber-500/20 dark:text-amber-300'
                    : 'bg-blue-50 border-blue-200 text-blue-700 dark:bg-blue-500/15 dark:border-blue-500/20 dark:text-blue-300';

                  const categoryShortLabel = isAi
                    ? 'AI Skill'
                    : isTech
                    ? 'Technical Skill'
                    : isSoft
                    ? 'Soft Skill'
                    : isBiz
                    ? 'Business Skill'
                    : 'Process / Governance';

                  return (
                    <div
                      key={`${sk.category}-${sk.name}`}
                      className="flex flex-col justify-between rounded-xl border border-slate-200 bg-white p-3.5 shadow-2xs transition-all hover:border-slate-300 dark:border-white/10 dark:bg-white/[0.02]"
                    >
                      <div>
                        <div className="flex items-center justify-between gap-2">
                          <span className={`inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-[10px] font-bold ${badgeClass}`}>
                            {isAi && <Bot className="h-3 w-3" />}
                            {isTech && <Briefcase className="h-3 w-3" />}
                            {isSoft && <Heart className="h-3 w-3" />}
                            {isBiz && <Layers className="h-3 w-3" />}
                            {isProc && <Building2 className="h-3 w-3" />}
                            {categoryShortLabel}
                          </span>

                          <span className="text-[10px] font-medium text-slate-400">
                            {sk.sourceSteps.length} {sk.sourceSteps.length === 1 ? 'paso' : 'pasos'}
                          </span>
                        </div>

                        <h3 className="mt-2 text-xs font-bold text-slate-900 dark:text-white">
                          {sk.name}
                        </h3>

                        {sk.reason && (
                          <p className="mt-1 text-[11px] text-slate-500 dark:text-white/60 line-clamp-2">
                            {sk.reason}
                          </p>
                        )}
                      </div>

                      {/* Source steps tags */}
                      <div className="mt-3 border-t border-slate-100 pt-2.5 dark:border-white/5">
                        <p className="text-[10px] font-semibold uppercase text-slate-400 mb-1.5">
                          Requerida en:
                        </p>
                        <div className="flex flex-wrap gap-1">
                          {sk.sourceSteps.map((st) => (
                            <button
                              key={st.stepId}
                              type="button"
                              onClick={() => {
                                onOpenStep(st.stepId, 4);
                                onClose();
                              }}
                              title={`Ir al paso: ${st.stepName}`}
                              className="inline-flex items-center gap-1 rounded-md bg-slate-100 px-2 py-0.5 text-[10px] font-medium text-slate-700 hover:bg-brand-50 hover:text-brand-700 dark:bg-white/10 dark:text-white/80 dark:hover:bg-brand-500/20 dark:hover:text-brand-300 transition-colors"
                            >
                              <span>{st.stepNumber ? `Paso ${st.stepNumber}` : st.stepName}</span>
                              <ArrowRight className="h-2.5 w-2.5" />
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}

        {/* Content View: Tab 2 (Por Paso de Trabajo) */}
        {activeTab === 'byStep' && (
        <div className="flex-1 overflow-y-auto p-5">
          <table className="w-full text-left text-xs">
            <thead className="border-b border-slate-200 text-[10px] uppercase font-bold text-slate-400 dark:border-white/10">
              <tr>
                <th className="pb-2.5">Paso de Trabajo</th>
                <th className="pb-2.5">Análisis IA</th>
                <th className="pb-2.5">Habilidades (Soft / Tech / Procesos)</th>
                <th className="pb-2.5">IA (Mandatorio)</th>
                <th className="pb-2.5 text-right">Acción</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-white/5">
              {stepSummaries.map((s) => (
                <tr key={s.stepId} className="hover:bg-slate-50/60 dark:hover:bg-white/[0.02]">
                  <td className="py-2.5 font-medium text-slate-800 dark:text-white max-w-xs truncate">
                    {s.stepNumber}. {s.stepName}
                    {s.isExcluded && (
                      <span className="ml-2 rounded bg-slate-200 px-1 py-0.5 text-[9px] font-bold text-slate-600 dark:bg-white/10 dark:text-white/50">
                        Excluido
                      </span>
                    )}
                  </td>
                  <td className="py-2.5">
                    {s.hasRequirements ? (
                      <span className="inline-flex items-center gap-1 text-emerald-600 dark:text-emerald-400 font-semibold">
                        <CheckCircle2 className="h-3.5 w-3.5" /> Analizado
                      </span>
                    ) : (
                      <span className="text-slate-400">Pendiente</span>
                    )}
                  </td>
                  <td className="py-2.5 text-slate-600 dark:text-white/70">
                    {s.hasRequirements ? (
                      <div className="flex items-center gap-2">
                        <span className="rounded bg-violet-50 px-1.5 py-0.5 text-[10px] font-bold text-violet-700 dark:bg-violet-500/20 dark:text-violet-300">
                          {s.softCount} Soft
                        </span>
                        <span className="rounded bg-teal-50 px-1.5 py-0.5 text-[10px] font-bold text-teal-700 dark:bg-teal-500/20 dark:text-teal-300">
                          {s.technicalCount} Tech
                        </span>
                        <span className="rounded bg-blue-50 px-1.5 py-0.5 text-[10px] font-bold text-blue-700 dark:bg-blue-500/20 dark:text-blue-300">
                          {s.processesCount} Proc
                        </span>
                      </div>
                    ) : (
                      '—'
                    )}
                  </td>
                  <td className="py-2.5">
                    {(s.aiCount || 0) > 0 ? (
                      <span className="inline-flex items-center gap-1 rounded bg-indigo-50 px-1.5 py-0.5 text-[10px] font-bold text-indigo-700 dark:bg-indigo-500/20 dark:text-indigo-300">
                        <Bot className="h-3 w-3" />
                        {s.aiCount} AI Skill
                      </span>
                    ) : (
                      <span className="text-slate-400">—</span>
                    )}
                  </td>
                  <td className="py-2.5 text-right">
                    <button
                      type="button"
                      onClick={() => {
                        onOpenStep(s.stepId, 4);
                        onClose();
                      }}
                      className="inline-flex items-center gap-1 rounded-lg px-2.5 py-1 font-semibold text-brand-600 hover:bg-brand-50 dark:text-brand-400 dark:hover:bg-brand-500/10"
                    >
                      <span>Ver Paso</span>
                      <ArrowRight className="h-3 w-3" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        )}

        {/* Footer */}
        <div className="flex items-center justify-between border-t border-slate-200 p-4 bg-slate-50/50 dark:border-white/10 dark:bg-white/[0.02]">
          <p className="text-xs text-slate-500 dark:text-white/60">
            {totalSteps} pasos y {totalSkills} habilidades formativas registradas en la Matriz.
          </p>
          <button
            type="button"
            onClick={onClose}
            className="rounded-xl bg-slate-900 px-5 py-2 text-xs font-bold text-white shadow-sm hover:bg-slate-800 dark:bg-white dark:text-slate-900 dark:hover:bg-slate-100 transition-colors"
          >
            Aceptar y Cerrar
          </button>
        </div>
      </div>
    </div>
  );
}
