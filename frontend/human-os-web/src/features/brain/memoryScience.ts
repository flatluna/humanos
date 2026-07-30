import type { RootGroup } from './types';

const DAY_MS = 24 * 60 * 60 * 1000;

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
  const elapsedDays = Math.max(0, (Date.now() - new Date(lastCompletedDate).getTime()) / DAY_MS);
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
