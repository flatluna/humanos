import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import {
  Layers as LayersIcon,
  Compass,
  Trophy,
  Briefcase,
  Radar,
  Sparkles,
  type LucideIcon,
} from 'lucide-react';
import { EVOLUTION_LAYER_ORDER, type EvolutionLayerId } from '../types';

const ICONS: Record<EvolutionLayerId, LucideIcon> = {
  foundation: LayersIcon,
  exploration: Compass,
  mastery: Trophy,
  professional: Briefcase,
  frontier: Radar,
  creator: Sparkles,
};

interface EvolutionJourneyProps {
  currentLayer: EvolutionLayerId;
}

export function EvolutionJourney({ currentLayer }: EvolutionJourneyProps) {
  const { t } = useTranslation();
  const currentIndex = EVOLUTION_LAYER_ORDER.indexOf(currentLayer);

  return (
    <section aria-labelledby="evolution-journey-heading" className="mx-auto max-w-6xl px-6 pt-10">
      <h2
        id="evolution-journey-heading"
        className="text-sm font-medium uppercase tracking-widest text-slate-400 dark:text-white/40"
      >
        {t('evolution.sectionTitle')}
      </h2>

      <div className="mt-6 overflow-x-auto pb-2">
        <ol className="flex min-w-max items-center gap-2 sm:gap-4">
          {EVOLUTION_LAYER_ORDER.map((layer, i) => {
            const Icon = ICONS[layer];
            const state = i < currentIndex ? 'past' : i === currentIndex ? 'current' : 'future';

            return (
              <li key={layer} className="flex items-center gap-2 sm:gap-4">
                <motion.div
                  initial={{ opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ duration: 0.4, delay: i * 0.05 }}
                  className="flex flex-col items-center gap-2"
                >
                  <div
                    className={
                      state === 'current'
                        ? 'flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-to-br from-blue-500 to-violet-500 text-white shadow-lg shadow-blue-500/30'
                        : state === 'past'
                          ? 'flex h-11 w-11 items-center justify-center rounded-2xl bg-slate-900 text-white dark:bg-white/10'
                          : 'flex h-11 w-11 items-center justify-center rounded-2xl border border-dashed border-slate-300 text-slate-300 dark:border-white/15 dark:text-white/20'
                    }
                  >
                    <Icon className={state === 'current' ? 'h-6 w-6' : 'h-4 w-4'} />
                  </div>

                  <span
                    className={
                      state === 'current'
                        ? 'text-sm font-semibold text-slate-900 dark:text-white'
                        : 'text-xs font-medium text-slate-400 dark:text-white/40'
                    }
                  >
                    {t(`evolution.layers.${layer}`)}
                  </span>

                  {state === 'current' && (
                    <span className="rounded-full bg-blue-50 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-blue-600 dark:bg-blue-500/10 dark:text-blue-300">
                      {t('evolution.youAreHere')}
                    </span>
                  )}
                </motion.div>

                {i < EVOLUTION_LAYER_ORDER.length - 1 && (
                  <div
                    className={
                      i < currentIndex
                        ? 'h-px w-8 bg-slate-900 sm:w-12 dark:bg-white/30'
                        : 'h-px w-8 bg-slate-200 sm:w-12 dark:bg-white/10'
                    }
                  />
                )}
              </li>
            );
          })}
        </ol>
      </div>
    </section>
  );
}
