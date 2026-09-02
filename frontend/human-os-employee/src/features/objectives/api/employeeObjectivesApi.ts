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

export const DEFAULT_DEPT_OBJECTIVES_ES: DepartmentObjectiveRef[] = [
  {
    id: 'd-obj-se-1',
    title: 'Desplegar Agentes de IA en el Pipeline de CI/CD para Revisión Automatizada de Código',
    description: 'Implementar agentes autónomos basados en Inteligencia Artificial para análisis estático, generación automática de pruebas unitarias y detección de vulnerabilidades previas al merge.',
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-30',
    progressPercentage: 65,
    status: 'InProgress',
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
    strategicGoalTitle: 'Certificación en Seguridad y Cumplimiento Normativo',
    pillarTitle: 'Excelencia Operativa & Calidad',
    metricName: 'Latencia P95 de APIs Backend',
    currentValue: 320,
    targetValue: 150,
    unit: 'ms',
  },
];

export const DEFAULT_DEPT_OBJECTIVES_EN: DepartmentObjectiveRef[] = [
  {
    id: 'd-obj-se-1',
    title: 'Deploy AI Agents Across the CI/CD Pipeline for Automated Code Review',
    description: 'Implement AI autonomous agents for static analysis, unit test generation, and security pre-validation before merging into core repositories.',
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-30',
    progressPercentage: 65,
    status: 'InProgress',
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
    strategicGoalTitle: 'Security and Compliance Certification',
    pillarTitle: 'Operational Excellence & Quality',
    metricName: 'Backend API P95 Response Latency',
    currentValue: 320,
    targetValue: 150,
    unit: 'ms',
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

export function getEmployeeGrowthRecommendations(
  tenantId: string,
  personId: string,
  isEn: boolean = false
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
  isEn: boolean = false
): EmployeeObjective[] {
  const key = getEmployeeObjectivesStorageKey(tenantId, personId);
  const saved = localStorage.getItem(key);
  if (saved) {
    try {
      return JSON.parse(saved) as EmployeeObjective[];
    } catch {
      // fallback
    }
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

export function getAvailableDepartmentObjectives(
  tenantId: string,
  departmentId: string | null | undefined,
  isEn: boolean = false
): DepartmentObjectiveRef[] {
  if (!departmentId) {
    return isEn ? DEFAULT_DEPT_OBJECTIVES_EN : DEFAULT_DEPT_OBJECTIVES_ES;
  }

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

  return isEn ? DEFAULT_DEPT_OBJECTIVES_EN : DEFAULT_DEPT_OBJECTIVES_ES;
}
