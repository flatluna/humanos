import type { RootGroup } from './types';

const DAY_MS = 24 * 60 * 60 * 1000;

/** Backend datetime strings for LastCompletedDate/FirstCompletedDate are
 * serialized from a UTC value with no "Z"/offset suffix (e.g.
 * "2026-08-06T01:17:58.68"). The native `new Date(...)` parser treats a
 * timezone-less ISO string as LOCAL time, not UTC — in negative-UTC-offset
 * timezones this can shift the parsed instant into "tomorrow", making an
 * engram mastered moments ago look like it hasn't happened yet from
 * "today"'s point of view. Append "Z" when the string has no timezone
 * designator so it's always parsed as the UTC instant it actually is. */
export function toUtcMs(dateString: string): number {
  const hasTimezone = /[Zz]|[+-]\d{2}:\d{2}$/.test(dateString);
  return new Date(hasTimezone ? dateString : `${dateString}Z`).getTime();
}

/** How many days a memory takes to reach 50% retrievability, indexed by
 * how many times the student has genuinely mastered the node (reviewCount).
 * Grows the same way spaced-repetition systems like Anki/FSRS grow their
 * interval after each successful recall: the more times you've truly
 * recalled something, the more stable it becomes and the slower it fades.
 * Index 0 = never reviewed (fallback), 1st mastery, 2nd, 3rd, 4th, 5th+. */
const STABILITY_DAYS_BY_REVIEW = [1, 1, 3, 7, 15, 30];

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
  const elapsedDays = Math.max(0, (Date.now() - toUtcMs(lastCompletedDate)) / DAY_MS);
  const stability = getStabilityDays(reviewCount);
  return 1 / (1 + elapsedDays / (9 * stability));
}

/** Color gradient stops (retrievability -> hex), from "just learned" (emerald)
 * fading down to "essentially forgotten" (rose). Interpolated linearly in
 * RGB space so the ring/dot color breathes smoothly instead of snapping
 * between hard buckets. */
export const COLOR_STOPS: Array<[number, string]> = [
  [1, '#34d399'],
  [0.75, '#22d3ee'],
  [0.5, '#fbbf24'],
  [0.25, '#fb923c'],
  [0, '#fb7185'],
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

/** Global "memory score" (0-100) — the simple average retrievability across
 * every mastered node the student has, straight from data already fetched
 * for the Brain page (no extra backend round-trip). null when there are no
 * mastered nodes yet. */
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

/** One day's point on the "Mi Evolución" growth timeline. */
export interface EngramCountHistoryPoint {
  /** ISO yyyy-MM-dd for this day (local time, midnight). */
  date: string;
  /** Cumulative number of distinct engrams mastered on/before this day —
   * a simple growth counter, never goes down (unlike memory freshness,
   * which is what Mi Cerebro's own gauge already covers). */
  count: number;
  /** Average best-assessment score (0-100) across every engram counted so
   * far, using each node's CURRENT finalScore (we only keep the latest
   * attempt's score, not a full history) — a quality signal, drives the
   * bar's color intensity. null while count is 0. */
  avgScore: number | null;
  /** Names of engrams whose most recent completion landed on this exact
   * calendar day — drives the little "brain" marker on that day's bar. */
  events: string[];
}

/** Builds a daily cumulative "engrams mastered" time series for the last
 * `days` days (ending today) straight from data the Brain page already
 * fetches — no new backend endpoint needed. Always trending up (or flat),
 * telling a clear growth story without needing to explain the forgetting
 * curve (that's Mi Cerebro's job). */
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
    // A node counts for THIS day if it was mastered any time up through the
    // END of this calendar day — not just before its midnight start —
    // otherwise a node mastered later today wouldn't count on today's point.
    const endOfDayMs = day.getTime() + DAY_MS - 1;
    let count = 0;
    let scoreSum = 0;
    let scoreCount = 0;
    const events: string[] = [];
    for (const node of masteredNodes) {
      const masteredAtMs = toUtcMs(node.lastCompletedDate);
      if (masteredAtMs > endOfDayMs) continue; // not mastered yet as of this day
      count += 1;
      if (typeof node.finalScore === 'number') {
        scoreSum += node.finalScore;
        scoreCount += 1;
      }
      if (masteredAtMs >= day.getTime() && masteredAtMs <= endOfDayMs) events.push(node.name);
    }
    points.push({
      date: day.toISOString().slice(0, 10),
      count,
      avgScore: scoreCount > 0 ? Math.round(scoreSum / scoreCount) : null,
      events,
    });
  }
  return points;
}



