import React, { useEffect, useState } from 'react';
import { Sparkles, Zap, Flame, Trophy, Award, CheckCircle2 } from 'lucide-react';
import type { GamificationRewardBundleDto } from '../types';
import { ConfettiCanvas } from './ConfettiCanvas';
import { useGamificationStore } from '../store/useGamificationStore';
import { useI18n } from '@/features/capabilities/i18n/useI18n';

interface RewardCelebrationProps {
  bundle: GamificationRewardBundleDto;
  onClose: () => void;
}

const LEVEL_CONFIG: Record<number, { min: number; max: number; en: string; es: string }> = {
  1: { min: 0, max: 500, en: 'Neuron', es: 'Neurona' },
  2: { min: 500, max: 1500, en: 'Synapse', es: 'Sinapsis' },
  3: { min: 1500, max: 3500, en: 'Circuit', es: 'Circuito' },
  4: { min: 3500, max: 7000, en: 'Neural Network', es: 'Red Neuronal' },
  5: { min: 7000, max: 7000, en: 'Mastermind', es: 'Mente Maestra' },
};

export const RewardCelebration: React.FC<RewardCelebrationProps> = ({ bundle, onClose }) => {
  const { language } = useI18n();
  const isEn = language === 'en';

  const reward = bundle.reward ?? bundle.Reward;
  const streak = bundle.streak ?? bundle.Streak;
  const rawBadges = bundle.newBadges ?? bundle.NewBadges ?? [];

  const pointsAwarded = reward?.pointsAwarded ?? reward?.PointsAwarded ?? 0;
  const surpriseType = (reward?.surpriseType ?? reward?.SurpriseType ?? 'NONE').toUpperCase();
  const newTotalPoints = reward?.newTotalPoints ?? reward?.NewTotalPoints ?? 0;
  const newLevel = reward?.newLevel ?? reward?.NewLevel ?? 1;

  const currentLevelInfo = LEVEL_CONFIG[newLevel] ?? LEVEL_CONFIG[1];
  const newLevelName = isEn ? currentLevelInfo.en : currentLevelInfo.es;
  const leveledUp = reward?.leveledUp ?? reward?.LeveledUp ?? false;

  const currentStreak = streak?.currentStreakDays ?? streak?.CurrentStreakDays ?? 0;
  const streakChanged = streak?.streakChanged ?? streak?.StreakChanged ?? '';
  const isNewRecord = streak?.isNewRecord ?? streak?.IsNewRecord ?? false;

  const updateProfileFromReward = useGamificationStore((s) => s.updateProfileFromReward);

  // Sync new points & streak into the global store immediately upon celebration
  useEffect(() => {
    if (newTotalPoints > 0) {
      updateProfileFromReward(newTotalPoints, newLevel, newLevelName, currentStreak);
    }
  }, [newTotalPoints, newLevel, newLevelName, currentStreak, updateProfileFromReward]);

  // Animated Points Counter
  const [displayPoints, setDisplayPoints] = useState(0);

  useEffect(() => {
    if (pointsAwarded <= 0) {
      setDisplayPoints(0);
      return;
    }

    const duration = 1200; // ms
    const startTime = performance.now();

    const animateCount = (now: number) => {
      const elapsed = now - startTime;
      const progress = Math.min(elapsed / duration, 1);
      const eased = 1 - (1 - progress) * (1 - progress);
      setDisplayPoints(Math.round(eased * pointsAwarded));

      if (progress < 1) {
        requestAnimationFrame(animateCount);
      }
    };

    const animId = requestAnimationFrame(animateCount);
    return () => cancelAnimationFrame(animId);
  }, [pointsAwarded]);

  // Handle ESC key
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  // Calculate Level Progress Bar
  const levelProgressPct =
    newLevel >= 5
      ? 100
      : Math.min(
          100,
          Math.max(
            0,
            ((newTotalPoints - currentLevelInfo.min) / (currentLevelInfo.max - currentLevelInfo.min)) * 100
          )
        );

  const pointsRemaining = newLevel >= 5 ? 0 : Math.max(0, currentLevelInfo.max - newTotalPoints);
  const nextLevelNumber = Math.min(5, newLevel + 1);
  const nextLevelInfo = newLevel < 5 ? LEVEL_CONFIG[nextLevelNumber] : null;
  const nextLevelName = nextLevelInfo ? (isEn ? nextLevelInfo.en : nextLevelInfo.es) : null;

  const isJackpot = surpriseType === 'JACKPOT';
  const isDouble = surpriseType === 'DOUBLE';
  const isBonus = surpriseType === 'BONUS';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-slate-950/80 backdrop-blur-md transition-opacity duration-300"
        onClick={onClose}
      />

      {/* Confetti if Jackpot */}
      {isJackpot && <ConfettiCanvas durationMs={5000} />}

      {/* Modal Dialog */}
      <div
        role="dialog"
        aria-modal="true"
        className={`relative z-10 w-full max-w-lg overflow-hidden rounded-2xl border bg-slate-900 p-6 shadow-2xl transition-all ${
          isJackpot
            ? 'border-amber-400/60 shadow-amber-500/20 ring-4 ring-amber-500/30'
            : isDouble
            ? 'border-indigo-500/60 shadow-indigo-500/20 ring-2 ring-indigo-500/30'
            : isBonus
            ? 'border-emerald-500/50 shadow-emerald-500/10'
            : 'border-slate-800 shadow-slate-950/50'
        }`}
      >
        {/* Header Ribbon / Surprise Banner */}
        <div className="mb-4 flex flex-col items-center text-center">
          {isJackpot && (
            <div className="mb-2 inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-amber-500 via-orange-500 to-yellow-400 px-4 py-1 text-xs font-black uppercase tracking-wider text-slate-950 shadow-lg animate-pulse">
              <Sparkles className="h-4 w-4" />
              {isEn ? '🎉 JACKPOT 3X MAXIMUM REWARD! 🎉' : '¡JACKPOT x3 RECOMPENSA MÁXIMA!'}
            </div>
          )}

          {isDouble && (
            <div className="mb-2 inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-indigo-500 to-purple-500 px-4 py-1 text-xs font-bold uppercase tracking-wider text-white shadow-md">
              <Zap className="h-4 w-4" />
              {isEn ? '⚡ DOUBLE 2X MULTIPLIER! ⚡' : '¡DOBLE x2 MULTIPLICADOR!'}
            </div>
          )}

          {isBonus && (
            <div className="mb-2 inline-flex items-center gap-2 rounded-full bg-emerald-500/20 border border-emerald-500/40 px-3 py-1 text-xs font-semibold uppercase tracking-wider text-emerald-300">
              <Sparkles className="h-3.5 w-3.5 text-emerald-400" />
              {isEn ? '🎲 BONUS 1.5X REWARD! 🎲' : '¡BONUS x1.5 OBTENIDO!'}
            </div>
          )}

          <h2 className="text-xl font-bold tracking-tight text-white flex items-center gap-2">
            <CheckCircle2 className="h-5 w-5 text-emerald-400" />
            {isEn ? 'Engram Mastered!' : '¡Engrama Dominado!'}
          </h2>
          <p className="mt-0.5 text-xs text-slate-400">
            {isEn
              ? 'Your deliberate effort consolidates long-term memory.'
              : 'Tu esfuerzo consolida memoria a largo plazo.'}
          </p>
        </div>

        {/* Big Points Award Card */}
        <div className="mb-5 rounded-xl border border-slate-800 bg-slate-950/60 p-4 text-center">
          <div className="text-xs font-medium uppercase tracking-widest text-slate-400">
            {isEn ? 'Points Earned' : 'Puntos Ganados'}
          </div>
          <div className="my-1 flex items-center justify-center gap-2">
            <span
              className={`text-4xl font-extrabold tracking-tight ${
                isJackpot
                  ? 'bg-gradient-to-r from-amber-300 via-yellow-200 to-orange-400 bg-clip-text text-transparent'
                  : isDouble
                  ? 'bg-gradient-to-r from-indigo-300 to-purple-300 bg-clip-text text-transparent'
                  : 'text-emerald-400'
              }`}
            >
              +{displayPoints}
            </span>
            <span className="text-sm font-semibold text-slate-400">pts</span>
          </div>

          <div className="mt-2 flex items-center justify-center gap-3 text-xs text-slate-400">
            <span>
              {isEn ? 'Total accumulated:' : 'Total acumulado:'}{' '}
              <strong className="text-white">{newTotalPoints}</strong> pts
            </span>
          </div>
        </div>

        {/* Level Progression */}
        <div className="mb-4 rounded-xl border border-slate-800 bg-slate-950/40 p-3.5">
          <div className="flex items-center justify-between text-xs">
            <span className="flex items-center gap-1.5 font-medium text-slate-300">
              <Trophy className="h-3.5 w-3.5 text-amber-400" />
              {isEn ? 'Level' : 'Nivel'} {newLevel}: <strong className="text-amber-300">{newLevelName}</strong>
            </span>
            {newLevel < 5 && nextLevelName && (
              <span className="text-slate-400 text-[11px]">
                {isEn ? (
                  <><strong>{pointsRemaining} pts</strong> to {nextLevelName}</>
                ) : (
                  <>Faltan <strong>{pointsRemaining} pts</strong> para {nextLevelName}</>
                )}
              </span>
            )}
          </div>

          <div className="mt-2 h-2 w-full overflow-hidden rounded-full bg-slate-800">
            <div
              className="h-full bg-gradient-to-r from-amber-500 to-emerald-400 transition-all duration-700"
              style={{ width: `${levelProgressPct}%` }}
            />
          </div>

          {leveledUp && (
            <div className="mt-2.5 flex items-center gap-2 rounded-lg bg-amber-500/10 border border-amber-500/30 p-2 text-xs font-semibold text-amber-200">
              <Sparkles className="h-4 w-4 text-amber-400 flex-shrink-0" />
              <span>
                {isEn
                  ? <>Congratulations! You've reached Level <strong>{newLevelName}</strong>.</>
                  : <>¡Felicidades! Has alcanzado el nivel <strong>{newLevelName}</strong>.</>}
              </span>
            </div>
          )}
        </div>

        {/* Streak & Badges Grid */}
        <div className="mb-5 space-y-3">
          {/* Daily Streak */}
          {currentStreak > 0 && (
            <div className="flex items-center justify-between rounded-xl border border-orange-500/20 bg-orange-950/10 p-3 text-xs">
              <div className="flex items-center gap-2.5">
                <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-orange-500/20 text-orange-400">
                  <Flame className="h-5 w-5 fill-orange-500 text-orange-500" />
                </div>
                <div>
                  <div className="font-semibold text-white">
                    {isEn
                      ? `${currentStreak} day streak`
                      : `Racha de ${currentStreak} ${currentStreak === 1 ? 'día' : 'días'}`}
                  </div>
                  <div className="text-[11px] text-slate-400">
                    {streakChanged === 'FROZEN'
                      ? isEn ? 'Streak saved with a freeze' : 'Racha salvada con un freeze'
                      : isNewRecord
                      ? isEn ? 'New personal record!' : '¡Nuevo récord personal!'
                      : isEn ? 'Daily learning habit active' : 'Hábito de aprendizaje activo'}
                  </div>
                </div>
              </div>

              {isNewRecord && (
                <span className="rounded-full bg-orange-500/20 px-2 py-0.5 text-[10px] font-bold text-orange-300">
                  {isEn ? 'RECORD' : 'RÉCORD'}
                </span>
              )}
            </div>
          )}

          {/* New Badges */}
          {rawBadges.length > 0 && (
            <div className="space-y-2">
              <div className="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                <Award className="h-3.5 w-3.5 text-purple-400" />
                <span>{isEn ? 'New Badges Unlocked' : 'Nuevas Medallas Desbloqueadas'}</span>
              </div>

              {rawBadges.map((badge, idx) => {
                const badgeCode = badge.badgeCode ?? badge.BadgeCode ?? String(idx);
                const name = badge.name ?? badge.Name ?? 'Medalla';
                const emoji = badge.iconEmoji ?? badge.IconEmoji ?? '🏆';
                const desc = badge.description ?? badge.Description ?? '';

                return (
                  <div
                    key={badgeCode}
                    className="flex items-start gap-3 rounded-xl border border-purple-500/30 bg-purple-950/20 p-3 text-xs"
                  >
                    <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-lg bg-purple-500/20 text-lg">
                      {emoji}
                    </div>
                    <div>
                      <div className="font-bold text-purple-200">{name}</div>
                      <div className="mt-0.5 text-[11px] text-purple-300/80">{desc}</div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Action Button */}
        <button
          type="button"
          onClick={onClose}
          className="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-emerald-600 to-teal-600 py-3 text-sm font-bold text-white shadow-lg shadow-emerald-950/50 hover:from-emerald-500 hover:to-teal-500 active:scale-[0.99] transition-all cursor-pointer"
        >
          {isEn ? 'Continue Learning' : 'Continuar Aprendiendo'}
        </button>
      </div>
    </div>
  );
};
