import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '@/auth/AuthContext';
import { useQuery } from '@tanstack/react-query';
import { getSkills } from '@/api/skillsApi';
import {
  Sparkles,
  RefreshCw,
  CheckCircle2,
  AlertTriangle,
  Plus,
  Trash2,
  Edit2,
  Check,
  FileCheck2,
  Wrench,
  Heart,
  Cpu,
  Bot,
  Building2,
  ShieldCheck,
} from 'lucide-react';
import type {
  WorkStepRequirementsAnalysisResultDto,
  ProcessStepDto,
} from '@/api/processDocumentsApi';

export interface WorkRequirementsReviewStepProps {
  documentId?: string;
  step: ProcessStepDto;
  requirements: WorkStepRequirementsAnalysisResultDto | null;
  isLoading: boolean;
  onAnalyzeStep: (stepId: string) => Promise<void>;
  onAnalyzeAllSteps?: () => Promise<void>;
  isBatchAnalyzing?: boolean;
  batchProgress?: { current: number; total: number } | null;
  onUpdateRequirements: (updated: WorkStepRequirementsAnalysisResultDto) => void;
  onRemoveSkill?: (categoryKey: 'softSkills' | 'technicalSkills' | 'businessSkills' | 'departmentProcesses' | 'hrProcesses' | 'aiSkills', idx: number) => void;
  onApproveAndContinue: () => void;
  onSave?: () => void;
  isSavingProcess?: boolean;
  isEditMode?: boolean;
  hasUnsavedChanges?: boolean;
}

export function WorkRequirementsReviewStep({
  step,
  requirements,
  isLoading,
  onAnalyzeStep,
  onAnalyzeAllSteps,
  isBatchAnalyzing,
  batchProgress,
  onUpdateRequirements,
  onRemoveSkill,
  onApproveAndContinue,
  onSave,
  isSavingProcess = false,
  isEditMode = false,
  hasUnsavedChanges = false,
}: WorkRequirementsReviewStepProps) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const effectiveTenantId = user?.tenantId || user?.tid;

  const { data: catalogSkills = [] } = useQuery({
    queryKey: ['skills', effectiveTenantId],
    queryFn: () => (effectiveTenantId ? getSkills(effectiveTenantId) : Promise.resolve([])),
    enabled: Boolean(effectiveTenantId),
  });

  const catalogSkillNames = useMemo(() => {
    const set = new Set<string>();
    for (const s of catalogSkills) {
      if (s.name) {
        set.add(s.name.trim().toLowerCase());
      }
    }
    return set;
  }, [catalogSkills]);

  const isSkillInCatalog = (name: string) => {
    return catalogSkillNames.has(name.trim().toLowerCase());
  };

  const [isEditing, setIsEditing] = useState(false);
  const [editedAbilities, setEditedAbilities] = useState<string[]>([]);
  const [newAbilityText, setNewAbilityText] = useState('');

  const [hasUserEdited, setHasUserEdited] = useState(false);

  const startEditing = () => {
    if (!requirements) return;
    setEditedAbilities([...requirements.employeeMustBeAbleTo]);
    setIsEditing(true);
  };

  const cancelEditing = () => {
    setIsEditing(false);
  };

  const saveEditing = () => {
    if (!requirements) return;
    const updated: WorkStepRequirementsAnalysisResultDto = {
      ...requirements,
      employeeMustBeAbleTo: editedAbilities.filter((a) => a.trim().length > 0),
    };
    onUpdateRequirements(updated);
    setHasUserEdited(true);
    setIsEditing(false);
  };

  const handleAddAbility = () => {
    if (newAbilityText.trim()) {
      setEditedAbilities([...editedAbilities, newAbilityText.trim()]);
      setNewAbilityText('');
    }
  };

  const handleRemoveAbility = (idx: number) => {
    setEditedAbilities(editedAbilities.filter((_, i) => i !== idx));
  };

  const handleRemoveSkill = (categoryKey: 'softSkills' | 'technicalSkills' | 'businessSkills' | 'departmentProcesses' | 'hrProcesses' | 'aiSkills', idx: number) => {
    if (!requirements) return;
    if (onRemoveSkill) {
      onRemoveSkill(categoryKey, idx);
      return;
    }
    const currentList = requirements.requiredSkillAreas[categoryKey] || [];
    const updatedList = currentList.filter((_, i) => i !== idx);

    const updated: WorkStepRequirementsAnalysisResultDto = {
      ...requirements,
      requiredSkillAreas: {
        ...requirements.requiredSkillAreas,
        [categoryKey]: updatedList,
      },
    };
    onUpdateRequirements(updated);
    setHasUserEdited(true);
  };

  const handleRerun = async () => {
    if (hasUserEdited) {
      const confirmRerun = window.confirm(t('processLearning.requirementsStep.reRunConfirm'));
      if (!confirmRerun) return;
    }
    await onAnalyzeStep(step.step_id);
    setHasUserEdited(false);
  };

  return (
    <div className="space-y-6">
      {/* Step Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
        <div>
          <div className="flex items-center gap-2">
            <span className="rounded bg-brand-100 px-2 py-0.5 text-xs font-bold text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
              {t('processLearning.coverageStep.stepLabel', { number: step.step_number })}
            </span>
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {step.step_name}
            </h3>
          </div>
          <p className="mt-1 text-xs text-slate-500 dark:text-white/60">
            {t('processLearning.requirementsStep.subtitle')}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {/* In edit mode, analysis is already done. Only show Save button IF user made changes (e.g. deleted a skill) */}
          {isEditMode ? (
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={onApproveAndContinue}
                className="rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-xs hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white"
              >
                Ver Matriz (Paso 4) →
              </button>

              {hasUnsavedChanges && onSave && (
                <button
                  type="button"
                  onClick={onSave}
                  disabled={isSavingProcess}
                  className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-emerald-600 to-brand-600 px-5 py-2 text-xs font-bold text-white shadow-md shadow-emerald-500/20 hover:from-emerald-500 hover:to-brand-500 disabled:opacity-50 transition-all animate-in fade-in"
                >
                  {isSavingProcess ? (
                    <>
                      <RefreshCw className="h-4 w-4 animate-spin text-white" />
                      <span>Guardando Cambios...</span>
                    </>
                  ) : (
                    <>
                      <CheckCircle2 className="h-4 w-4" />
                      <span>Guardar Cambios</span>
                    </>
                  )}
                </button>
              )}
            </div>
          ) : (
            <>
              {onAnalyzeAllSteps && (
                <button
                  type="button"
                  onClick={onAnalyzeAllSteps}
                  disabled={isBatchAnalyzing || isLoading}
                  className="inline-flex items-center gap-1.5 rounded-xl border-2 border-brand-500 bg-brand-50/80 px-4 py-2 text-xs font-bold text-brand-700 shadow-sm hover:bg-brand-100 dark:border-brand-400 dark:bg-brand-500/20 dark:text-brand-300 disabled:opacity-50 transition-all"
                >
                  {isBatchAnalyzing ? (
                    <>
                      <RefreshCw className="h-4 w-4 animate-spin text-brand-600" />
                      <span>Analizando ({batchProgress?.current}/{batchProgress?.total})...</span>
                    </>
                  ) : (
                    <>
                      <Sparkles className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                      <span>{t('processLearning.requirementsStep.analyzeAllBtn')}</span>
                    </>
                  )}
                </button>
              )}

              {requirements ? (
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={handleRerun}
                    disabled={isLoading || isBatchAnalyzing}
                    className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white/80"
                  >
                    {t('processLearning.requirementsStep.reRunBtn')}
                  </button>
                  <button
                    type="button"
                    onClick={onApproveAndContinue}
                    disabled={isBatchAnalyzing}
                    className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-5 py-2 text-xs font-semibold text-white shadow-sm hover:from-brand-500 hover:to-indigo-500 disabled:opacity-50"
                  >
                    <FileCheck2 className="h-4 w-4" />
                    <span>{t('processLearning.requirementsStep.approveAndContinue')}</span>
                  </button>
                </div>
              ) : null}
            </>
          )}
        </div>
      </div>

      {/* Main Requirements Card */}
      {!requirements ? (
        <div className="flex flex-col items-center justify-center rounded-2xl border-2 border-dashed border-slate-200 p-12 text-center dark:border-white/10 dark:bg-slate-900/20">
          <Sparkles className="h-10 w-10 text-slate-300 dark:text-white/20" />
          <h4 className="mt-3 text-sm font-bold text-slate-700 dark:text-white/70">
            Requisitos aún no analizados
          </h4>
          <p className="mt-1 max-w-md text-xs text-slate-400 dark:text-white/40">
            {t('processLearning.requirementsStep.batchNotice')}
          </p>
          <div className="mt-4 flex items-center justify-center">
            {onAnalyzeAllSteps && (
              <button
                type="button"
                onClick={onAnalyzeAllSteps}
                disabled={isBatchAnalyzing || isLoading}
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-6 py-3 text-xs font-bold text-white shadow-md shadow-brand-500/20 hover:from-brand-500 hover:to-indigo-500 disabled:opacity-50 transition-all"
              >
                {isBatchAnalyzing ? (
                  <>
                    <RefreshCw className="h-4 w-4 animate-spin text-white" />
                    <span>Analizando ({batchProgress?.current}/{batchProgress?.total})...</span>
                  </>
                ) : (
                  <>
                    <Sparkles className="h-4 w-4" />
                    <span>{t('processLearning.requirementsStep.analyzeAllBtn')}</span>
                  </>
                )}
              </button>
            )}
          </div>
        </div>
      ) : (
        <div className="space-y-6">
          {/* Section 1: Employee Must Be Able To */}
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/40 space-y-4">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3 dark:border-white/5">
              <div className="flex items-center gap-2">
                <CheckCircle2 className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white">
                  {t('processLearning.requirementsStep.abilitiesHeader')}
                </h4>
              </div>

              {!isEditing ? (
                <button
                  type="button"
                  onClick={startEditing}
                  className="inline-flex items-center gap-1 rounded-lg border border-slate-200 px-2.5 py-1 text-xs font-medium text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:text-white/70"
                >
                  <Edit2 className="h-3 w-3" />
                  <span>{t('processLearning.common.edit')}</span>
                </button>
              ) : (
                <div className="flex items-center gap-1">
                  <button
                    type="button"
                    onClick={cancelEditing}
                    className="rounded-lg border px-2.5 py-1 text-xs text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:text-white/70"
                  >
                    {t('processLearning.common.cancel')}
                  </button>
                  <button
                    type="button"
                    onClick={saveEditing}
                    className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-2.5 py-1 text-xs font-semibold text-white hover:bg-emerald-500"
                  >
                    <Check className="h-3 w-3" />
                    <span>{t('processLearning.common.save')}</span>
                  </button>
                </div>
              )}
            </div>

            {isEditing ? (
              <div className="space-y-2 text-xs">
                <ul className="space-y-1.5">
                  {editedAbilities.map((ab, i) => (
                    <li key={i} className="flex items-center justify-between gap-2 rounded-lg border p-2 dark:border-white/10">
                      <span className="text-slate-800 dark:text-white/90">{ab}</span>
                      <button
                        type="button"
                        onClick={() => handleRemoveAbility(i)}
                        className="text-rose-500 hover:text-rose-700"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </li>
                  ))}
                </ul>
                <div className="flex gap-2">
                  <input
                    type="text"
                    value={newAbilityText}
                    onChange={(e) => setNewAbilityText(e.target.value)}
                    placeholder="Nueva capacidad observable..."
                    className="flex-1 rounded-lg border px-2.5 py-1.5 text-xs dark:bg-slate-800 dark:text-white"
                  />
                  <button
                    type="button"
                    onClick={handleAddAbility}
                    className="rounded-lg bg-slate-800 px-3 py-1.5 font-semibold text-white dark:bg-white dark:text-slate-900"
                  >
                    <Plus className="h-4 w-4" />
                  </button>
                </div>
              </div>
            ) : (
              <ul className="list-inside list-disc space-y-1 text-xs text-slate-700 dark:text-white/80">
                {requirements.employeeMustBeAbleTo.map((ability, i) => (
                  <li key={i}>{ability}</li>
                ))}
              </ul>
            )}
          </div>

          {/* Section 2: Canonical Skill Categories & Mandatory AI Skill */}
          <div className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {/* Soft Skills */}
              {requirements.requiredSkillAreas.softSkills?.length > 0 && (
                <div className="rounded-2xl border border-violet-200 bg-violet-50/30 p-5 dark:border-violet-500/20 dark:bg-white/5 space-y-3">
                  <div className="flex items-center gap-2 text-violet-800 dark:text-violet-300 font-bold text-xs uppercase tracking-wider">
                    <Heart className="h-4 w-4" />
                    <span>{t('processLearning.requirementsStep.softSkillsHeader')}</span>
                  </div>
                  <ul className="space-y-2 text-xs">
                    {requirements.requiredSkillAreas.softSkills.map((sk, i) => {
                      const inCatalog = isSkillInCatalog(sk.name);
                      return (
                        <li key={i} className="group relative rounded-xl bg-white p-3 shadow-xs dark:bg-slate-800/80 border border-violet-100 dark:border-white/5 space-y-0.5">
                          <div className="flex items-center justify-between gap-2">
                            <p className="font-bold text-slate-900 dark:text-white truncate">{sk.name}</p>
                            <div className="flex items-center gap-1.5 shrink-0">
                              {inCatalog ? (
                                <span className="rounded bg-emerald-50 px-1.5 py-0.5 text-[9px] font-bold text-emerald-700 border border-emerald-200 dark:bg-emerald-500/20 dark:text-emerald-300 dark:border-emerald-500/30">
                                  En catálogo
                                </span>
                              ) : (
                                <span className="rounded bg-rose-50 px-1.5 py-0.5 text-[9px] font-bold text-rose-700 border border-rose-200 dark:bg-rose-500/20 dark:text-rose-300 dark:border-rose-500/30">
                                  Nuevo
                                </span>
                              )}
                              <span className="rounded bg-violet-50 px-1.5 py-0.5 text-[9px] font-mono font-bold text-violet-700 dark:bg-violet-500/20 dark:text-violet-300">
                                {sk.skillId || 'soft-skills'}
                              </span>
                              <button
                                type="button"
                                onClick={() => handleRemoveSkill('softSkills', i)}
                                title="Eliminar habilidad del proceso"
                                className="rounded p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600 dark:text-white/40 dark:hover:bg-rose-500/20 dark:hover:text-rose-300 transition-colors"
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </button>
                            </div>
                          </div>
                          <p className="text-[11px] text-slate-500 dark:text-white/60">{sk.reason}</p>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              )}

              {/* Technical / Job Skills */}
              {requirements.requiredSkillAreas.technicalSkills?.length > 0 && (
                <div className="rounded-2xl border border-teal-200 bg-teal-50/30 p-5 dark:border-teal-500/20 dark:bg-white/5 space-y-3">
                  <div className="flex items-center gap-2 text-teal-800 dark:text-teal-300 font-bold text-xs uppercase tracking-wider">
                    <Cpu className="h-4 w-4" />
                    <span>{t('processLearning.requirementsStep.technicalSkillsHeader')}</span>
                  </div>
                  <ul className="space-y-2 text-xs">
                    {requirements.requiredSkillAreas.technicalSkills.map((sk, i) => {
                      const inCatalog = isSkillInCatalog(sk.name);
                      return (
                        <li key={i} className="group relative rounded-xl bg-white p-3 shadow-xs dark:bg-slate-800/80 border border-teal-100 dark:border-white/5 space-y-0.5">
                          <div className="flex items-center justify-between gap-2">
                            <p className="font-bold text-slate-900 dark:text-white truncate">{sk.name}</p>
                            <div className="flex items-center gap-1.5 shrink-0">
                              {inCatalog ? (
                                <span className="rounded bg-emerald-50 px-1.5 py-0.5 text-[9px] font-bold text-emerald-700 border border-emerald-200 dark:bg-emerald-500/20 dark:text-emerald-300 dark:border-emerald-500/30">
                                  En catálogo
                                </span>
                              ) : (
                                <span className="rounded bg-rose-50 px-1.5 py-0.5 text-[9px] font-bold text-rose-700 border border-rose-200 dark:bg-rose-500/20 dark:text-rose-300 dark:border-rose-500/30">
                                  Nuevo
                                </span>
                              )}
                              <span className="rounded bg-teal-50 px-1.5 py-0.5 text-[9px] font-mono font-bold text-teal-700 dark:bg-teal-500/20 dark:text-teal-300">
                                {sk.skillId || 'job-skills'}
                              </span>
                              <button
                                type="button"
                                onClick={() => handleRemoveSkill('technicalSkills', i)}
                                title="Eliminar habilidad del proceso"
                                className="rounded p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600 dark:text-white/40 dark:hover:bg-rose-500/20 dark:hover:text-rose-300 transition-colors"
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </button>
                            </div>
                          </div>
                          <p className="text-[11px] text-slate-500 dark:text-white/60">{sk.reason}</p>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              )}

              {/* Department Processes */}
              {requirements.requiredSkillAreas.departmentProcesses && requirements.requiredSkillAreas.departmentProcesses.length > 0 && (
                <div className="rounded-2xl border border-blue-200 bg-blue-50/30 p-5 dark:border-blue-500/20 dark:bg-white/5 space-y-3">
                  <div className="flex items-center gap-2 text-blue-800 dark:text-blue-300 font-bold text-xs uppercase tracking-wider">
                    <Building2 className="h-4 w-4" />
                    <span>{t('processLearning.requirementsStep.departmentProcessesHeader')}</span>
                  </div>
                  <ul className="space-y-2 text-xs">
                    {requirements.requiredSkillAreas.departmentProcesses.map((sk, i) => {
                      const inCatalog = isSkillInCatalog(sk.name);
                      return (
                        <li key={i} className="group relative rounded-xl bg-white p-3 shadow-xs dark:bg-slate-800/80 border border-blue-100 dark:border-white/5 space-y-0.5">
                          <div className="flex items-center justify-between gap-2">
                            <p className="font-bold text-slate-900 dark:text-white truncate">{sk.name}</p>
                            <div className="flex items-center gap-1.5 shrink-0">
                              {inCatalog ? (
                                <span className="rounded bg-emerald-50 px-1.5 py-0.5 text-[9px] font-bold text-emerald-700 border border-emerald-200 dark:bg-emerald-500/20 dark:text-emerald-300 dark:border-emerald-500/30">
                                  En catálogo
                                </span>
                              ) : (
                                <span className="rounded bg-rose-50 px-1.5 py-0.5 text-[9px] font-bold text-rose-700 border border-rose-200 dark:bg-rose-500/20 dark:text-rose-300 dark:border-rose-500/30">
                                  Nuevo
                                </span>
                              )}
                              <span className="rounded bg-blue-50 px-1.5 py-0.5 text-[9px] font-mono font-bold text-blue-700 dark:bg-blue-500/20 dark:text-blue-300">
                                {sk.skillId || 'department-processes'}
                              </span>
                              <button
                                type="button"
                                onClick={() => handleRemoveSkill('departmentProcesses', i)}
                                title="Eliminar habilidad del proceso"
                                className="rounded p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600 dark:text-white/40 dark:hover:bg-rose-500/20 dark:hover:text-rose-300 transition-colors"
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </button>
                            </div>
                          </div>
                          <p className="text-[11px] text-slate-500 dark:text-white/60">{sk.reason}</p>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              )}

              {/* HR Processes */}
              {requirements.requiredSkillAreas.hrProcesses && requirements.requiredSkillAreas.hrProcesses.length > 0 && (
                <div className="rounded-2xl border border-emerald-200 bg-emerald-50/30 p-5 dark:border-emerald-500/20 dark:bg-white/5 space-y-3">
                  <div className="flex items-center gap-2 text-emerald-800 dark:text-emerald-300 font-bold text-xs uppercase tracking-wider">
                    <ShieldCheck className="h-4 w-4" />
                    <span>{t('processLearning.requirementsStep.hrProcessesHeader')}</span>
                  </div>
                  <ul className="space-y-2 text-xs">
                    {requirements.requiredSkillAreas.hrProcesses.map((sk, i) => {
                      const inCatalog = isSkillInCatalog(sk.name);
                      return (
                        <li key={i} className="group relative rounded-xl bg-white p-3 shadow-xs dark:bg-slate-800/80 border border-emerald-100 dark:border-white/5 space-y-0.5">
                          <div className="flex items-center justify-between gap-2">
                            <p className="font-bold text-slate-900 dark:text-white truncate">{sk.name}</p>
                            <div className="flex items-center gap-1.5 shrink-0">
                              {inCatalog ? (
                                <span className="rounded bg-emerald-50 px-1.5 py-0.5 text-[9px] font-bold text-emerald-700 border border-emerald-200 dark:bg-emerald-500/20 dark:text-emerald-300 dark:border-emerald-500/30">
                                  En catálogo
                                </span>
                              ) : (
                                <span className="rounded bg-rose-50 px-1.5 py-0.5 text-[9px] font-bold text-rose-700 border border-rose-200 dark:bg-rose-500/20 dark:text-rose-300 dark:border-rose-500/30">
                                  Nuevo
                                </span>
                              )}
                              <span className="rounded bg-emerald-50 px-1.5 py-0.5 text-[9px] font-mono font-bold text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300">
                                {sk.skillId || 'hr-processes'}
                              </span>
                              <button
                                type="button"
                                onClick={() => handleRemoveSkill('hrProcesses', i)}
                                title="Eliminar habilidad del proceso"
                                className="rounded p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600 dark:text-white/40 dark:hover:bg-rose-500/20 dark:hover:text-rose-300 transition-colors"
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </button>
                            </div>
                          </div>
                          <p className="text-[11px] text-slate-500 dark:text-white/60">{sk.reason}</p>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              )}

              {/* Mandatory AI Skill Area */}
              {requirements.requiredSkillAreas.aiSkills && requirements.requiredSkillAreas.aiSkills.length > 0 && (
                <div className="rounded-2xl border border-indigo-300 bg-gradient-to-br from-indigo-50/60 to-purple-50/40 p-5 dark:border-indigo-500/30 dark:bg-indigo-950/20 space-y-3 shadow-xs">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2 text-indigo-900 dark:text-indigo-300 font-bold text-xs uppercase tracking-wider">
                      <Bot className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
                      <span>{t('processLearning.requirementsStep.aiSkillsHeader')}</span>
                    </div>
                    <span className="rounded-full bg-indigo-100 px-2 py-0.5 text-[9px] font-bold text-indigo-800 dark:bg-indigo-500/20 dark:text-indigo-300">
                      Mandatorio
                    </span>
                  </div>
                  <ul className="space-y-2 text-xs">
                    {requirements.requiredSkillAreas.aiSkills.map((sk, i) => {
                      const inCatalog = isSkillInCatalog(sk.name);
                      return (
                        <li key={i} className="group relative rounded-xl bg-white p-3 shadow-xs dark:bg-slate-800/90 border border-indigo-100 dark:border-white/10 space-y-0.5">
                          <div className="flex items-center justify-between gap-2">
                            <p className="font-bold text-slate-900 dark:text-white flex items-center gap-1.5 truncate">
                              <Sparkles className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400 shrink-0" />
                              {sk.name}
                            </p>
                            <div className="flex items-center gap-1.5 shrink-0">
                              {inCatalog ? (
                                <span className="rounded bg-emerald-50 px-1.5 py-0.5 text-[9px] font-bold text-emerald-700 border border-emerald-200 dark:bg-emerald-500/20 dark:text-emerald-300 dark:border-emerald-500/30">
                                  En catálogo
                                </span>
                              ) : (
                                <span className="rounded bg-rose-50 px-1.5 py-0.5 text-[9px] font-bold text-rose-700 border border-rose-200 dark:bg-rose-500/20 dark:text-rose-300 dark:border-rose-500/30">
                                  Nuevo
                                </span>
                              )}
                              <span className="rounded bg-indigo-50 px-1.5 py-0.5 text-[9px] font-mono font-bold text-indigo-700 dark:bg-indigo-500/20 dark:text-indigo-300">
                                {sk.skillId || 'skill-ai'}
                              </span>
                              <button
                                type="button"
                                onClick={() => handleRemoveSkill('aiSkills', i)}
                                title="Eliminar habilidad del proceso"
                                className="rounded p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600 dark:text-white/40 dark:hover:bg-rose-500/20 dark:hover:text-rose-300 transition-colors"
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </button>
                            </div>
                          </div>
                          <p className="text-[11px] text-slate-500 dark:text-white/60">{sk.reason}</p>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              )}
            </div>
          </div>

          {/* Section 3: Recommended Tools and Software (Clarified as Tools, NOT Skills) */}
          {requirements.requiredTools.length > 0 && (
            <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/40 space-y-3">
              <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1 border-b border-slate-100 pb-2.5 dark:border-white/5">
                <div className="flex items-center gap-2">
                  <Wrench className="h-4 w-4 text-slate-600 dark:text-slate-400" />
                  <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white">
                    {t('processLearning.requirementsStep.toolsHeader')}
                  </h4>
                </div>
                <p className="text-[10px] text-slate-500 dark:text-white/50">
                  {t('processLearning.requirementsStep.toolsSubtitle')}
                </p>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2.5">
                {requirements.requiredTools.map((tool, i) => (
                  <div key={i} className="rounded-xl border border-slate-200/80 p-3 bg-slate-50/50 dark:border-white/5 dark:bg-white/5 space-y-0.5 text-xs">
                    <p className="font-bold text-slate-900 dark:text-white">{tool.name}</p>
                    {tool.useInStep && <p className="text-[11px] text-slate-500 dark:text-white/60">{tool.useInStep}</p>}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Section 4: Expected Completion Evidence */}
          {requirements.completionEvidence.length > 0 && (
            <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/40 space-y-3">
              <div className="flex items-center gap-2">
                <FileCheck2 className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white">
                  {t('processLearning.requirementsStep.evidenceHeader')}
                </h4>
              </div>
              <div className="flex flex-wrap gap-2">
                {requirements.completionEvidence.map((ev, i) => (
                  <span
                    key={i}
                    className="rounded-full bg-emerald-50 px-3.5 py-1 text-xs font-medium text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-500/20"
                  >
                    {ev}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* Section 5: Assumptions and Warnings */}
          {(requirements.assumptions.length > 0 || requirements.warnings.length > 0) && (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {requirements.assumptions.length > 0 && (
                <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4 text-xs dark:border-white/10 dark:bg-white/5 space-y-1.5">
                  <p className="font-bold text-slate-800 dark:text-white">
                    {t('processLearning.requirementsStep.assumptionsHeader')}
                  </p>
                  <ul className="list-inside list-disc space-y-1 text-slate-600 dark:text-white/70">
                    {requirements.assumptions.map((a, i) => (
                      <li key={i}>{a}</li>
                    ))}
                  </ul>
                </div>
              )}

              {requirements.warnings.length > 0 && (
                <div className="rounded-2xl border border-amber-200 bg-amber-50/60 p-4 text-xs text-amber-900 dark:border-amber-500/20 dark:bg-amber-500/10 dark:text-amber-300 space-y-1.5">
                  <p className="font-bold flex items-center gap-1.5">
                    <AlertTriangle className="h-4 w-4 text-amber-600" />
                    <span>{t('processLearning.requirementsStep.warningsHeader')}</span>
                  </p>
                  <ul className="list-inside list-disc space-y-1">
                    {requirements.warnings.map((w, i) => (
                      <li key={i}>{w}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
