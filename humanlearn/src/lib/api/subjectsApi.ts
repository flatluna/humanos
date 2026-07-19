import type { Language } from '../../i18n';

/**
 * Subject — NOT YET a real backend entity (see
 * /memories/repo/student-graph-ui-redesign-final-design.md). Proposed
 * shape: admin-managed lookup table (Code/Name/Description per language +
 * an IconKey), Capability gains a nullable SubjectId FK. Stubbed locally
 * here (bilingual, matching the app's i18n) so the student navigation
 * (Home → Subjects → Capabilities) can be built and demoed before the
 * real backend entity/migration/API exists. Replace `getSubjects()` with
 * a real `apiGet<BackendSubject[]>('/subjects?language=...')` call once
 * the backend exists — keep the same function signature so the swap is a
 * one-file change.
 *
 * `iconKey` maps to a real lucide-react icon component chosen in
 * HomePage.tsx (SUBJECT_ICONS) — deliberately NOT an emoji: emoji icons
 * read as childish/unprofessional (user feedback 2026-07-18).
 */
export interface Subject {
  code: string;
  name: string;
  iconKey: string;
  description: string;
}

interface SubjectContent {
  code: string;
  iconKey: string;
  name: Record<Language, string>;
  description: Record<Language, string>;
}

const STUB_SUBJECTS: SubjectContent[] = [
  {
    code: 'finanzas',
    iconKey: 'finanzas',
    name: { es: 'Finanzas', en: 'Finance' },
    description: {
      es: 'Presupuesto, inversión, negociación y decisiones de dinero.',
      en: 'Budgeting, investing, negotiation and money decisions.',
    },
  },
  {
    code: 'cocina',
    iconKey: 'cocina',
    name: { es: 'Cocina', en: 'Cooking' },
    description: {
      es: 'Técnicas, recetas y fundamentos culinarios prácticos.',
      en: 'Techniques, recipes and practical culinary fundamentals.',
    },
  },
  {
    code: 'recursos-humanos',
    iconKey: 'recursos-humanos',
    name: { es: 'Recursos Humanos', en: 'Human Resources' },
    description: {
      es: 'Contratación, desempeño, cultura y gestión de equipos.',
      en: 'Hiring, performance, culture and team management.',
    },
  },
  {
    code: 'animales',
    iconKey: 'animales',
    name: { es: 'Animales', en: 'Animals' },
    description: {
      es: 'Comportamiento, cuidado y biología animal.',
      en: 'Behavior, care and animal biology.',
    },
  },
  {
    code: 'ciencia',
    iconKey: 'ciencia',
    name: { es: 'Ciencia', en: 'Science' },
    description: {
      es: 'Método científico, física, química y biología aplicada.',
      en: 'Scientific method, physics, chemistry and applied biology.',
    },
  },
  {
    code: 'geografia',
    iconKey: 'geografia',
    name: { es: 'Geografía', en: 'Geography' },
    description: {
      es: 'Países, mapas, culturas y sistemas del mundo.',
      en: 'Countries, maps, cultures and world systems.',
    },
  },
  {
    code: 'matematicas',
    iconKey: 'matematicas',
    name: { es: 'Matemáticas', en: 'Math' },
    description: {
      es: 'Álgebra, lógica, geometría y razonamiento cuantitativo.',
      en: 'Algebra, logic, geometry and quantitative reasoning.',
    },
  },
];

export async function getSubjects(language: Language = 'es'): Promise<Subject[]> {
  return Promise.resolve(
    STUB_SUBJECTS.map((subject) => ({
      code: subject.code,
      iconKey: subject.iconKey,
      name: subject.name[language],
      description: subject.description[language],
    })),
  );
}
