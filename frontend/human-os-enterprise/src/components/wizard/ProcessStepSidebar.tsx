import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ListTree,
  ChevronDown,
  ChevronRight,
  FileText,
  CheckCircle2,
  AlertTriangle,
  Clock,
  Sparkles,
  Layers,
  ChevronLeft,
} from 'lucide-react';
import type { ProcessDocumentStructureDto } from '@/api/processDocumentsApi';

export type StepProgressState =
  | 'not_analyzed'
  | 'reqs_ready'
  | 'matched'
  | 'gap'
  | 'ready'
  | 'assigned';

export interface ProcessStepSidebarProps {
  structure: ProcessDocumentStructureDto;
  selectedStepId: string | null;
  stepProgressMap: Record<string, StepProgressState>;
  excludedStepIds: Set<string>;
  hasUnsavedChanges: boolean;
  onSelectStep: (stepId: string) => void;
}

export function ProcessStepSidebar({
  structure,
  selectedStepId,
  stepProgressMap,
  excludedStepIds,
  hasUnsavedChanges,
  onSelectStep,
}: ProcessStepSidebarProps) {
  const { t } = useTranslation();
  const [collapsed, setCollapsed] = useState(false);
  const [expandedChapters, setExpandedChapters] = useState<Record<string, boolean>>({
    [structure.chapters[0]?.chapter_id || 'ch-1']: true,
  });

  const toggleChapter = (chapterId: string) => {
    setExpandedChapters((prev) => ({
      ...prev,
      [chapterId]: !prev[chapterId],
    }));
  };

  const handleStepClick = (stepId: string) => {
    if (stepId === selectedStepId) return;
    if (hasUnsavedChanges) {
      const confirmLeave = window.confirm(t('processLearning.common.unsavedWarning'));
      if (!confirmLeave) return;
    }
    onSelectStep(stepId);
  };

  const renderStatusBadge = (status: StepProgressState, isExcluded: boolean) => {
    if (isExcluded) {
      return (
        <span className="rounded bg-slate-200/80 px-1.5 py-0.5 text-[9px] font-bold text-slate-600 dark:bg-white/10 dark:text-white/50">
          Excluido
        </span>
      );
    }

    switch (status) {
      case 'assigned':
        return (
          <span className="inline-flex items-center gap-1 rounded bg-emerald-100 px-1.5 py-0.5 text-[9px] font-bold text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300">
            <CheckCircle2 className="h-2.5 w-2.5" />
            {t('processLearning.sidebar.status.assigned')}
          </span>
        );
      case 'ready':
        return (
          <span className="inline-flex items-center gap-1 rounded bg-blue-100 px-1.5 py-0.5 text-[9px] font-bold text-blue-800 dark:bg-blue-500/20 dark:text-blue-300">
            <FileText className="h-2.5 w-2.5" />
            {t('processLearning.sidebar.status.ready')}
          </span>
        );
      case 'gap':
        return (
          <span className="inline-flex items-center gap-1 rounded bg-amber-100 px-1.5 py-0.5 text-[9px] font-bold text-amber-800 dark:bg-amber-500/20 dark:text-amber-300">
            <AlertTriangle className="h-2.5 w-2.5" />
            {t('processLearning.sidebar.status.gap')}
          </span>
        );
      case 'matched':
        return (
          <span className="inline-flex items-center gap-1 rounded bg-violet-100 px-1.5 py-0.5 text-[9px] font-bold text-violet-800 dark:bg-violet-500/20 dark:text-violet-300">
            <Layers className="h-2.5 w-2.5" />
            {t('processLearning.sidebar.status.matched')}
          </span>
        );
      case 'reqs_ready':
        return (
          <span className="inline-flex items-center gap-1 rounded bg-teal-100 px-1.5 py-0.5 text-[9px] font-bold text-teal-800 dark:bg-teal-500/20 dark:text-teal-300">
            <Sparkles className="h-2.5 w-2.5" />
            {t('processLearning.sidebar.status.reqsReady')}
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1 rounded bg-slate-100 px-1.5 py-0.5 text-[9px] font-medium text-slate-500 dark:bg-white/10 dark:text-white/40">
            <Clock className="h-2.5 w-2.5" />
            {t('processLearning.sidebar.status.notAnalyzed')}
          </span>
        );
    }
  };

  if (collapsed) {
    return (
      <aside className="sticky top-20 flex h-[calc(100vh-6rem)] w-12 flex-col items-center rounded-2xl border border-slate-200 bg-white py-4 shadow-sm dark:border-white/10 dark:bg-slate-900/60">
        <button
          type="button"
          onClick={() => setCollapsed(false)}
          title={t('processLearning.sidebar.expand')}
          className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
        >
          <ChevronRight className="h-5 w-5" />
        </button>
        <div className="mt-8 flex flex-col items-center gap-3 text-slate-400">
          <ListTree className="h-5 w-5" />
          <span className="rotate-90 text-[10px] font-bold uppercase tracking-widest">
            {t('processLearning.sidebar.title')}
          </span>
        </div>
      </aside>
    );
  }

  return (
    <aside
      aria-label="Jerarquía de pasos del proceso"
      className="sticky top-20 flex h-[calc(100vh-6rem)] w-72 shrink-0 flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/60"
    >
      {/* Header */}
      <div className="flex items-center justify-between border-b border-slate-200 p-3.5 dark:border-white/10">
        <div className="flex items-center gap-2">
          <ListTree className="h-4 w-4 text-brand-600 dark:text-brand-400" />
          <h2 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white">
            {t('processLearning.sidebar.title')}
          </h2>
        </div>
        <button
          type="button"
          onClick={() => setCollapsed(true)}
          title={t('processLearning.sidebar.collapse')}
          className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-white/10 dark:hover:text-white"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
      </div>

      {/* Chapters & Steps list */}
      <div className="flex-1 overflow-y-auto p-2 space-y-2">
        {structure.chapters.map((chapter) => {
          const isExpanded = !!expandedChapters[chapter.chapter_id];
          return (
            <div key={chapter.chapter_id} className="rounded-xl border border-slate-100 dark:border-white/5 overflow-hidden">
              <button
                type="button"
                onClick={() => toggleChapter(chapter.chapter_id)}
                className="flex w-full items-center justify-between bg-slate-50/80 px-2.5 py-2 text-left text-xs font-bold text-slate-800 transition hover:bg-slate-100 dark:bg-white/5 dark:text-white dark:hover:bg-white/10"
              >
                <div className="flex items-center gap-1.5 truncate">
                  <span className="rounded bg-brand-100 px-1 py-0.2 text-[10px] font-extrabold text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
                    C{chapter.chapter_number || '1'}
                  </span>
                  <span className="truncate">{chapter.title}</span>
                </div>
                {isExpanded ? <ChevronDown className="h-3.5 w-3.5 shrink-0" /> : <ChevronRight className="h-3.5 w-3.5 shrink-0" />}
              </button>

              {isExpanded && (
                <div className="p-1.5 space-y-1 bg-white dark:bg-transparent">
                  {chapter.steps.length === 0 ? (
                    <p className="p-2 text-[11px] italic text-slate-400 dark:text-white/40">
                      Sin pasos específicos
                    </p>
                  ) : (
                    chapter.steps.map((st) => {
                      const isSelected = selectedStepId === st.step_id;
                      const isExcluded = excludedStepIds.has(st.step_id);
                      const status = stepProgressMap[st.step_id] || 'not_analyzed';

                      return (
                        <button
                          key={st.step_id}
                          type="button"
                          onClick={() => handleStepClick(st.step_id)}
                          className={`flex w-full flex-col gap-1 rounded-lg p-2 text-left transition ${
                            isSelected
                              ? 'bg-brand-50 border border-brand-500 text-brand-900 shadow-sm dark:bg-brand-500/15 dark:border-brand-400 dark:text-white'
                              : 'hover:bg-slate-50 text-slate-700 dark:text-white/80 dark:hover:bg-white/5'
                          } ${isExcluded ? 'opacity-60' : ''}`}
                        >
                          <div className="flex items-start justify-between gap-1 w-full">
                            <span className="text-[11px] font-semibold line-clamp-2">
                              {st.step_number}. {st.step_name}
                            </span>
                          </div>

                          <div className="flex items-center justify-between gap-1 mt-0.5">
                            {renderStatusBadge(status, isExcluded)}
                            {st.source_pages.length > 0 && (
                              <span className="text-[9px] font-mono text-slate-400 dark:text-white/40">
                                p.{st.source_pages.join(',')}
                              </span>
                            )}
                          </div>
                        </button>
                      );
                    })
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </aside>
  );
}
