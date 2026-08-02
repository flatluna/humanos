/** Deterministic color per Subject code — shared between the subject filter
 *  chips (BrainPage) and the subject badge shown on each capability cluster
 *  (BrainRecallGrid), so the same subject always reads as the same color
 *  across the page. Simple string hash → palette index, no backend/config
 *  needed. Ported verbatim from human-os-web's /brain feature. */
const SUBJECT_PALETTE = ['#f472b6', '#60a5fa', '#4ade80', '#facc15', '#fb923c', '#c084fc', '#2dd4bf', '#f87171'];

export function getSubjectColor(subjectCode: string): string {
  let hash = 0;
  for (let i = 0; i < subjectCode.length; i++) {
    hash = (hash * 31 + subjectCode.charCodeAt(i)) >>> 0;
  }
  return SUBJECT_PALETTE[hash % SUBJECT_PALETTE.length];
}
