import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { GraduationCap, Layers, BookOpen, Star, CheckCircle2, ArrowRight } from 'lucide-react';
import { Card } from '@/components/ui/Card';

interface ProgressOverviewProps {
  programCount: number;
  /** Standalone (no Program) enrolled capabilities — "Inscribirme" on
   *  /capabilities, tracked separately from Program enrollment but shown
   *  here so overall progress reflects ALL real enrolled learning. */
  capabilityCount: number;
  masteredNodes: number;
  totalNodes: number;
  /** 0-100. Undefined while nothing has been mastered yet across any enrolled program. */
  averageScore?: number;
}

/** Replaces the earlier invented "6-layer Evolution Journey" hero — every
 *  number here comes straight from GrowthPlanService.GetEnrolledProgramsAsync
 *  and CapabilityEnrollmentService.GetEnrolledCapabilitiesAsync (the same
 *  real data already shown on /programs and /my-capabilities), nothing
 *  derived or invented. Shows a real empty state (no stats, just a CTA)
 *  when the person isn't enrolled in anything yet. */
export function ProgressOverview({
  programCount,
  capabilityCount,
  masteredNodes,
  totalNodes,
  averageScore,
}: ProgressOverviewProps) {
  const { t } = useTranslation();
  const overallProgress = totalNodes > 0 ? Math.round((masteredNodes / totalNodes) * 100) : 0;

  if (programCount === 0 && capabilityCount === 0) {
    return (
      <section className="mx-auto max-w-6xl px-6 pt-8">
        <Card className="flex flex-wrap items-center justify-between gap-4 p-6">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-500/10 text-blue-600 dark:text-blue-300">
              <GraduationCap className="h-5 w-5" />
            </div>
            <p className="font-medium text-slate-900 dark:text-white">{t('progressOverview.noPrograms')}</p>
          </div>
          <Link
            to="/capabilities"
            className="flex items-center gap-1.5 rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            {t('progressOverview.viewPrograms')}
            <ArrowRight className="h-4 w-4" />
          </Link>
        </Card>
      </section>
    );
  }

  const stats = [
    ...(programCount > 0
      ? [{ key: 'programs', icon: GraduationCap, value: programCount, label: t('progressOverview.programsEnrolled') }]
      : []),
    ...(capabilityCount > 0
      ? [
          {
            key: 'capabilities',
            icon: BookOpen,
            value: capabilityCount,
            label: t('progressOverview.capabilitiesEnrolled'),
          },
        ]
      : []),
    { key: 'nodes', icon: Layers, value: `${overallProgress}%`, label: t('progressOverview.overallProgress') },
    ...(totalNodes > 0
      ? [
          {
            key: 'nodesMastered',
            icon: CheckCircle2,
            value: `${masteredNodes}/${totalNodes}`,
            label: t('progressOverview.nodesMastered'),
          },
        ]
      : []),
    ...(averageScore !== undefined
      ? [{ key: 'score', icon: Star, value: `${averageScore}/100`, label: t('progressOverview.averageScore') }]
      : []),
  ];

  return (
    <section aria-labelledby="progress-overview-heading" className="mx-auto max-w-6xl px-6 pt-8">
      <h2
        id="progress-overview-heading"
        className="text-sm font-medium uppercase tracking-widest text-slate-400 dark:text-white/40"
      >
        {t('progressOverview.sectionTitle')}
      </h2>

      <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-5">
        {stats.map((stat, i) => {
          const Icon = stat.icon;
          return (
            <motion.div
              key={stat.key}
              initial={{ opacity: 0, y: 12 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, delay: i * 0.06 }}
            >
              <Card className="flex items-center gap-4 p-5">
                <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
                  <Icon className="h-5 w-5" />
                </div>
                <div>
                  <p className="text-xl font-semibold text-slate-900 dark:text-white">{stat.value}</p>
                  <p className="text-xs text-slate-500 dark:text-white/50">{stat.label}</p>
                </div>
              </Card>
            </motion.div>
          );
        })}
      </div>
    </section>
  );
}
