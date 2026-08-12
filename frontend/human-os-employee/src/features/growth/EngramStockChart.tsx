import { useMemo, useState } from 'react';
import { Brain } from 'lucide-react';
import { getMemoryColor, type EngramCountHistoryPoint } from '../brain/memoryScience';

const WIDTH = 600;
const HEIGHT = 240;
const AXIS_LEFT = 34; // room for the Y-axis value labels
const BARS_TOP = 28; // room above the tallest bar for the brain marker
const BARS_BOTTOM = 28; // room below the bars for date labels
const BAR_GAP = 2;
const MARKER_SIZE = 16;
const TOOLTIP_WIDTH = 108;
const TOOLTIP_HEIGHT = 22;

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
  dailyCountLabel: string;
  totalLabel: string;
  scoreLabel: string;
  locale: string;
}

/** "Mi Evolución" — a bar-per-day timeline of engrams ACTUALLY mastered
 *  that day (not a flattened running total), colored by that day's average
 *  assessment quality, with a little "brain" marker on any day an engram
 *  was mastered. The lifetime cumulative total is shown separately as an
 *  always-visible badge, so the bars themselves stay a genuine day-by-day
 *  activity signal. Hand-rolled SVG (no charting library dependency), same
 *  as MemoryScoreGauge/BrainRecallGrid. Ported verbatim from human-os-web
 *  (2026-08-08). */
export function EngramStockChart({ history, dailyCountLabel, totalLabel, scoreLabel, locale }: EngramStockChartProps) {
  const [hoverIndex, setHoverIndex] = useState<number | null>(null);

  const { bars, dateLabelEvery, yTicks } = useMemo(() => {
    if (history.length === 0) return { bars: [], dateLabelEvery: 1, yTicks: [] as number[] };
    const axisMax = niceAxisMax(Math.max(...history.map((p) => p.dailyCount), 1));
    const barsAreaHeight = HEIGHT - BARS_TOP - BARS_BOTTOM;
    const plotWidth = WIDTH - AXIS_LEFT;
    const barWidth = plotWidth / history.length - BAR_GAP;
    const built = history.map((p, i) => {
      const x = AXIS_LEFT + i * (plotWidth / history.length) + BAR_GAP / 2;
      const barHeight = p.dailyCount > 0 ? Math.max(2, (p.dailyCount / axisMax) * barsAreaHeight) : 0;
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
  const active = hoverIndex !== null ? bars[hoverIndex] : latest;

  return (
    <div>
      <div className="flex items-end justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-white/50">{dailyCountLabel}</p>
          <div className="mt-1 flex items-baseline gap-2">
            <p className="text-3xl font-bold text-slate-900 dark:text-white">{active.dailyCount}</p>
            <span className="text-xs text-slate-400 dark:text-white/40">
              {parseLocalDate(active.date).toLocaleDateString(locale, { day: 'numeric', month: 'short' })}
            </span>
          </div>
          {active.avgScore !== null && (
            <p className="mt-0.5 text-xs text-slate-400 dark:text-white/40">
              {scoreLabel}: {active.avgScore}%
            </p>
          )}
        </div>
        <div className="flex items-center gap-1 rounded-full bg-blue-500/10 px-2.5 py-1 text-xs font-semibold text-blue-500">
          {totalLabel}: {latest.cumulativeCount}
        </div>
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
        {hoverIndex !== null &&
          (() => {
            const bar = bars[hoverIndex];
            const fullDate = parseLocalDate(bar.date).toLocaleDateString(locale, {
              weekday: 'short',
              day: 'numeric',
              month: 'short',
              year: 'numeric',
            });
            const tooltipX = Math.min(Math.max(bar.centerX - TOOLTIP_WIDTH / 2, AXIS_LEFT), WIDTH - TOOLTIP_WIDTH);
            const tooltipY = Math.max(2, bar.y - (bar.events.length > 0 ? MARKER_SIZE + 8 : 0) - TOOLTIP_HEIGHT - 6);
            return (
              <g pointerEvents="none">
                <rect
                  x={tooltipX}
                  y={tooltipY}
                  width={TOOLTIP_WIDTH}
                  height={TOOLTIP_HEIGHT}
                  rx={5}
                  className="fill-slate-900 dark:fill-white"
                  opacity={0.92}
                />
                <text
                  x={tooltipX + TOOLTIP_WIDTH / 2}
                  y={tooltipY + TOOLTIP_HEIGHT / 2 + 3}
                  textAnchor="middle"
                  className="fill-white dark:fill-slate-900"
                  style={{ fontSize: 10, fontWeight: 600 }}
                >
                  {fullDate}
                </text>
              </g>
            );
          })()}
      </svg>
    </div>
  );
}
