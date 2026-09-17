import { useState, useMemo, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/auth/AuthContext';
import {
  ListTree,
  BarChart3,
  ArrowLeft,
  Loader2,
  GraduationCap,
} from 'lucide-react';

import {
  ProcessLearningWizardStepper,
  type WizardStepState,
} from '@/components/wizard/ProcessLearningWizardStepper';
import {
  ProcessStepSidebar,
  type StepProgressState,
} from '@/components/wizard/ProcessStepSidebar';
import {
  ProcessLearningSummaryModal,
  type StepSummaryStats,
} from '@/components/wizard/ProcessLearningSummaryModal';
import { ProcessDocumentsListView } from '@/components/wizard/ProcessDocumentsListView';

import { ProcessUploadStep } from '@/components/wizard/steps/ProcessUploadStep';
import { ProcessStructureReviewStep } from '@/components/wizard/steps/ProcessStructureReviewStep';
import { WorkRequirementsReviewStep } from '@/components/wizard/steps/WorkRequirementsReviewStep';
import { ContentGapReviewStep } from '@/components/wizard/steps/ContentGapReviewStep';

import {
  analyzeWorkStepRequirements,
  analyzeLearningContentCoverage,
  generateContentGapDraft,
  saveProcessAnalysis,
  getProcessDocumentDetail,
  type ProcessDocumentUploadResponse,
  type ProcessDocumentStructureDto,
  type WorkStepRequirementsAnalysisResultDto,
  type LearningContentCoverageResponseDto,
  type LearningContentGapDraftResponseDto,
  type SkillItemDto,
} from '@/api/processDocumentsApi';

export function ProcessLearningWizardPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const effectiveTenantId = user?.tenantId || user?.tid;
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();

  // Mode: 'list' (cards view) or 'wizard' (create new / edit existing)
  const initialDocId = searchParams.get('id');
  const [viewMode, setViewMode] = useState<'list' | 'wizard'>(initialDocId ? 'wizard' : 'list');
  const [isLoadingProcessDetail, setIsLoadingProcessDetail] = useState(false);

  // Wizard navigation state (capped at Step 4: Skills Gap Analysis & Matriz)
  const [currentStep, setCurrentStep] = useState<number>(1);
  const [maxStepReached, setMaxStepReached] = useState<number>(1);

  // Global summary modal state
  const [isSummaryModalOpen, setIsSummaryModalOpen] = useState(false);
  const [summaryInitialTab, setSummaryInitialTab] = useState<'byStep' | 'byType'>('byStep');
  const [isProcessSaved, setIsProcessSaved] = useState(false);

  // Document & Step state
  const [uploadResult, setUploadResult] = useState<ProcessDocumentUploadResponse | null>(null);
  const [structure, setStructure] = useState<ProcessDocumentStructureDto | null>(null);
  const [selectedStepId, setSelectedStepId] = useState<string | null>(null);
  const [excludedStepIds, setExcludedStepIds] = useState<Set<string>>(new Set());
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState<boolean>(false);

  // Workflow cache per step ID
  const [stepRequirements, setStepRequirements] = useState<Record<string, WorkStepRequirementsAnalysisResultDto>>({});
  const [stepCoverages, setStepCoverages] = useState<Record<string, LearningContentCoverageResponseDto>>({});
  const [stepGapDrafts, setStepGapDrafts] = useState<Record<string, LearningContentGapDraftResponseDto>>({});

  // Loading states
  const [isAnalyzingReqs, setIsAnalyzingReqs] = useState(false);
  const [isBatchAnalyzing, setIsBatchAnalyzing] = useState(false);
  const [batchProgress, setBatchProgress] = useState<{ current: number; total: number } | null>(null);
  const [isEvaluatingCoverage, setIsEvaluatingCoverage] = useState(false);
  const [isBatchEvaluating, setIsBatchEvaluating] = useState(false);
  const [batchEvalProgress, setBatchEvalProgress] = useState<{ current: number; total: number } | null>(null);
  const [isSavingProcess, setIsSavingProcess] = useState(false);

  // Function to load an existing saved process from database into wizard state
  const handleLoadProcessFromDb = async (documentId: string, openSummaryDirectly = false) => {
    setIsLoadingProcessDetail(true);
    try {
      const detail = await getProcessDocumentDetail(documentId, effectiveTenantId);
      setStructure(detail.structure);
      setStepRequirements(detail.stepRequirements || {});
      setStepCoverages(detail.stepCoverages || {});
      setHasUnsavedChanges(false);

      // Pick first step
      const firstStep = detail.structure.chapters[0]?.steps[0];
      if (firstStep) {
        setSelectedStepId(firstStep.step_id);
      }

      setMaxStepReached(4);
      setSearchParams({ id: documentId });

      if (openSummaryDirectly) {
        setIsProcessSaved(true);
        setIsSummaryModalOpen(true);
      } else {
        setCurrentStep(2); // Open in Step 2: Structure review / edit
        setViewMode('wizard');
      }
    } catch (err: any) {
      console.error('Error loading process detail from database:', err);
      alert(`Error al cargar el proceso desde la base de datos: ${err?.response?.data?.message || err?.message}`);
    } finally {
      setIsLoadingProcessDetail(false);
    }
  };

  // Check URL param on mount or URL change
  useEffect(() => {
    const docId = searchParams.get('id');
    if (docId && (!structure || structure.document_id !== docId)) {
      handleLoadProcessFromDb(docId);
    }
  }, [searchParams]);

  // Handler to start creating a new process
  const handleStartCreateNew = () => {
    setUploadResult(null);
    setStructure(null);
    setSelectedStepId(null);
    setExcludedStepIds(new Set());
    setStepRequirements({});
    setStepCoverages({});
    setStepGapDrafts({});
    setHasUnsavedChanges(false);
    setIsProcessSaved(false);
    setCurrentStep(1);
    setMaxStepReached(1);
    setSearchParams({});
    setViewMode('wizard');
  };

  // Handler to return to list view
  const handleBackToList = () => {
    if (hasUnsavedChanges) {
      const ok = window.confirm('Tienes cambios sin guardar. ¿Deseas salir a la lista de procesos?');
      if (!ok) return;
    }
    setSearchParams({});
    setViewMode('list');
  };

  // Active step object
  const activeStep = useMemo(() => {
    if (!structure) return null;
    for (const ch of structure.chapters) {
      const match = ch.steps.find((s) => s.step_id === selectedStepId);
      if (match) return match;
    }
    return structure.chapters[0]?.steps[0] || null;
  }, [structure, selectedStepId]);

  // Handle structure loaded after upload
  const handleStructureLoaded = (
    result: ProcessDocumentUploadResponse,
    newStructure: ProcessDocumentStructureDto,
  ) => {
    setUploadResult(result);
    setStructure(newStructure);
    if (newStructure.chapters[0]?.steps[0]) {
      setSelectedStepId(newStructure.chapters[0].steps[0].step_id);
    }
    setMaxStepReached(Math.max(maxStepReached, 2));
    setCurrentStep(2);
  };

  // Toggle exclusion of a step from the learning workflow
  const handleToggleExcludeStep = (stepId: string) => {
    setExcludedStepIds((prev) => {
      const next = new Set(prev);
      if (next.has(stepId)) {
        next.delete(stepId);
      } else {
        next.add(stepId);
      }
      return next;
    });
  };

  // Helper: collect cumulative skills already identified across all analyzed steps to prevent duplicate synonyms
  const getCumulativeIdentifiedSkills = (): SkillItemDto[] => {
    const map = new Map<string, SkillItemDto>();
    for (const reqs of Object.values(stepRequirements)) {
      if (!reqs.requiredSkillAreas) continue;
      const all: SkillItemDto[] = [
        ...(reqs.requiredSkillAreas.softSkills || []),
        ...(reqs.requiredSkillAreas.technicalSkills || []),
        ...(reqs.requiredSkillAreas.businessSkills || []),
        ...(reqs.requiredSkillAreas.departmentProcesses || []),
        ...(reqs.requiredSkillAreas.hrProcesses || []),
        ...(reqs.requiredSkillAreas.aiSkills || []),
      ];
      for (const sk of all) {
        const key = sk.skillId || sk.name.toLowerCase().trim();
        if (!map.has(key)) {
          map.set(key, sk);
        }
      }
    }
    return Array.from(map.values());
  };

  // Step 3: Analyze Requirements for one step (feeds previous skills to prevent synonym duplication)
  const handleAnalyzeStepRequirements = async (stepId: string) => {
    if (!structure?.document_id) return;
    setIsAnalyzingReqs(true);
    try {
      const priorSkills = getCumulativeIdentifiedSkills();
      const reqs = await analyzeWorkStepRequirements(structure.document_id, stepId, effectiveTenantId, priorSkills);
      setStepRequirements((prev) => ({ ...prev, [stepId]: reqs }));
    } catch (err) {
      console.error('Error analyzing work step requirements:', err);
      alert('Error al analizar los requisitos del paso. Verifica la conexión con Azure OpenAI.');
    } finally {
      setIsAnalyzingReqs(false);
    }
  };

  // Step 3 Batch: Analyze all non-excluded steps in order, passing accumulated skills along
  const handleAnalyzeAllSteps = async () => {
    if (!structure?.document_id) return;
    const allSteps = structure.chapters
      .flatMap((c) => c.steps)
      .filter((s) => !excludedStepIds.has(s.step_id));

    setIsBatchAnalyzing(true);
    setBatchProgress({ current: 0, total: allSteps.length });

    let runningMap = { ...stepRequirements };

    for (let i = 0; i < allSteps.length; i++) {
      const s = allSteps[i];
      setBatchProgress({ current: i + 1, total: allSteps.length });
      if (!runningMap[s.step_id]) {
        try {
          // Accumulate from runningMap
          const priorMap = new Map<string, SkillItemDto>();
          for (const reqs of Object.values(runningMap)) {
            if (!reqs.requiredSkillAreas) continue;
            const all: SkillItemDto[] = [
              ...(reqs.requiredSkillAreas.softSkills || []),
              ...(reqs.requiredSkillAreas.technicalSkills || []),
              ...(reqs.requiredSkillAreas.businessSkills || []),
              ...(reqs.requiredSkillAreas.departmentProcesses || []),
              ...(reqs.requiredSkillAreas.hrProcesses || []),
              ...(reqs.requiredSkillAreas.aiSkills || []),
            ];
            for (const sk of all) {
              const key = sk.skillId || sk.name.toLowerCase().trim();
              if (!priorMap.has(key)) priorMap.set(key, sk);
            }
          }
          const priorSkills = Array.from(priorMap.values());

          const reqs = await analyzeWorkStepRequirements(structure.document_id, s.step_id, effectiveTenantId, priorSkills);
          runningMap = { ...runningMap, [s.step_id]: reqs };
          setStepRequirements(runningMap);
        } catch (err) {
          console.warn(`Could not analyze step ${s.step_id}`, err);
        }
      }
    }

    setIsBatchAnalyzing(false);
    setBatchProgress(null);

    // After all steps analyzed in batch, advance and unlock Step 4 (Skills Gap Analysis)
    setMaxStepReached((prev) => Math.max(prev, 4));
  };

  // Step 4: Evaluate Coverage and Gaps
  const handleEvaluateCoverage = async (stepId: string) => {
    if (!structure?.document_id) return;
    setIsEvaluatingCoverage(true);
    try {
      const coverage = await analyzeLearningContentCoverage(structure.document_id, stepId, effectiveTenantId);
      setStepCoverages((prev) => ({ ...prev, [stepId]: coverage }));

      // If gaps exist, pre-generate or load drafts
      if (coverage.contentGaps.length > 0) {
        try {
          const drafts = await generateContentGapDraft(structure.document_id, stepId, effectiveTenantId);
          setStepGapDrafts((prev) => ({ ...prev, [stepId]: drafts }));
        } catch (dErr) {
          console.warn('Could not generate draft proposals:', dErr);
        }
      }
    } catch (err) {
      console.error('Error evaluating content coverage:', err);
      alert('Error al evaluar la cobertura de contenido.');
    } finally {
      setIsEvaluatingCoverage(false);
    }
  };

  // Step 4 Batch: Evaluate coverage and gaps for all non-excluded steps in one run
  const handleEvaluateAllCoverage = async () => {
    if (!structure?.document_id) return;
    const allSteps = structure.chapters
      .flatMap((c) => c.steps)
      .filter((s) => !excludedStepIds.has(s.step_id));

    setIsBatchEvaluating(true);
    setBatchEvalProgress({ current: 0, total: allSteps.length });

    for (let i = 0; i < allSteps.length; i++) {
      const s = allSteps[i];
      setBatchEvalProgress({ current: i + 1, total: allSteps.length });
      if (!stepCoverages[s.step_id]) {
        try {
          const coverage = await analyzeLearningContentCoverage(structure.document_id, s.step_id, effectiveTenantId);
          setStepCoverages((prev) => ({ ...prev, [s.step_id]: coverage }));

          if (coverage.contentGaps.length > 0) {
            try {
              const drafts = await generateContentGapDraft(structure.document_id, s.step_id, effectiveTenantId);
              setStepGapDrafts((prev) => ({ ...prev, [s.step_id]: drafts }));
            } catch {
              // Non-blocking
            }
          }
        } catch (err) {
          console.warn(`Could not evaluate coverage for step ${s.step_id}`, err);
        }
      }
    }

    setIsBatchEvaluating(false);
    setBatchEvalProgress(null);
    setMaxStepReached(4);
  };

  // Remove skill from a step (user curation)
  const handleRemoveSkillFromStep = (stepId: string, skillName: string) => {
    const lower = skillName.trim().toLowerCase();

    // 1. Update StepRequirements
    setStepRequirements((prev) => {
      const current = prev[stepId];
      if (!current?.requiredSkillAreas) return prev;
      const filterOut = (list?: SkillItemDto[]) =>
        (list || []).filter((s) => s.name.trim().toLowerCase() !== lower);

      return {
        ...prev,
        [stepId]: {
          ...current,
          requiredSkillAreas: {
            ...current.requiredSkillAreas,
            softSkills: filterOut(current.requiredSkillAreas.softSkills),
            technicalSkills: filterOut(current.requiredSkillAreas.technicalSkills),
            businessSkills: filterOut(current.requiredSkillAreas.businessSkills),
            departmentProcesses: filterOut(current.requiredSkillAreas.departmentProcesses),
            hrProcesses: filterOut(current.requiredSkillAreas.hrProcesses),
            aiSkills: filterOut(current.requiredSkillAreas.aiSkills),
          },
        },
      };
    });

    // 2. Automatically update StepCoverages in Step 4 so the matrix reflects the deletion immediately
    setStepCoverages((prev) => {
      const currentCov = prev[stepId];
      if (!currentCov) return prev;

      return {
        ...prev,
        [stepId]: {
          ...currentCov,
          coveredRequirements: (currentCov.coveredRequirements || []).filter(
            (c) => c.requirement.trim().toLowerCase() !== lower,
          ),
          contentGaps: (currentCov.contentGaps || []).filter(
            (g) => g.requirement.trim().toLowerCase() !== lower,
          ),
        },
      };
    });

    setHasUnsavedChanges(true);
  };

  // Final Action: Persist process, steps, and Skills Training Matrix to database
  const handleFinalizeAndSaveProcess = async (openModal = true) => {
    if (!structure?.document_id) return;
    setIsSavingProcess(true);
    try {
      const res = await saveProcessAnalysis(
        structure.document_id,
        {
          title: structure.process_name,
          structure,
          stepCoverages,
          stepRequirements,
        },
        effectiveTenantId,
        user?.oid || user?.personId,
      );
      if (res.success) {
        setIsProcessSaved(true);
        setHasUnsavedChanges(false);
        queryClient.invalidateQueries({ queryKey: ['process-documents', effectiveTenantId] });
        if (openModal) {
          setIsSummaryModalOpen(true);
        }
      }
    } catch (err: any) {
      console.error('Error saving process analysis to database:', err);
      const errMsg = err?.response?.data?.message || err?.message || 'Error desconocido';
      alert(`Error al guardar en la base de datos: ${errMsg}`);
      setIsProcessSaved(false);
    } finally {
      setIsSavingProcess(false);
    }
  };

  // Navigate forward in the wizard (capped at Step 4)
  const advanceToStep = (targetStepNumber: number) => {
    const capped = Math.min(targetStepNumber, 4);
    setMaxStepReached((prev) => Math.max(prev, capped));
    setCurrentStep(capped);
  };

  // Compact sidebar status map
  const stepProgressMap: Record<string, StepProgressState> = useMemo(() => {
    if (!structure) return {};
    const map: Record<string, StepProgressState> = {};

    for (const ch of structure.chapters) {
      for (const st of ch.steps) {
        if (stepCoverages[st.step_id]?.overallCoverageStatus === 'covered') {
          map[st.step_id] = 'ready';
        } else if (stepCoverages[st.step_id]?.overallCoverageStatus === 'content_gap' || stepCoverages[st.step_id]?.overallCoverageStatus === 'partially_covered') {
          map[st.step_id] = 'gap';
        } else if (stepRequirements[st.step_id]) {
          map[st.step_id] = 'reqs_ready';
        } else {
          map[st.step_id] = 'not_analyzed';
        }
      }
    }

    return map;
  }, [
    structure,
    stepCoverages,
    stepRequirements,
  ]);

  // Check if all non-excluded steps in the process have been analyzed
  const allNonExcludedSteps = useMemo(() => {
    if (!structure) return [];
    return structure.chapters
      .flatMap((c) => c.steps)
      .filter((s) => !excludedStepIds.has(s.step_id));
  }, [structure, excludedStepIds]);

  const areAllStepsAnalyzed = useMemo(() => {
    if (allNonExcludedSteps.length === 0) return false;
    return allNonExcludedSteps.every((s) => !!stepRequirements[s.step_id]);
  }, [allNonExcludedSteps, stepRequirements]);

  // Stepper state map (for the active step and global process readiness)
  const stepStates: Record<number, WizardStepState> = useMemo(() => {
    const states: Record<number, WizardStepState> = {
      1: structure ? 'completed' : 'in_progress',
      2: structure ? 'completed' : 'not_started',
      3: 'not_started',
      4: 'not_started',
    };

    if (areAllStepsAnalyzed) {
      states[3] = 'completed';
    } else if (currentStep === 3) {
      states[3] = isBatchAnalyzing || isAnalyzingReqs ? 'in_progress' : 'needs_review';
    } else if (Object.keys(stepRequirements).length > 0) {
      states[3] = 'in_progress';
    }

    if (!activeStep) return states;

    const sId = activeStep.step_id;

    if (stepCoverages[sId]) {
      states[4] = stepCoverages[sId].overallCoverageStatus === 'covered' ? 'completed' : 'content_gap';
    } else if (currentStep === 4) {
      states[4] = isEvaluatingCoverage ? 'in_progress' : 'needs_review';
    }

    return states;
  }, [
    structure,
    areAllStepsAnalyzed,
    activeStep,
    currentStep,
    stepRequirements,
    stepCoverages,
    isAnalyzingReqs,
    isBatchAnalyzing,
    isEvaluatingCoverage,
  ]);

  // Step summaries list for Global Summary Modal
  const stepSummaries: StepSummaryStats[] = useMemo(() => {
    if (!structure) return [];
    return structure.chapters.flatMap((ch) =>
      ch.steps.map((st) => {
        const reqs = stepRequirements[st.step_id];
        const softCount = reqs?.requiredSkillAreas?.softSkills?.length || 0;
        const technicalCount = (reqs?.requiredSkillAreas?.technicalSkills?.length || 0) + (reqs?.requiredSkillAreas?.businessSkills?.length || 0);
        const processesCount = (reqs?.requiredSkillAreas?.departmentProcesses?.length || 0) + (reqs?.requiredSkillAreas?.hrProcesses?.length || 0);
        const aiCount = reqs?.requiredSkillAreas?.aiSkills?.length || 0;
        const totalSkillsCount = softCount + technicalCount + processesCount + aiCount;
        const toolsCount = reqs?.requiredTools?.length || 0;

        return {
          stepId: st.step_id,
          stepNumber: st.step_number,
          stepName: st.step_name,
          chapterTitle: ch.title,
          isExcluded: excludedStepIds.has(st.step_id),
          hasRequirements: !!reqs,
          softCount,
          technicalCount,
          processesCount,
          aiCount,
          totalSkillsCount,
          toolsCount,
        };
      }),
    );
  }, [structure, excludedStepIds, stepRequirements]);

  // If in list view, render the processes cards grid
  if (viewMode === 'list') {
    return (
      <>
        {isLoadingProcessDetail && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-xs">
            <div className="flex items-center gap-3 rounded-2xl bg-white p-5 shadow-2xl dark:bg-slate-900">
              <Loader2 className="h-6 w-6 animate-spin text-brand-600 dark:text-brand-400" />
              <p className="text-xs font-bold text-slate-800 dark:text-white">
                Cargando proceso y matriz desde Azure SQL...
              </p>
            </div>
          </div>
        )}

        <ProcessDocumentsListView
          tenantId={effectiveTenantId}
          onCreateNew={handleStartCreateNew}
          onEditProcess={(docId) => handleLoadProcessFromDb(docId, false)}
          onViewSummary={(docId) => handleLoadProcessFromDb(docId, true)}
        />

        {structure && (
          <ProcessLearningSummaryModal
            isOpen={isSummaryModalOpen}
            onClose={() => setIsSummaryModalOpen(false)}
            structure={structure}
            stepSummaries={stepSummaries}
            stepRequirements={stepRequirements}
            initialTab={summaryInitialTab}
            isSaved={isProcessSaved}
            onOpenStep={(stepId, targetStep) => {
              setSelectedStepId(stepId);
              setCurrentStep(targetStep || 4);
              setViewMode('wizard');
            }}
          />
        )}
      </>
    );
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between border-b border-slate-200 pb-5 dark:border-white/10">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={handleBackToList}
            title="Volver a lista de procesos"
            className="flex h-10 w-10 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-600 shadow-sm transition hover:bg-slate-50 hover:text-slate-900 dark:border-white/10 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10 dark:hover:text-white"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>

          <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-indigo-500 to-brand-600 text-white shadow-md shadow-brand-500/20">
            <ListTree className="h-6 w-6" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
                {structure?.process_name || t('processLearning.wizardTitle')}
              </h1>
              {structure && (
                <span className="rounded-md bg-brand-50 px-2 py-0.5 text-[10px] font-bold text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
                  Modo Edición
                </span>
              )}
            </div>
            <p className="text-xs text-slate-500 dark:text-white/60">
              {structure
                ? 'Edita los pasos operativos, las actividades requeridas y la Matriz de Habilidades de este proceso.'
                : t('processLearning.wizardSubtitle')}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={handleBackToList}
            className="rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white"
          >
            ← Volver a Procesos
          </button>

          {structure && (
            <>
              <button
                type="button"
                onClick={() => {
                  setSummaryInitialTab('byType');
                  setIsProcessSaved(false);
                  setIsSummaryModalOpen(true);
                }}
                className="inline-flex items-center gap-1.5 rounded-xl border border-teal-200 bg-teal-50 px-3.5 py-2 text-xs font-bold text-teal-800 shadow-sm hover:bg-teal-100 dark:border-teal-500/20 dark:bg-teal-500/10 dark:text-teal-300"
              >
                <GraduationCap className="h-4 w-4 text-teal-600 dark:text-teal-400" />
                <span>Skills por Tipo</span>
              </button>

              <button
                type="button"
                onClick={() => {
                  setSummaryInitialTab('byStep');
                  setIsProcessSaved(false);
                  setIsSummaryModalOpen(true);
                }}
                className="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white"
              >
                <BarChart3 className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                <span>{t('processLearning.common.processSummary')}</span>
              </button>
            </>
          )}
        </div>
      </div>

      {/* Stepper Navigation */}
      <ProcessLearningWizardStepper
        currentStep={currentStep}
        maxStepReached={maxStepReached}
        stepStates={stepStates}
        onSelectStep={(stepNum) => setCurrentStep(stepNum)}
        isEditMode={!!structure?.document_id && !!searchParams.get('id')}
      />

      {/* Main Layout (Sidebar + Step View) */}
      <div className="flex items-start gap-6">
        {/* Step Sidebar (Visible after upload in Steps 2-7) */}
        {structure && currentStep > 1 && (
          <ProcessStepSidebar
            structure={structure}
            selectedStepId={selectedStepId}
            stepProgressMap={stepProgressMap}
            excludedStepIds={excludedStepIds}
            hasUnsavedChanges={hasUnsavedChanges}
            onSelectStep={(sId) => setSelectedStepId(sId)}
          />
        )}

        {/* Step Views Container */}
        <main className="min-w-0 flex-1">
          {currentStep === 1 && (
            <ProcessUploadStep
              tenantId={effectiveTenantId}
              initialUploadResult={uploadResult}
              initialStructure={structure}
              onStructureLoaded={handleStructureLoaded}
              onNext={() => advanceToStep(2)}
            />
          )}

          {currentStep === 2 && structure && (
            <ProcessStructureReviewStep
              structure={structure}
              selectedStepId={selectedStepId}
              excludedStepIds={excludedStepIds}
              onSelectStep={(sId) => setSelectedStepId(sId)}
              onUpdateStructure={(newStruct) => {
                setStructure(newStruct);
                setHasUnsavedChanges(true);
              }}
              onToggleExcludeStep={handleToggleExcludeStep}
              isEditMode={!!structure?.document_id && !!searchParams.get('id')}
              onApproveAndContinue={() => {
                setHasUnsavedChanges(false);
                advanceToStep(3);
                // Do NOT auto-trigger single step analysis. Let user click "Complete Analysis for All Steps" or analyze step explicitly.
              }}
            />
          )}

          {currentStep === 3 && activeStep && (
            <WorkRequirementsReviewStep
              documentId={structure?.document_id || ''}
              step={activeStep}
              requirements={stepRequirements[activeStep.step_id] || null}
              isLoading={isAnalyzingReqs}
              onAnalyzeStep={handleAnalyzeStepRequirements}
              onAnalyzeAllSteps={handleAnalyzeAllSteps}
              isBatchAnalyzing={isBatchAnalyzing}
              batchProgress={batchProgress}
              isEditMode={!!structure?.document_id && !!searchParams.get('id')}
              hasUnsavedChanges={hasUnsavedChanges}
              isSavingProcess={isSavingProcess}
              onSave={() => handleFinalizeAndSaveProcess(false)}
              onUpdateRequirements={(updated) => {
                setStepRequirements((prev) => ({ ...prev, [activeStep.step_id]: updated }));
                setHasUnsavedChanges(true);
              }}
              onRemoveSkill={(catKey, skillIdx) => {
                const currentReqs = stepRequirements[activeStep.step_id];
                if (!currentReqs) return;
                const skillToRemove = currentReqs.requiredSkillAreas[catKey]?.[skillIdx];
                if (skillToRemove) {
                  handleRemoveSkillFromStep(activeStep.step_id, skillToRemove.name);
                }
              }}
              onApproveAndContinue={() => {
                advanceToStep(4);
              }}
            />
          )}

          {currentStep === 4 && activeStep && (
            <ContentGapReviewStep
              step={activeStep}
              structure={structure}
              allStepRequirements={stepRequirements}
              allStepCoverages={stepCoverages}
              coverageResult={stepCoverages[activeStep.step_id] || null}
              gapDraftResult={stepGapDrafts[activeStep.step_id] || null}
              isLoading={isEvaluatingCoverage}
              onEvaluateCoverage={handleEvaluateCoverage}
              onEvaluateAllSteps={handleEvaluateAllCoverage}
              isBatchEvaluating={isBatchEvaluating}
              batchProgress={batchEvalProgress}
              onSelectStep={(sId) => setSelectedStepId(sId)}
              onApproveAndContinue={handleFinalizeAndSaveProcess}
              onRemoveSkillFromStep={handleRemoveSkillFromStep}
              isSavingProcess={isSavingProcess}
            />
          )}
        </main>
      </div>

      {/* Process Summary Modal */}
      {structure && (
        <ProcessLearningSummaryModal
          isOpen={isSummaryModalOpen}
          onClose={() => setIsSummaryModalOpen(false)}
          structure={structure}
          stepSummaries={stepSummaries}
          stepRequirements={stepRequirements}
          initialTab={summaryInitialTab}
          isSaved={isProcessSaved}
          onOpenStep={(stepId, targetStep) => {
            setSelectedStepId(stepId);
            setCurrentStep(targetStep || 4);
          }}
        />
      )}
    </div>
  );
}
