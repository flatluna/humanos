import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ClipboardList,
  CheckCircle2,
  AlertTriangle,
  XCircle,
  Copy,
  Check,
  Download,
  RefreshCw,
  Sparkles,
  UserCheck,
} from 'lucide-react';
import type {
  ProcessStepDto,
  StepLearningAssignmentResponseDto,
} from '@/api/processDocumentsApi';

export interface LearningAssignmentReviewStepProps {
  step: ProcessStepDto;
  assignmentResult: StepLearningAssignmentResponseDto | null;
  isLoading: boolean;
  onGenerateAssignment: (stepId: string) => Promise<void>;
  onApproveAndContinue: () => void;
}

export function LearningAssignmentReviewStep({
  step,
  assignmentResult,
  isLoading,
  onGenerateAssignment,
  onApproveAndContinue,
}: LearningAssignmentReviewStepProps) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    if (!assignmentResult?.assignmentMarkdown) return;
    await navigator.clipboard.writeText(assignmentResult.assignmentMarkdown);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDownload = () => {
    if (!assignmentResult?.assignmentMarkdown) return;
    const blob = new Blob([assignmentResult.assignmentMarkdown], { type: 'text/markdown;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Asignacion_Paso_${step.step_number}_${step.step_name.replace(/[^a-zA-Z0-9]/g, '_')}.md`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
        <div>
          <div className="flex items-center gap-2">
            <span className="rounded bg-brand-100 px-2 py-0.5 text-xs font-bold text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
              Paso {step.step_number}
            </span>
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {t('processLearning.assignmentStep.title')}
            </h3>
          </div>
          <p className="mt-1 text-xs text-slate-500 dark:text-white/60">
            {t('processLearning.assignmentStep.subtitle')}
          </p>
        </div>

        <div className="flex items-center gap-2">
          {assignmentResult ? (
            <>
              <button
                type="button"
                onClick={handleCopy}
                className="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white"
              >
                {copied ? <Check className="h-3.5 w-3.5 text-emerald-600" /> : <Copy className="h-3.5 w-3.5" />}
                <span>{copied ? t('processLearning.assignmentStep.copiedSuccess') : t('processLearning.assignmentStep.copyMarkdown')}</span>
              </button>

              <button
                type="button"
                onClick={handleDownload}
                className="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white"
              >
                <Download className="h-3.5 w-3.5" />
                <span>{t('processLearning.assignmentStep.downloadMarkdown')}</span>
              </button>

              <button
                type="button"
                onClick={onApproveAndContinue}
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-5 py-2 text-xs font-semibold text-white shadow-sm hover:from-brand-500 hover:to-indigo-500"
              >
                <UserCheck className="h-4 w-4" />
                <span>{t('processLearning.assignmentStep.approveAndPersonalize')}</span>
              </button>
            </>
          ) : (
            <button
              type="button"
              onClick={() => onGenerateAssignment(step.step_id)}
              disabled={isLoading}
              className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-5 py-2.5 text-xs font-semibold text-white shadow-md shadow-brand-500/20 hover:from-brand-500 hover:to-indigo-500 disabled:opacity-50"
            >
              {isLoading ? (
                <>
                  <RefreshCw className="h-4 w-4 animate-spin" />
                  <span>{t('processLearning.assignmentStep.generatingText')}</span>
                </>
              ) : (
                <>
                  <ClipboardList className="h-4 w-4" />
                  <span>{t('processLearning.assignmentStep.generateBtn')}</span>
                </>
              )}
            </button>
          )}
        </div>
      </div>

      {/* Main Content */}
      {!assignmentResult ? (
        <div className="flex flex-col items-center justify-center rounded-2xl border-2 border-dashed border-slate-200 p-12 text-center dark:border-white/10 dark:bg-slate-900/20">
          <ClipboardList className="h-10 w-10 text-slate-300 dark:text-white/20" />
          <h4 className="mt-3 text-sm font-bold text-slate-700 dark:text-white/70">
            Asignación estándar no generada
          </h4>
          <p className="mt-1 max-w-sm text-xs text-slate-400 dark:text-white/40">
            Genera la asignación de aprendizaje estructurada para revisión del supervisor y del equipo antes de personalizarla para un colaborador.
          </p>
          <button
            type="button"
            onClick={() => onGenerateAssignment(step.step_id)}
            disabled={isLoading}
            className="mt-4 inline-flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2 text-xs font-semibold text-white shadow-sm hover:bg-brand-500 disabled:opacity-50"
          >
            {isLoading ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
            <span>Generar Asignación Estándar</span>
          </button>
        </div>
      ) : (
        <div className="space-y-6">
          {/* Status Alert Banner */}
          <div
            className={`rounded-2xl border p-5 ${
              assignmentResult.assignmentStatus === 'ready'
                ? 'border-emerald-200 bg-emerald-50/50 dark:border-emerald-500/20 dark:bg-emerald-500/5'
                : assignmentResult.assignmentStatus === 'ready_with_content_gaps'
                ? 'border-amber-200 bg-amber-50/50 dark:border-amber-500/20 dark:bg-amber-500/5'
                : 'border-rose-200 bg-rose-50/50 dark:border-rose-500/20 dark:bg-rose-500/5'
            }`}
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                {assignmentResult.assignmentStatus === 'ready' ? (
                  <CheckCircle2 className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
                ) : assignmentResult.assignmentStatus === 'ready_with_content_gaps' ? (
                  <AlertTriangle className="h-5 w-5 text-amber-600 dark:text-amber-400" />
                ) : (
                  <XCircle className="h-5 w-5 text-rose-600 dark:text-rose-400" />
                )}
                <div>
                  <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                    {assignmentResult.assignmentStatus === 'ready'
                      ? t('processLearning.assignmentStep.statusReady')
                      : assignmentResult.assignmentStatus === 'ready_with_content_gaps'
                      ? t('processLearning.assignmentStep.statusReadyGaps')
                      : t('processLearning.assignmentStep.statusBlocked')}
                  </h4>
                  {assignmentResult.blockingContentGap && (
                    <p className="text-xs text-rose-700 dark:text-rose-300 mt-0.5">
                      {t('processLearning.assignmentStep.blockedWarning')}
                    </p>
                  )}
                </div>
              </div>

              <span
                className={`rounded-full px-3 py-1 text-xs font-bold uppercase ${
                  assignmentResult.assignmentStatus === 'ready'
                    ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                    : assignmentResult.assignmentStatus === 'ready_with_content_gaps'
                    ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                    : 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                }`}
              >
                {assignmentResult.assignmentStatus}
              </span>
            </div>
          </div>

          {/* Rendered Document View */}
          <div className="rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/40 overflow-hidden">
            <div className="border-b border-slate-100 bg-slate-50/80 px-6 py-3.5 flex items-center justify-between dark:border-white/5 dark:bg-white/5">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-white/60">
                Documento de Asignación Estándar (Markdown)
              </span>
              <span className="text-xs text-slate-400">
                {assignmentResult.classIds.length} Clases • {assignmentResult.engramIds.length} Engramas
              </span>
            </div>

            <div className="p-6 md:p-8 font-sans text-xs leading-relaxed overflow-x-auto whitespace-pre-wrap dark:text-slate-200">
              {assignmentResult.assignmentMarkdown}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
