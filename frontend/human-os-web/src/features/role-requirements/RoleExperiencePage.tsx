import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { ArrowRight, CheckCircle2 } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { SetupProgress } from '@/features/enterprise-context/components/SetupProgress';
import { JobDescriptionSummary } from './components/JobDescriptionSummary';
import { MissingJobDescription } from './components/MissingJobDescription';
import { ProfessionalProfileOptions } from './components/ProfessionalProfileOptions';
import { ResumeUpload } from './components/ResumeUpload';
import { useJobDescription, useSubmitJobDescriptionFeedback, useResumeUpload } from './hooks/useRoleExperience';
import { useRoleExperienceStore } from './store/useRoleExperienceStore';

type ProfileMode = 'options' | 'upload' | 'skipped';

function JobDescriptionSkeleton() {
  return (
    <Card className="animate-pulse p-6 sm:p-8" aria-hidden="true">
      <div className="h-3 w-24 rounded bg-slate-200 dark:bg-white/10" />
      <div className="mt-3 h-6 w-48 rounded bg-slate-200 dark:bg-white/10" />
      <div className="mt-6 h-4 w-full rounded bg-slate-200 dark:bg-white/10" />
      <div className="mt-2 h-4 w-2/3 rounded bg-slate-200 dark:bg-white/10" />
    </Card>
  );
}

export function RoleExperiencePage() {
  const { t } = useTranslation();
  const [profileMode, setProfileMode] = useState<ProfileMode>('options');

  const { data: jobDescription, isLoading: isJobDescriptionLoading } = useJobDescription();
  const submitFeedback = useSubmitJobDescriptionFeedback();
  const resumeUpload = useResumeUpload();

  const employeeProvidedJobDescription = useRoleExperienceStore((state) => state.employeeProvidedJobDescription);
  const jobDescriptionFeedback = useRoleExperienceStore((state) => state.jobDescriptionFeedback);
  const setEmployeeProvidedJobDescription = useRoleExperienceStore(
    (state) => state.setEmployeeProvidedJobDescription,
  );
  const resumeUploadStatus = useRoleExperienceStore((state) => state.resumeUploadStatus);
  const resumeDocument = useRoleExperienceStore((state) => state.resumeDocument);
  const declaredExperience = useRoleExperienceStore((state) => state.declaredExperience);
  const resumeErrorMessage = useRoleExperienceStore((state) => state.resumeErrorMessage);
  const resetResume = useRoleExperienceStore((state) => state.resetResume);

  const isJobDescriptionResolved = Boolean(jobDescriptionFeedback) || Boolean(employeeProvidedJobDescription);

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <SetupProgress currentStep={2} totalSteps={9} stepLabel={t('growthPlan.roleExperience.pageTitle')} />

      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-slate-900 dark:text-white">
        {t('growthPlan.roleExperience.headline')}
      </h1>
      <p className="mt-3 max-w-xl text-slate-500 dark:text-white/50">{t('growthPlan.roleExperience.description')}</p>

      <div className="mt-8 space-y-5">
        {isJobDescriptionLoading && <JobDescriptionSkeleton />}

        {!isJobDescriptionLoading && jobDescription && (
          <JobDescriptionSummary
            jobDescription={jobDescription}
            onReflectsMyRole={() => submitFeedback.mutate({ type: 'reflects' })}
            onWorkIsDifferent={() => submitFeedback.mutate({ type: 'different' })}
          />
        )}

        {!isJobDescriptionLoading && !jobDescription && (
          <MissingJobDescription onSaveDraft={setEmployeeProvidedJobDescription} />
        )}

        {isJobDescriptionResolved && (
          <>
            {profileMode === 'options' && (
              <ProfessionalProfileOptions
                onSelectUpload={() => setProfileMode('upload')}
                onSkip={() => setProfileMode('skipped')}
              />
            )}

            {profileMode === 'upload' && (
              <ResumeUpload
                status={resumeUploadStatus}
                resumeDocument={resumeDocument}
                declaredExperience={declaredExperience}
                errorMessage={resumeErrorMessage}
                onSelectFile={(file) => resumeUpload.mutate(file)}
                onRetry={() => resumeUpload.reset()}
                onRemove={() => {
                  resetResume();
                  setProfileMode('options');
                }}
              />
            )}

            {profileMode === 'skipped' && (
              <Card className="flex items-center gap-3 p-6">
                <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-500" />
                <p className="text-sm text-slate-600 dark:text-white/70">
                  {t('growthPlan.roleExperience.professionalProfile.skipForNow')}
                </p>
              </Card>
            )}

            {(profileMode === 'skipped' || resumeUploadStatus === 'extracted') && (
              <Card className="flex flex-col gap-4 p-6 sm:flex-row sm:items-center sm:justify-between sm:p-8">
                <p className="text-sm text-slate-600 dark:text-white/70">
                  {t('growthPlan.roleExperience.alignmentGuide.title')}
                </p>
                <Link
                  to="/growth-plan/role-experience/alignment-guide"
                  className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
                >
                  {t('growthPlan.roleExperience.alignmentGuide.summary.startReview')}
                  <ArrowRight className="h-4 w-4" />
                </Link>
              </Card>
            )}
          </>
        )}
      </div>
    </div>
  );
}
