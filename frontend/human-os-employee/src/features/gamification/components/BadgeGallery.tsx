import React, { useEffect, useState, useMemo, useCallback } from 'react';
import {
  Trophy,
  Lock,
  CheckCircle2,
  Award,
  Sparkles,
  Flame,
  Zap,
  Target,
  RefreshCw,
  X,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { getBadgeProgress, type BadgeProgressDto } from '../api/gamificationApi';
import { useGamificationStore } from '../store/useGamificationStore';
import { useI18n } from '@/features/capabilities/i18n/useI18n';

interface BadgeGalleryProps {
  /** If provided, renders within a modal frame with a close button */
  onClose?: () => void;
  className?: string;
}

interface CategoryMeta {
  key: string;
  nameKey: string;
  iconEmoji: string;
  descriptionKey: string;
}

const CATEGORY_ORDER: CategoryMeta[] = [
  {
    key: 'Effort',
    nameKey: 'badges.categories.Effort',
    iconEmoji: '🌱',
    descriptionKey: 'badges.categoryDescriptions.Effort',
  },
  {
    key: 'Progress',
    nameKey: 'badges.categories.Progress',
    iconEmoji: '📈',
    descriptionKey: 'badges.categoryDescriptions.Progress',
  },
  {
    key: 'Skills',
    nameKey: 'badges.categories.Skills',
    iconEmoji: '🏆',
    descriptionKey: 'badges.categoryDescriptions.Skills',
  },
  {
    key: 'Quality',
    nameKey: 'badges.categories.Quality',
    iconEmoji: '⭐',
    descriptionKey: 'badges.categoryDescriptions.Quality',
  },
  {
    key: 'Challenge',
    nameKey: 'badges.categories.Challenge',
    iconEmoji: '🛡️',
    descriptionKey: 'badges.categoryDescriptions.Challenge',
  },
  {
    key: 'Habit',
    nameKey: 'badges.categories.Habit',
    iconEmoji: '🔥',
    descriptionKey: 'badges.categoryDescriptions.Habit',
  },
  {
    key: 'Luck',
    nameKey: 'badges.categories.Luck',
    iconEmoji: '🎲',
    descriptionKey: 'badges.categoryDescriptions.Luck',
  },
];

export const BadgeGallery: React.FC<BadgeGalleryProps> = ({ onClose, className = '' }) => {
  const { language } = useI18n();
  const { t } = useTranslation();
  const isEn = language === 'en';
  const { fetchProfile } = useGamificationStore();

  const [badges, setBadges] = useState<BadgeProgressDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadBadges = useCallback(async () => {
    setLoading(true);
    setError(null);
    void fetchProfile();
    try {
      const data = await getBadgeProgress();
      setBadges(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, [fetchProfile]);

  useEffect(() => {
    void loadBadges();
  }, [loadBadges]);

  const earnedBadges = useMemo(() => badges.filter((b) => b.isEarned === true), [badges]);
  const unearnedBadges = useMemo(() => badges.filter((b) => !b.isEarned), [badges]);

  // Top 3-5 unearned badges with progressPercent > 0 and < 100 (strictly excluding already earned badges)
  const closeToUnlockingBadges = useMemo(() => {
    return unearnedBadges
      .filter((b) => !b.isEarned && b.progressPercent > 0 && b.progressPercent < 100)
      .slice(0, 4);
  }, [unearnedBadges]);

  // Group all badges by category adhering to the canonical CATEGORY_ORDER
  const groupedCategories = useMemo(() => {
    const map = new Map<string, BadgeProgressDto[]>();
    for (const badge of badges) {
      const cat = badge.category || 'Other';
      if (!map.has(cat)) {
        map.set(cat, []);
      }
      map.get(cat)!.push(badge);
    }

    const result: Array<{ meta: CategoryMeta; badges: BadgeProgressDto[] }> = [];

    for (const catMeta of CATEGORY_ORDER) {
      const list = map.get(catMeta.key) || [];
      if (list.length > 0) {
        result.push({ meta: catMeta, badges: list });
        map.delete(catMeta.key);
      }
    }

    // Any remaining dynamic category
    for (const [key, list] of map.entries()) {
      result.push({
        meta: {
          key,
          nameKey: `badges.categories.${key}`,
          iconEmoji: '🎖️',
          descriptionKey: `badges.categoryDescriptions.${key}`,
        },
        badges: list,
      });
    }

    return result;
  }, [badges]);

  const formatDate = (dateStr?: string | null) => {
    if (!dateStr) return '';
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString(isEn ? 'en-US' : 'es-MX', {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
      });
    } catch {
      return dateStr;
    }
  };

  const formatProgressLabel = (badge: BadgeProgressDto) => {
    const rule = badge.ruleType?.toLowerCase().trim();
    if (rule === 'course_progress_pct') {
      return `${badge.currentValue}% / ${badge.targetValue}%`;
    }
    if (rule === 'streak_days') {
      return `${badge.currentValue} / ${badge.targetValue} ${t('badges.days')}`;
    }
    if (rule === 'automatic_engrams') {
      return `${badge.currentValue} / ${badge.targetValue}`;
    }
    return `${badge.currentValue} / ${badge.targetValue}`;
  };

  return (
    <div className={`space-y-8 ${className}`}>
      {/* HEADER */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between border-b border-slate-100 pb-5 dark:border-white/10">
        <div className="flex items-center gap-3">
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-amber-400 to-amber-600 text-white shadow-md shadow-amber-500/20">
            <Trophy className="h-6 w-6" />
          </div>
          <div>
            <div className="flex items-center gap-2.5">
              <h2 className="text-lg font-black tracking-tight text-slate-900 dark:text-white sm:text-xl">
                🏆 {t('badges.myAchievements')}
              </h2>
              <span className="inline-flex items-center gap-1 rounded-full bg-amber-500/10 px-2.5 py-0.5 text-xs font-bold text-amber-700 dark:bg-amber-400/20 dark:text-amber-300">
                <Sparkles className="h-3 w-3" />
                {t('badges.progressCounter', { earned: earnedBadges.length, total: badges.length })}
              </span>
            </div>
            <p className="mt-0.5 text-xs text-slate-500 dark:text-white/60">
              {t('badges.subtitle')}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 self-end sm:self-auto">
          {onClose && (
            <button
              type="button"
              onClick={onClose}
              aria-label={t('common.close')}
              className="rounded-full p-2 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-white/10 dark:hover:text-white"
            >
              <X className="h-5 w-5" />
            </button>
          )}
        </div>
      </div>

      {/* SKELETON LOADING STATE */}
      {loading && (
        <div className="space-y-6">
          <div className="rounded-2xl border border-amber-500/20 bg-amber-50/40 p-5 dark:bg-amber-500/5 animate-pulse">
            <div className="h-4 w-44 rounded bg-amber-200 dark:bg-white/10" />
            <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
              {[1, 2, 3, 4].map((i) => (
                <div key={i} className="h-28 rounded-xl bg-slate-200/60 dark:bg-white/5" />
              ))}
            </div>
          </div>
          <div className="space-y-3">
            <div className="h-4 w-32 rounded bg-slate-200 dark:bg-white/10 animate-pulse" />
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {[1, 2, 3, 4, 5, 6].map((i) => (
                <div key={i} className="h-36 rounded-xl bg-slate-200/60 dark:bg-white/5 animate-pulse" />
              ))}
            </div>
          </div>
        </div>
      )}

      {/* ERROR STATE */}
      {error && !loading && (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center text-xs text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          <p className="font-semibold text-sm">
            {t('common.error')}
          </p>
          <p className="mt-1 opacity-80">{error}</p>
          <button
            type="button"
            onClick={loadBadges}
            className="mt-4 inline-flex items-center gap-1.5 rounded-lg bg-red-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-xs hover:bg-red-700 transition"
          >
            <RefreshCw className="h-3.5 w-3.5" />
            {t('common.retry')}
          </button>
        </div>
      )}

      {!loading && !error && (
        <div className="space-y-10">
          {/* SECTION 1 — CLOSE TO UNLOCKING (MOTIVATION BOOSTER) */}
          {closeToUnlockingBadges.length > 0 && (
            <section className="relative overflow-hidden rounded-2xl border border-amber-500/30 bg-gradient-to-br from-amber-500/10 via-amber-500/[0.03] to-transparent p-5 sm:p-6 shadow-sm dark:border-amber-400/20 dark:from-amber-400/10">
              <div className="mb-4 flex items-center justify-between">
                <div className="flex items-center gap-2 text-xs font-black uppercase tracking-wider text-amber-700 dark:text-amber-300">
                  <Flame className="h-4 w-4 fill-amber-500 text-amber-500 animate-pulse" />
                  <span>{t('badges.closeToUnlocking')}</span>
                  <span className="rounded-full bg-amber-500/20 px-2 py-0.5 text-[10px] font-bold text-amber-800 dark:text-amber-200">
                    {t('badges.withinReach', { count: closeToUnlockingBadges.length })}
                  </span>
                </div>
                <span className="text-[11px] font-medium text-amber-700/80 dark:text-amber-300/80">
                  {t('badges.keepMomentum')}
                </span>
              </div>

              <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-4">
                {closeToUnlockingBadges.map((badge) => (
                  <div
                    key={badge.code}
                    className="group relative flex flex-col justify-between rounded-xl border border-amber-500/25 bg-white/90 p-4 shadow-xs backdrop-blur-xs transition hover:border-amber-500/50 hover:shadow-md dark:border-white/10 dark:bg-[#121626]/90"
                  >
                    <div>
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-amber-500/15 text-2xl shadow-inner group-hover:scale-105 transition-transform">
                          {badge.iconEmoji || '🎯'}
                        </div>
                        <span className="inline-flex items-center gap-1 rounded-full bg-gradient-to-r from-amber-500/20 to-emerald-500/20 px-2 py-0.5 text-[11px] font-extrabold text-amber-700 dark:text-amber-300">
                          <Zap className="h-3 w-3 fill-current" />
                          {badge.progressPercent}%
                        </span>
                      </div>

                      <div className="mt-3">
                        <div className="flex items-center gap-1.5">
                          <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400 dark:text-white/40">
                            {t(`badges.categories.${badge.category}`, { defaultValue: badge.category })}
                          </span>
                        </div>
                        <h4 className="font-bold text-slate-900 dark:text-white text-sm line-clamp-1">
                          {badge.name}
                        </h4>
                        <p className="mt-1 text-xs text-slate-500 dark:text-slate-400 line-clamp-2">
                          {badge.description}
                        </p>
                      </div>
                    </div>

                    <div className="mt-4 space-y-1.5 border-t border-slate-100 pt-3 dark:border-white/5">
                      <div className="flex items-center justify-between text-[11px] font-semibold text-slate-600 dark:text-slate-300">
                        <span className="text-slate-400 dark:text-white/40">{t('badges.progress')}</span>
                        <span className="font-bold text-amber-600 dark:text-amber-400">
                          {formatProgressLabel(badge)}
                        </span>
                      </div>
                      <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                        <div
                          className="h-full rounded-full bg-gradient-to-r from-amber-500 via-emerald-400 to-teal-400 transition-all duration-700 ease-out"
                          style={{ width: `${Math.max(4, badge.progressPercent)}%` }}
                        />
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </section>
          )}

          {/* SECTION 2 — EARNED BADGES */}
          <section className="space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2 text-xs font-black uppercase tracking-wider text-emerald-600 dark:text-emerald-400">
                <CheckCircle2 className="h-4 w-4" />
                <span>{t('badges.earnedBadges')}</span>
                <span className="rounded-full bg-emerald-500/10 px-2.5 py-0.5 text-xs font-bold text-emerald-700 dark:bg-emerald-400/20 dark:text-emerald-300">
                  {earnedBadges.length}
                </span>
              </div>
            </div>

            {earnedBadges.length === 0 ? (
              <div className="rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 p-8 text-center text-xs text-slate-500 dark:border-white/10 dark:bg-white/[0.02] dark:text-white/40">
                <Award className="mx-auto mb-2 h-8 w-8 opacity-30 text-emerald-500" />
                <p className="font-semibold text-slate-700 dark:text-slate-300 text-sm">
                  {t('badges.noBadgesTitle')}
                </p>
                <p className="mt-1">
                  {t('badges.noBadgesSubtitle')}
                </p>
              </div>
            ) : (
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                {earnedBadges.map((badge) => (
                  <div
                    key={badge.code}
                    className="group relative flex flex-col justify-between overflow-hidden rounded-xl border border-emerald-500/30 bg-gradient-to-b from-emerald-500/10 via-emerald-500/[0.02] to-transparent p-4 shadow-xs transition hover:border-emerald-500/60 hover:shadow-md dark:border-emerald-500/25 dark:from-emerald-500/15"
                  >
                    <div>
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-emerald-500/20 text-2xl shadow-inner group-hover:scale-105 transition-transform">
                          {badge.iconEmoji || '🏆'}
                        </div>
                        <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/20 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-emerald-700 dark:text-emerald-300">
                          <Sparkles className="h-2.5 w-2.5" />
                          {t('badges.earned')}
                        </span>
                      </div>

                      <div className="mt-3">
                        <span className="text-[10px] font-bold uppercase tracking-wider text-emerald-600/80 dark:text-emerald-400/80">
                          {t(`badges.categories.${badge.category}`, { defaultValue: badge.category })}
                        </span>
                        <h4 className="font-bold text-slate-900 dark:text-white text-sm">
                          {badge.name}
                        </h4>
                        <p className="mt-1 text-xs text-slate-600 dark:text-slate-300 line-clamp-2">
                          {badge.description}
                        </p>
                      </div>
                    </div>

                    <div className="mt-4 flex items-center justify-between border-t border-emerald-500/15 pt-2.5 text-[10px] font-medium text-slate-400 dark:text-white/40">
                      <span>{t('badges.unlockedOn')}</span>
                      <span className="font-semibold text-emerald-700 dark:text-emerald-300">
                        {formatDate(badge.earnedAt)}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* SECTION 3 — BY CATEGORY (GROUPED) */}
          <section className="space-y-8">
            <div className="border-b border-slate-100 pb-2 dark:border-white/10">
              <h3 className="text-sm font-black uppercase tracking-wider text-slate-800 dark:text-slate-200 flex items-center gap-2">
                <Target className="h-4 w-4 text-indigo-500" />
                {t('badges.allBadgesByCategory')}
              </h3>
            </div>

            <div className="space-y-8">
              {groupedCategories.map(({ meta, badges: catBadges }) => {
                const catEarned = catBadges.filter((b) => b.isEarned).length;
                return (
                  <div key={meta.key} className="space-y-3">
                    {/* Category Header */}
                    <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1 rounded-xl bg-slate-50 px-4 py-2.5 dark:bg-white/[0.03] border border-slate-200/60 dark:border-white/5">
                      <div className="flex items-center gap-2">
                        <span className="text-lg">{meta.iconEmoji}</span>
                        <div>
                          <h4 className="text-xs font-bold uppercase tracking-wider text-slate-800 dark:text-slate-200">
                            {t(meta.nameKey, { defaultValue: meta.key })}
                          </h4>
                          <p className="text-[11px] text-slate-500 dark:text-white/40">
                            {t(meta.descriptionKey, { defaultValue: '' })}
                          </p>
                        </div>
                      </div>
                      <span className="self-start sm:self-auto rounded-full bg-white px-2.5 py-0.5 text-[11px] font-bold text-slate-600 shadow-xs dark:bg-white/10 dark:text-white/80">
                        {t('badges.unlockedCount', { earned: catEarned, total: catBadges.length })}
                      </span>
                    </div>

                    {/* Category Grid */}
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                      {catBadges.map((badge) => {
                        const isAutomaticEngrams = badge.ruleType?.toLowerCase().trim() === 'automatic_engrams';

                        if (badge.isEarned) {
                          return (
                            <div
                              key={badge.code}
                              className="group relative flex flex-col justify-between rounded-xl border border-emerald-500/30 bg-emerald-500/5 p-4 shadow-xs transition hover:border-emerald-500/50 dark:border-emerald-500/20 dark:bg-emerald-500/10"
                            >
                              <div>
                                <div className="flex items-start justify-between gap-2">
                                  <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-emerald-500/20 text-2xl shadow-inner">
                                    {badge.iconEmoji || '🏆'}
                                  </div>
                                  <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/20 px-2 py-0.5 text-[10px] font-bold text-emerald-700 dark:text-emerald-300">
                                    <CheckCircle2 className="h-2.5 w-2.5" />
                                    {t('badges.earned')}
                                  </span>
                                </div>

                                <div className="mt-3">
                                  <h5 className="font-bold text-slate-900 dark:text-white text-sm">
                                    {badge.name}
                                  </h5>
                                  <p className="mt-1 text-xs text-slate-600 dark:text-slate-300 line-clamp-2">
                                    {badge.description}
                                  </p>
                                </div>
                              </div>

                              <div className="mt-3 border-t border-emerald-500/10 pt-2 text-[10px] font-medium text-emerald-700/80 dark:text-emerald-300/80">
                                {formatDate(badge.earnedAt) ? (
                                  <>
                                    {t('badges.earnedOn')} {formatDate(badge.earnedAt)}
                                  </>
                                ) : (
                                  <>{t('badges.completed100')}</>
                                )}
                              </div>
                            </div>
                          );
                        }

                        // UNEARNED BADGE CARD
                        return (
                          <div
                            key={badge.code}
                            className="flex flex-col justify-between rounded-xl border border-slate-200/80 bg-slate-50/70 p-4 transition hover:border-slate-300 hover:bg-slate-50 dark:border-white/10 dark:bg-white/[0.02] dark:hover:border-white/20"
                          >
                            <div>
                              <div className="flex items-start justify-between gap-2">
                                <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-slate-200/80 text-2xl grayscale dark:bg-white/10">
                                  {badge.iconEmoji || '🔒'}
                                </div>
                                <span className="inline-flex items-center gap-1 rounded-full bg-slate-200/80 px-2 py-0.5 text-[10px] font-semibold text-slate-600 dark:bg-white/10 dark:text-white/60">
                                  <Lock className="h-2.5 w-2.5" />
                                  {isAutomaticEngrams
                                    ? t('badges.comingSoon')
                                    : t('badges.locked')}
                                </span>
                              </div>

                              <div className="mt-3">
                                <h5 className="font-semibold text-slate-700 dark:text-slate-200 text-sm">
                                  {badge.name}
                                </h5>
                                <p className="mt-1 text-xs text-slate-500 dark:text-slate-400 line-clamp-2">
                                  {badge.description}
                                </p>
                              </div>
                            </div>

                            <div className="mt-3 space-y-1.5 border-t border-slate-200/60 pt-2.5 dark:border-white/5">
                              <div className="flex items-center justify-between text-[11px] font-medium text-slate-500 dark:text-white/60">
                                <span className="text-[10px]">
                                  {isAutomaticEngrams
                                    ? t('badges.myBrainAuto')
                                    : t('badges.progress')}
                                </span>
                                <span className="font-semibold text-slate-700 dark:text-slate-300">
                                  {isAutomaticEngrams ? '0 / 5' : formatProgressLabel(badge)}
                                </span>
                              </div>
                              <div className="h-1.5 w-full overflow-hidden rounded-full bg-slate-200/80 dark:bg-white/10">
                                <div
                                  className="h-full rounded-full bg-gradient-to-r from-amber-500 to-emerald-400 transition-all duration-500"
                                  style={{
                                    width: `${isAutomaticEngrams ? 0 : Math.min(100, badge.progressPercent)}%`,
                                  }}
                                />
                              </div>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                );
              })}
            </div>
          </section>
        </div>
      )}
    </div>
  );
};
