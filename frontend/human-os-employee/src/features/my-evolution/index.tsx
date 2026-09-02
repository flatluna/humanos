import { useMemo } from 'react';
import { TrendingUp, FlaskConical } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { useBrainGraph } from '@/features/brain/useBrainGraph';
import { computeEngramCountHistory } from '@/features/brain/memoryScience';
import { EngramStockChart } from '@/features/growth/EngramStockChart';
import { useI18n } from '@/features/capabilities/i18n/useI18n';

/** "Mi Evolución" — cumulative engrams mastered over time (see
 *  EngramStockChart + computeEngramCountHistory): a simple growth counter
 *  that only ever goes up, colored by average assessment quality — memory
 *  freshness/decay is Mi Cerebro's job, not this page's. */
export function MyEvolutionPage() {
  const { user } = useAuth();
  const { t, language } = useI18n();
  const personId = user?.personId ?? '';

  const { data: rootGroups = [], isLoading } = useBrainGraph(personId);
  const history = useMemo(() => computeEngramCountHistory(rootGroups, 30), [rootGroups]);
  const hasData = (history[history.length - 1]?.cumulativeCount ?? 0) > 0;

  return (
    <div className="mx-auto max-w-3xl px-4 py-8 sm:px-6">
      <div className="mb-6 flex items-center gap-3">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
          <TrendingUp className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">{t.evolutionTitle}</h1>
          <p className="text-sm text-slate-500 dark:text-white/50">{t.evolutionSubtitle}</p>
        </div>
      </div>

      {isLoading ? (
        <Card className="p-8 text-center text-sm text-slate-500 dark:text-white/50">{t.evolutionLoading}</Card>
      ) : !hasData ? (
        <Card className="flex flex-col items-center gap-3 p-12 text-center">
          <TrendingUp className="h-8 w-8 text-slate-400" />
          <p className="max-w-sm text-slate-500 dark:text-white/50">
            {t.evolutionEmpty}
          </p>
        </Card>
      ) : (
        <Card className="p-5 sm:p-6">
          <EngramStockChart
            history={history}
            dailyCountLabel={t.evolutionDailyLabel}
            totalLabel={t.evolutionTotalLabel}
            scoreLabel={t.evolutionScoreLabel}
            locale={language}
          />
        </Card>
      )}

      <Card className="mt-4 flex items-start gap-3 border-fuchsia-500/20 bg-gradient-to-br from-fuchsia-500/5 via-transparent to-indigo-500/5 p-4">
        <FlaskConical className="mt-0.5 h-4 w-4 shrink-0 text-fuchsia-500" />
        <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300">
          {t.evolutionExplainer}
        </p>
      </Card>
    </div>
  );
}
