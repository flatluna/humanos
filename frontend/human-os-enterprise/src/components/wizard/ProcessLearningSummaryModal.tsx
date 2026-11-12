import { useState, useMemo } from 'react';
import {
  X,
  CheckCircle2,
  Bot,
  Database,
  Layers,
  GraduationCap,
  Heart,
  Briefcase,
  Building2,
  Search,
} from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import type { ProcessDocumentStructureDto, WorkStepRequirementsAnalysisResultDto } from '@/api/processDocumentsApi';
import { getProcessSkillRequirements } from '@/api/processDocumentsApi';
import { useAuth } from '@/auth/AuthContext';
import { getSkills } from '@/api/skillsApi';

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
  inCatalog: boolean;
}

export function ProcessLearningSummaryModal({
  isOpen,
  onClose,
  structure,
  stepSummaries,
  isSaved = false,
}: ProcessLearningSummaryModalProps) {
  const { user } = useAuth();
  const effectiveTenantId = user?.tenantId || user?.tid;
  const documentId = structure.document_id;

  const [categoryFilter, setCategoryFilter] = useState<'all' | 'technical' | 'ai' | 'soft' | 'business' | 'processes'>('all');
  const [skillSearch, setSkillSearch] = useState('');

  // Real skills actually saved to the Catalog for this process (Step 4), not the raw/stale AI analysis.
  const { data: savedProcessSkills = [] } = useQuery({
    queryKey: ['process-skill-requirements', documentId],
    queryFn: () => (documentId ? getProcessSkillRequirements(documentId) : Promise.resolve([])),
    enabled: Boolean(documentId) && isOpen,
  });

  const { data: catalogSkills = [] } = useQuery({
    queryKey: ['skills', effectiveTenantId],
    queryFn: () => (effectiveTenantId ? getSkills(effectiveTenantId) : Promise.resolve([])),
    enabled: Boolean(effectiveTenantId) && isOpen,
  });

  const catalogSkillById = useMemo(() => {
    const map = new Map<string, (typeof catalogSkills)[number]>();
    for (const s of catalogSkills) {
      if (s.skillId) map.set(s.skillId.toLowerCase(), s);
    }
    return map;
  }, [catalogSkills]);

  const totalSteps = stepSummaries.length;
  const analyzedSteps = stepSummaries.filter((s) => s.hasRequirements).length;

  const categoryMeta = (
    category: string,
    isAiSkill: boolean,
  ): { category: GroupedSkillItem['category']; label: string } => {
    if (isAiSkill || category === 'ai') return { category: 'ai', label: 'AI Skills (Habilidades de IA)' };
    const normalized = category.toLowerCase();
    if (normalized === 'technical' || normalized === 'job') return { category: 'technical', label: 'Technical Skills (Habilidades Técnicas)' };
    if (normalized === 'soft') return { category: 'soft', label: 'Soft Skills (Habilidades Blandas)' };
    if (normalized === 'business') return { category: 'business', label: 'Business Skills (Negocio y Estrategia)' };
    if (normalized === 'hr') return { category: 'hr', label: 'HR Processes (Políticas y RRHH)' };
    return { category: 'dept', label: 'Department Processes (Procesos Operativos)' };
  };

  // Group the actually-saved process skills by type/category (single source of truth, matches the main matrix).
  const groupedSkills = useMemo(() => {
    const items: GroupedSkillItem[] = [];
    for (const req of savedProcessSkills) {
      const matched = req.matchedSkillId ? catalogSkillById.get(req.matchedSkillId.toLowerCase()) : undefined;
      const { category, label } = categoryMeta(req.category, req.isAiSkill);
      items.push({
        name: matched?.name || req.skillName,
        reason: req.reason || 'Requerimiento identificado para la operación',
        category,
        categoryLabel: label,
        isAi: req.isAiSkill,
        inCatalog: Boolean(matched),
      });
    }
    return items;
  }, [savedProcessSkills, catalogSkillById]);

  const totalSkills = groupedSkills.length;
  const totalAiSkills = groupedSkills.filter((s) => s.isAi).length;
  const totalTools = stepSummaries.reduce((acc, s) => acc + (s.toolsCount || 0), 0);

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
        return s.name.toLowerCase().includes(q) || s.reason.toLowerCase().includes(q);
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

        {/* Filters (category pills + search) */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 bg-slate-50/50 px-5 py-2.5 dark:border-white/10 dark:bg-white/[0.02]">
          <div className="inline-flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold shadow-xs dark:border-white/10 dark:bg-white/5">
            <GraduationCap className="h-4 w-4 text-brand-600 dark:text-brand-400" />
            <span>Skills Required ({groupedSkills.length})</span>
          </div>

          {(
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

        {/* Content: Skills Required (grouped by type) */}
        {(
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
                        </div>

                        <h3 className="mt-2 text-xs font-bold text-slate-900 dark:text-white">
                          {sk.name}
                        </h3>

                        {sk.reason && (
                          <p className="mt-1 text-[11px] text-slate-500 dark:text-white/60 line-clamp-2">
                            {sk.reason}
                          </p>
                        )}

                        <p className="mt-2 text-[10px] font-semibold uppercase text-slate-400">
                          {sk.inCatalog ? (
                            <span className="text-emerald-600 dark:text-emerald-400">En catálogo ✓</span>
                          ) : (
                            <span className="text-amber-600 dark:text-amber-400">Nuevo (no en catálogo)</span>
                          )}
                        </p>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
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
