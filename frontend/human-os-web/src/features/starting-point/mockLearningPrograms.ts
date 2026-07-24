/** Mock "Programa" (Learning Path) catalog — Growth Plan Step 3 merged
 *  step ("Planeemos Juntos tu Desarrollo", agreed 2026-07-22).
 *
 *  DESIGN CONTEXT (do not remove without re-reading the discussion):
 *  A Programa is a NEW concept, distinct from the existing three-tier
 *  taxonomy (CapabilityDomain -> Subject -> Capability). It's a named,
 *  goal-oriented bundle of ORDERED Capabilities that together fulfill a
 *  certifiable outcome ("Aprende Inglés", "Certificación AI-900",
 *  "Certificación Plomero") — pre-built by us/Studio, not improvised
 *  per-user. This file is a PURE FRONTEND MOCK to validate the UX
 *  concept before the real `Programa` entity/table exists in the
 *  backend — every capability name below is a PLACEHOLDER (none exist
 *  in the real catalog yet), exactly like the gap-suggestion chips in
 *  subjectGapSuggestions.ts. Do not wire this to real Capability IDs.
 *
 *  IMPORTANT (agreed 2026-07-22, second pass): the mock "agent" is NOT
 *  a second, disconnected dataset — its "knowledge" IS the same
 *  SUBJECT_GAP_SUGGESTIONS catalog already shown as chips elsewhere on
 *  this page. `recommendPath` below tries a fully curated Program
 *  first (best result — a real ordered path), and if none matches,
 *  falls back to recommending the matching cluster of individual
 *  capabilities straight out of subjectGapSuggestions.ts, instead of a
 *  flat "no match" dead end. Both the curated-Program search and the
 *  cluster fallback are scoped to only the Subjects the person already
 *  selected in Step 1 (`allowedSubjectCodes`) — the goal prompt is not
 *  a fully open free-for-all, it only elaborates on the areas already
 *  chosen.
 */

import { SUBJECT_GAP_SUGGESTIONS } from './subjectGapSuggestions';

export type ProgramLevel = 'Beginner' | 'Intermediate' | 'Advanced';

export interface ProgramStep {
  /** Placeholder capability name — matches the granularity rule in
   *  subjectGapSuggestions.ts (one bounded, teachable, assessable topic). */
  name: string;
  /** The self-assessed level this step corresponds to, used to compute
   *  a suggested entry point from the person's declared current level. */
  level: ProgramLevel;
}

export interface LearningProgram {
  id: string;
  subjectCode: string;
  name: string;
  description: string;
  /** Free-text keywords matched (case-insensitively, substring) against
   *  the person's goal prompt by the mock recommendation function below. */
  matchKeywords: string[];
  steps: ProgramStep[];
}

export const MOCK_LEARNING_PROGRAMS: LearningProgram[] = [
  {
    id: 'aprende-ingles',
    subjectCode: 'idiomas',
    name: 'Aprende Inglés',
    description: 'Un camino completo de inglés, desde cero hasta conversación fluida.',
    matchKeywords: ['ingles', 'inglés', 'english', 'usa', 'estados unidos'],
    steps: [
      { name: 'Inglés Básico', level: 'Beginner' },
      { name: 'Inglés Medio', level: 'Intermediate' },
      { name: 'Inglés Avanzado', level: 'Advanced' },
      { name: 'Inglés de Conversación', level: 'Advanced' },
    ],
  },
  {
    id: 'certificacion-ai-900',
    subjectCode: 'tecnologia',
    name: 'Certificación AI-900 (Microsoft Azure AI Fundamentals)',
    description: 'Prepárate paso a paso para el examen de certificación AI-900 de Microsoft.',
    matchKeywords: ['ai-900', 'ai900', 'azure ai', 'certificación microsoft', 'inteligencia artificial'],
    steps: [
      { name: 'Fundamentos de la Nube', level: 'Beginner' },
      { name: 'Fundamentos de Machine Learning', level: 'Beginner' },
      { name: 'Visión por Computadora en Azure', level: 'Intermediate' },
      { name: 'Procesamiento de Lenguaje Natural en Azure', level: 'Intermediate' },
      { name: 'IA Generativa en Azure', level: 'Advanced' },
    ],
  },
  {
    id: 'certificacion-plomero',
    subjectCode: 'oficios',
    name: 'Certificación como Plomero',
    description: 'De lo más básico hasta poder certificarte como plomero profesional.',
    matchKeywords: ['plomer', 'tuberia', 'tubería', 'fontaner'],
    steps: [
      { name: 'Herramientas y Seguridad Básica de Plomería', level: 'Beginner' },
      { name: 'Instalación de Tuberías Residenciales', level: 'Beginner' },
      { name: 'Reparación de Fugas y Drenajes', level: 'Intermediate' },
      { name: 'Sistemas de Calentadores de Agua', level: 'Intermediate' },
      { name: 'Códigos y Normativas de Plomería', level: 'Advanced' },
    ],
  },
  {
    id: 'certificacion-carpintero',
    subjectCode: 'oficios',
    name: 'Certificación como Carpintero',
    description: 'De lo más básico hasta poder certificarte como carpintero profesional.',
    // Reuses the exact item names from the "Carpintería" cluster in
    // subjectGapSuggestions.ts, now ordered into a real Programa.
    matchKeywords: ['carpinter', 'muebles a medida', 'ebanist'],
    steps: [
      { name: 'Carpintería Básica', level: 'Beginner' },
      { name: 'Uso y Cuidado de Herramientas', level: 'Beginner' },
      { name: 'Acabados y Barnizado', level: 'Intermediate' },
      { name: 'Muebles a Medida', level: 'Advanced' },
    ],
  },
];

export type Recommendation =
  | { type: 'program'; program: LearningProgram }
  | { type: 'capabilities'; subjectCode: string; clusterLabel: string; items: string[] };

function normalize(text: string): string {
  return text
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase();
}

const STOPWORDS = new Set([
  'quiero',
  'quisiera',
  'necesito',
  'aprender',
  'saber',
  'estudiar',
  'mejorar',
  'desarrollar',
  'para',
  'sobre',
  'como',
  'desde',
  'estar',
  'tener',
  'poder',
  'trabajar',
  'de',
  'del',
  'la',
  'el',
  'los',
  'las',
  'un',
  'una',
  'y',
  'o',
  'en',
  'al',
  'mi',
  'tu',
  'su',
  'es',
  'con',
  'por',
  'que',
  'lo',
  'me',
  'te',
  'se',
]);

/** Kept short (>= 2 chars) so acronyms like "IA", "ML", "CI", "CD" are
 *  not silently dropped — see subjectGapSuggestions.ts's "IA" cluster.
 *  Short words are matched as whole tokens only (see `clusterMatches`)
 *  to avoid false substring hits (e.g. "ia" inside "historia"). */
function meaningfulWords(prompt: string): string[] {
  return normalize(prompt)
    .split(/[^a-z0-9]+/)
    .filter((word) => word.length >= 2 && !STOPWORDS.has(word));
}

/** A prompt word matches a cluster if: for short words (<=3 chars) it
 *  appears as a whole token in the cluster's text (avoids "ia" matching
 *  inside "historia"/"ciencia"); for longer words a substring match is
 *  fine (catches stems like "electricidad" / "eléctrico"). */
function clusterMatches(clusterLabel: string, items: string[], words: string[]): boolean {
  const haystackText = normalize(`${clusterLabel} ${items.join(' ')}`);
  const haystackWords = new Set(haystackText.split(/[^a-z0-9]+/));
  return words.some((word) => (word.length <= 3 ? haystackWords.has(word) : haystackText.includes(word)));
}


/** Mock "agent" — pure keyword matching, no LLM call. First tries a
 *  fully curated Programa among `allowedSubjectCodes`; if none matches,
 *  falls back to the matching cluster of individual capabilities from
 *  subjectGapSuggestions.ts (same catalog, same subjects); returns null
 *  only if nothing matches at all. */
export function recommendPath(goalPrompt: string, allowedSubjectCodes: string[]): Recommendation | null {
  const normalizedPrompt = normalize(goalPrompt.trim());
  if (!normalizedPrompt) {
    return null;
  }

  const allowedSet = new Set(allowedSubjectCodes);

  const program = MOCK_LEARNING_PROGRAMS.find(
    (candidate) =>
      allowedSet.has(candidate.subjectCode) &&
      candidate.matchKeywords.some((keyword) => normalizedPrompt.includes(normalize(keyword))),
  );
  if (program) {
    return { type: 'program', program };
  }

  const words = meaningfulWords(goalPrompt);
  if (words.length === 0) {
    return null;
  }

  for (const [subjectCode, clusters] of Object.entries(SUBJECT_GAP_SUGGESTIONS)) {
    if (!allowedSet.has(subjectCode)) continue;

    for (const cluster of clusters) {
      if (clusterMatches(cluster.clusterLabel, cluster.items, words)) {
        return { type: 'capabilities', subjectCode, clusterLabel: cluster.clusterLabel, items: cluster.items };
      }
    }
  }

  return null;
}

const LEVEL_ORDER: ProgramLevel[] = ['Beginner', 'Intermediate', 'Advanced'];

/** Given the person's declared current level, returns the index of the
 *  first ProgramStep they should start at (skipping steps at or below
 *  their declared level). Returns 0 (start from the very beginning) when
 *  no level was declared. */
export function suggestedEntryIndex(steps: ProgramStep[], currentLevel: ProgramLevel | null): number {
  if (!currentLevel) {
    return 0;
  }

  const currentRank = LEVEL_ORDER.indexOf(currentLevel);
  const firstAboveIndex = steps.findIndex((step) => LEVEL_ORDER.indexOf(step.level) > currentRank);
  return firstAboveIndex === -1 ? steps.length - 1 : firstAboveIndex;
}

/** Serializes this same catalog (curated Programs + gap-suggestion
 *  clusters), scoped to the Subjects the person selected in Step 1, into
 *  a compact text blob sent as "catalog context" to the real
 *  GrowthPathRecommenderAgent backend agent (see humanOsApi.ts's
 *  recommendGrowthPath). This is the "large list of programs and
 *  courses that don't exist yet" the agent is told it may draw
 *  inspiration from — the LLM is NOT restricted to it verbatim, see the
 *  agent's own instructions. */
export function buildCatalogContext(allowedSubjectCodes: string[]): string {
  const allowedSet = new Set(allowedSubjectCodes);
  const lines: string[] = [];

  const programs = MOCK_LEARNING_PROGRAMS.filter((program) => allowedSet.has(program.subjectCode));
  if (programs.length > 0) {
    lines.push('Existing curated Programs (ordered):');
    for (const program of programs) {
      const stepNames = program.steps.map((step) => `${step.name} [${step.level}]`).join(' -> ');
      lines.push(`- [${program.subjectCode}] "${program.name}": ${program.description} Steps: ${stepNames}`);
    }
  }

  const clusterLines: string[] = [];
  for (const [subjectCode, clusters] of Object.entries(SUBJECT_GAP_SUGGESTIONS)) {
    if (!allowedSet.has(subjectCode)) continue;
    for (const cluster of clusters) {
      clusterLines.push(`- [${subjectCode}] "${cluster.clusterLabel}": ${cluster.items.join(', ')}`);
    }
  }
  if (clusterLines.length > 0) {
    lines.push('Individual capability-cluster suggestions (unordered):');
    lines.push(...clusterLines);
  }

  return lines.join('\n');
}


