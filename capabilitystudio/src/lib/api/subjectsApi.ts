import { apiGet } from './httpClient';
import i18n from '../../i18n';
import { getLaunchSession } from '../../auth/launchSession';

/** Backend response shape (PascalCase) — see SubjectResponse.cs. The
 * student-facing topical browsing axis (Matematicas, Finanzas, Cocina...),
 * distinct from CapabilityDomain. */
export interface Subject {
  SubjectId: string;
  Code: string;
  Name: string;
  Description: string | null;
}

/** Business subjects relevant to corporate authoring. Family/global subjects
 * such as cooking remain available to non-enterprise Studio launches. */
const ENTERPRISE_SUBJECT_CODES = new Set([
  'negocios',
  'finanzas',
  'recursos-humanos',
  'leyes',
  'tecnologia',
  'inventario',
  'gestion',
  'operaciones',
  'compras',
  'ventas',
  'marketing',
  'contabilidad',
  'servicio-al-cliente',
  'logistica',
  'cumplimiento',
  'gestion-de-proyectos',
  'manufactura',
  'cadena-de-suministro',
  'calidad',
  'ingenieria',
  'construccion',
  'bienes-raices',
  'seguros',
  'banca',
]);

const ENTERPRISE_SUBJECT_FALLBACKS: Subject[] = [
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000001',
    Code: 'inventario',
    Name: 'Inventory',
    Description: 'Stock control, warehouse operations and inventory planning.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000002',
    Code: 'gestion',
    Name: 'Management',
    Description: 'Leadership, administration and organizational management.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000003',
    Code: 'operaciones',
    Name: 'Operations',
    Description: 'Business operations, processes and service delivery.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000004',
    Code: 'compras',
    Name: 'Purchasing',
    Description: 'Purchasing, sourcing, suppliers and procurement strategy.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000005',
    Code: 'ventas',
    Name: 'Sales',
    Description: 'Sales strategy, account management and commercial execution.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000006',
    Code: 'marketing',
    Name: 'Marketing',
    Description: 'Marketing strategy, customers, brands and campaigns.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000007',
    Code: 'contabilidad',
    Name: 'Accounting',
    Description: 'Accounting, financial reporting and bookkeeping.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000008',
    Code: 'servicio-al-cliente',
    Name: 'Customer Service',
    Description: 'Customer support, service quality and experience.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000009',
    Code: 'logistica',
    Name: 'Transportation',
    Description: 'Transportation, fleet, distribution and delivery operations.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-00000000000a',
    Code: 'cumplimiento',
    Name: 'Compliance',
    Description: 'Policies, controls, risk and regulatory compliance.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-00000000000b',
    Code: 'gestion-de-proyectos',
    Name: 'Project Management',
    Description: 'Planning, delivery, collaboration and project governance.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-00000000000c',
    Code: 'manufactura',
    Name: 'Manufacturing',
    Description: 'Production planning, processes, equipment and manufacturing systems.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-00000000000d',
    Code: 'cadena-de-suministro',
    Name: 'Supply Chain',
    Description: 'Sourcing, planning, logistics and end-to-end supply chain management.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-00000000000e',
    Code: 'calidad',
    Name: 'Quality',
    Description: 'Quality systems, audits, continuous improvement and standards.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-00000000000f',
    Code: 'ingenieria',
    Name: 'Engineering',
    Description: 'Engineering design, technical systems and applied problem solving.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000010',
    Code: 'construccion',
    Name: 'Construction',
    Description: 'Construction planning, project delivery, safety and site operations.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000011',
    Code: 'bienes-raices',
    Name: 'Real Estate',
    Description: 'Property development, transactions, leasing and asset management.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000012',
    Code: 'seguros',
    Name: 'Insurance',
    Description: 'Insurance products, underwriting, claims and risk management.',
  },
  {
    SubjectId: 'a0000000-0000-0000-0000-000000000013',
    Code: 'banca',
    Name: 'Banking',
    Description: 'Banking products, financial services and regulatory operations.',
  },
];

const ENTERPRISE_SUBJECT_FALLBACK_TRANSLATIONS: Record<string, Pick<Subject, 'Name' | 'Description'>> = {
  inventario: { Name: 'Inventario', Description: 'Control de existencias, operaciones de almacén y planificación de inventario.' },
  gestion: { Name: 'Gestión', Description: 'Liderazgo, administración y gestión organizacional.' },
  operaciones: { Name: 'Operaciones', Description: 'Operaciones empresariales, procesos y entrega de servicios.' },
  compras: { Name: 'Compras', Description: 'Adquisiciones, abastecimiento, proveedores y estrategia de compras.' },
  ventas: { Name: 'Ventas', Description: 'Estrategia comercial, gestión de cuentas y ejecución de ventas.' },
  marketing: { Name: 'Marketing', Description: 'Estrategia de marketing, clientes, marcas y campañas.' },
  contabilidad: { Name: 'Contabilidad', Description: 'Contabilidad, reportes financieros y registro contable.' },
  'servicio-al-cliente': { Name: 'Servicio al Cliente', Description: 'Atención al cliente, calidad del servicio y experiencia.' },
  logistica: { Name: 'Transporte', Description: 'Transporte, flotillas, distribución y operaciones de entrega.' },
  cumplimiento: { Name: 'Cumplimiento', Description: 'Políticas, controles, riesgos y cumplimiento normativo.' },
  'gestion-de-proyectos': { Name: 'Gestión de Proyectos', Description: 'Planeación, entrega, colaboración y gobierno de proyectos.' },
  manufactura: { Name: 'Manufactura', Description: 'Planeación de producción, procesos, equipos y sistemas de manufactura.' },
  'cadena-de-suministro': { Name: 'Cadena de Suministro', Description: 'Abastecimiento, planeación, logística y gestión integral de la cadena de suministro.' },
  calidad: { Name: 'Calidad', Description: 'Sistemas de calidad, auditorías, mejora continua y estándares.' },
  ingenieria: { Name: 'Ingeniería', Description: 'Diseño de ingeniería, sistemas técnicos y resolución aplicada de problemas.' },
  construccion: { Name: 'Construcción', Description: 'Planeación de construcción, entrega de proyectos, seguridad y operaciones en obra.' },
  'bienes-raices': { Name: 'Bienes Raíces', Description: 'Desarrollo inmobiliario, transacciones, arrendamiento y gestión de activos.' },
  seguros: { Name: 'Seguros', Description: 'Productos de seguros, suscripción, siniestros y gestión de riesgos.' },
  banca: { Name: 'Banca', Description: 'Productos bancarios, servicios financieros y operaciones regulatorias.' },
};

function filterSubjectsForLaunch(subjects: Subject[]): Subject[] {
  const sourceAppType = getLaunchSession()?.claims.sourceAppType?.toLowerCase();
  if (sourceAppType !== 'enterprise') return subjects;

  const filtered = subjects.filter((subject) => ENTERPRISE_SUBJECT_CODES.has(subject.Code.toLowerCase()));
  const existingCodes = new Set(filtered.map((subject) => subject.Code.toLowerCase()));
  const useSpanish = i18n.language.toLowerCase().startsWith('es');
  const fallbackSubjects = ENTERPRISE_SUBJECT_FALLBACKS
    .filter((subject) => !existingCodes.has(subject.Code))
    .map((subject) => {
      const translation = useSpanish ? ENTERPRISE_SUBJECT_FALLBACK_TRANSLATIONS[subject.Code] : undefined;
      return translation ? { ...subject, ...translation } : subject;
    });

  return [...filtered, ...fallbackSubjects];
}

/** BUG FIX (2026-08-23): this used to default to 'es' unconditionally, so
 * every caller that omitted `language` (almost all of them) always got
 * Spanish subject names back from the backend regardless of the Studio
 * UI's own selected language (EN/ES toggle in the header). Default now
 * follows i18n's CURRENT language at call time instead. Enterprise launches
 * additionally receive only company-relevant domains. */
export function getSubjects(language: string = i18n.language): Promise<Subject[]> {
  return apiGet<Subject[]>(`/subjects?language=${language}`).then(filterSubjectsForLaunch);
}
