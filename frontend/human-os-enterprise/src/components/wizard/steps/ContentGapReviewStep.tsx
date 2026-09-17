import { useState, useMemo } from 'react';
import { useTranslation, Trans } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import {
  ShieldAlert,
  CheckCircle2,
  RefreshCw,
  BookOpen,
  Plus,
  Check,
  Bot,
  Table as TableIcon,
  List,
  Trash2,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { createSkill, getSkills, type SkillCategory, type SkillDto } from '@/api/skillsApi';
import type {
  ProcessStepDto,
  ProcessDocumentStructureDto,
  WorkStepRequirementsAnalysisResultDto,
  LearningContentCoverageResponseDto,
  LearningContentGapDraftResponseDto,
  ContentGapItemDto,
} from '@/api/processDocumentsApi';

export interface SkillMatrixItem {
  name: string;
  skillId?: string;
  status: 'ready' | 'pending' | 'new';
  category: string;
  isAi?: boolean;
  course?: string;
  gap?: ContentGapItemDto;
  isInCatalog?: boolean;
}

export interface ContentGapReviewStepProps {
  step: ProcessStepDto;
  structure?: ProcessDocumentStructureDto | null;
  allStepRequirements?: Record<string, WorkStepRequirementsAnalysisResultDto>;
  allStepCoverages?: Record<string, LearningContentCoverageResponseDto>;
  coverageResult?: LearningContentCoverageResponseDto | null;
  gapDraftResult?: LearningContentGapDraftResponseDto | null;
  isLoading?: boolean;
  onEvaluateCoverage?: (stepId: string) => Promise<void>;
  onEvaluateAllSteps?: () => Promise<void>;
  isBatchEvaluating?: boolean;
  batchProgress?: { current: number; total: number } | null;
  onSelectStep?: (stepId: string) => void;
  onApproveAndContinue: () => void;
  onRemoveSkillFromStep?: (stepId: string, skillName: string) => void;
  isSavingProcess?: boolean;
}

export function ContentGapReviewStep({
  step,
  structure,
  allStepRequirements = {},
  allStepCoverages = {},
  coverageResult: _coverageResult,
  gapDraftResult: _gapDraftResult,
  isLoading: _isLoading,
  onEvaluateCoverage: _onEvaluateCoverage,
  onEvaluateAllSteps: _onEvaluateAllSteps,
  isBatchEvaluating: _isBatchEvaluating,
  batchProgress: _batchProgress,
  onSelectStep,
  onApproveAndContinue,
  onRemoveSkillFromStep,
  isSavingProcess,
}: ContentGapReviewStepProps) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const effectiveTenantId = user?.tenantId || user?.tid;

  const [activeTab, setActiveTab] = useState<'matrix' | 'stepDetail'>('matrix');
  const [registeringSkillName, setRegisteringSkillName] = useState<string | null>(null);
  const [registeredSkillNames, setRegisteredSkillNames] = useState<Set<string>>(new Set());
  const [isRegisteringAll, setIsRegisteringAll] = useState(false);

  // Load tenant skill catalog directly from database for instantaneous, ground-truth matching
  const { data: catalogSkills = [], refetch: refetchCatalog } = useQuery({
    queryKey: ['skills', effectiveTenantId],
    queryFn: () => (effectiveTenantId ? getSkills(effectiveTenantId) : Promise.resolve([])),
    enabled: Boolean(effectiveTenantId),
  });

  // Fast normalization helper
  const normalizeKey = (str: string) =>
    str.trim().toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^a-z0-9]/g, '');

  const catalogSkillMap = useMemo(() => {
    const map = new Map<string, SkillDto>();
    for (const s of catalogSkills) {
      if (s.name) {
        map.set(s.name.trim().toLowerCase(), s);
        const norm = normalizeKey(s.name);
        if (norm) {
          map.set(norm, s);
        }
      }
    }
    return map;
  }, [catalogSkills]);

  // Global Matrix Data across all process steps
  const allProcessSteps = useMemo(() => {
    if (!structure) return [step];
    return structure.chapters.flatMap((c) => c.steps);
  }, [structure, step]);

  // Aggregate matrix rows with skills categorized
  const matrixRows = useMemo(() => {
    return allProcessSteps.map((st) => {
      const reqs = allStepRequirements[st.step_id];
      const cov = allStepCoverages[st.step_id];

      const soft: Array<{ name: string; skillId?: string; status: 'ready' | 'pending' | 'new'; course?: string; gap?: ContentGapItemDto; category: string; isAi?: boolean }> = [];
      const technical: Array<{ name: string; skillId?: string; status: 'ready' | 'pending' | 'new'; course?: string; gap?: ContentGapItemDto; category: string; isAi?: boolean }> = [];
      const processes: Array<{ name: string; skillId?: string; status: 'ready' | 'pending' | 'new'; course?: string; gap?: ContentGapItemDto; category: string; isAi?: boolean }> = [];
      const ai: Array<{ name: string; skillId?: string; status: 'ready' | 'pending' | 'new'; course?: string; gap?: ContentGapItemDto; category: string; isAi?: boolean }> = [];

      if (reqs?.requiredSkillAreas) {
        const processSkill = (name: string, category: string, isAi?: boolean, skillId?: string) => {
          const lowerName = name.trim().toLowerCase();
          const normKey = normalizeKey(name);
          const catalogEntry = catalogSkillMap.get(lowerName) || catalogSkillMap.get(normKey);

          // Check in coverage if available
          const covered = cov?.coveredRequirements.find((c) => c.requirement.toLowerCase() === lowerName || normalizeKey(c.requirement) === normKey);
          const gap = cov?.contentGaps.find((g) => g.requirement.toLowerCase() === lowerName || normalizeKey(g.requirement) === normKey);

          let status: 'ready' | 'pending' | 'new' = 'new';
          let course: string | undefined = undefined;

          if (covered) {
            status = 'ready';
            course = covered.matchedCapabilityName || 'Course Active';
          } else if (catalogEntry) {
            if (catalogEntry.matchedCapabilityName || catalogEntry.matchedCapabilityId) {
              status = 'ready';
              course = catalogEntry.matchedCapabilityName || 'Course Active';
            } else {
              status = 'pending'; // In catalog, but course not created yet
            }
          } else if (gap?.isInCatalog || registeredSkillNames.has(name) || registeredSkillNames.has(lowerName)) {
            status = 'pending';
          } else {
            status = 'new';
          }

          const resolvedId = skillId || catalogEntry?.skillId || gap?.skillId || covered?.skillId;
          const isAiSkillName = lowerName.includes('prompt engineering') ||
                                lowerName.includes('introduction to ai') ||
                                lowerName.includes('ai-assisted') ||
                                lowerName.includes('artificial intelligence') ||
                                lowerName.includes('inteligencia artificial');
          const isAiSkill = Boolean(isAi || isAiSkillName);
          const item = { name, skillId: resolvedId, status, course, gap, category, isAi: isAiSkill };

          if (isAiSkill) {
            ai.push(item);
          } else if (category === 'soft-skills') {
            soft.push(item);
          } else if (category === 'department-processes' || category === 'hr-processes') {
            processes.push(item);
          } else {
            technical.push(item);
          }
        };

        (reqs.requiredSkillAreas.softSkills || []).forEach((s) => processSkill(s.name, 'soft-skills', false, s.skillId));
        (reqs.requiredSkillAreas.technicalSkills || []).forEach((s) => processSkill(s.name, 'job-skills', false, s.skillId));
        (reqs.requiredSkillAreas.businessSkills || []).forEach((s) => processSkill(s.name, 'job-skills', false, s.skillId));
        (reqs.requiredSkillAreas.departmentProcesses || []).forEach((s) => processSkill(s.name, 'department-processes', false, s.skillId));
        (reqs.requiredSkillAreas.hrProcesses || []).forEach((s) => processSkill(s.name, 'hr-processes', false, s.skillId));
        (reqs.requiredSkillAreas.aiSkills || []).forEach((s) => processSkill(s.name, 'job-skills', true, s.skillId));
      }

      return {
        step: st,
        hasCoverage: !!cov,
        soft,
        technical,
        processes,
        ai,
      };
    });
  }, [allProcessSteps, allStepRequirements, allStepCoverages, catalogSkillMap, registeredSkillNames]);

  // Global unique counts
  const globalStats = useMemo(() => {
    const readySet = new Set<string>();
    const pendingSet = new Set<string>();
    const newSet = new Set<string>();

    for (const row of matrixRows) {
      for (const item of [...row.soft, ...row.technical, ...row.processes, ...row.ai]) {
        const key = item.name.toLowerCase().trim();
        if (item.status === 'ready') readySet.add(key);
        else if (item.status === 'pending') pendingSet.add(key);
        else newSet.add(key);
      }
    }

    // Remove duplicates from newSet if in pendingSet or readySet
    for (const name of pendingSet) newSet.delete(name);
    for (const name of readySet) {
      pendingSet.delete(name);
      newSet.delete(name);
    }

    return {
      totalSteps: allProcessSteps.length,
      totalUniqueSkills: readySet.size + pendingSet.size + newSet.size,
      readyCount: readySet.size,
      pendingCount: pendingSet.size,
      newCount: newSet.size,
    };
  }, [matrixRows, allProcessSteps.length]);

  // Partition skills for focused active step breakdown view:
  const activeStepPartition = useMemo(() => {
    const currentRow = matrixRows.find((r) => r.step.step_id === step.step_id);
    if (!currentRow) return { ready: [], pending: [], newSkills: [] };

    const allItems = [...currentRow.soft, ...currentRow.technical, ...currentRow.processes, ...currentRow.ai];
    return {
      ready: allItems.filter((i) => i.status === 'ready'),
      pending: allItems.filter((i) => i.status === 'pending'),
      newSkills: allItems.filter((i) => i.status === 'new'),
    };
  }, [matrixRows, step.step_id]);

  // Register single skill in catalog
  const handleRegisterSkill = async (skillName: string, category: string) => {
    if (!effectiveTenantId) {
      alert('Tenant ID is required.');
      return;
    }

    setRegisteringSkillName(skillName);
    try {
      const validCategory: SkillCategory = ['soft-skills', 'job-skills', 'department-processes', 'hr-processes'].includes(category)
        ? (category as SkillCategory)
        : 'job-skills';

      await createSkill(effectiveTenantId, {
        name: skillName,
        category: validCategory,
        description: `Identified from process: ${structure?.process_name || 'Standard Process'}`,
      });

      setRegisteredSkillNames((prev) => new Set(prev).add(skillName).add(skillName.trim().toLowerCase()));
      await refetchCatalog();
    } catch (err: any) {
      console.error('Failed to register skill in catalog:', err);
      alert(err?.response?.data?.message || 'Error registering skill in catalog.');
    } finally {
      setRegisteringSkillName(null);
    }
  };

  // Register all new skills across the process with 1 click
  const handleRegisterAllNewSkills = async () => {
    if (!effectiveTenantId) return;
    setIsRegisteringAll(true);

    const skillsToAdd = new Map<string, SkillCategory>();
    for (const row of matrixRows) {
      for (const item of [...row.soft, ...row.technical, ...row.processes, ...row.ai]) {
        const lower = item.name.toLowerCase().trim();
        if (item.status === 'new' && !registeredSkillNames.has(item.name) && !registeredSkillNames.has(lower)) {
          const validCat: SkillCategory = ['soft-skills', 'job-skills', 'department-processes', 'hr-processes'].includes(item.category)
            ? (item.category as SkillCategory)
            : 'job-skills';
          skillsToAdd.set(item.name, validCat);
        }
      }
    }

    let addedCount = 0;
    for (const [name, cat] of skillsToAdd.entries()) {
      try {
        await createSkill(effectiveTenantId, {
          name,
          category: cat,
          description: `Identified from process: ${structure?.process_name || 'Standard Process'}`,
        });
        setRegisteredSkillNames((prev) => new Set(prev).add(name).add(name.toLowerCase().trim()));
        addedCount++;
      } catch (err) {
        console.warn(`Could not add skill ${name}:`, err);
      }
    }

    await refetchCatalog();
    setIsRegisteringAll(false);
    if (addedCount > 0) {
      alert(t('processLearning.coverageStep.allSkillsAddedSuccess'));
    }
  };

  const renderSkillPill = (
    item: { name: string; skillId?: string; status: 'ready' | 'pending' | 'new'; course?: string; gap?: ContentGapItemDto; category: string },
    stepId: string,
  ) => {
    const isRegistering = registeringSkillName === item.name;
    const isRegistered = registeredSkillNames.has(item.name) || registeredSkillNames.has(item.name.toLowerCase().trim()) || item.status === 'pending';
    const effectiveStatus = isRegistered && item.status === 'new' ? 'pending' : item.status;

    return (
      <div
        key={item.name}
        className={`group relative flex items-start justify-between gap-1.5 rounded-lg border p-2 text-xs transition-all ${
          effectiveStatus === 'ready'
            ? 'border-emerald-200 bg-emerald-50/50 dark:border-emerald-500/20 dark:bg-emerald-500/5'
            : effectiveStatus === 'pending'
            ? 'border-amber-200 bg-amber-50/50 dark:border-amber-500/20 dark:bg-amber-500/5'
            : 'border-rose-200 bg-rose-50/50 dark:border-rose-500/20 dark:bg-rose-500/5'
        }`}
      >
        <div className="space-y-1 min-w-0 flex-1">
          <div className="flex items-center gap-1.5">
            <span
              className={`h-2 w-2 rounded-full shrink-0 ${
                effectiveStatus === 'ready'
                  ? 'bg-emerald-500'
                  : effectiveStatus === 'pending'
                  ? 'bg-amber-500'
                  : 'bg-rose-500'
              }`}
            />
            <p className="font-bold text-slate-900 dark:text-white truncate" title={item.name}>
              {item.name}
            </p>
          </div>
          {item.skillId && (
            <span className="inline-block rounded bg-slate-100 dark:bg-white/10 px-1.5 py-0.2 text-[8.5px] font-mono font-medium text-slate-500 dark:text-white/50">
              {item.skillId}
            </span>
          )}
          {effectiveStatus === 'ready' && item.course && (
            <p className="text-[10px] text-emerald-700 dark:text-emerald-400 font-medium truncate">
              ✓ {item.course}
            </p>
          )}
          {effectiveStatus === 'pending' && (
            <p className="text-[10px] text-amber-700 dark:text-amber-400 font-medium">
              {t('processLearning.coverageStep.partialBadge')}
            </p>
          )}
          {effectiveStatus === 'new' && (
            <p className="text-[10px] text-rose-700 dark:text-rose-400 font-medium">
              {t('processLearning.coverageStep.uncoveredBadge')}
            </p>
          )}
        </div>

        <div className="flex items-center gap-1 shrink-0">
          {effectiveStatus === 'new' && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                handleRegisterSkill(item.name, item.category);
              }}
              disabled={isRegistering}
              className="rounded bg-brand-600 px-1.5 py-0.5 text-[9px] font-bold text-white hover:bg-brand-500 disabled:opacity-50 transition-colors"
              title={t('processLearning.coverageStep.addToCatalogBtn')}
            >
              {isRegistering ? '...' : '+ Add'}
            </button>
          )}
          {onRemoveSkillFromStep && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                onRemoveSkillFromStep(stepId, item.name);
              }}
              className="rounded p-1 text-slate-300 hover:text-rose-600 hover:bg-rose-50 dark:text-white/30 dark:hover:text-rose-300 dark:hover:bg-rose-500/20 transition-colors"
              title="Eliminar habilidad del proceso"
            >
              <Trash2 className="h-3 w-3" />
            </button>
          )}
        </div>
      </div>
    );
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
        <div>
          <div className="flex items-center gap-2">
            <span className="rounded bg-emerald-100 px-2 py-0.5 text-xs font-bold text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300">
              {t('processLearning.coverageStep.wizardStepBadge', 'Paso 4: Finalizar y Guardar')}
            </span>
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {t('processLearning.coverageStep.title')}
            </h3>
          </div>
          <p className="mt-1 text-xs text-slate-500 dark:text-white/60">
            {t('processLearning.coverageStep.subtitle')}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {/* Tab Switcher */}
          <div className="inline-flex items-center rounded-xl border border-slate-200 bg-slate-50 p-0.5 text-xs font-semibold text-slate-600 dark:border-white/10 dark:bg-white/5 dark:text-white/70">
            <button
              type="button"
              onClick={() => setActiveTab('matrix')}
              className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs transition-colors ${
                activeTab === 'matrix'
                  ? 'bg-white font-bold text-brand-700 shadow-sm dark:bg-white/15 dark:text-white'
                  : 'hover:text-slate-900 dark:hover:text-white'
              }`}
            >
              <TableIcon className="h-3.5 w-3.5" />
              <span>{t('processLearning.coverageStep.matrixTab')}</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('stepDetail')}
              className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs transition-colors ${
                activeTab === 'stepDetail'
                  ? 'bg-white font-bold text-brand-700 shadow-sm dark:bg-white/15 dark:text-white'
                  : 'hover:text-slate-900 dark:hover:text-white'
              }`}
            >
              <List className="h-3.5 w-3.5" />
              <span>{t('processLearning.coverageStep.stepDetailTab')}</span>
            </button>
          </div>

          <button
            type="button"
            onClick={onApproveAndContinue}
            disabled={isSavingProcess}
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-emerald-600 to-brand-600 px-5 py-2 text-xs font-bold text-white shadow-sm hover:from-emerald-500 hover:to-brand-500 disabled:opacity-50 transition-all"
          >
            {isSavingProcess ? (
              <>
                <RefreshCw className="h-4 w-4 animate-spin text-white" />
                <span>Guardando Proceso y Matriz...</span>
              </>
            ) : (
              <>
                <CheckCircle2 className="h-4 w-4" />
                <span>{t('processLearning.coverageStep.continueBtn')}</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* Global Process Training Matrix View (Default) */}
      {activeTab === 'matrix' ? (
        <div className="space-y-6">
          {/* Executive Summary Cards */}
          <div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
            <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/40 text-center">
              <p className="text-2xl font-black text-slate-900 dark:text-white">{globalStats.totalSteps}</p>
              <p className="text-[10px] font-bold uppercase tracking-wider text-slate-400 mt-0.5">{t('processLearning.coverageStep.kpiSteps')}</p>
            </div>
            <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/40 text-center">
              <p className="text-2xl font-black text-brand-600 dark:text-brand-400">{globalStats.totalUniqueSkills}</p>
              <p className="text-[10px] font-bold uppercase tracking-wider text-slate-400 mt-0.5">{t('processLearning.coverageStep.kpiTotalSkills')}</p>
            </div>
            <div className="rounded-2xl border border-emerald-200 bg-emerald-50/40 p-4 shadow-sm dark:border-emerald-500/20 dark:bg-emerald-950/20 text-center">
              <p className="text-2xl font-black text-emerald-600 dark:text-emerald-400">{globalStats.readyCount}</p>
              <p className="text-[10px] font-bold uppercase tracking-wider text-emerald-700 dark:text-emerald-300 mt-0.5">{t('processLearning.coverageStep.kpiReadyInStudio')}</p>
            </div>
            <div className="rounded-2xl border border-amber-200 bg-amber-50/40 p-4 shadow-sm dark:border-amber-500/20 dark:bg-amber-950/20 text-center">
              <p className="text-2xl font-black text-amber-600 dark:text-amber-400">{globalStats.pendingCount}</p>
              <p className="text-[10px] font-bold uppercase tracking-wider text-amber-700 dark:text-amber-300 mt-0.5">{t('processLearning.coverageStep.kpiPendingCourse')}</p>
            </div>
            <div className="rounded-2xl border border-rose-200 bg-rose-50/40 p-4 shadow-sm dark:border-rose-500/20 dark:bg-rose-950/20 text-center">
              <p className="text-2xl font-black text-rose-600 dark:text-rose-400">{globalStats.newCount}</p>
              <p className="text-[10px] font-bold uppercase tracking-wider text-rose-700 dark:text-rose-300 mt-0.5">{t('processLearning.coverageStep.kpiNotInCatalog')}</p>
            </div>
          </div>

          {/* Action Banner for Bulk Registration */}
          {globalStats.newCount > 0 && (
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 rounded-2xl border border-rose-200 bg-rose-50/50 p-4 text-xs text-rose-900 dark:border-rose-500/20 dark:bg-rose-950/20 dark:text-rose-200">
              <div className="flex items-center gap-2.5">
                <ShieldAlert className="h-5 w-5 text-rose-600 shrink-0" />
                <p>
                  <Trans
                    i18nKey="processLearning.coverageStep.newSkillsBanner"
                    values={{ count: globalStats.newCount }}
                    components={{ 1: <span className="font-bold" /> }}
                  />
                </p>
              </div>
              <button
                type="button"
                onClick={handleRegisterAllNewSkills}
                disabled={isRegisteringAll}
                className="inline-flex items-center justify-center gap-2 rounded-xl bg-rose-600 px-4 py-2 font-bold text-white shadow-sm hover:bg-rose-500 disabled:opacity-50 transition-colors shrink-0"
              >
                {isRegisteringAll ? (
                  <>
                    <RefreshCw className="h-4 w-4 animate-spin" />
                    <span>{t('processLearning.coverageStep.addingAllSkillsText')}</span>
                  </>
                ) : (
                  <>
                    <Plus className="h-4 w-4" />
                    <span>{t('processLearning.coverageStep.addAllNewSkillsBtn', { count: globalStats.newCount })}</span>
                  </>
                )}
              </button>
            </div>
          )}

          {/* The Process Training Matrix Table */}
          <div className="rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/40 overflow-hidden">
            <div className="border-b border-slate-200 bg-slate-50/80 p-4 dark:border-white/5 dark:bg-white/5">
              <h4 className="text-sm font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <TableIcon className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                <span>{t('processLearning.coverageStep.matrixTitle')}</span>
              </h4>
              <p className="text-xs text-slate-500 dark:text-white/60 mt-0.5">
                {t('processLearning.coverageStep.matrixSubtitle')}
              </p>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead>
                  <tr className="border-b border-slate-200 bg-slate-100/50 dark:border-white/5 dark:bg-white/[0.02] text-slate-500 dark:text-white/60 font-bold uppercase tracking-wider text-[10px]">
                    <th className="p-4 w-52">{t('processLearning.coverageStep.colStep')}</th>
                    <th className="p-4 min-w-[200px]">{t('processLearning.coverageStep.colSoftSkills')}</th>
                    <th className="p-4 min-w-[220px]">{t('processLearning.coverageStep.colTechnicalSkills')}</th>
                    <th className="p-4 min-w-[200px]">{t('processLearning.coverageStep.colProcesses')}</th>
                    <th className="p-4 min-w-[200px]">{t('processLearning.coverageStep.colAiSkills')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 dark:divide-white/5">
                  {matrixRows.map((row) => {
                    const isSelected = row.step.step_id === step.step_id;

                    return (
                      <tr
                        key={row.step.step_id}
                        onClick={() => onSelectStep?.(row.step.step_id)}
                        className={`transition-colors cursor-pointer hover:bg-slate-50/80 dark:hover:bg-white/[0.02] ${
                          isSelected ? 'bg-brand-50/40 dark:bg-brand-500/5' : ''
                        }`}
                      >
                        {/* Step Column */}
                        <td className="p-4 align-top">
                          <div className="space-y-1">
                            <span className="inline-block rounded bg-brand-100 px-2 py-0.5 text-[10px] font-bold text-brand-800 dark:bg-brand-500/20 dark:text-brand-300">
                              {t('processLearning.coverageStep.stepLabel', { number: row.step.step_number })}
                            </span>
                            <p className="font-bold text-slate-900 dark:text-white">{row.step.step_name}</p>
                          </div>
                        </td>

                        {/* Soft Skills */}
                        <td className="p-4 align-top">
                          <div className="space-y-1.5">
                            {row.soft.length > 0 ? (
                              row.soft.map((item) => renderSkillPill(item, row.step.step_id))
                            ) : (
                              <span className="text-slate-400 dark:text-white/30 text-[11px]">—</span>
                            )}
                          </div>
                        </td>

                        {/* Technical / Job Skills */}
                        <td className="p-4 align-top">
                          <div className="space-y-1.5">
                            {row.technical.length > 0 ? (
                              row.technical.map((item) => renderSkillPill(item, row.step.step_id))
                            ) : (
                              <span className="text-slate-400 dark:text-white/30 text-[11px]">—</span>
                            )}
                          </div>
                        </td>

                        {/* Processes (Dept / HR) */}
                        <td className="p-4 align-top">
                          <div className="space-y-1.5">
                            {row.processes.length > 0 ? (
                              row.processes.map((item) => renderSkillPill(item, row.step.step_id))
                            ) : (
                              <span className="text-slate-400 dark:text-white/30 text-[11px]">—</span>
                            )}
                          </div>
                        </td>

                        {/* AI Skills (Mandatory) */}
                        <td className="p-4 align-top">
                          <div className="space-y-1.5">
                            {row.ai.length > 0 ? (
                              row.ai.map((item) => renderSkillPill(item, row.step.step_id))
                            ) : (
                              <span className="text-slate-400 dark:text-white/30 text-[11px]">—</span>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {/* Bottom Finalize Bar */}
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 p-5 border-t border-slate-200 bg-slate-50/80 dark:border-white/5 dark:bg-white/[0.02]">
              <div className="text-xs text-slate-600 dark:text-white/70">
                <p className="font-bold text-slate-900 dark:text-white">
                  {t('processLearning.coverageStep.footerSummary', { steps: globalStats.totalSteps, skills: globalStats.totalUniqueSkills })}
                </p>
                <p className="text-[11px] text-slate-500 dark:text-white/50 mt-0.5">
                  Se persistirá la estructura completa, entregables y matriz relacional en Azure SQL.
                </p>
              </div>
              <button
                type="button"
                onClick={onApproveAndContinue}
                disabled={isSavingProcess}
                className="inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-emerald-600 to-brand-600 px-6 py-2.5 text-xs font-bold text-white shadow-md hover:from-emerald-500 hover:to-brand-500 disabled:opacity-50 transition-all shrink-0"
              >
                {isSavingProcess ? (
                  <>
                    <RefreshCw className="h-4 w-4 animate-spin text-white" />
                    <span>Guardando Proceso y Matriz...</span>
                  </>
                ) : (
                  <>
                    <CheckCircle2 className="h-4 w-4" />
                    <span>{t('processLearning.coverageStep.continueBtn')}</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      ) : (
        /* Focused Step Breakdown View */
        <div className="space-y-6">
          {/* Section 1: Covered Skills (Ready with Course in Studio) */}
          {activeStepPartition.ready.length > 0 && (
            <div className="rounded-2xl border border-emerald-200 bg-white p-5 shadow-sm dark:border-emerald-500/20 dark:bg-slate-900/40 space-y-3">
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-emerald-800 dark:text-emerald-400">
                <CheckCircle2 className="h-4 w-4" />
                <span>{t('processLearning.coverageStep.coveredListTitle', { count: activeStepPartition.ready.length })}</span>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {activeStepPartition.ready.map((cov, i) => (
                  <div key={i} className="rounded-xl border border-emerald-100 bg-emerald-50/30 p-4 text-xs dark:border-white/5 dark:bg-white/[0.02] space-y-2">
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex items-center gap-2">
                        {cov.isAi ? <Bot className="h-4 w-4 text-indigo-600" /> : <BookOpen className="h-4 w-4 text-emerald-600" />}
                        <p className="font-bold text-slate-900 dark:text-white">{cov.name}</p>
                      </div>
                      <span className="rounded bg-emerald-100 px-1.5 py-0.5 text-[9px] font-bold text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300">
                        {cov.isAi ? 'AI Skill' : cov.category || 'job-skills'}
                      </span>
                    </div>
                    {cov.course && (
                      <p className="text-[11px] text-emerald-800 dark:text-emerald-300 font-medium">
                        {t('processLearning.coverageStep.courseInStudio', { name: cov.course })}
                      </p>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Section 2: Skills in Catalog (Course Pending Creation in Studio) */}
          {activeStepPartition.pending.length > 0 && (
            <div className="rounded-2xl border border-amber-200 bg-white p-5 shadow-sm dark:border-amber-500/20 dark:bg-slate-900/40 space-y-3">
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-amber-800 dark:text-amber-400">
                <BookOpen className="h-4 w-4" />
                <span>{t('processLearning.coverageStep.inCatalogListTitle', { count: activeStepPartition.pending.length })}</span>
              </div>
              <div className="space-y-3">
                {activeStepPartition.pending.map((sk, i) => (
                  <div key={i} className="rounded-xl border border-amber-100 bg-amber-50/20 p-4 text-xs dark:border-white/5 dark:bg-white/[0.02] space-y-2">
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex items-center gap-2">
                        {sk.isAi ? <Bot className="h-4 w-4 text-indigo-600" /> : <BookOpen className="h-4 w-4 text-amber-600" />}
                        <p className="font-bold text-slate-900 dark:text-white">{sk.name}</p>
                      </div>
                      <div className="flex items-center gap-1.5">
                        <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[9px] font-bold text-amber-800 dark:bg-amber-500/20 dark:text-amber-300">
                          {sk.isAi ? 'AI Skill' : sk.category || 'job-skills'}
                        </span>
                        <span className="rounded-full bg-amber-100 px-2.5 py-0.5 text-[10px] font-bold text-amber-800 dark:bg-amber-500/20 dark:text-amber-300">
                          {t('processLearning.coverageStep.partialBadge')}
                        </span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Section 3: New Skills to Register in Catalog */}
          {activeStepPartition.newSkills.length > 0 && (
            <div className="rounded-2xl border border-rose-200 bg-white p-5 shadow-sm dark:border-rose-500/20 dark:bg-slate-900/40 space-y-3">
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-rose-800 dark:text-rose-400">
                <ShieldAlert className="h-4 w-4" />
                <span>{t('processLearning.coverageStep.newSkillsListTitle', { count: activeStepPartition.newSkills.length })}</span>
              </div>
              <div className="space-y-3">
                {activeStepPartition.newSkills.map((gap, i) => {
                  const isRegistered = registeredSkillNames.has(gap.name);
                  const isRegistering = registeringSkillName === gap.name;

                  return (
                    <div key={i} className="rounded-xl border border-rose-100 bg-rose-50/20 p-4 text-xs dark:border-white/5 dark:bg-white/[0.02] space-y-2">
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex items-center gap-2">
                          {gap.isAi ? <Bot className="h-4 w-4 text-indigo-600" /> : <BookOpen className="h-4 w-4 text-rose-600" />}
                          <p className="font-bold text-slate-900 dark:text-white">{gap.name}</p>
                        </div>
                        <div className="flex items-center gap-1.5">
                          <span className="rounded bg-rose-100 px-1.5 py-0.5 text-[9px] font-bold text-rose-800 dark:bg-rose-500/20 dark:text-rose-300">
                            {gap.isAi ? 'AI Skill' : gap.category || 'job-skills'}
                          </span>
                          <span className="rounded-full bg-rose-100 px-2.5 py-0.5 text-[10px] font-bold text-rose-800 dark:bg-rose-500/20 dark:text-rose-300">
                            {t('processLearning.coverageStep.uncoveredBadge')}
                          </span>
                        </div>
                      </div>

                      <div className="flex items-center justify-between pt-1">
                        <p className="text-[10px] text-slate-400 dark:text-white/50">
                          {t('processLearning.coverageStep.blockingNotice')}
                        </p>

                        <button
                          type="button"
                          onClick={() => handleRegisterSkill(gap.name, gap.category)}
                          disabled={isRegistered || isRegistering}
                          className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-sm hover:bg-brand-500 disabled:bg-emerald-600 disabled:opacity-90 transition-colors"
                        >
                          {isRegistered ? (
                            <>
                              <Check className="h-3.5 w-3.5" />
                              <span>{t('processLearning.coverageStep.addedToCatalogBadge')}</span>
                            </>
                          ) : isRegistering ? (
                            <>
                              <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                              <span>Guardando...</span>
                            </>
                          ) : (
                            <>
                              <Plus className="h-3.5 w-3.5" />
                              <span>{t('processLearning.coverageStep.addToCatalogBtn')}</span>
                            </>
                          )}
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Bottom Save Bar in Step Detail */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 p-5 rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/40">
            <div className="text-xs text-slate-600 dark:text-white/70">
              <p className="font-bold text-slate-900 dark:text-white">
                {t('processLearning.coverageStep.footerSummary', { steps: globalStats.totalSteps, skills: globalStats.totalUniqueSkills })}
              </p>
            </div>
            <button
              type="button"
              onClick={onApproveAndContinue}
              disabled={isSavingProcess}
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-emerald-600 to-brand-600 px-6 py-2.5 text-xs font-bold text-white shadow-md hover:from-emerald-500 hover:to-brand-500 disabled:opacity-50 transition-all shrink-0"
            >
              {isSavingProcess ? (
                <>
                  <RefreshCw className="h-4 w-4 animate-spin text-white" />
                  <span>Guardando Proceso y Matriz...</span>
                </>
              ) : (
                <>
                  <CheckCircle2 className="h-4 w-4" />
                  <span>{t('processLearning.coverageStep.continueBtn')}</span>
                </>
              )}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
