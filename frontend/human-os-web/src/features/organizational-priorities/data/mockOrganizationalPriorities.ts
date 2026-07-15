import type { OrganizationalInitiative } from '../types';

/** TODO: Replace with a real Azure Function once an organizational
 *  initiative catalog exists. Filtering to only what's relevant to this
 *  employee's role/department/goals would happen server-side once real
 *  data exists; for now this mock already returns only the pre-filtered,
 *  relevant subset (not the full company-wide initiative list).
 */
export async function getRelevantOrganizationalInitiatives(): Promise<OrganizationalInitiative[]> {
  return [
    {
      id: 'org-ai-transformation',
      name: { en: 'Enterprise AI Transformation', es: 'Transformación de IA Empresarial' },
      whyItMattersToOrg: {
        en: 'Contoso is redesigning core products around AI-assisted workflows to stay competitive.',
        es: 'Contoso está rediseñando sus productos principales con flujos asistidos por IA para mantenerse competitivo.',
      },
      whyItMattersToYou: {
        en: 'Product Owners who can lead AI-assisted product development will shape what comes next.',
        es: 'Los Product Owners que puedan liderar el desarrollo de producto asistido por IA definirán lo que sigue.',
      },
      requiredCapabilities: [
        { en: 'AI Product Development', es: 'Desarrollo de Producto con IA' },
        { en: 'Critical Thinking', es: 'Pensamiento Crítico' },
        { en: 'Data-Informed Decision Making', es: 'Decisiones Basadas en Datos' },
      ],
    },
    {
      id: 'org-future-leadership',
      name: { en: 'Future Leadership Pipeline', es: 'Cantera de Liderazgo Futuro' },
      whyItMattersToOrg: {
        en: 'Contoso is building a bench of leaders ready to guide cross-functional product teams.',
        es: 'Contoso está formando un grupo de líderes listos para guiar equipos de producto multifuncionales.',
      },
      whyItMattersToYou: {
        en: 'Strong stakeholder communication and influence are core to your next career step.',
        es: 'Una comunicación e influencia sólidas con interesados son clave para tu próximo paso de carrera.',
      },
      requiredCapabilities: [
        { en: 'Stakeholder Communication', es: 'Comunicación con Interesados' },
        { en: 'Leadership', es: 'Liderazgo' },
      ],
    },
  ];
}
