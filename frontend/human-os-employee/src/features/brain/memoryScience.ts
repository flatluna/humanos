import type { RootGroup } from './types';

const DAY_MS = 24 * 60 * 60 * 1000;

/** How many days a memory takes to reach 50% retrievability, indexed by
 * how many times the employee has genuinely mastered the node (reviewCount).
 * Grows the same way spaced-repetition systems like Anki/FSRS grow their
 * interval after each successful recall: the more times you've truly
 * recalled something, the more stable it becomes and the slower it fades.
 * Index 0 = never reviewed (fallback), 1st mastery, 2nd, 3rd, 4th, 5th+. */
const STABILITY_DAYS_BY_REVIEW = [8, 8, 18, 40, 80, 150];

/** Reviews needed to cross from DECLARATIVE memory (explicit facts/concepts,
 * fades per the power-law curve above) into PROCEDURAL/AUTOMATIC memory
 * (Fitts & Posner's "autonomous" skill-acquisition stage — like riding a
 * bike, near-permanent, barely fades). The power law of practice (Newell &
 * Rosenbloom, 1981) and skill-automaticity research generally place this
 * transition around 5-7 successful repetitions for simple, well-defined
 * tasks (e.g. an arithmetic fact) — 5 is used here as a practical
 * threshold, not a precise universal constant (real automaticity varies by
 * task complexity and person). */
const AUTOMATICITY_REVIEW_THRESHOLD = 5;

/** Stability (in days) once a memory has become automatic/procedural —
 * ~10 years, i.e. functionally "almost never forgotten" for the purposes of
 * this app's forgetting curve, mirroring how procedural skills barely decay
 * even after years of disuse. */
const AUTOMATIC_STABILITY_DAYS = 3650;

/** "Stability" (S) in the FSRS-style power-law forgetting curve, in days. */
export function getStabilityDays(reviewCount: number): number {
  if (reviewCount >= AUTOMATICITY_REVIEW_THRESHOLD) return AUTOMATIC_STABILITY_DAYS;
  if (reviewCount <= 0) return STABILITY_DAYS_BY_REVIEW[0];
  if (reviewCount < STABILITY_DAYS_BY_REVIEW.length) return STABILITY_DAYS_BY_REVIEW[reviewCount];
  const extraReviews = reviewCount - (STABILITY_DAYS_BY_REVIEW.length - 1);
  return STABILITY_DAYS_BY_REVIEW[STABILITY_DAYS_BY_REVIEW.length - 1] + extraReviews * 30;
}

/** True once a node has crossed into automatic/procedural memory (Fitts &
 * Posner's "autonomous" stage) — used by the UI to show a distinct
 * "automático" badge instead of the usual fading percentage. */
export function isAutomatic(reviewCount: number): boolean {
  return reviewCount >= AUTOMATICITY_REVIEW_THRESHOLD;
}

/**
 * Retrievability R(t,S) using the power-law forgetting curve popularized by
 * FSRS (the algorithm behind modern Anki) and grounded in Wixted & Ebbesen's
 * research showing human forgetting fits a power law better than the
 * classic Ebbinghaus exponential:
 *
 *   R(t, S) = (1 + t / (9 * S)) ^ -1
 *
 * Returns a value in [0, 1] — 1 = just learned, fading toward 0 over time.
 * Returns null when the node has never been mastered (no date to decay from).
 */
export function getRetrievability(lastCompletedDate: string | undefined, reviewCount = 1): number | null {
  if (!lastCompletedDate) return null;
  const elapsedDays = Math.max(0, (Date.now() - new Date(lastCompletedDate).getTime()) / DAY_MS);
  const stability = getStabilityDays(reviewCount);
  return 1 / (1 + elapsedDays / (9 * stability));
}

/** Color gradient stops (retrievability -> hex), redesigned (2026-08-07,
 * product decision) as a "traffic light" heat gradient: vivid red = fully
 * forgotten (an urgent "go review this" signal), warming through orange
 * and yellow, to green = just reviewed/100% fresh. Routed through
 * orange/yellow rather than a straight red->green blend, which would
 * otherwise interpolate through a muddy brown in RGB space. Fills the
 * WHOLE node circle (not just a thin ring), so the "how much do I
 * remember" state reads at a glance from the dot's color. Interpolated
 * linearly in RGB space so the dot color breathes smoothly instead of
 * snapping between hard buckets. */
export const COLOR_STOPS: Array<[number, string]> = [
  [1, '#16a34a'],
  [0.75, '#65a30d'],
  [0.5, '#eab308'],
  [0.25, '#f97316'],
  [0, '#ef4444'],
];

function hexToRgb(hex: string): [number, number, number] {
  const v = parseInt(hex.slice(1), 16);
  return [(v >> 16) & 255, (v >> 8) & 255, v & 255];
}

function rgbToHex(r: number, g: number, b: number): string {
  const clamp = (n: number) => Math.max(0, Math.min(255, Math.round(n)));
  return `#${[r, g, b].map((n) => clamp(n).toString(16).padStart(2, '0')).join('')}`;
}

/** Maps a retrievability value [0,1] to a smoothly-interpolated hex color. */
export function getMemoryColor(retrievability: number | null): string {
  if (retrievability === null) return '#64748b';
  const r = Math.max(0, Math.min(1, retrievability));
  for (let i = 0; i < COLOR_STOPS.length - 1; i++) {
    const [hi, hiColor] = COLOR_STOPS[i];
    const [lo, loColor] = COLOR_STOPS[i + 1];
    if (r <= hi && r >= lo) {
      const t = hi === lo ? 0 : (r - lo) / (hi - lo);
      const [r1, g1, b1] = hexToRgb(loColor);
      const [r2, g2, b2] = hexToRgb(hiColor);
      return rgbToHex(r1 + (r2 - r1) * t, g1 + (g2 - g1) * t, b1 + (b2 - b1) * t);
    }
  }
  return COLOR_STOPS[COLOR_STOPS.length - 1][1];
}

/** Perceptual relative luminance (WCAG formula), used to pick a legible
 * text color on top of an arbitrary hex background. */
function relativeLuminance(hex: string): number {
  const [r, g, b] = hexToRgb(hex).map((c) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** Readable text/icon color to place ON TOP of a solid getMemoryColor()
 * fill — computed from the actual resulting background's luminance rather
 * than hardcoded buckets, so it stays correct as the gradient's colors
 * change (e.g. the yellow midpoint needs dark text, red/green endpoints
 * need white). */
export function getMemoryTextColor(retrievability: number | null): string {
  return relativeLuminance(getMemoryColor(retrievability)) > 0.4 ? '#0f172a' : '#ffffff';
}

/** Global "memory score" (0-100) — the simple average retrievability across
 * every mastered node, computed entirely client-side from data the Brain
 * page already fetched. null when there are no mastered nodes yet. */
export function computeGlobalMemoryScore(rootGroups: RootGroup[]): number | null {
  let sum = 0;
  let count = 0;
  for (const root of rootGroups) {
    for (const cluster of root.clusters) {
      for (const node of cluster.nodes) {
        const r = getRetrievability(node.lastCompletedDate, node.reviewCount ?? 1);
        if (r !== null) {
          sum += r;
          count += 1;
        }
      }
    }
  }
  if (count === 0) return null;
  return Math.round((sum / count) * 100);
}

/** One day's point on the "Mi Evolución" growth timeline. Ported verbatim
 * from human-os-web's memoryScience.ts (2026-08-08). */
export interface EngramCountHistoryPoint {
  /** ISO yyyy-MM-dd for this day (local time, midnight). */
  date: string;
  /** Cumulative number of distinct engrams mastered on/before this day —
   * a simple growth counter, never goes down (unlike memory freshness,
   * which is what Mi Cerebro's own gauge already covers). Shown as an
   * always-visible "total so far" badge, not the bar height. */
  cumulativeCount: number;
  /** Engrams mastered on THIS exact calendar day — the real day-by-day
   * activity signal, and what the chart's bar height plots (a cumulative
   * count would flatten every day's actual effort into one ever-growing
   * number, hiding what was actually done "today"). */
  dailyCount: number;
  /** Average best-assessment score (0-100) of ONLY the engrams mastered on
   * THIS day (not a cumulative/all-time average), so the bar's color
   * genuinely reflects that day's performance. null when dailyCount is 0. */
  avgScore: number | null;
  /** Names of engrams whose most recent completion landed on this exact
   * calendar day — drives the little "brain" marker on that day's bar. */
  events: string[];
}

/** Parses backend timestamps and returns the learner's local calendar date.
 * Backend values without a timezone suffix are UTC, while offset-bearing
 * values already carry their timezone information. */
function toLocalDateKey(dateString: string): string {
  const hasTimezone = /[Zz]|[+-]\d{2}:\d{2}$/.test(dateString);
  const date = new Date(hasTimezone ? dateString : `${dateString}Z`);
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** Builds a daily "engrams mastered" activity time series for the last
 * `days` days (ending today) straight from data the Brain page already
 * fetches — no new backend endpoint needed. Each point separates that
 * day's own activity (dailyCount/avgScore, what the bars plot) from the
 * running lifetime total (cumulativeCount, shown as a separate badge). */
export function computeEngramCountHistory(rootGroups: RootGroup[], days = 30): EngramCountHistoryPoint[] {
  const masteredNodes: { name: string; lastCompletedDate: string; finalScore?: number }[] = [];
  for (const root of rootGroups) {
    for (const cluster of root.clusters) {
      for (const node of cluster.nodes) {
        if (node.lastCompletedDate) {
          masteredNodes.push({ name: node.name, lastCompletedDate: node.lastCompletedDate, finalScore: node.finalScore });
        }
      }
    }
  }

  const today = new Date();
  today.setHours(0, 0, 0, 0);

  const points: EngramCountHistoryPoint[] = [];
  for (let i = days - 1; i >= 0; i--) {
    const day = new Date(today.getTime() - i * DAY_MS);
    const dayKey = `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;
    let cumulativeCount = 0;
    const events: string[] = [];
    let dailyScoreSum = 0;
    let dailyScoreCount = 0;
    for (const node of masteredNodes) {
      const masteredDateKey = toLocalDateKey(node.lastCompletedDate);
      if (masteredDateKey > dayKey) continue; // not mastered yet as of this day
      cumulativeCount += 1;
      const masteredToday = masteredDateKey === dayKey;
      if (masteredToday) {
        events.push(node.name);
        if (typeof node.finalScore === 'number') {
          dailyScoreSum += node.finalScore;
          dailyScoreCount += 1;
        }
      }
    }
    points.push({
      date: dayKey,
      cumulativeCount,
      dailyCount: events.length,
      avgScore: dailyScoreCount > 0 ? Math.round(dailyScoreSum / dailyScoreCount) : null,
      events,
    });
  }
  return points;
}
