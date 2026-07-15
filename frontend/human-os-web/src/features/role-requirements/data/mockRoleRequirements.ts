import type { JobRole } from '../types';

/** TODO: Replace with a real Azure Function once role-requirement catalog
 *  endpoints exist. Capability catalog and PersonCapability already exist
 *  in the backend (CapabilityService/PersonCapabilityService) — role
 *  requirements would map a JobRole to a set of required Capabilities plus
 *  a required level, layered on top of that existing catalog.
 */
export async function getRoleRequirements(): Promise<JobRole> {
  return {
    id: 'role-product-owner',
    name: { en: 'Product Owner', es: 'Product Owner' },
    requirements: [
      {
        id: 'req-product-strategy',
        name: { en: 'Product Strategy', es: 'Estrategia de Producto' },
        category: 'core',
        requiredLevel: 4,
        currentLevel: 3,
        hasEvidence: true,
      },
      {
        id: 'req-backlog-management',
        name: { en: 'Backlog Management', es: 'Gestión del Backlog' },
        category: 'core',
        requiredLevel: 4,
        currentLevel: 4,
        hasEvidence: true,
      },
      {
        id: 'req-stakeholder-communication',
        name: { en: 'Stakeholder Communication', es: 'Comunicación con Interesados' },
        category: 'core',
        requiredLevel: 4,
        currentLevel: 3,
        hasEvidence: true,
      },
      {
        id: 'req-agile-delivery',
        name: { en: 'Agile Delivery', es: 'Entrega Ágil' },
        category: 'core',
        requiredLevel: 3,
        currentLevel: 3,
        hasEvidence: true,
      },
      {
        id: 'req-financial-literacy',
        name: { en: 'Financial Literacy', es: 'Alfabetización Financiera' },
        category: 'core',
        requiredLevel: 3,
        currentLevel: 1,
        hasEvidence: false,
      },
      {
        id: 'req-ai-assisted-product-development',
        name: { en: 'AI-Assisted Product Development', es: 'Desarrollo de Producto Asistido por IA' },
        category: 'core',
        requiredLevel: 4,
        currentLevel: 2,
        hasEvidence: false,
      },
      {
        id: 'req-data-informed-decisions',
        name: { en: 'Data-Informed Decision Making', es: 'Decisiones Basadas en Datos' },
        category: 'core',
        requiredLevel: 3,
        currentLevel: 2,
        hasEvidence: false,
      },
      {
        id: 'req-hr-policies',
        name: { en: 'HR Policies', es: 'Políticas de Recursos Humanos' },
        category: 'policy',
        requiredLevel: 3,
        currentLevel: 3,
        hasEvidence: true,
      },
      {
        id: 'req-data-privacy-policy',
        name: { en: 'Data Privacy Policy', es: 'Política de Privacidad de Datos' },
        category: 'policy',
        requiredLevel: 3,
        currentLevel: 1,
        hasEvidence: false,
      },
      {
        id: 'req-cybersecurity-policy',
        name: { en: 'Cybersecurity Policy', es: 'Política de Ciberseguridad' },
        category: 'policy',
        requiredLevel: 3,
        currentLevel: 2,
        hasEvidence: false,
      },
      {
        id: 'req-emerging-ai-patterns',
        name: { en: 'Emerging AI Product Patterns', es: 'Patrones Emergentes de Producto con IA' },
        category: 'futureReady',
        requiredLevel: 3,
        currentLevel: 1,
        hasEvidence: false,
      },
    ],
  };
}
