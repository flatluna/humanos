import { motion } from 'framer-motion';
import { getMemoryColor } from './memoryScience';

const RADIUS = 40;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

export interface MemoryScoreGaugeProps {
  /** 0-100, or null when there are no mastered nodes yet. */
  score: number | null;
  label: string;
  caption: string;
}

/** Big animated "global memory strength" gauge — the average retrievability
 * (power-law forgetting curve, see memoryScience.ts) across every mastered
 * node, computed entirely client-side from data the Brain page already
 * fetched. A spectacular, single-glance answer to "how much of what I've
 * learned am I actually still retaining right now?" */
export function MemoryScoreGauge({ score, label, caption }: MemoryScoreGaugeProps) {
  const pct = score ?? 0;
  const color = getMemoryColor(score === null ? null : score / 100);

  return (
    <div
      className="relative flex items-center gap-4 overflow-hidden rounded-2xl border border-white/10 bg-gradient-to-br from-slate-900 via-slate-900 to-slate-800 p-4 shadow-lg"
      style={{ boxShadow: `0 0 40px -12px ${color}55` }}
    >
      <motion.div
        className="pointer-events-none absolute inset-0 opacity-20"
        style={{ background: `radial-gradient(circle at 20% 20%, ${color}, transparent 60%)` }}
        animate={{ opacity: [0.15, 0.3, 0.15] }}
        transition={{ repeat: Infinity, duration: 3, ease: 'easeInOut' }}
      />
      <div className="relative flex h-24 w-24 shrink-0 items-center justify-center">
        <svg className="absolute inset-0 -rotate-90" viewBox="0 0 96 96">
          <circle cx="48" cy="48" r={RADIUS} fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="7" />
          <motion.circle
            cx="48"
            cy="48"
            r={RADIUS}
            fill="none"
            stroke={color}
            strokeWidth="7"
            strokeLinecap="round"
            strokeDasharray={CIRCUMFERENCE}
            initial={{ strokeDashoffset: CIRCUMFERENCE }}
            animate={{ strokeDashoffset: CIRCUMFERENCE * (1 - pct / 100) }}
            transition={{ duration: 1.2, ease: 'easeOut' }}
          />
        </svg>
        <div className="flex flex-col items-center">
          <motion.span
            className="text-xl font-bold text-white"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.4 }}
          >
            {score === null ? '—' : `${pct}%`}
          </motion.span>
        </div>
      </div>
      <div className="relative min-w-0">
        <p className="text-xs font-semibold uppercase tracking-wide text-white/50">{label}</p>
        <p className="mt-1 text-sm text-white/70">{caption}</p>
      </div>
    </div>
  );
}
