import { useTranslation } from 'react-i18next';
import {
  UploadCloud,
  ListTree,
  FileCheck2,
  ShieldAlert,
  Check,
  AlertTriangle,
  Clock,
  XCircle,
} from 'lucide-react';

export type WizardStepState =
  | 'not_started'
  | 'in_progress'
  | 'completed'
  | 'needs_review'
  | 'content_gap'
  | 'blocked'
  | 'failed';

export interface ProcessLearningWizardStepperProps {
  currentStep: number;
  maxStepReached: number;
  stepStates: Record<number, WizardStepState>;
  onSelectStep: (stepNumber: number) => void;
  isEditMode?: boolean;
}

const STEP_ICONS = [
  UploadCloud,    // Step 1: Subir Proceso
  ListTree,       // Step 2: Estructura de Pasos
  FileCheck2,     // Step 3: Requisitos de Trabajo
  ShieldAlert,    // Step 4: Skills Gap Analysis & Matriz
];

export function ProcessLearningWizardStepper({
  currentStep,
  maxStepReached,
  stepStates,
  onSelectStep,
  isEditMode = false,
}: ProcessLearningWizardStepperProps) {
  const { t } = useTranslation();

  const stepsConfig = [
    {
      title: isEditMode
        ? t('processLearning.steps.step1.editTitle', '1. Documento Original (Bloqueado)')
        : t('processLearning.steps.step1.title', 'Subir Proceso'),
    },
    { title: t('processLearning.steps.step2.title', 'Estructura de Pasos') },
    { title: t('processLearning.steps.step3.title', 'Requisitos & Skills') },
    { title: t('processLearning.steps.step4.title', 'Skills Gap & Matriz') },
  ];

  const getStatusLabel = (state: WizardStepState, stepNumber: number) => {
    if (isEditMode && stepNumber === 1) {
      return 'Original fijo';
    }
    switch (state) {
      case 'completed':
        return t('processLearning.stepperStatus.completed', 'Completado');
      case 'in_progress':
        return t('processLearning.stepperStatus.inProgress', 'En progreso');
      case 'needs_review':
        return t('processLearning.stepperStatus.needsReview', 'Por revisar');
      case 'content_gap':
        return t('processLearning.stepperStatus.contentGap', 'Con brecha');
      case 'blocked':
        return t('processLearning.stepperStatus.blocked', 'Bloqueado');
      case 'failed':
        return t('processLearning.stepperStatus.failed', 'Error');
      default:
        return t('processLearning.stepperStatus.notStarted', 'Sin iniciar');
    }
  };

  const renderStatusBadge = (state: WizardStepState, stepNumber: number) => {
    if (isEditMode && stepNumber === 1) {
      return <Check className="h-3 w-3 text-emerald-600 dark:text-emerald-400" />;
    }
    switch (state) {
      case 'completed':
        return <Check className="h-3 w-3 text-emerald-600 dark:text-emerald-400" />;
      case 'in_progress':
        return <Clock className="h-3 w-3 text-brand-600 dark:text-brand-400" />;
      case 'content_gap':
      case 'needs_review':
        return <AlertTriangle className="h-3 w-3 text-amber-500" />;
      case 'blocked':
      case 'failed':
        return <XCircle className="h-3 w-3 text-rose-500" />;
      default:
        return null;
    }
  };

  return (
    <nav aria-label="Progreso del asistente" className="w-full">
      <ol className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-4">
        {stepsConfig.map((item, idx) => {
          const stepNumber = idx + 1;
          const Icon = STEP_ICONS[idx];
          const isActive = currentStep === stepNumber;
          const isStep1InEdit = isEditMode && stepNumber === 1;
          const isAccessible = !isStep1InEdit && stepNumber <= maxStepReached;
          const state = stepStates[stepNumber] || 'not_started';

          return (
            <li key={stepNumber} className="flex">
              <button
                type="button"
                onClick={() => {
                  if (isAccessible) onSelectStep(stepNumber);
                }}
                disabled={!isAccessible}
                title={isStep1InEdit ? 'El archivo fuente original está guardado. Edita los pasos en el Paso 2.' : undefined}
                aria-current={isActive ? 'step' : undefined}
                className={`relative flex w-full flex-col justify-between rounded-xl border p-2.5 text-left transition-all ${
                  isStep1InEdit
                    ? 'border-slate-200 bg-slate-50/70 opacity-60 cursor-not-allowed dark:border-white/5 dark:bg-white/[0.02]'
                    : isActive
                    ? 'border-brand-500 bg-brand-50/50 shadow-sm shadow-brand-500/10 dark:border-brand-400 dark:bg-brand-500/10'
                    : isAccessible
                    ? 'border-slate-200 bg-white hover:border-slate-300 dark:border-white/10 dark:bg-white/5 dark:hover:border-white/20'
                    : 'cursor-not-allowed border-slate-100 bg-slate-50 opacity-60 dark:border-white/5 dark:bg-white/[0.02]'
                }`}
              >
                <div className="flex items-center justify-between gap-1">
                  <div className="flex items-center gap-1.5">
                    <span
                      className={`flex h-6 w-6 items-center justify-center rounded-lg text-xs font-bold ${
                        isStep1InEdit
                          ? 'bg-slate-200 text-slate-500 dark:bg-white/10 dark:text-white/40'
                          : isActive
                          ? 'bg-brand-600 text-white dark:bg-brand-500'
                          : state === 'completed'
                          ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                          : 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/70'
                      }`}
                    >
                      {stepNumber}
                    </span>
                    <Icon className="h-4 w-4 text-slate-500 dark:text-white/60" />
                  </div>
                  {renderStatusBadge(state, stepNumber)}
                </div>

                <div className="mt-2 min-w-0">
                  <p className="truncate text-xs font-bold text-slate-900 dark:text-white">
                    {item.title}
                  </p>
                  <p className="mt-0.5 truncate text-[10px] text-slate-500 dark:text-white/50">
                    {getStatusLabel(state, stepNumber)}
                  </p>
                </div>
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
