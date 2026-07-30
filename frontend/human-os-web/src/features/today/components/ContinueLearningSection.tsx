import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { PlayCircle, Compass, ArrowRight } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { useLocalizedText } from '@/localization/useLocalizedText';
import type { LocalizedText } from '../types';

export interface ContinueLearningItem {
  capabilityId: string;
  capabilityGraphNodeId: string;
  capabilityName: LocalizedText;
  programName: LocalizedText;
  /** Only set when this capability was resumed within a real enrolled
   *  Program — must be forwarded to the node URL so the resumed session
   *  matches the one actually in progress inside that program. */
  programId?: string;
}

interface ContinueLearningSectionProps {
  items: ContinueLearningItem[];
  loading: boolean;
}

/** Replaces the earlier invented "Today's Growth Actions" mock cards.
 *  Every item here comes from a real in-progress LearningSession
 *  (RuntimeGetActiveSessionFunction / SessionRecoveryEngine) — an honest
 *  "nothing in progress" empty state is shown when there's truly nothing
 *  to resume, rather than fabricating recommended actions. */
export function ContinueLearningSection({ items, loading }: ContinueLearningSectionProps) {
  const { t } = useTranslation();
  const localize = useLocalizedText();

  if (loading) {
    return null;
  }

  return (
    <section aria-labelledby="continue-learning-heading" className="mx-auto max-w-6xl px-6 pt-14">
      <h2 id="continue-learning-heading" className="text-2xl font-semibold text-slate-900 dark:text-white">
        {t('continueLearning.sectionTitle')}
      </h2>

      {items.length === 0 ? (
        <Card className="mt-6 flex flex-wrap items-center justify-between gap-4 p-6">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-500/10 text-blue-600 dark:text-blue-300">
              <Compass className="h-5 w-5" />
            </div>
            <div>
              <p className="font-medium text-slate-900 dark:text-white">{t('continueLearning.emptyTitle')}</p>
              <p className="text-sm text-slate-500 dark:text-white/50">{t('continueLearning.emptyHint')}</p>
            </div>
          </div>
          <Link
            to="/capabilities"
            className="flex items-center gap-1.5 rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            {t('continueLearning.emptyCta')}
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
              <Link
                to={`/capabilities/${item.capabilityId}/nodes/${item.capabilityGraphNodeId}?from=today${item.programId ? `&programId=${item.programId}` : ''}`}
              >
                <Card className="flex items-start gap-4 p-6 transition hover:border-blue-300 dark:hover:border-blue-400/40">
                  <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
                    <PlayCircle className="h-5 w-5" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
                      {localize(item.programName)}
                    </p>
                    <p className="mt-1 font-medium text-slate-900 dark:text-white">{localize(item.capabilityName)}</p>
                    <p className="mt-1 text-sm text-blue-600 dark:text-blue-300">{t('continueLearning.resume')}</p>
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
