import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import { Trophy, Star } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { ProgressBar } from '@/components/ui/ProgressBar';
import { useLocalizedText } from '@/localization/useLocalizedText';
import type { EvolutionLayerId, LocalizedText } from '../types';

interface CurrentLayerHeroProps {
  currentLayer: EvolutionLayerId;
  nextLayer: EvolutionLayerId;
  progress: number;
  futureSelf: LocalizedText;
}

export function CurrentLayerHero({ currentLayer, nextLayer, progress, futureSelf }: CurrentLayerHeroProps) {
  const { t } = useTranslation();
  const localize = useLocalizedText();

  return (
    <section className="mx-auto max-w-6xl px-6 pt-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.6 }}>
        <Card className="grid gap-8 p-8 sm:p-10 lg:grid-cols-[2fr_1fr] lg:items-center">
          <div>
            <div className="flex items-center gap-2 text-blue-600 dark:text-blue-300">
              <Trophy className="h-4 w-4" />
              <span className="text-xs font-semibold uppercase tracking-widest">{t('currentLayer.label')}</span>
            </div>

            <h1 className="mt-3 text-4xl font-semibold tracking-tight text-slate-900 sm:text-5xl dark:text-white">
              {t(`evolution.layers.${currentLayer}`)}
            </h1>

            <p className="mt-4 max-w-md text-slate-500 dark:text-white/50">
              {t('currentLayer.towardNext', { value: progress, next: t(`evolution.layers.${nextLayer}`) })}
            </p>

            <div className="mt-6 max-w-md">
              <ProgressBar value={progress} />
            </div>
          </div>

          <div className="rounded-2xl border border-blue-100 bg-gradient-to-br from-blue-50 to-violet-50 p-6 text-center dark:border-white/10 dark:from-blue-500/10 dark:to-violet-500/10">
            <div className="mx-auto flex h-11 w-11 items-center justify-center rounded-xl bg-white shadow-sm dark:bg-white/10">
              <Star className="h-5 w-5 text-violet-500 dark:text-violet-300" />
            </div>
            <p className="mt-3 text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
              {t('currentLayer.futureSelf')}
            </p>
            <p className="mt-1 text-2xl font-semibold text-slate-900 dark:text-white">{localize(futureSelf)}</p>
          </div>
        </Card>
      </motion.div>
    </section>
  );
}
