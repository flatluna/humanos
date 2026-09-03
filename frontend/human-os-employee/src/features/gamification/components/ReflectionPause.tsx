import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Brain, ArrowRight, Sparkles } from 'lucide-react';

interface ReflectionPauseProps {
  onComplete: () => void;
  onSkip?: () => void;
  engramTitle?: string;
  durationSeconds?: number;
}

export const ReflectionPause: React.FC<ReflectionPauseProps> = ({
  onComplete,
  onSkip,
  engramTitle,
  durationSeconds = 7,
}) => {
  const { t } = useTranslation();
  const [secondsRemaining, setSecondsRemaining] = useState(durationSeconds);
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    const startTime = performance.now();
    const totalMs = durationSeconds * 1000;

    const interval = setInterval(() => {
      const elapsed = performance.now() - startTime;
      const pct = Math.min(100, (elapsed / totalMs) * 100);
      setProgress(pct);

      const rem = Math.max(0, Math.ceil((totalMs - elapsed) / 1000));
      setSecondsRemaining(rem);

      if (elapsed >= totalMs) {
        clearInterval(interval);
        onComplete();
      }
    }, 50);

    return () => clearInterval(interval);
  }, [durationSeconds, onComplete]);

  const handleSkip = () => {
    if (onSkip) {
      onSkip();
    } else {
      onComplete();
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/85 backdrop-blur-md p-4 animate-fade-in"
      role="dialog"
      aria-modal="true"
      aria-label={t('reflection.title')}
    >
      <div className="relative w-full max-w-lg rounded-3xl border border-indigo-500/30 bg-gradient-to-b from-slate-900 via-slate-900/95 to-slate-950 p-6 md:p-8 shadow-2xl text-center overflow-hidden">
        {/* Subtle breathing ambient glow */}
        <div className="pointer-events-none absolute -top-24 left-1/2 h-56 w-56 -translate-x-1/2 rounded-full bg-indigo-500/20 blur-3xl animate-pulse" />

        {/* Top badge */}
        <div className="inline-flex items-center gap-1.5 rounded-full border border-indigo-400/30 bg-indigo-500/10 px-3 py-1 text-xs font-medium text-indigo-300">
          <Sparkles className="h-3.5 w-3.5 text-indigo-400" />
          <span>{t('reflection.badge')}</span>
        </div>

        {/* Serene Brain Icon */}
        <div className="mt-5 flex justify-center">
          <div className="relative flex h-16 w-16 items-center justify-center rounded-2xl bg-indigo-600/15 border border-indigo-400/30 shadow-inner">
            <Brain className="h-9 w-9 text-indigo-400 animate-pulse" />
          </div>
        </div>

        {/* Header */}
        <h2 className="mt-4 text-xl md:text-2xl font-bold tracking-tight text-white">
          {t('reflection.title')}
        </h2>
        <p className="mt-1.5 text-xs md:text-sm text-slate-300">
          {t('reflection.subtitle')}
        </p>

        {engramTitle && (
          <div className="mt-2 text-xs font-semibold text-indigo-300">
            {engramTitle}
          </div>
        )}

        {/* Active Reflection Question Card */}
        <div className="mt-6 rounded-2xl border border-white/10 bg-white/[0.03] p-4 text-left shadow-xs">
          <p className="text-sm md:text-base font-semibold text-indigo-100">
            💡 {t('reflection.question')}
          </p>
          <p className="mt-2 text-xs text-slate-400 leading-relaxed">
            {t('reflection.promptHint')}
          </p>
        </div>

        {/* Progress bar */}
        <div className="mt-6">
          <div className="flex items-center justify-between text-[11px] font-medium text-slate-400 mb-1.5">
            <span>{t('reflection.title')}</span>
            <span>{secondsRemaining}s</span>
          </div>
          <div className="h-1.5 w-full overflow-hidden rounded-full bg-slate-800">
            <div
              className="h-full bg-gradient-to-r from-indigo-500 to-indigo-300 transition-all duration-75 ease-linear rounded-full"
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>

        {/* Action Buttons */}
        <div className="mt-6 flex items-center justify-between gap-3">
          <button
            type="button"
            onClick={handleSkip}
            className="rounded-xl px-4 py-2 text-xs font-medium text-slate-400 hover:text-white hover:bg-white/[0.05] transition"
          >
            {t('reflection.skip')}
          </button>
          <button
            type="button"
            onClick={onComplete}
            className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-600 hover:bg-indigo-500 px-5 py-2 text-xs font-semibold text-white shadow-md transition"
          >
            <span>{t('reflection.ready')}</span>
            <ArrowRight className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>
    </div>
  );
};
