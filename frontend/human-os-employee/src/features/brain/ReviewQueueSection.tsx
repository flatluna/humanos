import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  RotateCcw,
  Sparkles,
  Sprout,
  ShieldAlert,
  BookOpen,
  CheckCircle2,
  ArrowRight,
} from 'lucide-react';
import { getReviewQueue, type ReviewQueueItemDto } from '../gamification/api/gamificationApi';
import { getMemoryColor, getMemoryTextColor } from './memoryScience';
import { Card } from '@/components/ui/Card';

interface ReviewQueueSectionProps {
  personId: string;
  onSelectNeuron?: (item: ReviewQueueItemDto) => void;
}

export const ReviewQueueSection: React.FC<ReviewQueueSectionProps> = ({ personId, onSelectNeuron }) => {
  const { t } = useTranslation();

  const { data: queue, isLoading, error } = useQuery({
    queryKey: ['review-queue', personId],
    queryFn: () => getReviewQueue(),
    enabled: !!personId,
    refetchInterval: 60_000,
  });

  if (isLoading) {
    return (
      <div className="mt-6 rounded-2xl border border-slate-200/80 bg-slate-50/50 p-5 dark:border-white/10 dark:bg-white/[0.02] animate-pulse">
        <div className="h-4 w-48 rounded bg-slate-200 dark:bg-white/10" />
        <div className="mt-3 h-12 rounded-xl bg-slate-200/60 dark:bg-white/5" />
      </div>
    );
  }

  if (error || !queue) {
    return null;
  }

  const isPhase1 = queue.phase === 'Phase1';
  const hasItems = queue.items && queue.items.length > 0;

  // 1. EMPTY QUEUE STATE (CELEBRATORY & MOTIVATING)
  if (!hasItems) {
    return (
      <Card className="mt-6 border border-emerald-500/20 bg-gradient-to-r from-emerald-500/10 via-brand-500/5 to-transparent p-4 sm:p-5 shadow-xs">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div className="flex items-start gap-3.5">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-emerald-500/20 text-emerald-600 dark:text-emerald-400 shadow-inner">
              {isPhase1 ? <Sprout className="h-5 w-5" /> : <CheckCircle2 className="h-5 w-5" />}
            </div>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  {isPhase1 ? t('review.phase1EmptyTitle') : t('review.phase2EmptyTitle')}
                </h3>
                <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/15 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-emerald-700 dark:text-emerald-300">
                  {isPhase1 ? t('review.learningPhaseBadge') : t('review.maintenancePhaseBadge')}
                </span>
              </div>
              <p className="mt-1 text-xs leading-relaxed text-slate-600 dark:text-slate-300 max-w-2xl">
                {isPhase1 ? t('review.phase1EmptyDesc') : t('review.phase2EmptyDesc')}
              </p>
            </div>
          </div>
        </div>
      </Card>
    );
  }

  // 2. ACTIVE QUEUE WITH INTERLEAVED ITEMS
  return (
    <Card className="mt-6 border border-amber-500/30 bg-gradient-to-br from-amber-500/10 via-amber-500/[0.02] to-transparent p-5 shadow-sm">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 border-b border-amber-500/20 pb-3.5">
        <div className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-amber-500/20 text-amber-600 dark:text-amber-400">
            <RotateCcw className="h-4 w-4 animate-spin-slow" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h3 className="text-sm font-black uppercase tracking-wider text-slate-900 dark:text-white">
                {t('review.titleWithCount', { count: queue.totalPending })}
              </h3>
              <span className="rounded-full bg-amber-500/20 px-2 py-0.5 text-[10px] font-bold text-amber-800 dark:text-amber-300">
                {isPhase1 ? t('review.learningPhaseBadge') : t('review.maintenancePhaseBadge')}
              </span>
            </div>
            <p className="mt-0.5 text-[11px] text-amber-700/90 dark:text-amber-300/80 font-medium flex items-center gap-1">
              <Sparkles className="h-3 w-3" />
              {t('review.interleavingHint')}
            </p>
          </div>
        </div>
      </div>

      {/* Interleaved Items Grid */}
      <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {queue.items.map((item) => {
          const retrievability = item.recallPercent / 100.0;
          const memColor = getMemoryColor(retrievability);
          const memTextColor = getMemoryTextColor(retrievability);
          const isCritical = item.tier.toLowerCase() === 'critical';

          return (
            <div
              key={`${item.courseId}_${item.engramId}`}
              className="group relative flex flex-col justify-between rounded-xl border border-amber-500/20 bg-white/90 p-3.5 shadow-xs transition hover:border-amber-500/50 hover:shadow-md dark:border-white/10 dark:bg-[#121626]/90"
            >
              <div>
                {/* Course badge & Tier */}
                <div className="flex items-center justify-between gap-1.5 text-[10px] font-bold">
                  <span className="flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-slate-700 dark:bg-white/10 dark:text-slate-300 line-clamp-1 max-w-[140px]">
                    <BookOpen className="h-2.5 w-2.5 shrink-0" />
                    {item.courseName}
                  </span>

                  <span
                    className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 uppercase tracking-wide ${
                      isCritical
                        ? 'bg-red-500/20 text-red-700 dark:text-red-300'
                        : 'bg-amber-500/20 text-amber-700 dark:text-amber-300'
                    }`}
                  >
                    {isCritical && <ShieldAlert className="h-2.5 w-2.5" />}
                    {isCritical ? t('review.criticalTier') : t('review.importantTier')}
                  </span>
                </div>

                {/* Engram Title */}
                <div className="mt-2.5">
                  <h4 className="text-sm font-bold text-slate-900 dark:text-white line-clamp-2">
                    {item.title}
                  </h4>
                </div>
              </div>

              {/* Bottom Recall & Action */}
              <div className="mt-3.5 flex items-center justify-between border-t border-slate-100 pt-2.5 dark:border-white/5">
                <div className="flex items-center gap-2">
                  <div
                    className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-[10px] font-bold shadow-xs"
                    style={{ background: memColor, color: memTextColor }}
                  >
                    {item.recallPercent}%
                  </div>
                  <span className="text-[11px] text-slate-500 dark:text-slate-400 font-medium">
                    {t('review.retention')}
                  </span>
                </div>

                {onSelectNeuron ? (
                  <button
                    type="button"
                    onClick={() => onSelectNeuron(item)}
                    className="inline-flex items-center gap-1 rounded-lg bg-amber-500/15 px-2.5 py-1 text-xs font-bold text-amber-800 hover:bg-amber-500/25 dark:text-amber-300 transition cursor-pointer"
                  >
                    <span>{t('review.reviewNow')}</span>
                    <ArrowRight className="h-3 w-3" />
                  </button>
                ) : item.isStructuredLearning ? (
                  <Link
                    to={`/structured-learning/${item.courseId}/sections/${item.engramId}`}
                    className="inline-flex items-center gap-1 rounded-lg bg-amber-500/15 px-2.5 py-1 text-xs font-bold text-amber-800 hover:bg-amber-500/25 dark:text-amber-300 transition"
                  >
                    <span>{t('review.reviewNow')}</span>
                    <ArrowRight className="h-3 w-3" />
                  </Link>
                ) : (
                  <Link
                    to={`/capabilities/${item.courseId}/nodes/${item.engramId}`}
                    className="inline-flex items-center gap-1 rounded-lg bg-amber-500/15 px-2.5 py-1 text-xs font-bold text-amber-800 hover:bg-amber-500/25 dark:text-amber-300 transition"
                  >
                    <span>{t('review.reviewNow')}</span>
                    <ArrowRight className="h-3 w-3" />
                  </Link>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </Card>
  );
};
