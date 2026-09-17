import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  BookOpen,
  RefreshCw,
  ChevronDown,
  ChevronRight,
  AlertTriangle,
  Trash2,
  RotateCcw,
  FileCheck2,
  Layers,
} from 'lucide-react';
import type {
  ProcessStepDto,
  RelevantEngramsResponseDto,
  SelectedEngramDto,
} from '@/api/processDocumentsApi';

export interface LearningContentMatchStepProps {
  step: ProcessStepDto;
  relevantEngramsResult: RelevantEngramsResponseDto | null;
  isLoading: boolean;
  onSearchAndMatch: (stepId: string) => Promise<void>;
  onMatchAllSteps?: () => Promise<void>;
  isBatchMatching?: boolean;
  batchProgress?: { current: number; total: number } | null;
  onApproveAndContinue: () => void;
}

export function LearningContentMatchStep({
  step,
  relevantEngramsResult,
  isLoading,
  onMatchAllSteps,
  isBatchMatching,
  batchProgress,
  onApproveAndContinue,
}: LearningContentMatchStepProps) {
  const { t } = useTranslation();

  const [expandedClasses, setExpandedClasses] = useState<Record<string, boolean>>({});
  const [removedEngramIds, setRemovedEngramIds] = useState<Set<string>>(new Set());
  const [criticalRemovalWarning, setCriticalRemovalWarning] = useState<string | null>(null);

  const toggleClass = (classId: string) => {
    setExpandedClasses((prev) => ({
      ...prev,
      [classId]: !prev[classId],
    }));
  };

  const handleRemoveEngram = (engram: SelectedEngramDto) => {
    if (engram.criticality === 'critical') {
      setCriticalRemovalWarning(
        t('processLearning.matchingStep.criticalRemoveAlert', { name: engram.engramName }),
      );
    }
    setRemovedEngramIds((prev) => {
      const next = new Set(prev);
      next.add(engram.engramId);
      return next;
    });
  };

  const handleRestoreEngram = (engramId: string) => {
    setRemovedEngramIds((prev) => {
      const next = new Set(prev);
      next.delete(engramId);
      return next;
    });
    setCriticalRemovalWarning(null);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
        <div>
          <div className="flex items-center gap-2">
            <span className="rounded bg-brand-100 px-2 py-0.5 text-xs font-bold text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
              {t('processLearning.coverageStep.stepLabel', { number: step.step_number })}
            </span>
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {t('processLearning.matchingStep.title')}
            </h3>
          </div>
          <p className="mt-1 text-xs text-slate-500 dark:text-white/60">
            {t('processLearning.matchingStep.subtitle')}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {onMatchAllSteps && (
            <button
              type="button"
              onClick={onMatchAllSteps}
              disabled={isBatchMatching || isLoading}
              className="inline-flex items-center gap-1.5 rounded-xl border-2 border-brand-500 bg-brand-50/80 px-4 py-2 text-xs font-bold text-brand-700 shadow-sm hover:bg-brand-100 dark:border-brand-400 dark:bg-brand-500/20 dark:text-brand-300 disabled:opacity-50 transition-all"
            >
              {isBatchMatching ? (
                <>
                  <RefreshCw className="h-4 w-4 animate-spin text-brand-600" />
                  <span>{t('processLearning.matchingStep.matchingAllText', { current: batchProgress?.current, total: batchProgress?.total })}</span>
                </>
              ) : (
                <>
                  <Layers className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                  <span>{t('processLearning.matchingStep.matchAllBtn')}</span>
                </>
              )}
            </button>
          )}

          {relevantEngramsResult ? (
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={onApproveAndContinue}
                disabled={isBatchMatching || isLoading}
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-5 py-2 text-xs font-semibold text-white shadow-sm hover:from-brand-500 hover:to-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <FileCheck2 className="h-4 w-4" />
                <span>{t('processLearning.matchingStep.acceptAndContinue')}</span>
              </button>
            </div>
          ) : null}
        </div>
      </div>

      {/* Critical removal warning banner */}
      {criticalRemovalWarning && (
        <div className="flex items-start justify-between rounded-xl border border-rose-200 bg-rose-50 p-4 text-xs text-rose-800 dark:border-rose-500/20 dark:bg-rose-500/10 dark:text-rose-300">
          <div className="flex items-start gap-2.5">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-rose-600" />
            <p className="font-medium">{criticalRemovalWarning}</p>
          </div>
          <button
            type="button"
            onClick={() => setCriticalRemovalWarning(null)}
            className="text-xs font-bold text-rose-700 hover:underline dark:text-rose-400"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* Main Content */}
      {!relevantEngramsResult ? (
        <div className="flex flex-col items-center justify-center rounded-2xl border-2 border-dashed border-slate-200 p-12 text-center dark:border-white/10 dark:bg-slate-900/20">
          <BookOpen className="h-10 w-10 text-slate-300 dark:text-white/20" />
          <h4 className="mt-3 text-sm font-bold text-slate-700 dark:text-white/70">
            {t('processLearning.matchingStep.subtitle')}
          </h4>
          <p className="mt-1 max-w-sm text-xs text-slate-400 dark:text-white/40">
            {t('processLearning.matchingStep.batchNotice')}
          </p>
          <div className="mt-4 flex items-center justify-center">
            {onMatchAllSteps && (
              <button
                type="button"
                onClick={onMatchAllSteps}
                disabled={isBatchMatching || isLoading}
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-6 py-3 text-xs font-bold text-white shadow-md shadow-brand-500/20 hover:from-brand-500 hover:to-indigo-500 disabled:opacity-50 transition-all"
              >
                {isBatchMatching ? (
                  <>
                    <RefreshCw className="h-4 w-4 animate-spin text-white" />
                    <span>{t('processLearning.matchingStep.matchingAllText', { current: batchProgress?.current, total: batchProgress?.total })}</span>
                  </>
                ) : (
                  <>
                    <Layers className="h-4 w-4" />
                    <span>{t('processLearning.matchingStep.matchAllBtn')}</span>
                  </>
                )}
              </button>
            )}
          </div>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="flex items-center justify-between px-1">
            <span className="text-xs font-semibold text-slate-500 dark:text-white/50">
              {t('processLearning.matchingStep.classesFound', { count: relevantEngramsResult.selectedClasses.length })}
            </span>
          </div>

          {relevantEngramsResult.selectedClasses.length === 0 ? (
            <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center text-xs text-slate-500 dark:border-white/10 dark:bg-slate-900/40">
              {t('processLearning.matchingStep.noCoursesFound')}
            </div>
          ) : (
            relevantEngramsResult.selectedClasses.map((sc) => {
              const isExpanded = expandedClasses[sc.classId] !== false; // expanded by default
              const activeEngrams = sc.selectedEngrams.filter((e) => !removedEngramIds.has(e.engramId));
              const removedEngrams = sc.selectedEngrams.filter((e) => removedEngramIds.has(e.engramId));

              return (
                <div
                  key={sc.classId}
                  className="rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/40 overflow-hidden"
                >
                  {/* Course Header with Learning Path */}
                  <div
                    onClick={() => toggleClass(sc.classId)}
                    className="flex cursor-pointer items-start justify-between border-b border-slate-100 bg-slate-50/70 p-4 transition hover:bg-slate-100/60 dark:border-white/5 dark:bg-white/5 dark:hover:bg-white/10"
                  >
                    <div className="space-y-1">
                      <span className="inline-block rounded-md bg-violet-100 px-2 py-0.5 text-[10px] font-bold text-violet-800 dark:bg-violet-500/20 dark:text-violet-300">
                        {t('processLearning.matchingStep.learningPath', { name: sc.learningPathName })}
                      </span>
                      <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                        {sc.className}
                      </h4>
                      {sc.classDescription && (
                        <p className="text-xs text-slate-500 dark:text-white/60">
                          {sc.classDescription}
                        </p>
                      )}
                    </div>

                    <div className="flex items-center gap-3">
                      <span className="rounded-full bg-slate-200/70 px-2.5 py-0.5 text-[11px] font-semibold text-slate-700 dark:bg-white/10 dark:text-white/80">
                        {t('processLearning.matchingStep.engramsCount', { count: activeEngrams.length })}
                      </span>
                      {isExpanded ? <ChevronDown className="h-4 w-4 text-slate-400" /> : <ChevronRight className="h-4 w-4 text-slate-400" />}
                    </div>
                  </div>

                  {/* Skills/Modules List */}
                  {isExpanded && (
                    <div className="p-4 space-y-3">
                      {activeEngrams.map((eng) => (
                        <div
                          key={eng.engramId}
                          className="rounded-xl border border-slate-200/80 p-3.5 space-y-2 dark:border-white/10 dark:bg-white/[0.02]"
                        >
                          <div className="flex items-start justify-between gap-2">
                            <div className="flex items-center gap-2">
                              <h5 className="text-xs font-bold text-slate-900 dark:text-white">
                                {eng.engramName}
                              </h5>
                              <span
                                className={`rounded-full px-2 py-0.5 text-[10px] font-bold uppercase ${
                                  eng.criticality === 'critical'
                                    ? 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                    : 'bg-blue-100 text-blue-800 dark:bg-blue-500/20 dark:text-blue-300'
                                }`}
                              >
                                {eng.criticality === 'critical'
                                  ? t('processLearning.matchingStep.criticalBadge')
                                  : t('processLearning.matchingStep.supportingBadge')}
                              </span>
                            </div>

                            <button
                              type="button"
                              onClick={() => handleRemoveEngram(eng)}
                              className="text-slate-400 hover:text-rose-600 transition"
                              title={t('processLearning.matchingStep.removeEngramPrompt')}
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </button>
                          </div>

                          {eng.engramDescription && (
                            <p className="text-[11px] text-slate-600 dark:text-white/70">
                              {eng.engramDescription}
                            </p>
                          )}

                          {eng.supportedRequirements.length > 0 && (
                            <div className="flex flex-wrap items-center gap-1.5 text-[10px]">
                              <span className="font-semibold text-slate-700 dark:text-white/80">Skills / Abilities:</span>
                              {eng.supportedRequirements.map((r, ri) => (
                                <span
                                  key={ri}
                                  className="rounded bg-slate-100 px-2 py-0.5 font-medium text-slate-700 dark:bg-white/10 dark:text-white/80"
                                >
                                  {r}
                                </span>
                              ))}
                            </div>
                          )}
                        </div>
                      ))}

                      {/* Removed Skills/Courses (Option to restore) */}
                      {removedEngrams.length > 0 && (
                        <div className="pt-2 border-t border-slate-100 dark:border-white/5 space-y-1.5">
                          <p className="text-[11px] font-semibold text-slate-400">
                            Removed items:
                          </p>
                          <div className="flex flex-wrap gap-2">
                            {removedEngrams.map((re) => (
                              <button
                                key={re.engramId}
                                type="button"
                                onClick={() => handleRestoreEngram(re.engramId)}
                                className="inline-flex items-center gap-1.5 rounded-lg border border-dashed border-slate-300 px-2.5 py-1 text-[11px] text-slate-500 hover:border-brand-500 hover:text-brand-600 dark:border-white/20 dark:text-white/60"
                              >
                                <RotateCcw className="h-3 w-3" />
                                <span>Restore "{re.engramName}"</span>
                              </button>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              );
            })
          )}
        </div>
      )}
    </div>
  );
}
