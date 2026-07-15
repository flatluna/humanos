import type { FutureSelfOption, GrowthGoalOption, MotivationOption, PersonalDirection } from '../types';

export const FUTURE_SELF_OPTIONS: FutureSelfOption[] = [
  { id: 'senior-product-owner', title: { en: 'Senior Product Owner', es: 'Product Owner Senior' } },
  { id: 'ai-product-strategist', title: { en: 'AI Product Strategist', es: 'Estratega de Producto en IA' } },
  { id: 'product-leader', title: { en: 'Product Leader', es: 'Líder de Producto' } },
  { id: 'entrepreneur', title: { en: 'Entrepreneur', es: 'Emprendedor' } },
  { id: 'technology-consultant', title: { en: 'Technology Consultant', es: 'Consultor de Tecnología' } },
];

export const GROWTH_GOAL_OPTIONS: GrowthGoalOption[] = [
  { id: 'prepare-promotion', title: { en: 'Prepare for a promotion', es: 'Prepararme para un ascenso' } },
  {
    id: 'more-effective-role',
    title: { en: 'Become more effective in my current role', es: 'Ser más efectivo en mi rol actual' },
  },
  { id: 'develop-ai', title: { en: 'Develop AI capabilities', es: 'Desarrollar capacidades de IA' } },
  { id: 'strengthen-leadership', title: { en: 'Strengthen leadership', es: 'Fortalecer mi liderazgo' } },
  { id: 'financial-knowledge', title: { en: 'Increase financial knowledge', es: 'Aumentar mi conocimiento financiero' } },
  { id: 'explore-career', title: { en: 'Explore a different career path', es: 'Explorar una carrera diferente' } },
  { id: 'improve-communication', title: { en: 'Improve communication', es: 'Mejorar mi comunicación' } },
];

export const MOTIVATION_OPTIONS: MotivationOption[] = [
  { id: 'growth', title: { en: 'Growth', es: 'Crecimiento' } },
  { id: 'impact', title: { en: 'Impact', es: 'Impacto' } },
  { id: 'innovation', title: { en: 'Innovation', es: 'Innovación' } },
  { id: 'financial-progress', title: { en: 'Financial Progress', es: 'Progreso Financiero' } },
  { id: 'leadership', title: { en: 'Leadership', es: 'Liderazgo' } },
  { id: 'helping-others', title: { en: 'Helping Others', es: 'Ayudar a Otros' } },
  { id: 'creativity', title: { en: 'Creativity', es: 'Creatividad' } },
  { id: 'security', title: { en: 'Security', es: 'Seguridad' } },
  { id: 'independence', title: { en: 'Independence', es: 'Independencia' } },
];

/** TODO: Replace with a real Azure Function once a "personal direction"
 *  endpoint exists (e.g. person goals + a future-self/aspiration field on
 *  HumanProfile). `null` means the employee has not completed this step yet,
 *  which drives the Growth Plan page's progressive-disclosure wizard.
 */
export async function getPersonalDirection(): Promise<PersonalDirection | null> {
  return null;
}
