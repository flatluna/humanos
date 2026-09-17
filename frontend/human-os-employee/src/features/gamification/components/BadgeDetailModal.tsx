import React from 'react';
import {
  X,
  Sparkles,
  Brain,
  Target,
  Trophy,
  CheckCircle2,
  Lock,
  Calendar,
} from 'lucide-react';
import { type BadgeProgressDto } from '../api/gamificationApi';
import { BADGE_DEFINITIONS } from '../data/badgeDefinitions';
import { useI18n } from '@/features/capabilities/i18n/useI18n';

interface BadgeDetailModalProps {
  badge: BadgeProgressDto | null;
  onClose: () => void;
}

export const BadgeDetailModal: React.FC<BadgeDetailModalProps> = ({ badge, onClose }) => {
  const { language } = useI18n();
  const isEn = language === 'en';

  if (!badge) return null;

  const detail = BADGE_DEFINITIONS[badge.code];
  const name = isEn ? (detail?.name.en ?? badge.name) : (detail?.name.es ?? badge.name);
  const ruleDesc = isEn
    ? (detail?.ruleDescription.en ?? badge.description)
    : (detail?.ruleDescription.es ?? badge.description);
  const neuroExplanation = isEn
    ? detail?.neuroscienceExplanation.en
    : detail?.neuroscienceExplanation.es;
  const howToEarn = isEn ? detail?.howToEarn.en : detail?.howToEarn.es;

  const formatDate = (dateStr?: string | null) => {
    if (!dateStr) return '';
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString(isEn ? 'en-US' : 'es-MX', {
        day: 'numeric',
        month: 'long',
        year: 'numeric',
      });
    } catch {
      return dateStr;
    }
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="badge-detail-title"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-in fade-in duration-200"
      onClick={onClose}
    >
      <div
        className="relative w-full max-w-lg overflow-hidden rounded-3xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-[#0c0f1d] animate-in zoom-in-95 duration-200"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Close button */}
        <button
          type="button"
          onClick={onClose}
          aria-label={isEn ? 'Close' : 'Cerrar'}
          className="absolute right-4 top-4 rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-white/10 dark:hover:text-white transition"
        >
          <X className="h-5 w-5" />
        </button>

        {/* Header Icon + Title */}
        <div className="flex items-start gap-4 pr-10">
          <div
            className={`flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl text-3xl shadow-inner ${
              badge.isEarned
                ? 'bg-emerald-500/20 ring-2 ring-emerald-500/40'
                : 'bg-amber-500/15 ring-2 ring-amber-500/30'
            }`}
          >
            {badge.iconEmoji || (badge.isEarned ? '🏆' : '🎯')}
          </div>
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-[11px] font-black uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
                {badge.category}
              </span>
              {badge.isEarned ? (
                <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/20 px-2.5 py-0.5 text-[11px] font-bold text-emerald-700 dark:text-emerald-300">
                  <CheckCircle2 className="h-3 w-3" />
                  {isEn ? 'Unlocked' : 'Desbloqueado'}
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 rounded-full bg-slate-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-slate-600 dark:bg-white/10 dark:text-white/60">
                  <Lock className="h-3 w-3" />
                  {isEn ? 'In Progress' : 'En Progreso'}
                </span>
              )}
            </div>

            <h3 id="badge-detail-title" className="mt-1 text-xl font-bold text-slate-900 dark:text-white">
              {name}
            </h3>

            {badge.isEarned && badge.earnedAt && (
              <p className="mt-1 flex items-center gap-1.5 text-xs text-emerald-600 dark:text-emerald-400">
                <Calendar className="h-3.5 w-3.5" />
                <span>
                  {isEn ? 'Earned on' : 'Desbloqueado el'} {formatDate(badge.earnedAt)}
                </span>
              </p>
            )}
          </div>
        </div>

        {/* Progress bar if not earned */}
        {!badge.isEarned && (
          <div className="mt-5 rounded-2xl border border-amber-500/20 bg-amber-50/50 p-3.5 dark:bg-amber-500/5">
            <div className="flex items-center justify-between text-xs font-semibold text-slate-700 dark:text-slate-300">
              <span className="flex items-center gap-1">
                <Target className="h-3.5 w-3.5 text-amber-500" />
                {isEn ? 'Progress to Unlock' : 'Progreso para Desbloquear'}
              </span>
              <span className="font-bold text-amber-600 dark:text-amber-400">
                {badge.currentValue} / {badge.targetValue} ({badge.progressPercent}%)
              </span>
            </div>
            <div className="mt-2 h-2.5 w-full overflow-hidden rounded-full bg-slate-200/80 dark:bg-white/10">
              <div
                className="h-full rounded-full bg-gradient-to-r from-amber-500 to-emerald-400 transition-all duration-500"
                style={{ width: `${Math.min(100, Math.max(3, badge.progressPercent))}%` }}
              />
            </div>
          </div>
        )}

        {/* Breakdown Sections */}
        <div className="mt-5 space-y-4 max-h-[60vh] overflow-y-auto pr-1">
          {/* Section 1: What it means */}
          <div className="rounded-2xl border border-slate-100 bg-slate-50/70 p-4 dark:border-white/5 dark:bg-white/[0.03]">
            <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-200">
              <Trophy className="h-4 w-4 text-amber-500" />
              <span>{isEn ? 'What This Achievement Means' : '¿Qué significa este logro?'}</span>
            </div>
            <p className="mt-2 text-sm leading-relaxed text-slate-700 dark:text-slate-200">
              {ruleDesc}
            </p>
          </div>

          {/* Section 2: Neuroscience & Learning Science */}
          {neuroExplanation && (
            <div className="rounded-2xl border border-indigo-100 bg-indigo-50/60 p-4 dark:border-indigo-400/10 dark:bg-indigo-500/5">
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-indigo-700 dark:text-indigo-300">
                <Brain className="h-4 w-4 text-indigo-500" />
                <span>{isEn ? 'Neuroscience & Habit Principle' : 'Neurociencia y Principio de Hábito'}</span>
              </div>
              <p className="mt-2 text-sm leading-relaxed text-slate-700 dark:text-slate-300">
                {neuroExplanation}
              </p>
            </div>
          )}

          {/* Section 3: How to earn it */}
          {howToEarn && (
            <div className="rounded-2xl border border-emerald-100 bg-emerald-50/60 p-4 dark:border-emerald-400/10 dark:bg-emerald-500/5">
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-emerald-700 dark:text-emerald-300">
                <Sparkles className="h-4 w-4 text-emerald-500" />
                <span>{isEn ? 'How to Earn / Complete' : '¿Cómo se gana o completa?'}</span>
              </div>
              <p className="mt-2 text-sm leading-relaxed text-slate-700 dark:text-slate-300">
                {howToEarn}
              </p>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="mt-6 flex justify-end">
          <button
            type="button"
            onClick={onClose}
            className="rounded-full bg-slate-900 px-5 py-2 text-xs font-semibold text-white hover:bg-slate-800 dark:bg-white dark:text-slate-900 dark:hover:bg-slate-100 transition"
          >
            {isEn ? 'Got it' : 'Entendido'}
          </button>
        </div>
      </div>
    </div>
  );
};
