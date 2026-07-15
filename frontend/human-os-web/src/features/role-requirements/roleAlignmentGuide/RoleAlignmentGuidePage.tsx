import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/Card';
import { SetupProgress } from '@/features/enterprise-context/components/SetupProgress';
import { DimensionStepPlaceholder } from './components/DimensionStepPlaceholder';
import { RoleAlignmentSummaryStep } from './components/RoleAlignmentSummaryStep';
import { useRoleOperatingModelDraftQuery } from './hooks/useRoleAlignmentGuide';
import {
  useRoleAlignmentWizardProgress,
  useRoleAlignmentWizardSteps,
  useRoleAlignmentWizardStore,
} from './store/useRoleAlignmentWizardStore';
import { getActiveWizardSteps, getNextStepId } from './wizardSteps';

function RoleAlignmentGuideSkeleton() {
  return (
    <Card className="animate-pulse p-6 sm:p-8" aria-hidden="true">
      <div className="h-3 w-40 rounded bg-slate-200 dark:bg-white/10" />
      <div className="mt-4 h-4 w-full rounded bg-slate-200 dark:bg-white/10" />
      <div className="mt-2 h-4 w-2/3 rounded bg-slate-200 dark:bg-white/10" />
    </Card>
  );
}

export function RoleAlignmentGuidePage() {
  const { t } = useTranslation();
  const { data, isLoading } = useRoleOperatingModelDraftQuery();

  const draft = useRoleAlignmentWizardStore((state) => state.draft);
  const findings = useRoleAlignmentWizardStore((state) => state.findings);
  const currentStepId = useRoleAlignmentWizardStore((state) => state.currentStepId);
  const initializeReview = useRoleAlignmentWizardStore((state) => state.initializeReview);
  const startReview = useRoleAlignmentWizardStore((state) => state.startReview);
  const markSummaryComplete = useRoleAlignmentWizardStore((state) => state.markSummaryComplete);
  const goToStep = useRoleAlignmentWizardStore((state) => state.goToStep);

  const steps = useRoleAlignmentWizardSteps();
  const progress = useRoleAlignmentWizardProgress();

  // Guards against React 18 StrictMode's dev-only double-invoked effect
  // and against overwriting a returning employee's in-progress review
  // (persisted via zustand) with the canned draft.
  const hasInitializedRef = useRef(false);
  useEffect(() => {
    if (data && !draft && !hasInitializedRef.current) {
      hasInitializedRef.current = true;
      initializeReview(data.draft, data.findings);
    }
  }, [data, draft, initializeReview]);

  function handleStartReview() {
    startReview();
    markSummaryComplete();

    // Recompute steps with summary now acknowledged, then move to the
    // first active step after it — but never straight to Final Review
    // (only possible in the degenerate case of a draft with no
    // reviewable dimensions at all).
    const activeSteps = getActiveWizardSteps(steps);
    const nextStepId = getNextStepId(activeSteps, 'summary');
    if (nextStepId && nextStepId !== 'finalReview') {
      goToStep(nextStepId);
    }
  }

  const currentStep = steps.find((step) => step.id === currentStepId);

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <SetupProgress
        currentStep={progress.currentStepIndex + 1}
        totalSteps={progress.totalSteps || 1}
        stepLabel={t(`growthPlan.roleExperience.alignmentGuide.stepLabels.${currentStepId}`)}
      />

      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-slate-900 dark:text-white">
        {t('growthPlan.roleExperience.alignmentGuide.title')}
      </h1>

      <div className="mt-8">
        {isLoading && <RoleAlignmentGuideSkeleton />}

        {!isLoading && currentStepId === 'summary' && (
          <RoleAlignmentSummaryStep steps={steps} findings={findings} onStartReview={handleStartReview} />
        )}

        {!isLoading && currentStepId !== 'summary' && currentStep && (
          <DimensionStepPlaceholder step={currentStep} onBackToSummary={() => goToStep('summary')} />
        )}
      </div>
    </div>
  );
}
