import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ListTree,
  ArrowUp,
  ArrowDown,
  AlertTriangle,
  Table as TableIcon,
  Plus,
  Trash2,
  Edit2,
  Check,
  EyeOff,
  Eye,
  FileCheck2,
  FileText,
  Lock,
} from 'lucide-react';
import type {
  ProcessDocumentStructureDto,
  ProcessStepDto,
} from '@/api/processDocumentsApi';

export interface ProcessStructureReviewStepProps {
  structure: ProcessDocumentStructureDto;
  selectedStepId: string | null;
  excludedStepIds: Set<string>;
  onSelectStep: (stepId: string) => void;
  onUpdateStructure: (newStructure: ProcessDocumentStructureDto) => void;
  onToggleExcludeStep: (stepId: string) => void;
  onApproveAndContinue: () => void;
  isEditMode?: boolean;
}

export function ProcessStructureReviewStep({
  structure,
  selectedStepId,
  excludedStepIds,
  onSelectStep,
  onUpdateStructure,
  onToggleExcludeStep,
  onApproveAndContinue,
  isEditMode = false,
}: ProcessStructureReviewStepProps) {
  const { t } = useTranslation();

  // Find active step
  const activeChapter = structure.chapters.find((c) =>
    c.steps.some((s) => s.step_id === selectedStepId),
  ) || structure.chapters[0];

  const activeStep = activeChapter?.steps.find((s) => s.step_id === selectedStepId)
    || activeChapter?.steps[0]
    || null;

  // Local editing state for active step
  const [isEditing, setIsEditing] = useState(false);
  const [editedName, setEditedName] = useState('');
  const [editedObjective, setEditedObjective] = useState('');
  const [editedActivities, setEditedActivities] = useState<string[]>([]);
  const [editedDeliverables, setEditedDeliverables] = useState<string[]>([]);
  const [editedCompletion, setEditedCompletion] = useState('');
  const [newActivityText, setNewActivityText] = useState('');
  const [newDeliverableText, setNewDeliverableText] = useState('');

  const startEditing = () => {
    if (!activeStep) return;
    setEditedName(activeStep.step_name);
    setEditedObjective(activeStep.objective || '');
    setEditedActivities([...(activeStep.required_activities || [])]);
    setEditedDeliverables([...(activeStep.deliverables || [])]);
    setEditedCompletion(activeStep.completion_condition || '');
    setIsEditing(true);
  };

  const cancelEditing = () => {
    setIsEditing(false);
  };

  const saveEditing = () => {
    if (!activeStep || !activeChapter) return;

    const updatedStep: ProcessStepDto = {
      ...activeStep,
      step_name: editedName.trim() || activeStep.step_name,
      objective: editedObjective.trim(),
      required_activities: editedActivities.filter((a) => a.trim().length > 0),
      deliverables: editedDeliverables.filter((d) => d.trim().length > 0),
      completion_condition: editedCompletion.trim(),
    };

    const newChapters = structure.chapters.map((ch) => {
      if (ch.chapter_id !== activeChapter.chapter_id) return ch;
      return {
        ...ch,
        steps: ch.steps.map((st) => (st.step_id === activeStep.step_id ? updatedStep : st)),
      };
    });

    onUpdateStructure({
      ...structure,
      chapters: newChapters,
    });

    setIsEditing(false);
  };

  const handleAddActivity = () => {
    if (newActivityText.trim()) {
      setEditedActivities([...editedActivities, newActivityText.trim()]);
      setNewActivityText('');
    }
  };

  const handleRemoveActivity = (idx: number) => {
    setEditedActivities(editedActivities.filter((_, i) => i !== idx));
  };

  const handleAddDeliverable = () => {
    if (newDeliverableText.trim()) {
      setEditedDeliverables([...editedDeliverables, newDeliverableText.trim()]);
      setNewDeliverableText('');
    }
  };

  const handleRemoveDeliverable = (idx: number) => {
    setEditedDeliverables(editedDeliverables.filter((_, i) => i !== idx));
  };

  const handleMoveStep = (chapterId: string, stepIndex: number, direction: 'up' | 'down') => {
    const targetIdx = direction === 'up' ? stepIndex - 1 : stepIndex + 1;
    const newChapters = structure.chapters.map((ch) => {
      if (ch.chapter_id !== chapterId) return ch;
      if (targetIdx < 0 || targetIdx >= ch.steps.length) return ch;

      const stepsCopy = [...ch.steps];
      const temp = stepsCopy[stepIndex];
      stepsCopy[stepIndex] = stepsCopy[targetIdx];
      stepsCopy[targetIdx] = temp;

      return {
        ...ch,
        steps: stepsCopy,
      };
    });

    onUpdateStructure({
      ...structure,
      chapters: newChapters,
    });
  };

  // Find relevant tables for active step
  const relevantTables = activeStep
    ? structure.tables.filter((t) => activeStep.source_pages.includes(t.source_page))
    : [];

  const isCurrentStepExcluded = activeStep ? excludedStepIds.has(activeStep.step_id) : false;

  return (
    <div className="space-y-6">
      {/* Source Document File Banner (especially relevant in Edit Mode) */}
      {structure.source_file?.file_name && (
        <div className="flex items-center justify-between rounded-2xl border border-slate-200 bg-slate-50/80 px-5 py-3 text-xs dark:border-white/10 dark:bg-white/[0.02]">
          <div className="flex items-center gap-3">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand-50 text-brand-600 dark:bg-brand-500/20 dark:text-brand-300">
              <FileText className="h-4 w-4" />
            </span>
            <div>
              <div className="flex items-center gap-2">
                <span className="font-bold text-slate-900 dark:text-white">
                  {structure.source_file.file_name}
                </span>
                {isEditMode && (
                  <span className="inline-flex items-center gap-1 rounded bg-slate-200/70 px-1.5 py-0.2 text-[10px] font-semibold text-slate-700 dark:bg-white/10 dark:text-white/70">
                    <Lock className="h-2.5 w-2.5" /> Archivo fuente original
                  </span>
                )}
              </div>
              <p className="text-[11px] text-slate-500 dark:text-white/50">
                {structure.document_title || structure.process_name} — El documento original permanece almacenado en Azure Data Lake Storage.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Header with Title and Actions */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
        <div>
          <h3 className="text-base font-bold text-slate-900 dark:text-white">
            {t('processLearning.reviewStep.title')}
          </h3>
          <p className="text-xs text-slate-500 dark:text-white/60">
            {isEditMode
              ? 'Estructura de capítulos y pasos del proceso ya aprobada. Puedes consultar la jerarquía o continuar.'
              : t('processLearning.reviewStep.subtitle')}
          </p>
        </div>

        <button
          type="button"
          onClick={onApproveAndContinue}
          className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-5 py-2.5 text-xs font-semibold text-white shadow-md shadow-brand-500/20 hover:from-brand-500 hover:to-indigo-500 transition"
        >
          <FileCheck2 className="h-4 w-4" />
          <span>{isEditMode ? 'Continuar a Requisitos & Skills →' : t('processLearning.reviewStep.approveAndContinue')}</span>
        </button>
      </div>

      {/* Two-panel layout */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
        {/* Left Panel: Process Structure Hierarchy */}
        <div className="lg:col-span-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/40 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3 dark:border-white/5">
            <div className="flex items-center gap-2">
              <ListTree className="h-4 w-4 text-brand-600 dark:text-brand-400" />
              <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white">
                Jerarquía del Proceso
              </h4>
            </div>
            <span className="text-[11px] font-semibold text-slate-400">
              {structure.chapters.reduce((acc, c) => acc + c.steps.length, 0)} pasos
            </span>
          </div>

          <div className="space-y-3 max-h-[650px] overflow-y-auto pr-1">
            {structure.chapters.map((ch) => (
              <div key={ch.chapter_id} className="rounded-xl border border-slate-100 dark:border-white/5 overflow-hidden">
                <div className="bg-slate-50/80 p-2.5 text-xs font-bold text-slate-800 dark:bg-white/5 dark:text-white flex items-center justify-between">
                  <span className="truncate">
                    C{ch.chapter_number || '1'}: {ch.title}
                  </span>
                  <span className="text-[10px] text-slate-400">({ch.steps.length})</span>
                </div>

                <div className="p-1 space-y-1">
                  {ch.steps.map((st, idx) => {
                    const isSelected = activeStep?.step_id === st.step_id;
                    const isExcluded = excludedStepIds.has(st.step_id);

                    return (
                      <div
                        key={st.step_id}
                        className={`group flex items-center justify-between rounded-lg p-2 text-xs transition ${
                          isSelected
                            ? 'bg-brand-50 border border-brand-500 font-bold text-brand-900 dark:bg-brand-500/15 dark:border-brand-400 dark:text-white'
                            : 'hover:bg-slate-50 text-slate-700 dark:text-white/80 dark:hover:bg-white/5'
                        } ${isExcluded ? 'opacity-50 line-through' : ''}`}
                      >
                        <button
                          type="button"
                          onClick={() => onSelectStep(st.step_id)}
                          className="flex-1 text-left truncate mr-2"
                        >
                          <span className="mr-1.5">{st.step_number}.</span>
                          <span>{st.step_name}</span>
                        </button>

                        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition">
                          <button
                            type="button"
                            disabled={idx === 0}
                            onClick={() => handleMoveStep(ch.chapter_id, idx, 'up')}
                            className="rounded p-0.5 text-slate-400 hover:text-slate-700 disabled:opacity-20"
                            title="Mover arriba"
                          >
                            <ArrowUp className="h-3 w-3" />
                          </button>
                          <button
                            type="button"
                            disabled={idx === ch.steps.length - 1}
                            onClick={() => handleMoveStep(ch.chapter_id, idx, 'down')}
                            className="rounded p-0.5 text-slate-400 hover:text-slate-700 disabled:opacity-20"
                            title="Mover abajo"
                          >
                            <ArrowDown className="h-3 w-3" />
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Main Panel: Selected Step Details & Editor */}
        <div className="lg:col-span-8 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/40 space-y-6">
          {!activeStep ? (
            <div className="flex h-64 items-center justify-center text-xs italic text-slate-400">
              Selecciona un paso de trabajo para ver sus detalles.
            </div>
          ) : (
            <>
              {/* Step Title Header & Exclusion Toggle */}
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between border-b border-slate-100 pb-4 dark:border-white/5">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="rounded-md bg-brand-100 px-2 py-0.5 text-xs font-bold text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
                      Paso {activeStep.step_number}
                    </span>
                    {activeStep.source_pages.length > 0 && (
                      <span className="rounded bg-slate-100 px-2 py-0.5 font-mono text-[11px] text-slate-500 dark:bg-white/10 dark:text-white/50">
                        {t('processLearning.common.pages', { pages: activeStep.source_pages.join(', ') })}
                      </span>
                    )}
                  </div>
                  {isEditing ? (
                    <input
                      type="text"
                      value={editedName}
                      onChange={(e) => setEditedName(e.target.value)}
                      className="w-full text-base font-bold text-slate-900 border rounded-lg p-1.5 dark:bg-slate-800 dark:text-white"
                    />
                  ) : (
                    <h4 className="text-lg font-bold text-slate-900 dark:text-white">
                      {activeStep.step_name}
                    </h4>
                  )}
                </div>

                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => onToggleExcludeStep(activeStep.step_id)}
                    className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition ${
                      isCurrentStepExcluded
                        ? 'bg-amber-100 text-amber-800 hover:bg-amber-200 dark:bg-amber-500/20 dark:text-amber-300'
                        : 'border border-slate-200 text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:text-white/70'
                    }`}
                  >
                    {isCurrentStepExcluded ? (
                      <>
                        <Eye className="h-3.5 w-3.5" />
                        <span>{t('processLearning.reviewStep.includeStepBtn')}</span>
                      </>
                    ) : (
                      <>
                        <EyeOff className="h-3.5 w-3.5" />
                        <span>{t('processLearning.reviewStep.excludeStepBtn')}</span>
                      </>
                    )}
                  </button>

                  {!isEditing ? (
                    <button
                      type="button"
                      onClick={startEditing}
                      className="inline-flex items-center gap-1 rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:text-white/80"
                    >
                      <Edit2 className="h-3.5 w-3.5" />
                      <span>{t('processLearning.common.edit')}</span>
                    </button>
                  ) : (
                    <div className="flex items-center gap-1.5">
                      <button
                        type="button"
                        onClick={cancelEditing}
                        className="rounded-lg border px-3 py-1.5 text-xs text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:text-white/70"
                      >
                        {t('processLearning.common.cancel')}
                      </button>
                      <button
                        type="button"
                        onClick={saveEditing}
                        className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-500"
                      >
                        <Check className="h-3.5 w-3.5" />
                        <span>{t('processLearning.common.save')}</span>
                      </button>
                    </div>
                  )}
                </div>
              </div>

              {isCurrentStepExcluded && (
                <div className="rounded-xl bg-amber-50 p-3 text-xs text-amber-800 dark:bg-amber-500/10 dark:text-amber-300 flex items-center gap-2">
                  <AlertTriangle className="h-4 w-4 shrink-0" />
                  <span>{t('processLearning.reviewStep.stepExcludedNotice')}</span>
                </div>
              )}

              {/* Objective */}
              <div className="space-y-1.5 text-xs">
                <span className="font-bold uppercase tracking-wider text-slate-500 dark:text-white/50">
                  {t('processLearning.reviewStep.objectiveLabel')}
                </span>
                {isEditing ? (
                  <textarea
                    rows={2}
                    value={editedObjective}
                    onChange={(e) => setEditedObjective(e.target.value)}
                    className="w-full rounded-lg border p-2 text-slate-800 dark:bg-slate-800 dark:text-white"
                  />
                ) : (
                  <p className="text-slate-800 dark:text-white/90">
                    {activeStep.objective || '—'}
                  </p>
                )}
              </div>

              {/* Required Activities */}
              <div className="space-y-2 text-xs">
                <span className="font-bold uppercase tracking-wider text-slate-500 dark:text-white/50">
                  {t('processLearning.reviewStep.activitiesLabel')}
                </span>

                {isEditing ? (
                  <div className="space-y-2">
                    <ul className="space-y-1.5">
                      {editedActivities.map((act, i) => (
                        <li key={i} className="flex items-center justify-between gap-2 rounded-lg border p-2 dark:border-white/10">
                          <span className="text-slate-800 dark:text-white/90">{act}</span>
                          <button
                            type="button"
                            onClick={() => handleRemoveActivity(i)}
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
                        value={newActivityText}
                        onChange={(e) => setNewActivityText(e.target.value)}
                        placeholder="Nueva actividad..."
                        className="flex-1 rounded-lg border px-2.5 py-1.5 text-xs dark:bg-slate-800 dark:text-white"
                      />
                      <button
                        type="button"
                        onClick={handleAddActivity}
                        className="rounded-lg bg-slate-800 px-3 py-1.5 font-semibold text-white dark:bg-white dark:text-slate-900"
                      >
                        <Plus className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                ) : (
                  <ul className="list-inside list-disc space-y-1 text-slate-700 dark:text-white/80">
                    {activeStep.required_activities.map((act, i) => (
                      <li key={i}>{act}</li>
                    ))}
                  </ul>
                )}
              </div>

              {/* Deliverables */}
              <div className="space-y-2 text-xs">
                <span className="font-bold uppercase tracking-wider text-slate-500 dark:text-white/50">
                  {t('processLearning.reviewStep.deliverablesLabel')}
                </span>

                {isEditing ? (
                  <div className="space-y-2">
                    <ul className="space-y-1.5">
                      {editedDeliverables.map((deliv, i) => (
                        <li key={i} className="flex items-center justify-between gap-2 rounded-lg border p-2 dark:border-white/10">
                          <span className="text-slate-800 dark:text-white/90">{deliv}</span>
                          <button
                            type="button"
                            onClick={() => handleRemoveDeliverable(i)}
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
                        value={newDeliverableText}
                        onChange={(e) => setNewDeliverableText(e.target.value)}
                        placeholder="Nuevo entregable..."
                        className="flex-1 rounded-lg border px-2.5 py-1.5 text-xs dark:bg-slate-800 dark:text-white"
                      />
                      <button
                        type="button"
                        onClick={handleAddDeliverable}
                        className="rounded-lg bg-slate-800 px-3 py-1.5 font-semibold text-white dark:bg-white dark:text-slate-900"
                      >
                        <Plus className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="flex flex-wrap gap-1.5">
                    {activeStep.deliverables.map((d, i) => (
                      <span
                        key={i}
                        className="rounded-full bg-slate-100 px-3 py-1 font-medium text-slate-700 dark:bg-white/10 dark:text-white/80"
                      >
                        {d}
                      </span>
                    ))}
                  </div>
                )}
              </div>

              {/* Completion condition */}
              <div className="space-y-1 text-xs">
                <span className="font-bold uppercase tracking-wider text-slate-500 dark:text-white/50">
                  {t('processLearning.reviewStep.completionConditionLabel')}
                </span>
                {isEditing ? (
                  <input
                    type="text"
                    value={editedCompletion}
                    onChange={(e) => setEditedCompletion(e.target.value)}
                    className="w-full rounded-lg border p-2 text-slate-800 dark:bg-slate-800 dark:text-white"
                  />
                ) : (
                  <p className="rounded-lg bg-emerald-50/60 p-2.5 text-emerald-900 dark:bg-emerald-500/10 dark:text-emerald-300 font-medium">
                    {activeStep.completion_condition || '—'}
                  </p>
                )}
              </div>

              {/* Skills mentioned in document */}
              {activeStep.required_skills.length > 0 && (
                <div className="space-y-1.5 text-xs">
                  <span className="font-bold uppercase tracking-wider text-slate-500 dark:text-white/50">
                    {t('processLearning.reviewStep.skillsInDocLabel')}
                  </span>
                  <div className="flex flex-wrap gap-1.5">
                    {activeStep.required_skills.map((sk, i) => (
                      <span
                        key={i}
                        className="rounded-md bg-teal-50 px-2 py-0.5 font-medium text-teal-800 dark:bg-teal-500/10 dark:text-teal-300"
                      >
                        {sk}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              {/* Tables on same page */}
              {relevantTables.length > 0 && (
                <div className="space-y-3 pt-3 border-t border-slate-100 dark:border-white/5">
                  <div className="flex items-center gap-1.5 text-xs font-bold text-slate-700 dark:text-white/70">
                    <TableIcon className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                    <span>{t('processLearning.reviewStep.tablesOnPageLabel')}</span>
                  </div>

                  {relevantTables.map((t) => (
                    <div key={t.table_id} className="overflow-x-auto rounded-xl border border-slate-200 dark:border-white/10">
                      <table className="min-w-full divide-y divide-slate-200 text-left text-xs dark:divide-white/10">
                        {t.headers.length > 0 && (
                          <thead className="bg-slate-50 dark:bg-white/5">
                            <tr>
                              {t.headers.map((h, i) => (
                                <th key={i} className="px-3 py-1.5 font-semibold text-slate-700 dark:text-white">
                                  {h}
                                </th>
                              ))}
                            </tr>
                          </thead>
                        )}
                        <tbody className="divide-y divide-slate-100 dark:divide-white/5">
                          {t.rows.map((row, ri) => (
                            <tr key={ri}>
                              {row.map((c, ci) => (
                                <td key={ci} className="px-3 py-1.5 text-slate-600 dark:text-white/70">
                                  {c}
                                </td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
