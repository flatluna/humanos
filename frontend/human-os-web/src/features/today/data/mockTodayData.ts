import type { TodaySnapshot } from '../types';

/** Placeholder data for the "Today" screen.
 *  TODO: Replace with data fetched from the Human OS API once the
 *  corresponding endpoints (person profile, human profile, person
 *  capabilities, person goals, evidence, practice/recall history) are
 *  wired up through @/api and Microsoft Entra authentication.
 */
export const todaySnapshot: TodaySnapshot = {
  userName: 'Jorge',
  currentLayer: 'mastery',
  progressTowardNextLayer: 67,
  futureSelf: { en: 'AI Consultant', es: 'Consultor de IA' },
  motivations: [
    { en: 'Innovation', es: 'Innovación' },
    { en: 'Impact', es: 'Impacto' },
    { en: 'Leadership', es: 'Liderazgo' },
  ],
  personalGoal: {
    id: 'goal-ai-consulting',
    title: { en: 'Become AI Consultant', es: 'Convertirme en Consultor de IA' },
  },
  organizationInitiative: {
    id: 'org-ai-transformation',
    title: { en: 'AI Transformation Initiative', es: 'Iniciativa de Transformación de IA' },
  },
  sharedCapabilities: [
    { en: 'AI Automation', es: 'Automatización con IA' },
    { en: 'Critical Thinking', es: 'Pensamiento Crítico' },
    { en: 'Communication', es: 'Comunicación' },
    { en: 'Problem Solving', es: 'Resolución de Problemas' },
  ],
  capabilities: [
    {
      id: 'cap-ai-automation',
      name: { en: 'AI Automation', es: 'Automatización con IA' },
      progress: 82,
      level: 4,
      supports: [
        { en: 'AI Consultant', es: 'Consultor de IA' },
        { en: 'AI Transformation Initiative', es: 'Iniciativa de Transformación de IA' },
      ],
    },
    {
      id: 'cap-critical-thinking',
      name: { en: 'Critical Thinking', es: 'Pensamiento Crítico' },
      progress: 61,
      level: 3,
      supports: [{ en: 'AI Consultant', es: 'Consultor de IA' }],
    },
    {
      id: 'cap-communication',
      name: { en: 'Communication', es: 'Comunicación' },
      progress: 74,
      level: 3,
      supports: [{ en: 'AI Consultant', es: 'Consultor de IA' }],
    },
  ],
  actions: [
    {
      id: 'action-recall-ai-automation',
      type: 'recall',
      title: { en: 'Recall AI Automation', es: 'Recordar Automatización con IA' },
      why: {
        en: 'Fuels your AI Consultant goal and the AI Transformation Initiative.',
        es: 'Impulsa tu meta de Consultor de IA y la Iniciativa de Transformación de IA.',
      },
    },
    {
      id: 'action-continue-ai-project',
      type: 'project',
      title: { en: 'Continue AI Project', es: 'Continuar Proyecto de IA' },
      why: {
        en: 'Builds Mastery evidence toward Promotion Readiness.',
        es: 'Genera evidencia de Maestría para tu preparación de ascenso.',
      },
    },
    {
      id: 'action-practice-critical-thinking',
      type: 'practice',
      title: { en: 'Practice Critical Thinking', es: 'Practicar Pensamiento Crítico' },
      why: {
        en: 'Strengthens a capability shared with your organization.',
        es: 'Fortalece una capacidad compartida con tu organización.',
      },
    },
    {
      id: 'action-submit-evidence',
      type: 'evidence',
      title: { en: 'Submit Evidence', es: 'Enviar Evidencia' },
      why: {
        en: 'Proves growth toward the Professional layer.',
        es: 'Demuestra tu crecimiento hacia la capa Profesional.',
      },
    },
  ],
  humanState: [
    { key: 'focus', value: 78 },
    { key: 'energy', value: 64 },
    { key: 'purpose', value: 85 },
    { key: 'confidence', value: 70 },
  ],
};
