import type { FutureSelfOption, GrowthGoalOption, MotivationOption, PersonalDirection } from '../types';

/** TODO: Replace with real Azure Functions once Future Self / personal
 *  goal / motivation concepts have dedicated backend endpoints. Personal
 *  goals overlap conceptually with the existing GoalService, but
 *  "Future Self" and "Motivation" are enterprise-experience-only concepts
 *  that do not yet exist in the backend.
 */

export const futureSelfOptions: FutureSelfOption[] = [
  { id: 'senior-product-owner', title: { en: 'Senior Product Owner', es: 'Product Owner Senior' } },
  { id: 'ai-product-strategist', title: { en: 'AI Product Strategist', es: 'Estratega de Producto en IA' } },
  { id: 'product-leader', title: { en: 'Product Leader', es: 'Líder de Producto' } },
  { id: 'entrepreneur', title: { en: 'Entrepreneur', es: 'Emprendedor' } },
  { id: 'technology-consultant', title: { en: 'Technology Consultant', es: 'Consultor de Tecnología' } },
];

export const growthGoalOptions: GrowthGoalOption[] = [
  { id: 'prepare-for-promotion', title: { en: 'Prepare for a promotion', es: 'Prepararme para un ascenso' } },
  {
    id: 'more-effective-current-role',
    title: { en: 'Become more effective in my current role', es: 'Ser más efectivo en mi rol actual' },
  },
  { id: 'develop-ai-capabilities', title: { en: 'Develop AI capabilities', es: 'Desarrollar capacidades en IA' } },
  { id: 'strengthen-leadership', title: { en: 'Strengthen leadership', es: 'Fortalecer mi liderazgo' } },
  {
    id: 'increase-financial-knowledge',
    title: { en: 'Increase financial knowledge', es: 'Aumentar mi conocimiento financiero' },
  },
  {
    id: 'explore-different-career',
    title: { en: 'Explore a different career path', es: 'Explorar una ruta de carrera diferente' },
  },
  { id: 'improve-communication', title: { en: 'Improve communication', es: 'Mejorar mi comunicación' } },
];

export const motivationOptions: MotivationOption[] = [
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

/** `null` future/empty arrays represent a new employee who has not yet
 *  completed the Future Self / direction wizard (State A).
 */
export async function getPersonalDirection(): Promise<PersonalDirection> {
  return {
    futureSelfId: null,
    goalIds: [],
    motivationIds: [],
  };
}
