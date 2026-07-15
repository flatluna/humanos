import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle, RefreshCw, CheckCircle2, ArrowRight, Info, Settings2 } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { JobDescriptionQuickCapture } from '@/features/role-requirements/jobDescriptionSource/components/JobDescriptionQuickCapture';
import { SetupProgress } from './components/SetupProgress';
import { WorkContextSummary } from './components/WorkContextSummary';
import { InformationSourceNotice } from './components/InformationSourceNotice';
import { PrivacyNotice } from './components/PrivacyNotice';
import { RequestCorrectionDialog } from './components/RequestCorrectionDialog';
import { useWorkContext, useConfirmWorkContext, useSubmitCorrectionRequest } from './hooks/useWorkContext';
import type { CorrectionReason } from './types';

function SummarySkeleton() {
  return (
    <Card className="animate-pulse p-6 sm:p-8" aria-hidden="true">
      <div className="mb-6 border-b border-slate-100 pb-6 dark:border-white/10">
        <div className="h-3 w-24 rounded bg-slate-200 dark:bg-white/10" />
        <div className="mt-3 h-6 w-48 rounded bg-slate-200 dark:bg-white/10" />
      </div>
      <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2">
        {Array.from({ length: 6 }, (_, i) => (
          <div key={i}>
            <div className="h-3 w-20 rounded bg-slate-200 dark:bg-white/10" />
            <div className="mt-2 h-4 w-32 rounded bg-slate-200 dark:bg-white/10" />
          </div>
        ))}
      </div>
    </Card>
  );
}

export function WorkContextPage() {
  const { t } = useTranslation();
  const [isCorrectionDialogOpen, setCorrectionDialogOpen] = useState(false);
  const [isConfiguringRole, setConfiguringRole] = useState(false);

  const { data: workContext, isLoading, isError, refetch } = useWorkContext();
  const confirmMutation = useConfirmWorkContext();
  const correctionMutation = useSubmitCorrectionRequest();

  function handleSubmitCorrection(reason: CorrectionReason, details: string) {
    correctionMutation.mutate(
      { reason, details },
      { onSuccess: () => setCorrectionDialogOpen(false) },
    );
  }

  const isIncomplete = workContext ? !workContext.organization || !workContext.currentRole : false;
  const isResolved = workContext
    ? workContext.confirmationStatus === 'confirmed' || workContext.pendingCorrectionStatus === 'submitted'
    : false;

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <SetupProgress currentStep={1} totalSteps={9} stepLabel={t('growthPlan.workContext.stepLabel')} />

      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-slate-900 dark:text-white">
        {t('growthPlan.workContext.headline')}
      </h1>
      <p className="mt-3 max-w-xl text-slate-500 dark:text-white/50">{t('growthPlan.workContext.description')}</p>

      <div className="mt-8 space-y-5">
        {isLoading && <SummarySkeleton />}

        {!isLoading && isError && (
          <Card className="flex flex-col items-center gap-4 p-10 text-center">
            <AlertTriangle className="h-8 w-8 text-amber-500" />
            <p className="font-medium text-slate-900 dark:text-white">{t('growthPlan.workContext.error.heading')}</p>
            <p className="text-sm text-slate-500 dark:text-white/50">{t('growthPlan.workContext.error.message')}</p>

            <div className="flex flex-col gap-3 sm:flex-row">
              <button
                type="button"
                onClick={() => refetch()}
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-slate-900 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
              >
                <RefreshCw className="h-4 w-4" />
                {t('growthPlan.workContext.error.retry')}
              </button>

              <button
                type="button"
                onClick={() => setConfiguringRole((current) => !current)}
                aria-expanded={isConfiguringRole}
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-full border border-slate-200 px-5 py-2.5 text-sm font-medium text-slate-700 transition hover:border-slate-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/70 dark:hover:border-white/20"
              >
                <Settings2 className="h-4 w-4" />
                {t('growthPlan.workContext.error.configureRole')}
              </button>
            </div>

            {isConfiguringRole && (
              <div className="mt-2 w-full border-t border-slate-100 pt-6 dark:border-white/10">
                <JobDescriptionQuickCapture />
              </div>
            )}
          </Card>
        )}

        {!isLoading && !isError && workContext && isIncomplete && (
          <Card className="flex flex-col items-center gap-4 p-10 text-center">
            <Info className="h-8 w-8 text-blue-500" />
            <p className="font-medium text-slate-900 dark:text-white">
              {t('growthPlan.workContext.incomplete.heading')}
            </p>
            <p className="text-sm text-slate-500 dark:text-white/50">
              {t('growthPlan.workContext.incomplete.message')}
            </p>
            <button
              type="button"
              className="inline-flex items-center gap-2 rounded-full bg-slate-900 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
            >
              {t('growthPlan.workContext.incomplete.action')}
            </button>
          </Card>
        )}

        {!isLoading && !isError && workContext && !isIncomplete && (
          <>
            <WorkContextSummary workContext={workContext} />
            <InformationSourceNotice workContext={workContext} />
            <PrivacyNotice />

            <div aria-live="polite">
              {isResolved ? (
                <Card className="flex flex-col items-start gap-4 p-6 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex items-start gap-3">
                    <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-emerald-500" />
                    <p className="text-sm font-medium text-slate-700 dark:text-white/80">
                      {workContext.confirmationStatus === 'confirmed'
                        ? t('growthPlan.workContext.confirmedMessage')
                        : t('growthPlan.workContext.correction.submittedMessage')}
                    </p>
                  </div>

                  <button
                    type="button"
                    disabled
                    aria-disabled="true"
                    title="Role Requirements (Step 2) is not implemented yet"
                    className="inline-flex min-h-11 shrink-0 items-center gap-2 rounded-full bg-slate-900 px-5 py-2.5 text-sm font-medium text-white opacity-50 dark:bg-white dark:text-slate-900"
                  >
                    {t('growthPlan.workContext.actions.continueToRoleRequirements')}
                    <ArrowRight className="h-4 w-4" />
                  </button>
                </Card>
              ) : (
                <div className="flex flex-col gap-3 sm:flex-row">
                  <button
                    type="button"
                    onClick={() => confirmMutation.mutate()}
                    disabled={confirmMutation.isPending}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:opacity-60 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
                  >
                    {t('growthPlan.workContext.actions.confirmCorrect')}
                  </button>

                  <button
                    type="button"
                    onClick={() => setCorrectionDialogOpen(true)}
                    className="inline-flex min-h-11 items-center justify-center rounded-full border border-slate-200 px-6 py-2.5 text-sm font-medium text-slate-600 transition hover:border-slate-300 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:text-white/70 dark:hover:border-white/20 dark:hover:text-white"
                  >
                    {t('growthPlan.workContext.actions.requestCorrection')}
                  </button>
                </div>
              )}
            </div>
          </>
        )}
      </div>

      {isCorrectionDialogOpen && (
        <RequestCorrectionDialog
          onSubmit={handleSubmitCorrection}
          onClose={() => setCorrectionDialogOpen(false)}
          isSubmitting={correctionMutation.isPending}
        />
      )}
    </div>
  );
}
