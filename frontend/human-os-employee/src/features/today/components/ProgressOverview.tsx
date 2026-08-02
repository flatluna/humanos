import { motion } from 'framer-motion';
import { GraduationCap, Layers, Star, CheckCircle2, ArrowRight } from 'lucide-react';
import { Link } from 'react-router';
import { Card } from '@/components/ui/Card';

interface ProgressOverviewProps {
  /** Enrolled/assigned capabilities (courses your company gave you). */
  capabilityCount: number;
  masteredNodes: number;
  totalNodes: number;
  /** 0-100. Undefined while nothing has been mastered yet. */
  averageScore?: number;
}

/** Real progress numbers only — comes straight from
 *  CapabilityEnrollmentService.GetEnrolledCapabilitiesAsync (the same data
 *  shown on /capabilities), nothing invented. Shows a real empty state
 *  (no stats, just a CTA) when the employee isn't enrolled in anything yet. */
export function ProgressOverview({ capabilityCount, masteredNodes, totalNodes, averageScore }: ProgressOverviewProps) {
  const overallProgress = totalNodes > 0 ? Math.round((masteredNodes / totalNodes) * 100) : 0;

  if (capabilityCount === 0) {
    return (
      <section className="mx-auto max-w-6xl px-6 pt-8">
        <Card className="flex flex-wrap items-center justify-between gap-4 p-6">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-500/10 text-brand-600 dark:text-brand-300">
              <GraduationCap className="h-5 w-5" />
            </div>
            <p className="font-medium text-slate-900 dark:text-white">Aún no tienes cursos asignados.</p>
          </div>
          <Link
            to="/capabilities"
            className="flex items-center gap-1.5 rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            Ver mis cursos
            <ArrowRight className="h-4 w-4" />
          </Link>
        </Card>
      </section>
    );
  }

  const stats = [
    { key: 'capabilities', icon: GraduationCap, value: capabilityCount, label: 'Cursos asignados' },
    { key: 'progress', icon: Layers, value: `${overallProgress}%`, label: 'Progreso general' },
    ...(totalNodes > 0
      ? [{ key: 'nodesMastered', icon: CheckCircle2, value: `${masteredNodes}/${totalNodes}`, label: 'Temas completados' }]
      : []),
    ...(averageScore !== undefined
      ? [{ key: 'score', icon: Star, value: `${averageScore}/100`, label: 'Puntaje promedio' }]
      : []),
  ];

  return (
    <section aria-labelledby="progress-overview-heading" className="mx-auto max-w-6xl px-6 pt-8">
      <h2
        id="progress-overview-heading"
        className="text-sm font-medium uppercase tracking-widest text-slate-400 dark:text-white/40"
      >
        Tu progreso
      </h2>

      <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
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
                <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-brand-500/10 to-accent-500/10 text-brand-600 dark:text-brand-300">
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
