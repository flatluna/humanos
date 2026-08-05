import { useMemo, useState } from 'react';
import { TrendingUp, Brain } from 'lucide-react';
import { getMemoryColor, type EngramCountHistoryPoint } from '../brain/memoryScience';

const WIDTH = 600;
const HEIGHT = 240;
const AXIS_LEFT = 34; // room for the Y-axis value labels
const BARS_TOP = 28; // room above the tallest bar for the brain marker
const BARS_BOTTOM = 28; // room below the bars for date labels
const BAR_GAP = 2;
const MARKER_SIZE = 16;

/** Rounds a chart max UP to a "nice" number (1/2/5 × a power of ten) so the
 * Y-axis gridlines land on round values instead of odd fractions. */
function niceAxisMax(value: number): number {
  if (value <= 0) return 10;
  const magnitude = 10 ** Math.floor(Math.log10(value));
  const residual = value / magnitude;
  const niceResidual = residual <= 1 ? 1 : residual <= 2 ? 2 : residual <= 5 ? 5 : 10;
  return niceResidual * magnitude;
}

/** Parses a "yyyy-MM-dd" date-only string as LOCAL midnight, not UTC — the
 * native `new Date("yyyy-MM-dd")` parses as UTC, which shows the wrong day
 * once formatted back in a negative-UTC-offset timezone (e.g. "today" shows
 * as yesterday). */
function parseLocalDate(isoDate: string): Date {
  const [year, month, day] = isoDate.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export interface EngramStockChartProps {
  history: EngramCountHistoryPoint[];
  countLabel: string;
  todayDeltaLabel: string;
  locale: string;
}

/** "Mi Evolución" — cumulative engrams mastered as a bar-per-day timeline:
 *  one bar per day (its height = engrams mastered by that day, so it only
 *  ever goes up or stays flat), colored by average assessment quality, with
 *  a little "brain" marker on top of any day an engram was mastered.
 *  Hand-rolled SVG (no charting library dependency) to match this
 *  codebase's existing hand-rolled gauges/rings (MemoryScoreGauge,
 *  BrainRecallGrid). */
export function EngramStockChart({ history, countLabel, todayDeltaLabel, locale }: EngramStockChartProps) {
  const [hoverIndex, setHoverIndex] = useState<number | null>(null);

  const { bars, dateLabelEvery, yTicks } = useMemo(() => {
    if (history.length === 0) return { bars: [], dateLabelEvery: 1, yTicks: [] as number[] };
    const axisMax = niceAxisMax(Math.max(...history.map((p) => p.count), 1));
    const barsAreaHeight = HEIGHT - BARS_TOP - BARS_BOTTOM;
    const plotWidth = WIDTH - AXIS_LEFT;
    const barWidth = plotWidth / history.length - BAR_GAP;
    const built = history.map((p, i) => {
      const x = AXIS_LEFT + i * (plotWidth / history.length) + BAR_GAP / 2;
      const barHeight = p.count > 0 ? Math.max(2, (p.count / axisMax) * barsAreaHeight) : 0;
      const y = BARS_TOP + (barsAreaHeight - barHeight);
      return { ...p, x, y, width: Math.max(1, barWidth), height: barHeight, centerX: x + Math.max(1, barWidth) / 2 };
    });
    // Show ~6 date labels across the whole window, evenly spaced.
    const labelEvery = Math.max(1, Math.ceil(history.length / 6));
    // Round to whole numbers (this is an integer count) and drop duplicates —
    // otherwise small ranges (e.g. axisMax=2) show "0, 1, 1, 2, 2" labels.
    const ticks = Array.from(new Set([0, axisMax * 0.25, axisMax * 0.5, axisMax * 0.75, axisMax].map((t) => Math.round(t))));
    return { bars: built, dateLabelEvery: labelEvery, yTicks: ticks };
  }, [history]);

  if (bars.length === 0) return null;

  const barsAreaHeight = HEIGHT - BARS_TOP - BARS_BOTTOM;
  const axisMax = yTicks[yTicks.length - 1] ?? 1;
  const latest = bars[bars.length - 1];
  const previous = bars.length > 1 ? bars[bars.length - 2] : latest;
  const todayDelta = latest.count - previous.count;
  const active = hoverIndex !== null ? bars[hoverIndex] : latest;

  return (
    <div>
      <div className="flex items-end justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-white/50">{countLabel}</p>
          <div className="mt-1 flex items-baseline gap-2">
            <p className="text-3xl font-bold text-slate-900 dark:text-white">{active.count}</p>
            <span className="text-xs text-slate-400 dark:text-white/40">
              {parseLocalDate(active.date).toLocaleDateString(locale, { day: 'numeric', month: 'short' })}
            </span>
          </div>
        </div>
        {todayDelta > 0 && (
          <div className="flex items-center gap-1 rounded-full bg-emerald-500/10 px-2.5 py-1 text-xs font-semibold text-emerald-500">
            <TrendingUp className="h-3.5 w-3.5" />+{todayDelta} {todayDeltaLabel}
          </div>
        )}
      </div>

      <svg viewBox={`0 0 ${WIDTH} ${HEIGHT}`} preserveAspectRatio="none" className="mt-4 h-52 w-full overflow-visible">
        {yTicks.map((tick) => {
          const y = BARS_TOP + (barsAreaHeight - (tick / axisMax) * barsAreaHeight);
          return (
            <g key={tick}>
              <line
                x1={AXIS_LEFT}
                x2={WIDTH}
                y1={y}
                y2={y}
                stroke="currentColor"
                strokeDasharray="3 3"
                className="text-slate-200 dark:text-white/10"
              />
              <text
                x={AXIS_LEFT - 6}
                y={y + 3}
                textAnchor="end"
                className="fill-slate-400 dark:fill-white/40"
                style={{ fontSize: 9 }}
              >
                {Math.round(tick)}
              </text>
            </g>
          );
        })}
        {bars.map((bar, i) => {
          const isActive = hoverIndex === i || (hoverIndex === null && i === bars.length - 1);
          const color = bar.avgScore !== null ? getMemoryColor(bar.avgScore / 100) : '#cbd5e1';
          const hasEvent = bar.events.length > 0;
          return (
            <g key={bar.date}>
              {/* invisible full-height hit target so hovering the thin column works, not just the (short) bar itself */}
              <rect
                x={bar.x - BAR_GAP / 2}
                y={0}
                width={bar.width + BAR_GAP}
                height={HEIGHT}
                fill="transparent"
                onMouseEnter={() => setHoverIndex(i)}
                onMouseLeave={() => setHoverIndex(null)}
              />
              <rect
                x={bar.x}
                y={bar.y}
                width={bar.width}
                height={bar.height}
                rx={Math.min(3, bar.width / 2)}
                fill={color}
                opacity={isActive ? 1 : 0.55}
                className="transition-opacity"
              />
              {hasEvent && (
                <g transform={`translate(${bar.centerX - MARKER_SIZE / 2}, ${bar.y - MARKER_SIZE - 4})`}>
                  <circle cx={MARKER_SIZE / 2} cy={MARKER_SIZE / 2} r={MARKER_SIZE / 2} fill="#8b5cf6" stroke="white" strokeWidth="1.5" />
                  <foreignObject x={2} y={2} width={MARKER_SIZE - 4} height={MARKER_SIZE - 4}>
                    <Brain className="h-3 w-3 text-white" />
                  </foreignObject>
                </g>
              )}
              {i % dateLabelEvery === 0 && (
                <text
                  x={bar.centerX}
                  y={HEIGHT - 8}
                  textAnchor="middle"
                  className="fill-slate-400 dark:fill-white/40"
                  style={{ fontSize: 10 }}
                >
                  {parseLocalDate(bar.date).toLocaleDateString(locale, { day: 'numeric', month: 'short' })}
                </text>
              )}
            </g>
          );
        })}
      </svg>
    </div>
  );
}


