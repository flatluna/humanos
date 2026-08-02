import { motion } from 'framer-motion';
import { Link } from 'react-router';
import { PlayCircle, Compass, ArrowRight } from 'lucide-react';
import { Card } from '@/components/ui/Card';

export interface ContinueLearningItem {
  capabilityId: string;
  capabilityGraphNodeId: string;
  capabilityName: string;
  subjectName: string;
}

interface ContinueLearningSectionProps {
  items: ContinueLearningItem[];
  loading: boolean;
}

/** Every item here comes from a real in-progress LearningSession
 *  (RuntimeGetActiveSessionFunction / SessionRecoveryEngine) — an honest
 *  "nothing in progress" empty state is shown when there's truly nothing
 *  to resume, rather than fabricating recommended actions. */
export function ContinueLearningSection({ items, loading }: ContinueLearningSectionProps) {
  if (loading) return null;

  return (
    <section aria-labelledby="continue-learning-heading" className="mx-auto max-w-6xl px-6 pt-14 pb-14">
      <h2 id="continue-learning-heading" className="text-2xl font-semibold text-slate-900 dark:text-white">
        Continuar aprendiendo
      </h2>

      {items.length === 0 ? (
        <Card className="mt-6 flex flex-wrap items-center justify-between gap-4 p-6">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-500/10 text-brand-600 dark:text-brand-300">
              <Compass className="h-5 w-5" />
            </div>
            <div>
              <p className="font-medium text-slate-900 dark:text-white">No tienes nada en progreso ahora mismo.</p>
              <p className="text-sm text-slate-500 dark:text-white/50">Elige un curso para empezar.</p>
            </div>
          </div>
          <Link
            to="/capabilities"
            className="flex items-center gap-1.5 rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            Ver mis cursos
            <ArrowRight className="h-4 w-4" />
          </Link>
        </Card>
      ) : (
        <div className="mt-6 grid gap-5 sm:grid-cols-2">
          {items.map((item, i) => (
            <motion.div
              key={item.capabilityGraphNodeId}
              initial={{ opacity: 0, y: 16 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, amount: 0.4 }}
              transition={{ duration: 0.4, delay: i * 0.05 }}
            >
              <Link to={`/capabilities/${item.capabilityId}/nodes/${item.capabilityGraphNodeId}?from=today`}>
                <Card className="flex items-start gap-4 p-6 transition hover:border-brand-300 dark:hover:border-brand-400/40">
                  <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-brand-500/10 to-accent-500/10 text-brand-600 dark:text-brand-300">
                    <PlayCircle className="h-5 w-5" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
                      {item.subjectName}
                    </p>
                    <p className="mt-1 font-medium text-slate-900 dark:text-white">{item.capabilityName}</p>
                    <p className="mt-1 text-sm text-brand-600 dark:text-brand-300">Continuar</p>
                  </div>
                </Card>
              </Link>
            </motion.div>
          ))}
        </div>
      )}
    </section>
  );
}
