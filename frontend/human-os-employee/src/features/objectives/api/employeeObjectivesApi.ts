// ── Employee Personal SMART Objectives & Department Alignment API / Storage Helper ──

export interface DepartmentObjectiveRef {
  id: string;
  title: string;
  description: string;
  targetQuarter: string;
  targetDate: string;
  progressPercentage: number;
  status: 'Active' | 'InProgress' | 'Completed' | 'Paused';
  strategicGoalTitle?: string | null;
  pillarTitle?: string | null;
  metricName?: string | null;
  currentValue?: number | null;
  targetValue?: number | null;
  unit?: string | null;
  departmentId?: string | null;
  departmentName?: string | null;
}

export interface EmployeeObjective {
  id: string;
  personId: string;
  tenantId: string;
  title: string;
  description: string;
  targetQuarter: string;
  targetDate: string;
  progressPercentage: number;
  status: 'Active' | 'InProgress' | 'Completed' | 'Paused';

  // SMART Criteria & Measurement
  metricName?: string | null;
  currentValue?: number | null;
  targetValue?: number | null;
  unit?: string | null;
  measurementMethod?: string | null;
  actionPlan?: string | null;
  businessImpact?: string | null;

  // Recommended Skills to complete this objective
  recommendedTechnicalSkills?: string[] | null;
  recommendedSoftSkills?: string[] | null;
  recommendationReasoning?: string | null;

  // Department Alignment
  departmentObjectiveId?: string | null;
  departmentObjectiveTitle?: string | null;
  departmentName?: string | null;
  strategicGoalTitle?: string | null;
}

// ── Quarterly Growth Recommendations & Improvement Areas ───────────────────

export interface GrowthActionItem {
  id: string;
  text: string;
  completed: boolean;
}

export interface EmployeeGrowthRecommendation {
  id: string;
  personId: string;
  tenantId: string;
  quarter: string; // e.g. "Q1 2026", "Q4 2026"
  year: number;
  title: string;
  skillType: 'technical' | 'soft';
  skillName: string;
  recommendationText: string;
  managerName: string;
  managerTitle: string;
  assignedDate: string;
  status: 'NotStarted' | 'InProgress' | 'Applied' | 'Mastered';
  progressPercentage: number;

  // Alignment with Objectives & Courses
  linkedObjectiveId?: string | null;
  linkedObjectiveTitle?: string | null;
  linkedCourseId?: string | null;
  linkedCourseTitle?: string | null;
  linkedCourseType?: 'structured-learning' | 'graph' | null;

  // Measurement Criteria & Actionable Milestones
  actionItems: GrowthActionItem[];
  metricName?: string | null;
  currentValue?: string | null;
  targetValue?: string | null;
  quarterlyReflectionNotes?: string | null;
}

// ── Default Department Objectives Fallback (if not customized in Enterprise) ──

export const DEFAULT_LEGAL_OBJECTIVES_ES: DepartmentObjectiveRef[] = [
  {
    id: 'd-obj-legal-1',
    title: 'Implementar y Auditar el Sistema de Prevención e Identificación de Operaciones con Recursos de Procedencia Ilícita (PLD/AML)',
    description: 'Establecer y supervisar la matriz de riesgos, expedientes de debida diligencia de clientes y reportes oportunos conforme a la Ley Federal para la Prevención e Identificación de Operaciones con Recursos de Procedencia Ilícita (LFPIORPI).',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 40,
    status: 'Active',
    departmentId: '4aff08f5-0cd9-415d-9c81-9cdc03ea4212',
    departmentName: 'Legal',
    strategicGoalTitle: 'Certificación en Seguridad y Cumplimiento Normativo',
    pillarTitle: 'Excelencia Operativa & Calidad',
    metricName: 'Cumplimiento en Expedientes y Reportes de PLD/AML',
    currentValue: 75,
    targetValue: 100,
    unit: '%',
  },
  {
    id: 'd-obj-legal-2',
    title: 'Garantizar el 100% de Blindaje Contractual y Cumplimiento Normativo Corporativo',
    description: 'Revisar, estandarizar y auditar contratos marco, acuerdos de confidencialidad y términos de servicio, asegurando mitigación de contingencias legales y regulatorias.',
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-30',
    progressPercentage: 70,
    status: 'InProgress',
    departmentId: '4aff08f5-0cd9-415d-9c81-9cdc03ea4212',
    departmentName: 'Legal',
    strategicGoalTitle: 'Certificación en Seguridad y Cumplimiento Normativo',
    pillarTitle: 'Excelencia Operativa & Calidad',
    metricName: 'Contratos Auditados con Blindaje Legal Completo',
    currentValue: 85,
    targetValue: 100,
    unit: '%',
  },
  {
    id: 'd-obj-legal-3',
    title: 'Auditoría de Protección de Datos Personales y Privacidad (LFPDPPP)',
    description: 'Alinear todos los procesos operativos y plataformas tecnológicas a la normativa de protección de datos personales en posesión de particulares, asegurando derechos ARCO y avisos de privacidad actualizados.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-30',
    progressPercentage: 50,
    status: 'Active',
    departmentId: '4aff08f5-0cd9-415d-9c81-9cdc03ea4212',
    departmentName: 'Legal',
    strategicGoalTitle: 'Certificación en Seguridad y Cumplimiento Normativo',
    pillarTitle: 'Excelencia Operativa & Calidad',
    metricName: 'Procesos con Cumplimiento Certificado de Privacidad',
    currentValue: 50,
    targetValue: 100,
    unit: '%',
  },
];

export const DEFAULT_LEGAL_OBJECTIVES_EN: DepartmentObjectiveRef[] = [
  {
    id: 'd-obj-legal-1',
    title: 'Implement and Audit Anti-Money Laundering & Illicit Funds Prevention System (AML/CFT)',
    description: 'Establish and supervise the risk matrix, customer due diligence (CDD) files, and timely regulatory reporting in compliance with the Federal Law for Prevention and Identification of Operations with Illicit Resources.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 40,
    status: 'Active',
    departmentId: '4aff08f5-0cd9-415d-9c81-9cdc03ea4212',
    departmentName: 'Legal',
    strategicGoalTitle: 'Security and Compliance Certification',
    pillarTitle: 'Operational Excellence & Quality',
    metricName: 'AML/CFT Identification Files & Regulatory Reports Compliance',
    currentValue: 75,
    targetValue: 100,
    unit: '%',
  },
  {
    id: 'd-obj-legal-2',
    title: 'Ensure 100% Contractual Shielding and Corporate Regulatory Compliance',
    description: 'Review, standardize, and audit master agreements, NDAs, and terms of service, minimizing legal contingencies and regulatory liabilities.',
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-30',
    progressPercentage: 70,
    status: 'InProgress',
    departmentId: '4aff08f5-0cd9-415d-9c81-9cdc03ea4212',
    departmentName: 'Legal',
    strategicGoalTitle: 'Security and Compliance Certification',
    pillarTitle: 'Operational Excellence & Quality',
    metricName: 'Audited Contracts with Full Legal Shielding',
    currentValue: 85,
    targetValue: 100,
    unit: '%',
  },
  {
    id: 'd-obj-legal-3',
    title: 'Personal Data Protection and Privacy Compliance Audit (Data Privacy)',
    description: 'Align corporate workflows and platforms with personal data privacy regulations, guaranteeing ARCO rights handling and compliant privacy disclosures.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-30',
    progressPercentage: 50,
    status: 'Active',
    departmentId: '4aff08f5-0cd9-415d-9c81-9cdc03ea4212',
    departmentName: 'Legal',
    strategicGoalTitle: 'Security and Compliance Certification',
    pillarTitle: 'Operational Excellence & Quality',
    metricName: 'Workflows with Certified Privacy Compliance',
    currentValue: 50,
    targetValue: 100,
    unit: '%',
  },
];

export const DEFAULT_SOFTWARE_ENG_OBJECTIVES_ES: DepartmentObjectiveRef[] = [
  {
    id: 'd-obj-ai-org',
    title: 'Implementar Agentes de IA para la Organización',
    description: 'Diseñar, integrar y desplegar agentes inteligentes autónomos en procesos clave para automatizar flujos operativos y elevar la productividad del equipo.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 33,
    status: 'Active',
    departmentId: '6c95b6dc-e89b-4b0d-ad89-dfd67e782b12',
    departmentName: 'Software Engineering',
    strategicGoalTitle: 'Implementar Agentes de IA en Procesos Clave de Operación',
    pillarTitle: 'Innovación & Transformación Tecnológica',
    metricName: 'Agentes de IA en Producción',
    currentValue: 1,
    targetValue: 3,
    unit: 'Agentes',
  },
  {
    id: 'd-obj-se-1',
    title: 'Desplegar Agentes de IA en el Pipeline de CI/CD para Revisión Automatizada de Código',
    description: 'Implementar agentes autónomos basados en Inteligencia Artificial para análisis estático, generación automática de pruebas unitarias y detección de vulnerabilidades previas al merge.',
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-30',
    progressPercentage: 65,
    status: 'InProgress',
    departmentId: '6c95b6dc-e89b-4b0d-ad89-dfd67e782b12',
    departmentName: 'Software Engineering',
    strategicGoalTitle: 'Implementar Agentes de IA en Procesos Clave de Operación',
    pillarTitle: 'Innovación & Transformación Tecnológica',
    metricName: 'Cobertura de PRs con Code Review Autónomo por IA',
    currentValue: 65,
    targetValue: 100,
    unit: '%',
  },
  {
    id: 'd-obj-se-2',
    title: 'Optimizar Latencia P95 y Resiliencia de Microservicios Críticos',
    description: 'Refactorizar consultas de base de datos asíncronas, indexación estratégica y caching en Redis para garantizar alta disponibilidad y tiempos de respuesta mínimos.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-30',
    progressPercentage: 45,
    status: 'Active',
    departmentId: '6c95b6dc-e89b-4b0d-ad89-dfd67e782b12',
    departmentName: 'Software Engineering',
    strategicGoalTitle: 'Certificación en Seguridad y Cumplimiento Normativo',
    pillarTitle: 'Excelencia Operativa & Calidad',
    metricName: 'Latencia P95 de APIs Backend',
    currentValue: 320,
    targetValue: 150,
    unit: 'ms',
  },
];

export const DEFAULT_SOFTWARE_ENG_OBJECTIVES_EN: DepartmentObjectiveRef[] = [
  {
    id: 'd-obj-ai-org',
    title: 'Deploy AI Agents Across the Organization',
    description: 'Design, integrate, and deploy autonomous AI agents across core workflows to automate repetitive tasks and boost team productivity.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 33,
    status: 'Active',
    departmentId: '6c95b6dc-e89b-4b0d-ad89-dfd67e782b12',
    departmentName: 'Software Engineering',
    strategicGoalTitle: 'Deploy AI Agents Across Key Operational Workflows',
    pillarTitle: 'Innovation & Technological Transformation',
    metricName: 'Production AI Agents Deployed',
    currentValue: 1,
    targetValue: 3,
    unit: 'Agents',
  },
  {
    id: 'd-obj-se-1',
    title: 'Deploy AI Agents Across the CI/CD Pipeline for Automated Code Review',
    description: 'Implement AI autonomous agents for static analysis, unit test generation, and security pre-validation before merging into core repositories.',
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-30',
    progressPercentage: 65,
    status: 'InProgress',
    departmentId: '6c95b6dc-e89b-4b0d-ad89-dfd67e782b12',
    departmentName: 'Software Engineering',
    strategicGoalTitle: 'Deploy AI Agents Across Key Operational Workflows',
    pillarTitle: 'Innovation & Technological Transformation',
    metricName: 'PR Coverage with Autonomous AI Code Review',
    currentValue: 65,
    targetValue: 100,
    unit: '%',
  },
  {
    id: 'd-obj-se-2',
    title: 'Optimize P95 Latency & Reliability on Core Microservices',
    description: 'Refactor asynchronous database queries, index tuning, and distributed Redis caching for ultra-responsive API performance.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-30',
    progressPercentage: 45,
    status: 'Active',
    departmentId: '6c95b6dc-e89b-4b0d-ad89-dfd67e782b12',
    departmentName: 'Software Engineering',
    strategicGoalTitle: 'Security and Compliance Certification',
    pillarTitle: 'Operational Excellence & Quality',
    metricName: 'Backend API P95 Response Latency',
    currentValue: 320,
    targetValue: 150,
    unit: 'ms',
  },
];

export const DEFAULT_DEPT_OBJECTIVES_ES = DEFAULT_SOFTWARE_ENG_OBJECTIVES_ES;
export const DEFAULT_DEPT_OBJECTIVES_EN = DEFAULT_SOFTWARE_ENG_OBJECTIVES_EN;

export const DEFAULT_FINANCE_OBJECTIVES_ES: DepartmentObjectiveRef[] = [
  {
    id: 'd-obj-fin-1',
    title: 'Optimización del Flujo de Caja y Automatización de Conciliaciones Bancarias',
    description: 'Reducir el ciclo de cobro y automatizar el 90% de las conciliaciones bancarias y de cuentas por cobrar.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 55,
    status: 'Active',
    departmentId: '62180296-c4c1-4a99-ad63-415907867a5c',
    departmentName: 'Finance',
    strategicGoalTitle: 'Excelencia Operativa & Calidad',
    pillarTitle: 'Excelencia Operativa & Calidad',
    metricName: 'Conciliaciones Automatizadas',
    currentValue: 60,
    targetValue: 90,
    unit: '%',
  },
];

export const DEFAULT_FINANCE_OBJECTIVES_EN: DepartmentObjectiveRef[] = [
  {
    id: 'd-obj-fin-1',
    title: 'Cash Flow Optimization & Automated Bank Reconciliation',
    description: 'Reduce accounts receivable cycle and automate 90% of bank reconciliation workflows.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 55,
    status: 'Active',
    departmentId: '62180296-c4c1-4a99-ad63-415907867a5c',
    departmentName: 'Finance',
    strategicGoalTitle: 'Operational Excellence & Quality',
    pillarTitle: 'Operational Excellence & Quality',
    metricName: 'Automated Reconciliations',
    currentValue: 60,
    targetValue: 90,
    unit: '%',
  },
];

export const DEFAULT_HR_OBJECTIVES_ES: DepartmentObjectiveRef[] = [
  {
    id: 'd-obj-hr-1',
    title: 'Alcanzar 95% de Cobertura en Planes de Carrera y Evaluación de Desempeño',
    description: 'Garantizar que todos los colaboradores cuenten con su plan de desarrollo de competencias y evaluación de objetivos SMART.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 80,
    status: 'InProgress',
    departmentId: 'fd64e0c0-ff82-45ee-aafa-2f2d0da7b2cd',
    departmentName: 'Retail & People (HR)',
    strategicGoalTitle: 'Alcanzar 95% de Cobertura en Planes de Capacitación',
    pillarTitle: 'Crecimiento de Talento & Capacidades',
    metricName: 'Colaboradores con Plan de Carrera Activo',
    currentValue: 80,
    targetValue: 95,
    unit: '%',
  },
];

export const DEFAULT_HR_OBJECTIVES_EN: DepartmentObjectiveRef[] = [
  {
    id: 'd-obj-hr-1',
    title: 'Achieve 95% Coverage on Career Plans and SMART Performance Reviews',
    description: 'Ensure all team members have an active competency development roadmap and SMART goals.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 80,
    status: 'InProgress',
    departmentId: 'fd64e0c0-ff82-45ee-aafa-2f2d0da7b2cd',
    departmentName: 'Retail & People (HR)',
    strategicGoalTitle: 'Achieve 95% Coverage on Employee Training Plans',
    pillarTitle: 'Talent & Capability Growth',
    metricName: 'Staff with Active Career Plan',
    currentValue: 80,
    targetValue: 95,
    unit: '%',
  },
];

// ── Default Employee SMART Objectives ────────────────────────────────────────

export const DEFAULT_EMPLOYEE_OBJECTIVES_ES: EmployeeObjective[] = [
  {
    id: 'emp-obj-1',
    personId: 'b144127a-5d4e-4783-91ce-4f973b3b8004',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    title: 'Desarrollar el Subagente de Verificación Automática de Código y Tests Unitarios',
    description: 'Diseñar y programar el agente inteligente en C# (.NET 10) que analiza cambios de código contra el estándar de arquitectura y genera tests unitarios con >85% de cobertura.',
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-25',
    progressPercentage: 70,
    status: 'InProgress',
    departmentObjectiveId: 'd-obj-se-1',
    departmentObjectiveTitle: 'Desplegar Agentes de IA en el Pipeline de CI/CD para Revisión Automatizada de Código',
    departmentName: 'Software Engineering',
    strategicGoalTitle: 'Implementar Agentes de IA en Procesos Clave de Operación',
    metricName: 'Porcentaje de Tests Unitarios Autogenerados con Éxito',
    currentValue: 70,
    targetValue: 100,
    unit: '%',
    measurementMethod: 'Ejecución de test suites automáticas en el runner de GitHub Actions con reportes xUnit.',
    actionPlan: '1. Diseñar prompts estructurados con Microsoft Agents SDK.\n2. Crear suite de validación de sintaxis Roslyn.\n3. Integrar con el repositorio de HumanOS backend.',
    businessImpact: 'Disminuye el tiempo de desarrollo de pruebas en 5 horas semanales por desarrollador.',
  },
  {
    id: 'emp-obj-2',
    personId: 'b144127a-5d4e-4783-91ce-4f973b3b8004',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    title: 'Completar Certificación en Arquitectura Segura en la Nube y OWASP',
    description: 'Aprobar el curso de seguridad de la información corporativa y aplicar los principios de Zero Trust en todos los endpoints públicos de la API.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-15',
    progressPercentage: 40,
    status: 'Active',
    departmentObjectiveId: 'd-obj-se-2',
    departmentObjectiveTitle: 'Optimizar Latencia P95 y Resiliencia de Microservicios Críticos',
    departmentName: 'Software Engineering',
    strategicGoalTitle: 'Certificación en Seguridad y Cumplimiento Normativo',
    metricName: 'Módulos de Seguridad Aprobados',
    currentValue: 2,
    targetValue: 5,
    unit: 'módulos',
    measurementMethod: 'Registro de completitud y exámenes aprobados en Engram Academy.',
    actionPlan: 'Dedicar 2 horas semanales a la plataforma de aprendizaje y realizar auditoría de dependencias NuGet.',
    businessImpact: 'Garantiza el 100% de cumplimiento en auditorías de seguridad externas.',
  },
];

export const DEFAULT_EMPLOYEE_OBJECTIVES_EN: EmployeeObjective[] = [
  {
    id: 'emp-obj-1',
    personId: 'b144127a-5d4e-4783-91ce-4f973b3b8004',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    title: 'Build the Autonomous Code Verification & Unit Test Subagent',
    description: 'Architect and implement the C# (.NET 10) AI agent that validates code pull requests against architecture patterns and automatically generates unit tests.',
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-25',
    progressPercentage: 70,
    status: 'InProgress',
    departmentObjectiveId: 'd-obj-se-1',
    departmentObjectiveTitle: 'Deploy AI Agents Across the CI/CD Pipeline for Automated Code Review',
    departmentName: 'Software Engineering',
    strategicGoalTitle: 'Deploy AI Agents Across Key Operational Workflows',
    metricName: 'Percentage of Unit Tests Successfully Autogenerated',
    currentValue: 70,
    targetValue: 100,
    unit: '%',
    measurementMethod: 'Automated test suite execution in GitHub Actions runner with xUnit test result telemetry.',
    actionPlan: '1. Design structured prompts using Microsoft Agents SDK.\n2. Implement Roslyn syntax validator.\n3. Integrate with HumanOS backend repo.',
    businessImpact: 'Saves 5 engineering hours per week per developer in unit test authoring.',
  },
  {
    id: 'emp-obj-2',
    personId: 'b144127a-5d4e-4783-91ce-4f973b3b8004',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    title: 'Complete Cloud Security & OWASP Top 10 Certification',
    description: 'Pass corporate information security courses and apply Zero Trust validation principles across all public API endpoints.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-15',
    progressPercentage: 40,
    status: 'Active',
    departmentObjectiveId: 'd-obj-se-2',
    departmentObjectiveTitle: 'Optimize P95 Latency & Reliability on Core Microservices',
    departmentName: 'Software Engineering',
    strategicGoalTitle: 'Security and Compliance Certification',
    metricName: 'Security Modules Mastered',
    currentValue: 2,
    targetValue: 5,
    unit: 'modules',
    measurementMethod: 'Progress record and passed node assessments in Engram Academy.',
    actionPlan: 'Dedicate 2 hours per week on Engram portal and conduct automated NuGet vulnerability scanning.',
    businessImpact: 'Ensures 100% compliance during external security audits.',
  },
];

export const DEFAULT_LEGAL_EMPLOYEE_OBJECTIVES_ES: EmployeeObjective[] = [
  {
    id: 'emp-obj-legal-1',
    personId: 'ea9fe131-2b3a-4f65-989e-306f16266ddb',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    title: 'Auditar el 100% de Expedientes de Identificación de Clientes conforme a la LFPIORPI (PLD)',
    description: 'Realizar la revisión, integración y verificación exhaustiva de expedientes de debida diligencia de clientes y beneficiarios controladores para actividades vulnerables conforme a la Ley Antilavado.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-15',
    progressPercentage: 65,
    status: 'InProgress',
    departmentObjectiveId: 'd-obj-legal-1',
    departmentObjectiveTitle: 'Implementar y Auditar el Sistema de Prevención e Identificación de Operaciones con Recursos de Procedencia Ilícita (PLD/AML)',
    departmentName: 'Legal',
    strategicGoalTitle: 'Certificación en Seguridad y Cumplimiento Normativo',
    metricName: 'Porcentaje de Expedientes de Clientes Verificados y Auditados',
    currentValue: 65,
    targetValue: 100,
    unit: '%',
    measurementMethod: 'Matriz de control interno de debida diligencia y checklist de expediente único de identificación.',
    actionPlan: '1. Requerir documentación faltante a clientes con operaciones activas.\n2. Cotejar identificación oficial y comprobantes de domicilio en sistema.\n3. Elaborar dictamen mensual de expedientes completos para el oficial de cumplimiento.',
    businessImpact: 'Cero multas o requerimientos por parte de la UIF/SAT y blindaje total ante auditorías normativas.',
    recommendedTechnicalSkills: [
      'Mexico Prevención e Identificación de Operaciones con Recursos de Procedencia Ilícita',
      'Cumplimiento Normativo y Auditoría Legal',
      'Debida Diligencia de Clientes (KYC/CDD)',
    ],
    recommendedSoftSkills: [
      'Atención al detalle y rigor analítico',
      'Comunicación asertiva con clientes',
      'Resolución de problemas regulatorios',
    ],
    recommendationReasoning: 'Alineado con las obligaciones legales de la LFPIORPI y la mitigación de riesgos de lavado de dinero en el departamento legal.',
  },
  {
    id: 'emp-obj-legal-2',
    personId: 'ea9fe131-2b3a-4f65-989e-306f16266ddb',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    title: 'Completar la Capacitación Anual Obligatoria en Prevención de Lavado de Dinero y Financiamiento al Terrorismo',
    description: 'Aprobar al 100% el programa de certificación y actualización en normatividad mexicana de prevención e identificación de operaciones con recursos de procedencia ilícita.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-30',
    progressPercentage: 50,
    status: 'Active',
    departmentObjectiveId: 'd-obj-legal-1',
    departmentObjectiveTitle: 'Implementar y Auditar el Sistema de Prevención e Identificación de Operaciones con Recursos de Procedencia Ilícita (PLD/AML)',
    departmentName: 'Legal',
    strategicGoalTitle: 'Certificación en Seguridad y Cumplimiento Normativo',
    metricName: 'Módulos de Capacitación y Evaluación Aprobados',
    currentValue: 3,
    targetValue: 6,
    unit: 'módulos',
    measurementMethod: 'Registro de acreditación de evaluaciones y constancias de competencias en Engram.',
    actionPlan: 'Completar módulos de tipologías de operaciones inusuales, listas negras (OFAC/SAT 69-B) y reportes de 24 horas.',
    businessImpact: 'Acreditación formal del personal ante la autoridad supervisora y fortalecimiento de la cultura de compliance.',
    recommendedTechnicalSkills: [
      'Prevención de Lavado de Dinero y LFPIORPI',
      'Tipologías y Alertas Financieras',
    ],
    recommendedSoftSkills: [
      'Ética profesional e integridad',
      'Pensamiento crítico',
    ],
    recommendationReasoning: 'Obligación regulatoria anual de capacitación especializada para el personal asignado a prevención de operaciones ilícitas.',
  },
];

export const DEFAULT_LEGAL_EMPLOYEE_OBJECTIVES_EN: EmployeeObjective[] = [
  {
    id: 'emp-obj-legal-1',
    personId: 'ea9fe131-2b3a-4f65-989e-306f16266ddb',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    title: 'Audit 100% of Client Identification & KYC Files under AML Statutes',
    description: 'Execute thorough review, integration, and verification of customer due diligence (CDD) and ultimate beneficial ownership (UBO) records for vulnerable activities under Anti-Money Laundering legislation.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-15',
    progressPercentage: 65,
    status: 'InProgress',
    departmentObjectiveId: 'd-obj-legal-1',
    departmentObjectiveTitle: 'Implement and Audit Anti-Money Laundering & Illicit Funds Prevention System (AML/CFT)',
    departmentName: 'Legal',
    strategicGoalTitle: 'Security and Compliance Certification',
    metricName: 'Percentage of Client KYC Files Verified and Audited',
    currentValue: 65,
    targetValue: 100,
    unit: '%',
    measurementMethod: 'Internal customer due diligence control matrix and single identification file checklist.',
    actionPlan: '1. Request missing documentation from active clients.\n2. Reconcile official government IDs and proof of address.\n3. Prepare monthly audit certificate for the Chief Compliance Officer.',
    businessImpact: 'Zero fines or regulatory notices from oversight authorities and ironclad audit readiness.',
    recommendedTechnicalSkills: [
      'Anti-Money Laundering (AML/CFT) Regulatory Compliance',
      'Customer Due Diligence (KYC/CDD)',
      'Legal Compliance Audit',
    ],
    recommendedSoftSkills: [
      'Attention to Detail & Analytical Rigor',
      'Assertive Client Communication',
      'Regulatory Problem Solving',
    ],
    recommendationReasoning: 'Aligned with statutory AML/CFT mandates and operational risk mitigation for the Legal department.',
  },
  {
    id: 'emp-obj-legal-2',
    personId: 'ea9fe131-2b3a-4f65-989e-306f16266ddb',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    title: 'Complete Annual Mandatory Anti-Money Laundering & CFT Training',
    description: 'Achieve 100% completion in the annual regulatory training program on the prevention and detection of transactions with illicit funds.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-30',
    progressPercentage: 50,
    status: 'Active',
    departmentObjectiveId: 'd-obj-legal-1',
    departmentObjectiveTitle: 'Implement and Audit Anti-Money Laundering & Illicit Funds Prevention System (AML/CFT)',
    departmentName: 'Legal',
    strategicGoalTitle: 'Security and Compliance Certification',
    metricName: 'Training Modules & Assessments Completed',
    currentValue: 3,
    targetValue: 6,
    unit: 'modules',
    measurementMethod: 'Assessment records and competency badges in Engram platform.',
    actionPlan: 'Complete modules covering unusual transaction red flags, sanctions screening (OFAC/SAT), and 24-hr urgent alerts.',
    businessImpact: 'Formal accreditation before financial oversight authorities and reinforced corporate compliance culture.',
    recommendedTechnicalSkills: [
      'AML/CFT Statutes & Guidelines',
      'Financial Red Flags & Investigation',
    ],
    recommendedSoftSkills: [
      'Professional Ethics & Integrity',
      'Critical Thinking',
    ],
    recommendationReasoning: 'Mandatory annual training requirement for personnel managing vulnerable activities and anti-illicit compliance.',
  },
];

export const DEFAULT_LEGAL_GROWTH_RECOMMENDATIONS_ES: EmployeeGrowthRecommendation[] = [
  {
    id: 'rec-legal-1',
    personId: 'ea9fe131-2b3a-4f65-989e-306f16266ddb',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    quarter: 'Q4 2026',
    year: 2026,
    title: 'Especialización Avanzada en Identificación de Beneficiarios Controladores y Riesgo PLD',
    skillType: 'technical',
    skillName: 'Mexico Prevención e Identificación de Operaciones con Recursos de Procedencia Ilícita',
    recommendationText:
      'Para respaldar el objetivo de auditoría de expedientes PLD de este trimestre, te recomendamos profundizar en la regulación de Beneficiarios Controladores (CFF arts. 32-B Ter y LFPIORPI) y matrices de riesgo de operaciones vulnerables. Aprovecha el curso especializado en Engram para dominar los criterios de la UIF y el SAT.',
    managerName: 'Lic. Fernando Garza',
    managerTitle: 'Director Jurídico & Oficial de Cumplimiento',
    assignedDate: '2026-10-05',
    status: 'InProgress',
    progressPercentage: 65,
    linkedObjectiveId: 'emp-obj-legal-1',
    linkedObjectiveTitle: 'Auditar el 100% de Expedientes de Identificación de Clientes conforme a la LFPIORPI (PLD)',
    linkedCourseId: null,
    linkedCourseTitle: 'Mexico Prevención e Identificación de Operaciones con Recursos de Procedencia Ilícita',
    linkedCourseType: 'structured-learning',
    metricName: 'Porcentaje de Casos Complejos Dictaminados',
    currentValue: '12 de 15 casos dictaminados',
    targetValue: '100% de casos dictaminados en tiempo',
    actionItems: [
      { id: 'act-leg-1-1', text: 'Completar módulos de Debida Diligencia Reforzada en Engram', completed: true },
      { id: 'act-leg-1-2', text: 'Estandarizar formatos de declaración de beneficiario controlador en expedientes', completed: true },
      { id: 'act-leg-1-3', text: 'Verificar cotejo contra listas de personas bloqueadas y PEPs', completed: false },
      { id: 'act-leg-1-4', text: 'Impartir taller interno de 30 min sobre integración de expedientes de identificación', completed: false },
    ],
    quarterlyReflectionNotes:
      'El curso en Engram clarificó las reglas de integración para personas morales y fideicomisos, acelerando la dictaminación de expedientes en un 50%.',
  },
  {
    id: 'rec-legal-2',
    personId: 'ea9fe131-2b3a-4f65-989e-306f16266ddb',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    quarter: 'Q4 2026',
    year: 2026,
    title: 'Comunicación Asertiva en Auditorías y Negociación Regulatoria',
    skillType: 'soft',
    skillName: 'Comunicación Asertiva y Negociación',
    recommendationText:
      'Has demostrado un conocimiento normativo impecable. La oportunidad clave este trimestre es fortalecer la comunicación con clientes y áreas operativas al solicitar documentación sensible, logrando cooperación ágil sin generar fricción comercial.',
    managerName: 'Lic. Fernando Garza',
    managerTitle: 'Director Jurídico & Oficial de Cumplimiento',
    assignedDate: '2026-10-10',
    status: 'Applied',
    progressPercentage: 75,
    linkedObjectiveId: 'emp-obj-legal-1',
    linkedObjectiveTitle: 'Auditar el 100% de Expedientes de Identificación de Clientes conforme a la LFPIORPI (PLD)',
    linkedCourseId: null,
    linkedCourseTitle: 'Comunicación Asertiva en Compliance',
    linkedCourseType: 'graph',
    metricName: 'Tasa de Respuesta Oportuna de Clientes a Requerimientos de Documentación',
    currentValue: '85% de respuesta en primer contacto',
    targetValue: '95% de respuesta ágil',
    actionItems: [
      { id: 'act-leg-2-1', text: 'Rediseñar las guías amigables de requerimientos documentales para clientes', completed: true },
      { id: 'act-leg-2-2', text: 'Realizar sesiones de alineación con el equipo comercial para agilizar recolección', completed: true },
      { id: 'act-leg-2-3', text: 'Monitorear tiempo promedio de respuesta a solicitudes de actualización', completed: false },
    ],
    quarterlyReflectionNotes:
      'Las nuevas guías explicativas redujeron dudas de clientes y elevaron el porcentaje de recepción de expedientes completos en primera solicitud.',
  },
];

export const DEFAULT_LEGAL_GROWTH_RECOMMENDATIONS_EN: EmployeeGrowthRecommendation[] = [
  {
    id: 'rec-legal-1',
    personId: 'ea9fe131-2b3a-4f65-989e-306f16266ddb',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    quarter: 'Q4 2026',
    year: 2026,
    title: 'Advanced Specialization in Beneficial Ownership & AML Risk Mitigation',
    skillType: 'technical',
    skillName: 'Mexico Prevención e Identificación de Operaciones con Recursos de Procedencia Ilícita',
    recommendationText:
      'To support this quarter\'s AML compliance audit, we recommend mastering beneficial ownership verification rules and enhanced due diligence risk matrices. Leverage the dedicated Engram course to align with supervisory standards.',
    managerName: 'Fernando Garza, Esq.',
    managerTitle: 'Chief Compliance Officer & Legal Director',
    assignedDate: '2026-10-05',
    status: 'InProgress',
    progressPercentage: 65,
    linkedObjectiveId: 'emp-obj-legal-1',
    linkedObjectiveTitle: 'Audit 100% of Client Identification & KYC Files under AML Statutes',
    linkedCourseId: null,
    linkedCourseTitle: 'Mexico Prevención e Identificación de Operaciones con Recursos de Procedencia Ilícita',
    linkedCourseType: 'structured-learning',
    metricName: 'Complex Compliance Files Fully Adjudicated',
    currentValue: '12 of 15 cases resolved',
    targetValue: '100% timely adjudication',
    actionItems: [
      { id: 'act-leg-1-1', text: 'Complete Enhanced Due Diligence (EDD) modules in Engram', completed: true },
      { id: 'act-leg-1-2', text: 'Standardize Ultimate Beneficial Owner (UBO) disclosure statements', completed: true },
      { id: 'act-leg-1-3', text: 'Validate screening against PEP and sanctions watchlists', completed: false },
      { id: 'act-leg-1-4', text: 'Conduct 30-min internal workshop on customer identification file completeness', completed: false },
    ],
    quarterlyReflectionNotes:
      'The Engram training clarified corporate ownership structures and trust verification guidelines, cutting review cycles by 50%.',
  },
  {
    id: 'rec-legal-2',
    personId: 'ea9fe131-2b3a-4f65-989e-306f16266ddb',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    quarter: 'Q4 2026',
    year: 2026,
    title: 'Assertive Communication in Regulatory Audits & Compliance Negotiations',
    skillType: 'soft',
    skillName: 'Assertive Communication and Negotiation',
    recommendationText:
      'You possess exceptional legal rigor. The priority this quarter is refining communication with clients and commercial teams when requesting sensitive financial documentation, achieving prompt collaboration without business friction.',
    managerName: 'Fernando Garza, Esq.',
    managerTitle: 'Chief Compliance Officer & Legal Director',
    assignedDate: '2026-10-10',
    status: 'Applied',
    progressPercentage: 75,
    linkedObjectiveId: 'emp-obj-legal-1',
    linkedObjectiveTitle: 'Audit 100% of Client Identification & KYC Files under AML Statutes',
    linkedCourseId: null,
    linkedCourseTitle: 'Assertive Communication in Compliance',
    linkedCourseType: 'graph',
    metricName: 'Client Documentation Response Rate on First Notice',
    currentValue: '85% response on initial request',
    targetValue: '95% prompt response',
    actionItems: [
      { id: 'act-leg-2-1', text: 'Redesign client-facing explanation guides for compliance documentation', completed: true },
      { id: 'act-leg-2-2', text: 'Host coordination meetings with sales teams to streamline file intake', completed: true },
      { id: 'act-leg-2-3', text: 'Track average turnaround time for documentation refresh requests', completed: false },
    ],
    quarterlyReflectionNotes:
      'The revised explanatory guidance reduced client inquiries and accelerated first-pass complete submissions.',
  },
];

// ── Default Quarterly Growth Recommendations (Management Coaching) ──────────

export const DEFAULT_GROWTH_RECOMMENDATIONS_ES: EmployeeGrowthRecommendation[] = [
  {
    id: 'rec-1',
    personId: 'b144127a-5d4e-4783-91ce-4f973b3b8004',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    quarter: 'Q1 2026',
    year: 2026,
    title: 'Elevar Prácticas de Observabilidad y Trazabilidad en Microservicios',
    skillType: 'technical',
    skillName: 'API + Microservicios',
    recommendationText:
      'Para respaldar el objetivo de latencia P95 y disponibilidad del trimestre, te recomendamos profundizar en instrumentación con OpenTelemetry y patrones de resiliencia (circuit breakers, retry policies). Aprovecha el curso interactivo de Engram para dominar la arquitectura de microservicios.',
    managerName: 'Carlos Morales',
    managerTitle: 'Director de Ingeniería de Software',
    assignedDate: '2026-01-15',
    status: 'InProgress',
    progressPercentage: 65,
    linkedObjectiveId: 'emp-obj-2',
    linkedObjectiveTitle: 'Completar Certificación en Arquitectura Segura en la Nube y OWASP',
    linkedCourseId: '75a4d98f-0dcb-499e-a5d4-09faba01a885',
    linkedCourseTitle: 'API +MICROSERVICES',
    linkedCourseType: 'structured-learning',
    metricName: 'Tasa de Adopción de Trazabilidad Distribuida',
    currentValue: '2 de 4 microservicios instrumentados',
    targetValue: '100% de microservicios con trazas y métricas',
    actionItems: [
      { id: 'act-1-1', text: 'Completar capítulos 1 al 4 del curso estructurado de APIs y Microservicios en Engram', completed: true },
      { id: 'act-1-2', text: 'Integrar OpenTelemetry SDK con exportador de Application Insights en el Auth Service', completed: true },
      { id: 'act-1-3', text: 'Diseñar dashboard de latencia P95 en Grafana y configurar alertas automáticas', completed: false },
      { id: 'act-1-4', text: 'Presentar sesión de transferencia técnica de 20 min al equipo sobre distributed tracing', completed: false },
    ],
    quarterlyReflectionNotes:
      'Completé los primeros módulos en Engram con alta retención. La instrumentación del Auth Service redujo tiempos de diagnóstico de incidentes en un 40%.',
  },
  {
    id: 'rec-2',
    personId: 'b144127a-5d4e-4783-91ce-4f973b3b8004',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    quarter: 'Q1 2026',
    year: 2026,
    title: 'Comunicación Estratégica con Stakeholders y Documentación de Decisiones de Arquitectura',
    skillType: 'soft',
    skillName: 'Comunicación Asertiva y Liderazgo Técnico',
    recommendationText:
      'Has demostrado gran rigor técnico en el diseño de agentes. La oportunidad clave este trimestre es estructurar tus propuestas técnicas en ADRs (Architecture Decision Records) ejecutivos para facilitar el consenso con Producto y Seguridad sin fricción.',
    managerName: 'Valeria Sotomayor',
    managerTitle: 'VP de Tecnología & Producto',
    assignedDate: '2026-01-20',
    status: 'Applied',
    progressPercentage: 80,
    linkedObjectiveId: 'emp-obj-1',
    linkedObjectiveTitle: 'Desarrollar el Subagente de Verificación Automática de Código y Tests Unitarios',
    linkedCourseId: null,
    linkedCourseTitle: 'Comunicación Asertiva & Negociación Técnica',
    linkedCourseType: 'graph',
    metricName: 'ADRs formalizados y aprobados por comité técnico',
    currentValue: '3 ADRs presentados y aprobados',
    targetValue: '4 ADRs estructurados',
    actionItems: [
      { id: 'act-2-1', text: 'Adoptar el template estándar de ADR para los nuevos agentes autónomos', completed: true },
      { id: 'act-2-2', text: 'Facilitar la sesión de revisión de arquitectura con el equipo de Ciberseguridad', completed: true },
      { id: 'act-2-3', text: 'Crear resumen ejecutivo en una página para stakeholders no técnicos de Producto', completed: true },
      { id: 'act-2-4', text: 'Recopilar feedback de 360° de los líderes de proyecto al cierre del trimestre', completed: false },
    ],
    quarterlyReflectionNotes:
      'El nuevo formato de ADR aceleró la aprobación del pipeline de agentes con Seguridad de 3 semanas a solo 4 días hábiles.',
  },
];

export const DEFAULT_GROWTH_RECOMMENDATIONS_EN: EmployeeGrowthRecommendation[] = [
  {
    id: 'rec-1',
    personId: 'b144127a-5d4e-4783-91ce-4f973b3b8004',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    quarter: 'Q1 2026',
    year: 2026,
    title: 'Elevate Microservices Observability & Distributed Tracing Practices',
    skillType: 'technical',
    skillName: 'API + Microservices',
    recommendationText:
      'To support our quarterly P95 latency and SLA objectives, we recommend deepening your expertise in OpenTelemetry instrumentation and microservice resilience patterns (circuit breakers, intelligent retry policies). Leverage the Engram course to master modern API platform architecture.',
    managerName: 'Carlos Morales',
    managerTitle: 'Director of Software Engineering',
    assignedDate: '2026-01-15',
    status: 'InProgress',
    progressPercentage: 65,
    linkedObjectiveId: 'emp-obj-2',
    linkedObjectiveTitle: 'Complete Cloud Security & OWASP Top 10 Certification',
    linkedCourseId: '75a4d98f-0dcb-499e-a5d4-09faba01a885',
    linkedCourseTitle: 'API +MICROSERVICES',
    linkedCourseType: 'structured-learning',
    metricName: 'Distributed Tracing Adoption Rate',
    currentValue: '2 of 4 microservices instrumented',
    targetValue: '100% microservices with distributed traces',
    actionItems: [
      { id: 'act-1-1', text: 'Complete Chapters 1-4 of the Structured Learning course in Engram', completed: true },
      { id: 'act-1-2', text: 'Integrate OpenTelemetry SDK with Application Insights exporter in Auth Service', completed: true },
      { id: 'act-1-3', text: 'Build P95 latency and error rate dashboard in Grafana with auto-alerts', completed: false },
      { id: 'act-1-4', text: 'Host a 20-min tech-talk knowledge transfer session on distributed tracing for the team', completed: false },
    ],
    quarterlyReflectionNotes:
      'Mastered the initial modules in Engram. Auth Service instrumentation already cut incident root-cause investigation time by 40%.',
  },
  {
    id: 'rec-2',
    personId: 'b144127a-5d4e-4783-91ce-4f973b3b8004',
    tenantId: '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b',
    quarter: 'Q1 2026',
    year: 2026,
    title: 'Cross-Functional Stakeholder Communication & Architecture Decision Records (ADR)',
    skillType: 'soft',
    skillName: 'Assertive Communication & Technical Leadership',
    recommendationText:
      'You have demonstrated exceptional technical depth in AI agent design. The key growth opportunity this quarter is structuring technical proposals into executive Architecture Decision Records (ADRs) to accelerate consensus across Product and Security stakeholders.',
    managerName: 'Valeria Sotomayor',
    managerTitle: 'VP of Technology & Product',
    assignedDate: '2026-01-20',
    status: 'Applied',
    progressPercentage: 80,
    linkedObjectiveId: 'emp-obj-1',
    linkedObjectiveTitle: 'Build the Autonomous Code Verification & Unit Test Subagent',
    linkedCourseId: null,
    linkedCourseTitle: 'Assertive Communication & Technical Influence',
    linkedCourseType: 'graph',
    metricName: 'Formally Approved Architecture Decision Records',
    currentValue: '3 ADRs reviewed and approved',
    targetValue: '4 structured ADRs',
    actionItems: [
      { id: 'act-2-1', text: 'Adopt standardized ADR template for all autonomous agent implementations', completed: true },
      { id: 'act-2-2', text: 'Lead the architecture alignment review with InfoSec & compliance team', completed: true },
      { id: 'act-2-3', text: 'Draft 1-page executive briefs for non-technical Product managers', completed: true },
      { id: 'act-2-4', text: 'Collect 360 feedback from cross-functional project leads at quarter close', completed: false },
    ],
    quarterlyReflectionNotes:
      'The new ADR format shortened InfoSec sign-off turnaround for our AI pipeline from 3 weeks to just 4 business days.',
  },
];

// ── Storage Keys & Helper Functions ──────────────────────────────────────────

export function getEmployeeObjectivesStorageKey(tenantId: string, personId: string): string {
  return `humanos_employee_smart_objectives_${tenantId}_${personId}`;
}

export function getDepartmentObjectivesStorageKey(tenantId: string, departmentId: string): string {
  return `humanos_enterprise_dept_objectives_${tenantId}_${departmentId}`;
}

export function getGrowthRecommendationsStorageKey(tenantId: string, personId: string): string {
  return `humanos_employee_growth_recommendations_${tenantId}_${personId}`;
}

export function isLegalDepartment(
  departmentId?: string | null,
  departmentName?: string | null,
  jobRoleTitle?: string | null
): boolean {
  if (departmentId === '4aff08f5-0cd9-415d-9c81-9cdc03ea4212') return true;
  const name = (departmentName || '').toLowerCase();
  if (
    name.includes('legal') ||
    name.includes('cumplimiento') ||
    name.includes('compliance') ||
    name.includes('jurídic') ||
    name.includes('juridic') ||
    name.includes('normativ')
  ) {
    return true;
  }
  const role = (jobRoleTitle || '').toLowerCase();
  if (
    role.includes('legal') ||
    role.includes('prevención') ||
    role.includes('prevencion') ||
    role.includes('ilícita') ||
    role.includes('ilicita') ||
    role.includes('pld') ||
    role.includes('aml') ||
    role.includes('procedencia') ||
    role.includes('compliance') ||
    role.includes('abogado') ||
    role.includes('jurídic')
  ) {
    return true;
  }
  return false;
}

export function getEmployeeGrowthRecommendations(
  tenantId: string,
  personId: string,
  isEn: boolean = false,
  departmentName?: string | null,
  jobRoleTitle?: string | null
): EmployeeGrowthRecommendation[] {
  const key = getGrowthRecommendationsStorageKey(tenantId, personId);
  const saved = localStorage.getItem(key);
  if (saved) {
    try {
      const parsed = JSON.parse(saved) as EmployeeGrowthRecommendation[];
      if (parsed && parsed.length > 0) return parsed;
    } catch {
      // fallback
    }
  }

  if (isLegalDepartment(null, departmentName, jobRoleTitle)) {
    return isEn ? DEFAULT_LEGAL_GROWTH_RECOMMENDATIONS_EN : DEFAULT_LEGAL_GROWTH_RECOMMENDATIONS_ES;
  }

  return isEn ? DEFAULT_GROWTH_RECOMMENDATIONS_EN : DEFAULT_GROWTH_RECOMMENDATIONS_ES;
}

export function saveEmployeeGrowthRecommendations(
  tenantId: string,
  personId: string,
  recommendations: EmployeeGrowthRecommendation[]
): void {
  const key = getGrowthRecommendationsStorageKey(tenantId, personId);
  localStorage.setItem(key, JSON.stringify(recommendations));
}

export function getEmployeeObjectives(
  tenantId: string,
  personId: string,
  isEn: boolean = false,
  departmentName?: string | null,
  jobRoleTitle?: string | null
): EmployeeObjective[] {
  const key = getEmployeeObjectivesStorageKey(tenantId, personId);
  const saved = localStorage.getItem(key);
  if (saved) {
    try {
      const parsed = JSON.parse(saved) as EmployeeObjective[];
      if (parsed && parsed.length > 0) return parsed;
    } catch {
      // fallback
    }
  }

  if (isLegalDepartment(null, departmentName, jobRoleTitle)) {
    return isEn ? DEFAULT_LEGAL_EMPLOYEE_OBJECTIVES_EN : DEFAULT_LEGAL_EMPLOYEE_OBJECTIVES_ES;
  }

  return isEn ? DEFAULT_EMPLOYEE_OBJECTIVES_EN : DEFAULT_EMPLOYEE_OBJECTIVES_ES;
}

export function saveEmployeeObjectives(
  tenantId: string,
  personId: string,
  objectives: EmployeeObjective[]
): void {
  const key = getEmployeeObjectivesStorageKey(tenantId, personId);
  localStorage.setItem(key, JSON.stringify(objectives));
}

// ── Smart Objective Recommendation Agent Client ──────────────────────────

export interface RecommendSmartObjectivePayload {
  departmentObjectiveTitle?: string | null;
  departmentObjectiveDescription?: string | null;
  departmentObjectiveTargetQuarter?: string | null;
  departmentObjectiveTargetDate?: string | null;
  strategicGoalTitle?: string | null;
  pillarTitle?: string | null;
  departmentMetricName?: string | null;
  departmentTargetValue?: number | null;
  departmentUnit?: string | null;
  employeeName?: string | null;
  jobRoleTitle?: string | null;
  jobRoleLevel?: string | null;
  jobRolePurpose?: string | null;
  jobRoleSummary?: string | null;
  jobRoleTechnicalSkills?: string[];
  jobRoleSoftSkills?: string[];
  language?: string;
}

export interface SmartObjectiveRecommendationDto {
  title: string;
  description: string;
  metricName: string;
  currentValue: number;
  targetValue: number;
  unit: string;
  measurementMethod: string;
  actionPlan: string;
  businessImpact: string;
  targetQuarter: string;
  targetDate: string;
  recommendedTechnicalSkills: string[];
  recommendedSoftSkills: string[];
  recommendationReasoning: string;
}

export async function recommendSmartObjective(
  payload: RecommendSmartObjectivePayload
): Promise<SmartObjectiveRecommendationDto> {
  const response = await fetch('/api/objectives/recommend-smart', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    let errorMsg = 'Failed to generate recommendation';
    try {
      const errBody = await response.json();
      errorMsg = errBody.message || errBody.error || errorMsg;
    } catch {
      // fallback
    }
    throw new Error(errorMsg);
  }

  return response.json();
}

export function getAvailableDepartmentObjectives(
  tenantId: string,
  departmentId: string | null | undefined,
  isEn: boolean = false,
  departmentName?: string | null,
  jobRoleTitle?: string | null
): DepartmentObjectiveRef[] {
  if (departmentId) {
    const key = getDepartmentObjectivesStorageKey(tenantId, departmentId);
    const saved = localStorage.getItem(key);
    if (saved) {
      try {
        const parsed = JSON.parse(saved) as DepartmentObjectiveRef[];
        if (parsed && parsed.length > 0) return parsed;
      } catch {
        // fallback
      }
    }
  }

  if (isLegalDepartment(departmentId, departmentName, jobRoleTitle)) {
    return isEn ? DEFAULT_LEGAL_OBJECTIVES_EN : DEFAULT_LEGAL_OBJECTIVES_ES;
  }

  const name = (departmentName || '').toLowerCase();
  if (name.includes('finan') || departmentId === '62180296-c4c1-4a99-ad63-415907867a5c') {
    return isEn ? DEFAULT_FINANCE_OBJECTIVES_EN : DEFAULT_FINANCE_OBJECTIVES_ES;
  }
  if (
    name.includes('people') ||
    name.includes('hr') ||
    name.includes('recursos') ||
    departmentId === 'fd64e0c0-ff82-45ee-aafa-2f2d0da7b2cd'
  ) {
    return isEn ? DEFAULT_HR_OBJECTIVES_EN : DEFAULT_HR_OBJECTIVES_ES;
  }

  return isEn ? DEFAULT_SOFTWARE_ENG_OBJECTIVES_EN : DEFAULT_SOFTWARE_ENG_OBJECTIVES_ES;
}

export interface DepartmentGroupedObjectives {
  departmentName: string;
  isUserDepartment: boolean;
  objectives: DepartmentObjectiveRef[];
}

export function getAllCorporateDepartmentObjectives(
  tenantId: string,
  isEn: boolean = false,
  userDeptId?: string | null,
  userDeptName?: string | null,
  jobRoleTitle?: string | null
): DepartmentGroupedObjectives[] {
  const isLegal = isLegalDepartment(userDeptId, userDeptName, jobRoleTitle);

  // User's own department objectives
  const userDeptObjectives = getAvailableDepartmentObjectives(tenantId, userDeptId, isEn, userDeptName, jobRoleTitle);
  const userDeptTitle = isLegal
    ? isEn
      ? 'Legal & Compliance (Your Department)'
      : 'Legal & Cumplimiento (Tu Departamento)'
    : userDeptName
    ? `${userDeptName} (${isEn ? 'Your Department' : 'Tu Departamento'})`
    : isEn
    ? 'Software Engineering & AI (Your Department)'
    : 'Ingeniería de Software & IA (Tu Departamento)';

  const groups: DepartmentGroupedObjectives[] = [
    {
      departmentName: userDeptTitle,
      isUserDepartment: true,
      objectives: userDeptObjectives,
    },
  ];

  if (isLegal) {
    groups.push({
      departmentName: isEn ? 'Software Engineering & AI' : 'Ingeniería de Software & IA',
      isUserDepartment: false,
      objectives: isEn ? DEFAULT_SOFTWARE_ENG_OBJECTIVES_EN : DEFAULT_SOFTWARE_ENG_OBJECTIVES_ES,
    });
    groups.push({
      departmentName: isEn ? 'Finance & Accounting' : 'Finanzas & Contabilidad',
      isUserDepartment: false,
      objectives: isEn ? DEFAULT_FINANCE_OBJECTIVES_EN : DEFAULT_FINANCE_OBJECTIVES_ES,
    });
    groups.push({
      departmentName: isEn ? 'People & Human Resources' : 'Recursos Humanos & Cultura',
      isUserDepartment: false,
      objectives: isEn ? DEFAULT_HR_OBJECTIVES_EN : DEFAULT_HR_OBJECTIVES_ES,
    });
  } else {
    groups.push({
      departmentName: isEn ? 'Legal & Compliance' : 'Legal & Cumplimiento',
      isUserDepartment: false,
      objectives: isEn ? DEFAULT_LEGAL_OBJECTIVES_EN : DEFAULT_LEGAL_OBJECTIVES_ES,
    });
    groups.push({
      departmentName: isEn ? 'Finance & Accounting' : 'Finanzas & Contabilidad',
      isUserDepartment: false,
      objectives: isEn ? DEFAULT_FINANCE_OBJECTIVES_EN : DEFAULT_FINANCE_OBJECTIVES_ES,
    });
    groups.push({
      departmentName: isEn ? 'People & Human Resources' : 'Recursos Humanos & Cultura',
      isUserDepartment: false,
      objectives: isEn ? DEFAULT_HR_OBJECTIVES_EN : DEFAULT_HR_OBJECTIVES_ES,
    });
  }

  return groups;
}
