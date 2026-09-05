import React, { useEffect, useRef, useState } from 'react';
import {
  Trophy,
  Flame,
  Sparkles,
  ChevronDown,
  Brain,
  BookOpen,
  Award,
  Sparkle,
  Info,
  X,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useGamificationStore } from '../store/useGamificationStore';
import { BadgeGalleryModal } from './BadgeGalleryModal';
import { useI18n } from '@/features/capabilities/i18n/useI18n';

/** Compact legend for the "what is this / how do I earn it" info popover —
 * mirrors BadgeGallery.tsx's own CATEGORY_ORDER (emoji + i18n keys), kept
 * as a small local copy here since this panel only needs the category-level
 * summary, not the full per-badge gallery. */
const BADGE_CATEGORY_LEGEND: Array<{ key: string; iconEmoji: string }> = [
  { key: 'Effort', iconEmoji: '🌱' },
  { key: 'Progress', iconEmoji: '📈' },
  { key: 'Skills', iconEmoji: '🏆' },
  { key: 'Quality', iconEmoji: '⭐' },
  { key: 'Challenge', iconEmoji: '🛡️' },
  { key: 'Habit', iconEmoji: '🔥' },
  { key: 'Luck', iconEmoji: '🎲' },
];

export const GamificationProfilePanel: React.FC = () => {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { t } = useTranslation();
  const { profile, loading, fetchProfile } = useGamificationStore();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [badgeInfoOpen, setBadgeInfoOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    void fetchProfile();
  }, [fetchProfile]);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setDropdownOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setDropdownOpen(false);
      }
    }

    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, []);

  if (loading && !profile) {
    return (
      <div className="flex h-9 items-center gap-2 rounded-xl border border-slate-200/80 bg-slate-50/50 px-3 py-1 text-xs text-slate-400 dark:border-white/10 dark:bg-white/[0.03] animate-pulse">
        <div className="h-4 w-4 rounded-full bg-slate-200 dark:bg-white/10" />
        <div className="h-3 w-28 rounded bg-slate-200 dark:bg-white/10" />
      </div>
    );
  }

  const currentLevel = profile?.currentLevel ?? 1;
  const currentLevelName = t(`gamification.levels.${currentLevel}`, { defaultValue: isEn ? 'Neuron' : 'Neurona' });
  const totalPoints = profile?.totalPoints ?? 0;
  const currentStreak = profile?.currentStreakDays ?? 0;
  const streakFreezes = profile?.streakFreezesAvailable ?? 1;
  const pointsToNext = profile?.pointsToNextLevel ?? 0;
  const nextLevel = currentLevel + 1;
  const nextLevelName = nextLevel <= 7 ? t(`gamification.levels.${nextLevel}`) : null;

  // Career metrics
  const totalEngramsMastered = profile?.totalEngramsMastered ?? 0;
  const totalAssignedEngrams = profile?.totalAssignedEngrams ?? 0;
  const completedCourses = profile?.completedCourses ?? 0;
  const totalAssignedCourses = profile?.totalAssignedCourses ?? 0;
  const coursesReady = profile?.coursesReady ?? 0;
  const coursesPending = profile?.coursesPendingGeneration ?? 0;

  // Level thresholds for level progress bar
  const LEVEL_BANDS: Record<number, { min: number; max: number }> = {
    1: { min: 0, max: 500 },
    2: { min: 500, max: 1500 },
    3: { min: 1500, max: 3500 },
    4: { min: 3500, max: 7000 },
    5: { min: 7000, max: 7000 },
  };

  const levelBand = LEVEL_BANDS[currentLevel] ?? LEVEL_BANDS[1];
  const levelProgressPct =
    currentLevel >= 5
      ? 100
      : Math.min(
          100,
          Math.max(
            0,
            ((totalPoints - levelBand.min) / (levelBand.max - levelBand.min)) * 100
          )
        );

  // Safe percentage calculations (preventing division by zero)
  const engramsPct =
    totalAssignedEngrams > 0
      ? (totalEngramsMastered / totalAssignedEngrams) * 100
      : 0;

  const coursesPct =
    totalAssignedCourses > 0
      ? (completedCourses / totalAssignedCourses) * 100
      : 0;

  return (
    <div ref={dropdownRef} className="relative flex items-center gap-1.5">
      {/* COMPACT HEADER PILL (Click opens dropdown) */}
      <button
        type="button"
        onClick={() => setDropdownOpen((prev) => !prev)}
        aria-expanded={dropdownOpen}
        aria-haspopup="true"
        className="flex h-9 cursor-pointer items-center gap-2 sm:gap-2.5 rounded-full border border-slate-200/80 bg-slate-50/90 px-3 py-1 text-xs text-slate-700 shadow-xs transition-all hover:border-slate-300 hover:bg-slate-100/80 dark:border-white/10 dark:bg-white/[0.04] dark:text-slate-200 dark:hover:border-white/20 dark:hover:bg-white/[0.08]"
      >
        {/* Level and Name */}
        <div className="flex items-center gap-1.5 font-semibold text-slate-900 dark:text-white">
          <Brain className="h-3.5 w-3.5 text-indigo-500 dark:text-indigo-400" />
          <span>
            Lv{currentLevel} · <span className="text-amber-600 dark:text-amber-300 font-bold">{currentLevelName}</span>
          </span>
        </div>

        {/* Level Progress Bar */}
        <div className="hidden sm:block h-1.5 w-12 overflow-hidden rounded-full bg-slate-200 dark:bg-white/10">
          <div
            className="h-full bg-gradient-to-r from-amber-500 to-emerald-400 transition-all duration-500"
            style={{ width: `${levelProgressPct}%` }}
          />
        </div>

        {/* Points */}
        <span className="font-bold text-emerald-600 dark:text-emerald-400">
          {totalPoints} <span className="text-[10px] font-normal text-slate-500 dark:text-white/50">pts</span>
        </span>

        {/* Streak */}
        <div className="flex items-center gap-1 rounded-full bg-orange-500/10 px-1.5 py-0.5 text-[11px] font-bold text-orange-600 dark:bg-orange-500/20 dark:text-orange-400">
          <Flame className={`h-3 w-3 ${currentStreak > 0 ? 'fill-orange-500 text-orange-500' : 'text-orange-400/50'}`} />
          <span>{currentStreak}</span>
        </div>

        <ChevronDown
          className={`h-3.5 w-3.5 text-slate-400 transition-transform duration-200 ${
            dropdownOpen ? 'rotate-180 text-indigo-500' : ''
          }`}
        />
      </button>

      {/* BADGE INFO BUTTON — always visible next to the pill, independent of the dropdown */}
      <button
        type="button"
        onClick={() => setBadgeInfoOpen((prev) => !prev)}
        aria-label={isEn ? 'What are badges and how do I earn them?' : '¿Qué son las medallas y cómo se ganan?'}
        aria-expanded={badgeInfoOpen}
        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-slate-200/80 bg-slate-50/90 text-indigo-500 shadow-xs transition-all hover:border-indigo-300 hover:bg-indigo-50 dark:border-white/10 dark:bg-white/[0.04] dark:text-indigo-300 dark:hover:border-indigo-400/40 dark:hover:bg-indigo-400/10"
      >
        <Info className="h-4 w-4" />
      </button>

      {/* BADGE INFO POPOVER — "what is it / how do I earn it" legend */}
      {badgeInfoOpen && (
        <div className="absolute right-0 top-full z-50 mt-2 w-72 max-h-72 space-y-2.5 overflow-y-auto rounded-xl border border-indigo-200/60 bg-white p-3 shadow-2xl dark:border-indigo-400/20 dark:bg-[#0c0f1d] animate-in fade-in zoom-in-95 duration-150">
          <div className="flex items-center justify-between border-b border-indigo-100 pb-2 dark:border-indigo-400/10">
            <span className="text-[11px] font-black uppercase tracking-wider text-indigo-700 dark:text-indigo-300">
              {isEn ? 'Badge Categories' : 'Categorías de Medallas'}
            </span>
            <button
              type="button"
              onClick={() => setBadgeInfoOpen(false)}
              aria-label={t('common.close')}
              className="rounded-full p-0.5 text-indigo-400 transition hover:bg-indigo-500/10 hover:text-indigo-600"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          </div>
          <div className="space-y-2">
            {BADGE_CATEGORY_LEGEND.map((cat) => (
              <div key={cat.key} className="flex items-start gap-2">
                <span className="text-sm leading-none">{cat.iconEmoji}</span>
                <div>
                  <p className="text-[11px] font-bold text-slate-800 dark:text-slate-100">
                    {t(`badges.categories.${cat.key}`, { defaultValue: cat.key })}
                  </p>
                  <p className="text-[11px] text-slate-600 dark:text-white/60">
                    {t(`badges.categoryDescriptions.${cat.key}`, { defaultValue: '' })}
                  </p>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* DROPDOWN MENU — CAREER PROGRESS ON DEMAND */}
      {dropdownOpen && (
        <div
          role="menu"
          className="absolute right-0 mt-2 w-80 sm:w-88 origin-top-right rounded-2xl border border-slate-200 bg-white p-4 shadow-2xl transition-all dark:border-white/10 dark:bg-[#0c0f1d] z-50 animate-in fade-in zoom-in-95 duration-150"
        >
          {/* Header */}
          <div className="flex items-center justify-between border-b border-slate-100 pb-3 dark:border-white/10">
            <div className="flex items-center gap-1.5 text-xs font-black uppercase tracking-wider text-slate-700 dark:text-slate-200">
              <Trophy className="h-3.5 w-3.5 text-amber-500" />
              <span>{isEn ? 'Career Progress' : 'Progreso de Carrera'}</span>
            </div>
            <span className="rounded-full bg-indigo-500/10 px-2 py-0.5 text-[10px] font-bold text-indigo-600 dark:bg-indigo-500/20 dark:text-indigo-300">
              Level {currentLevel}
            </span>
          </div>

          <div className="mt-3 space-y-3.5">
            {/* Engrams Progress Bar */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between text-xs font-semibold">
                <span className="flex items-center gap-1.5 text-slate-600 dark:text-slate-300">
                  <Brain className="h-3.5 w-3.5 text-indigo-400" />
                  {isEn ? 'Engrams' : 'Engramas'}
                </span>
                <span className="font-mono text-slate-900 dark:text-white">
                  <span className="font-bold text-indigo-600 dark:text-indigo-400">{totalEngramsMastered}</span> / {totalAssignedEngrams}{' '}
                  <span className="text-[11px] font-normal text-slate-400 dark:text-white/40">
                    ({engramsPct.toFixed(1)}%)
                  </span>
                </span>
              </div>
              <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                <div
                  className="h-full rounded-full bg-gradient-to-r from-indigo-500 to-purple-500 transition-all duration-500"
                  style={{ width: `${Math.min(100, Math.max(0, engramsPct))}%` }}
                />
              </div>
            </div>

            {/* Courses Progress Bar */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between text-xs font-semibold">
                <span className="flex items-center gap-1.5 text-slate-600 dark:text-slate-300">
                  <BookOpen className="h-3.5 w-3.5 text-emerald-400" />
                  {isEn ? 'Courses' : 'Cursos'}
                </span>
                <span className="font-mono text-slate-900 dark:text-white">
                  <span className="font-bold text-emerald-600 dark:text-emerald-400">{completedCourses}</span> / {totalAssignedCourses}{' '}
                  <span className="text-[11px] font-normal text-slate-400 dark:text-white/40">
                    ({coursesPct.toFixed(0)}%)
                  </span>
                </span>
              </div>
              <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                <div
                  className="h-full rounded-full bg-gradient-to-r from-emerald-500 to-teal-400 transition-all duration-500"
                  style={{ width: `${Math.min(100, Math.max(0, coursesPct))}%` }}
                />
              </div>
            </div>

            {/* Courses Status subtext */}
            <div className="flex items-center gap-1.5 text-[11px] text-slate-500 dark:text-white/50 bg-slate-50 dark:bg-white/[0.03] rounded-xl px-2.5 py-1.5 border border-slate-100 dark:border-white/5">
              <Sparkle className="h-3 w-3 text-amber-500 shrink-0" />
              <span>
                <strong>{coursesReady}</strong> {isEn ? 'course ready' : 'curso listo'} ·{' '}
                <strong>{coursesPending}</strong> {isEn ? 'coming soon' : 'próximamente'}
              </span>
            </div>

            {/* Level & Points Details */}
            <div className="rounded-xl border border-slate-100 bg-slate-50/70 p-2.5 dark:border-white/5 dark:bg-white/[0.02] space-y-1.5">
              <div className="flex items-center justify-between text-xs font-medium text-slate-700 dark:text-slate-200">
                <span>{currentLevelName}</span>
                <span className="font-mono text-[11px] text-slate-500 dark:text-white/50">
                  {totalPoints} / {levelBand.max} pts
                </span>
              </div>
              <div className="h-1.5 w-full overflow-hidden rounded-full bg-slate-200 dark:bg-white/10">
                <div
                  className="h-full bg-gradient-to-r from-amber-500 to-emerald-400 transition-all duration-500"
                  style={{ width: `${levelProgressPct}%` }}
                />
              </div>
              <div className="flex items-center justify-between text-[11px] text-slate-500 dark:text-white/50 pt-0.5">
                {nextLevelName ? (
                  <span>
                    <strong>{pointsToNext}</strong> {isEn ? `to ${nextLevelName}` : `para ${nextLevelName}`}
                  </span>
                ) : (
                  <span className="font-semibold text-amber-500 dark:text-amber-300">
                    <Sparkles className="inline h-3 w-3 mr-1" />
                    {isEn ? 'Max level achieved!' : '¡Nivel máximo!'}
                  </span>
                )}
                <span className="flex items-center gap-1.5">
                  <Flame className="h-3 w-3 text-orange-500 fill-orange-500" />
                  {currentStreak} {isEn ? 'streak' : 'racha'} · ❄️ {streakFreezes} {isEn ? 'freeze' : 'freeze'}
                </span>
              </div>
            </div>
          </div>

          {/* Action button to open full BadgeGallery */}
          <div className="mt-4 border-t border-slate-100 pt-3 dark:border-white/10">
            <button
              type="button"
              onClick={() => {
                setDropdownOpen(false);
                setModalOpen(true);
              }}
              className="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-4 py-2 text-xs font-bold text-white shadow-md shadow-brand-500/20 transition-transform hover:scale-[1.01] active:scale-[0.99] cursor-pointer"
            >
              <Award className="h-3.5 w-3.5" />
              <span>{isEn ? 'View All Badges' : 'Ver Todas las Medallas'}</span>
            </button>
          </div>
        </div>
      )}

      {/* MODAL DIALOG FOR FULL BADGE GALLERY */}
      <BadgeGalleryModal isOpen={modalOpen} onClose={() => setModalOpen(false)} />
    </div>
  );
};
