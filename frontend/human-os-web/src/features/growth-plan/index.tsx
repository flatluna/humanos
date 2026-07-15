import { Outlet, Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import {
  Briefcase,
  FileText,
  Compass,
  Building2,
  Gauge,
  Sparkles,
  ClipboardCheck,
  Rocket,
  TrendingUp,
  Lock,
  CheckCircle2,
  ArrowRight,
  type LucideIcon,
} from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { useWorkContextStore } from '@/features/enterprise-context/store/useWorkContextStore';
import { useRoleExperienceStore } from '@/features/role-requirements/store/useRoleExperienceStore';

type StepStatus = 'completed' | 'inProgress' | 'notStarted' | 'locked';

type StepLabelKey =
  | 'currentRole'
  | 'workAndExperience'
  | 'yourFuture'
  | 'growthContext'
  | 'startingPoint'
  | 'agentProposedPlan'
  | 'humanReview'
  | 'planActivation'
  | 'continuousEvolution';

interface SequenceStep {
  labelKey: StepLabelKey;
  icon: LucideIcon;
  to: string | null;
  status: StepStatus;
}

const STATUS_TONE: Record<StepStatus, 'neutral' | 'accent'> = {
  completed: 'accent',
  inProgress: 'accent',
  notStarted: 'neutral',
  locked: 'neutral',
};

/** The single entry point into the Growth Plan experience — the full
 *  9-step wizard index, always shown in its entirety (nothing appears
 *  or disappears as steps get built; unbuilt steps just show as
 *  locked). Each step lists the concrete inputs/outputs it covers so
 *  the whole journey toward a Personalized Human Development Plan is
 *  visible from the very first screen, not just the step titles.
 */
export function GrowthPlanPage() {
  const { t } = useTranslation();

  const workContextConfirmation = useWorkContextStore((state) => state.confirmationStatus);
  const workContextCorrection = useWorkContextStore((state) => state.pendingCorrectionStatus);

  const jobDescriptionFeedback = useRoleExperienceStore((state) => state.jobDescriptionFeedback);
  const employeeProvidedJobDescription = useRoleExperienceStore((state) => state.employeeProvidedJobDescription);
  const resumeUploadStatus = useRoleExperienceStore((state) => state.resumeUploadStatus);

  const currentRoleStatus: StepStatus =
    workContextConfirmation === 'confirmed' || workContextCorrection === 'submitted' ? 'completed' : 'notStarted';

  const workAndExperienceStarted =
    Boolean(jobDescriptionFeedback) || Boolean(employeeProvidedJobDescription) || resumeUploadStatus !== 'idle';
  const workAndExperienceStatus: StepStatus = workAndExperienceStarted ? 'inProgress' : 'notStarted';

  const steps: SequenceStep[] = [
    { labelKey: 'currentRole', icon: Briefcase, to: '/growth-plan/currentrole', status: currentRoleStatus },
    {
      labelKey: 'workAndExperience',
      icon: FileText,
      to: '/growth-plan/role-experience',
      status: currentRoleStatus === 'completed' ? workAndExperienceStatus : 'locked',
    },
    { labelKey: 'yourFuture', icon: Compass, to: null, status: 'locked' },
    { labelKey: 'growthContext', icon: Building2, to: null, status: 'locked' },
    { labelKey: 'startingPoint', icon: Gauge, to: null, status: 'locked' },
    { labelKey: 'agentProposedPlan', icon: Sparkles, to: null, status: 'locked' },
    { labelKey: 'humanReview', icon: ClipboardCheck, to: null, status: 'locked' },
    { labelKey: 'planActivation', icon: Rocket, to: null, status: 'locked' },
    { labelKey: 'continuousEvolution', icon: TrendingUp, to: null, status: 'locked' },
  ];

  const flow = t('growthPlan.overview.result.flow', { returnObjects: true }) as string[];

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="text-3xl font-semibold tracking-tight text-slate-900 dark:text-white">
        {t('growthPlan.overview.sectionTitle')}
      </h1>
      <p className="mt-3 max-w-xl text-slate-500 dark:text-white/50">{t('growthPlan.overview.description')}</p>

      <ol className="mt-8 space-y-3">
        {steps.map((step, index) => {
          const Icon = step.icon;
          const isLocked = step.status === 'locked';
          const statusLabelKey =
            step.status === 'completed'
              ? 'statusCompleted'
              : step.status === 'inProgress'
                ? 'statusInProgress'
                : step.status === 'notStarted'
                  ? 'statusNotStarted'
                  : 'statusLocked';
          const items = t(`growthPlan.overview.steps.${step.labelKey}.items`, { returnObjects: true }) as string[];
          const description =
            step.labelKey === 'currentRole' ? t('growthPlan.overview.steps.currentRole.description') : null;

          const content = (
            <Card
              className={
                isLocked
                  ? 'p-5 opacity-60'
                  : 'p-5 transition hover:border-blue-300 dark:hover:border-blue-400/40'
              }
            >
              <div className="flex items-center gap-4">
                <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
                  {step.status === 'completed' ? (
                    <CheckCircle2 className="h-5 w-5" />
                  ) : isLocked ? (
                    <Lock className="h-4 w-4" />
                  ) : (
                    <Icon className="h-5 w-5" />
                  )}
                </div>

                <div className="min-w-0 flex-1">
                  <p className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
                    {t('growthPlan.overview.stepOf', { current: index + 1, total: steps.length })}
                  </p>
                  <p className="font-medium text-slate-900 dark:text-white">
                    {t(`growthPlan.overview.steps.${step.labelKey}.title`)}
                  </p>
                  {description && (
                    <p className="mt-1 text-sm text-slate-500 dark:text-white/50">{description}</p>
                  )}
                </div>

                <Badge tone={STATUS_TONE[step.status]} className="shrink-0">
                  {t(`growthPlan.overview.${statusLabelKey}`)}
                </Badge>
              </div>

              <ul className="mt-4 grid grid-cols-1 gap-x-4 gap-y-1 pl-15 sm:grid-cols-2">
                {items.map((item) => (
                  <li key={item} className="flex gap-2 text-sm text-slate-500 dark:text-white/50">
                    <span aria-hidden="true" className="text-slate-300 dark:text-white/20">
                      •
                    </span>
                    {item}
                  </li>
                ))}
              </ul>

              {isLocked && (
                <p className="mt-3 pl-15 text-xs text-slate-400 dark:text-white/40">
                  {t('growthPlan.overview.lockedHint')}
                </p>
              )}
            </Card>
          );

          return (
            <li key={step.labelKey}>
              {step.to && !isLocked ? (
                <Link
                  to={step.to}
                  className="block rounded-3xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500"
                >
                  {content}
                </Link>
              ) : (
                content
              )}
            </li>
          );
        })}
      </ol>

      <Card className="mt-8 p-6 sm:p-8">
        <p className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
          {t('growthPlan.overview.result.heading')}
        </p>
        <p className="mt-1 text-lg font-semibold text-slate-900 dark:text-white">
          {t('growthPlan.overview.result.planName')}
        </p>
        <div className="mt-4 flex flex-wrap items-center gap-x-1.5 gap-y-2">
          {flow.map((stage, index) => (
            <span key={stage} className="flex items-center gap-1.5">
              <Badge tone="accent">{stage}</Badge>
              {index < flow.length - 1 && (
                <ArrowRight className="h-3.5 w-3.5 shrink-0 text-slate-300 dark:text-white/20" />
              )}
            </span>
          ))}
        </div>
      </Card>
    </div>
  );
}

/** Pathless layout for the /growth-plan/* route tree — renders only an
 *  Outlet since AuthenticatedAppLayout already provides the header,
 *  sidebar, and mobile navigation chrome.
 */
export function GrowthPlanLayout() {
  return <Outlet />;
}
