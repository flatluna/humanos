// ── Strategic Goals & Company Direction API / Storage Helper ────────────────

export interface StrategicPillar {
  id: string;
  title: string;
  description: string;
  color: string;
}

export interface StrategicGoal {
  id: string;
  title: string;
  description: string;
  pillarId: string;
  departmentId: string | null;
  targetQuarter: string;
  targetDate: string;
  progressPercentage: number;
  status: 'Active' | 'InProgress' | 'Completed' | 'Paused';
  linkedSkillNames: string[];
}

export interface CompanyDirection {
  vision: string;
  mission: string;
  coreValues: string[];
  pillars: StrategicPillar[];
}

export interface DepartmentObjective {
  id: string;
  title: string;
  description: string;
  targetQuarter: string;
  targetDate: string;
  progressPercentage: number;
  status: 'Active' | 'InProgress' | 'Completed' | 'Paused';
  strategicGoalId?: string | null;
  strategicGoalTitle?: string | null;
  pillarTitle?: string | null;

  // SMART Criteria & Measurement
  metricName?: string | null;
  currentValue?: number | null;
  targetValue?: number | null;
  unit?: string | null;
  measurementMethod?: string | null;
  actionPlan?: string | null;
  businessImpact?: string | null;
}

export interface DepartmentKPI {
  id: string;
  name: string;
  targetValue: number;
  currentValue: number;
  unit: string;
  category: string;
}

// ── Default Strategic Pillars ──────────────────────────────────────────────

export const DEFAULT_PILLARS_ES: StrategicPillar[] = [
  {
    id: 'p-1',
    title: 'Innovación & Transformación Tecnológica',
    description: 'Adopción de Inteligencia Artificial y automatización en procesos clave.',
    color: 'from-violet-500 to-indigo-600',
  },
  {
    id: 'p-2',
    title: 'Excelencia Operativa & Calidad',
    description: 'Optimización de tiempos de respuesta, eficiencia y cumplimiento normativo.',
    color: 'from-sky-500 to-blue-600',
  },
  {
    id: 'p-3',
    title: 'Crecimiento de Talento & Capacidades',
    description: 'Desarrollo de competencias continuas y rutas de carrera para colaboradores.',
    color: 'from-emerald-500 to-teal-600',
  },
  {
    id: 'p-4',
    title: 'Satisfacción del Cliente & Impacto',
    description: 'Entrega de valor de alta calidad y fortalecimiento de alianzas estratégicas.',
    color: 'from-amber-500 to-orange-600',
  },
];

export const DEFAULT_PILLARS_EN: StrategicPillar[] = [
  {
    id: 'p-1',
    title: 'Innovation & Technological Transformation',
    description: 'Adoption of Artificial Intelligence and automation across key business processes.',
    color: 'from-violet-500 to-indigo-600',
  },
  {
    id: 'p-2',
    title: 'Operational Excellence & Quality',
    description: 'Optimization of response times, operational efficiency, and regulatory compliance.',
    color: 'from-sky-500 to-blue-600',
  },
  {
    id: 'p-3',
    title: 'Talent & Capability Growth',
    description: 'Continuous competency development and career roadmaps for team members.',
    color: 'from-emerald-500 to-teal-600',
  },
  {
    id: 'p-4',
    title: 'Customer Satisfaction & Impact',
    description: 'High-quality value delivery and strengthening of strategic partnerships.',
    color: 'from-amber-500 to-orange-600',
  },
];

// ── Default Company Direction ──────────────────────────────────────────────

export const DEFAULT_DIRECTION_ES: CompanyDirection = {
  vision:
    'Convertirnos en el referente líder de la industria mediante la aceleración del conocimiento, la adopción responsable de Inteligencia Artificial y la excelencia operativa continua.',
  mission:
    'Desarrollar y potenciar las capacidades de nuestros colaboradores y organizaciones a través de herramientas inteligentes y rutas de carrera personalizadas.',
  coreValues: [
    'Innovación Continua',
    'Excelencia en Ejecución',
    'Trabajo en Equipo',
    'Integridad & Transparencia',
    'Desarrollo del Talento',
  ],
  pillars: DEFAULT_PILLARS_ES,
};

export const DEFAULT_DIRECTION_EN: CompanyDirection = {
  vision:
    'To become the leading industry benchmark through accelerated knowledge, responsible AI adoption, and continuous operational excellence.',
  mission:
    'To develop and empower employee and organizational capabilities through intelligent tools and personalized career paths.',
  coreValues: [
    'Continuous Innovation',
    'Operational Excellence',
    'Teamwork',
    'Integrity & Transparency',
    'Talent Development',
  ],
  pillars: DEFAULT_PILLARS_EN,
};

// ── Default Strategic Goals ────────────────────────────────────────────────

export const DEFAULT_GOALS_ES: StrategicGoal[] = [
  {
    id: 'g-1',
    title: 'Implementar Agentes de IA en Procesos Clave de Operación',
    description: 'Automatizar tareas repetitivas y análisis de datos en departamentos operativos para elevar la productividad en 30%.',
    pillarId: 'p-1',
    departmentId: null,
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-30',
    progressPercentage: 65,
    status: 'InProgress',
    linkedSkillNames: ['Inteligencia Artificial', 'Python Avanzado', 'Machine Learning'],
  },
  {
    id: 'g-2',
    title: 'Alcanzar 95% de Cobertura en Planes de Capacitación',
    description: 'Garantizar que todo el personal asignado cuente con su plan de desarrollo de competencias actualizado.',
    pillarId: 'p-3',
    departmentId: null,
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 80,
    status: 'InProgress',
    linkedSkillNames: ['Gestión del Talento', 'Liderazgo de Equipos', 'Evaluación de Desempeño'],
  },
  {
    id: 'g-3',
    title: 'Certificación en Seguridad y Cumplimiento Normativo',
    description: 'Completar auditorías internas y capacitación obligatoria de normatividad para todos los equipos.',
    pillarId: 'p-2',
    departmentId: null,
    targetQuarter: 'Q2 2026',
    targetDate: '2026-06-30',
    progressPercentage: 100,
    status: 'Completed',
    linkedSkillNames: ['Prevención de Acoso Sexual Laboral', 'Seguridad de la Información', 'Gestión de Riesgos'],
  },
  {
    id: 'g-4',
    title: 'Optimizar Tiempos de Respuesta a Clientes a <2 Horas',
    description: 'Mejorar procesos del departamento comercial y soporte técnico para acelerar resolución de tickets.',
    pillarId: 'p-4',
    departmentId: null,
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-30',
    progressPercentage: 40,
    status: 'Active',
    linkedSkillNames: ['Atención a Clientes', 'Comunicación Asertiva', 'Resolución de Problemas'],
  },
];

export const DEFAULT_GOALS_EN: StrategicGoal[] = [
  {
    id: 'g-1',
    title: 'Deploy AI Agents Across Key Operational Workflows',
    description: 'Automate repetitive tasks and data analysis in operational departments to boost productivity by 30%.',
    pillarId: 'p-1',
    departmentId: null,
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-30',
    progressPercentage: 65,
    status: 'InProgress',
    linkedSkillNames: ['Artificial Intelligence', 'Advanced Python', 'Machine Learning'],
  },
  {
    id: 'g-2',
    title: 'Achieve 95% Coverage on Employee Training Plans',
    description: 'Ensure all assigned staff have an active and updated competency development roadmap.',
    pillarId: 'p-3',
    departmentId: null,
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 80,
    status: 'InProgress',
    linkedSkillNames: ['Talent Management', 'Team Leadership', 'Performance Evaluation'],
  },
  {
    id: 'g-3',
    title: 'Security and Compliance Certification',
    description: 'Complete internal audits and mandatory compliance training for all company teams.',
    pillarId: 'p-2',
    departmentId: null,
    targetQuarter: 'Q2 2026',
    targetDate: '2026-06-30',
    progressPercentage: 100,
    status: 'Completed',
    linkedSkillNames: ['Sexual Harassment Prevention', 'Information Security', 'Risk Management'],
  },
  {
    id: 'g-4',
    title: 'Optimize Customer Response Time to <2 Hours',
    description: 'Streamline commercial and technical support workflows to accelerate ticket resolution.',
    pillarId: 'p-4',
    departmentId: null,
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-30',
    progressPercentage: 40,
    status: 'Active',
    linkedSkillNames: ['Customer Support', 'Assertive Communication', 'Problem Solving'],
  },
];

// ── Default Department Objectives (e.g. Software Engineering) ──────────────

export const DEFAULT_SOFTWARE_ENG_OBJECTIVES_ES: DepartmentObjective[] = [
  {
    id: 'd-obj-ai-org',
    title: 'Implementar Agentes de IA para la Organización',
    description: 'Diseñar, integrar y desplegar agentes inteligentes autónomos en procesos clave para automatizar flujos operativos y elevar la productividad del equipo.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 33,
    status: 'Active',
    strategicGoalId: 'g-1',
    strategicGoalTitle: 'Implementar Agentes de IA en Procesos Clave de Operación',
    pillarTitle: 'Innovación & Transformación Tecnológica',
    metricName: 'Agentes de IA en Producción',
    currentValue: 1,
    targetValue: 3,
    unit: 'Agentes',
    measurementMethod: 'Telemetría de ejecución de agentes y métricas de resolución en Azure Application Insights.',
    actionPlan: '1. Seleccionar 3 flujos clave (HR, Soporte y Operaciones).\n2. Desarrollar MVPs con Microsoft Agent Framework.\n3. Piloto y pase a producción.',
    businessImpact: 'Ahorro estimado de 150 horas mensuales y 40% de reducción en tiempos de ciclo.',
  },
  {
    id: 'd-obj-se-1',
    title: 'Desplegar Agentes de IA en el Pipeline de CI/CD para Revisión Automatizada de Código',
    description: 'Implementar agentes autónomos basados en Inteligencia Artificial para análisis estático, generación automática de pruebas unitarias y detección de vulnerabilidades previas al merge.',
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-30',
    progressPercentage: 65,
    status: 'InProgress',
    strategicGoalId: 'g-1',
    strategicGoalTitle: 'Implementar Agentes de IA en Procesos Clave de Operación',
    pillarTitle: 'Innovación & Transformación Tecnológica',
    metricName: 'Cobertura de PRs con Code Review Autónomo por IA',
    currentValue: 65,
    targetValue: 100,
    unit: '%',
    measurementMethod: 'Métricas de telemetría de GitHub Actions y Azure DevOps conectadas a Application Insights.',
    actionPlan: '1. Integrar API de Azure OpenAI con webhooks de GitHub.\n2. Configurar validaciones automáticas de seguridad OWASP.\n3. Desplegar en 3 microservicios piloto.',
    businessImpact: 'Acelera el tiempo de entrega de software en un 40% y reduce un 70% los defectos en producción.',
  },
  {
    id: 'd-obj-se-2',
    title: 'Optimizar Latencia P95 y Resiliencia de Microservicios Críticos',
    description: 'Refactorizar consultas de base de datos asíncronas, indexación estratégica y caching en Redis para garantizar alta disponibilidad y tiempos de respuesta mínimos.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-30',
    progressPercentage: 45,
    status: 'Active',
    strategicGoalId: 'g-3',
    strategicGoalTitle: 'Certificación en Seguridad y Cumplimiento Normativo',
    pillarTitle: 'Excelencia Operativa & Calidad',
    metricName: 'Latencia P95 de APIs Backend',
    currentValue: 320,
    targetValue: 150,
    unit: 'ms',
    measurementMethod: 'Monitoreo en tiempo real a través de dashboards de APM en Azure Monitor y Datadog.',
    actionPlan: 'Optimización de consultas SQL asíncronas, implementación de pool de conexiones y caché distribuida Redis.',
    businessImpact: 'Mejora en la experiencia de usuario y disminución del 25% en costos de infraestructura cloud.',
  },
];

export const DEFAULT_SOFTWARE_ENG_OBJECTIVES_EN: DepartmentObjective[] = [
  {
    id: 'd-obj-ai-org',
    title: 'Deploy AI Agents Across the Organization',
    description: 'Design, integrate, and deploy autonomous AI agents across core workflows to automate repetitive tasks and boost team productivity.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-12-31',
    progressPercentage: 33,
    status: 'Active',
    strategicGoalId: 'g-1',
    strategicGoalTitle: 'Deploy AI Agents Across Key Operational Workflows',
    pillarTitle: 'Innovation & Technological Transformation',
    metricName: 'Production AI Agents Deployed',
    currentValue: 1,
    targetValue: 3,
    unit: 'Agents',
    measurementMethod: 'Agent execution telemetry and resolution metrics in Azure Application Insights.',
    actionPlan: '1. Select 3 priority workflows (HR, Support, Operations).\n2. Build MVPs with Microsoft Agent Framework.\n3. Pilot and roll out to production.',
    businessImpact: 'Estimated savings of 150 hours/month and 40% reduction in cycle times.',
  },
  {
    id: 'd-obj-se-1',
    title: 'Deploy AI Agents Across the CI/CD Pipeline for Automated Code Review',
    description: 'Implement AI autonomous agents for static analysis, unit test generation, and security pre-validation before merging into core repositories.',
    targetQuarter: 'Q3 2026',
    targetDate: '2026-09-30',
    progressPercentage: 65,
    status: 'InProgress',
    strategicGoalId: 'g-1',
    strategicGoalTitle: 'Deploy AI Agents Across Key Operational Workflows',
    pillarTitle: 'Innovation & Technological Transformation',
    metricName: 'PR Coverage with Autonomous AI Code Review',
    currentValue: 65,
    targetValue: 100,
    unit: '%',
    measurementMethod: 'GitHub Actions telemetry and Azure Application Insights monitoring PR review lead time.',
    actionPlan: '1. Integrate Azure OpenAI Assistants API with GitHub webhooks.\n2. Implement structured OWASP vulnerability checks.\n3. Roll out to 3 pilot microservices before full adoption.',
    businessImpact: 'Accelerates software release velocity by 40% while preventing 70% of common bugs from reaching production.',
  },
  {
    id: 'd-obj-se-2',
    title: 'Optimize P95 Latency & Reliability on Core Microservices',
    description: 'Refactor asynchronous database queries, index tuning, and distributed Redis caching for ultra-responsive API performance.',
    targetQuarter: 'Q4 2026',
    targetDate: '2026-11-30',
    progressPercentage: 45,
    status: 'Active',
    strategicGoalId: 'g-3',
    strategicGoalTitle: 'Security and Compliance Certification',
    pillarTitle: 'Operational Excellence & Quality',
    metricName: 'Backend API P95 Response Latency',
    currentValue: 320,
    targetValue: 150,
    unit: 'ms',
    measurementMethod: 'Synthetic APM monitoring dashboards via Azure Monitor and Datadog transactions.',
    actionPlan: 'Database index tuning, connection pool optimization, and distributed Redis caching.',
    businessImpact: 'Direct boost in customer experience and 25% decrease in cloud compute consumption.',
  },
];

export const DEFAULT_SOFTWARE_ENG_KPIS_ES: DepartmentKPI[] = [
  {
    id: 'kpi-se-1',
    name: 'Cobertura de Pruebas Automatizadas',
    targetValue: 85,
    currentValue: 74,
    unit: '%',
    category: 'Calidad de Software',
  },
  {
    id: 'kpi-se-2',
    name: 'Tiempo de Resolución de PRs (Lead Time)',
    targetValue: 4,
    currentValue: 2.8,
    unit: 'hrs',
    category: 'Eficiencia',
  },
];

export const DEFAULT_SOFTWARE_ENG_KPIS_EN: DepartmentKPI[] = [
  {
    id: 'kpi-se-1',
    name: 'Automated Test Coverage',
    targetValue: 85,
    currentValue: 74,
    unit: '%',
    category: 'Software Quality',
  },
  {
    id: 'kpi-se-2',
    name: 'Pull Request Lead Time',
    targetValue: 4,
    currentValue: 2.8,
    unit: 'hrs',
    category: 'Efficiency',
  },
];

// ── Storage Helpers ────────────────────────────────────────────────────────

export function getStrategicGoalsStorageKey(tenantId: string, isEn: boolean): string {
  return `humanos_enterprise_goals_${tenantId}_${isEn ? 'en' : 'es'}`;
}

export function getDirectionStorageKey(tenantId: string, isEn: boolean): string {
  return `humanos_enterprise_direction_${tenantId}_${isEn ? 'en' : 'es'}`;
}

export function getDeptObjectivesStorageKey(tenantId: string, departmentId: string): string {
  return `humanos_enterprise_dept_objectives_${tenantId}_${departmentId}`;
}

export function getDeptKpisStorageKey(tenantId: string, departmentId: string): string {
  return `humanos_enterprise_dept_kpis_${tenantId}_${departmentId}`;
}

export function getStrategicGoals(tenantId: string, isEn: boolean): StrategicGoal[] {
  const key = getStrategicGoalsStorageKey(tenantId, isEn);
  const saved = localStorage.getItem(key);
  if (saved) {
    try {
      return JSON.parse(saved) as StrategicGoal[];
    } catch {
      // fallback
    }
  }
  return isEn ? DEFAULT_GOALS_EN : DEFAULT_GOALS_ES;
}

export function saveStrategicGoals(tenantId: string, isEn: boolean, goals: StrategicGoal[]): void {
  const key = getStrategicGoalsStorageKey(tenantId, isEn);
  localStorage.setItem(key, JSON.stringify(goals));
}

export function getCompanyDirection(tenantId: string, isEn: boolean): CompanyDirection {
  const key = getDirectionStorageKey(tenantId, isEn);
  const saved = localStorage.getItem(key);
  if (saved) {
    try {
      return JSON.parse(saved) as CompanyDirection;
    } catch {
      // fallback
    }
  }
  return isEn ? DEFAULT_DIRECTION_EN : DEFAULT_DIRECTION_ES;
}

export function saveCompanyDirection(tenantId: string, isEn: boolean, dir: CompanyDirection): void {
  const key = getDirectionStorageKey(tenantId, isEn);
  localStorage.setItem(key, JSON.stringify(dir));
}

export function getDepartmentObjectives(
  tenantId: string,
  departmentId: string,
  isEn: boolean = false
): DepartmentObjective[] {
  const key = getDeptObjectivesStorageKey(tenantId, departmentId);
  const saved = localStorage.getItem(key);
  if (saved) {
    try {
      return JSON.parse(saved) as DepartmentObjective[];
    } catch {
      // fallback
    }
  }

  // Seed default SMART objectives for Software Engineering department
  if (departmentId === '6c95b6dc-e89b-4b0d-ad89-dfd67e782b12') {
    return isEn ? DEFAULT_SOFTWARE_ENG_OBJECTIVES_EN : DEFAULT_SOFTWARE_ENG_OBJECTIVES_ES;
  }

  return [];
}

export function saveDepartmentObjectives(
  tenantId: string,
  departmentId: string,
  objectives: DepartmentObjective[]
): void {
  const key = getDeptObjectivesStorageKey(tenantId, departmentId);
  localStorage.setItem(key, JSON.stringify(objectives));
}

export function getDepartmentKpis(
  tenantId: string,
  departmentId: string,
  isEn: boolean = false
): DepartmentKPI[] {
  const key = getDeptKpisStorageKey(tenantId, departmentId);
  const saved = localStorage.getItem(key);
  if (saved) {
    try {
      return JSON.parse(saved) as DepartmentKPI[];
    } catch {
      // fallback
    }
  }

  // Seed default KPIs for Software Engineering department
  if (departmentId === '6c95b6dc-e89b-4b0d-ad89-dfd67e782b12') {
    return isEn ? DEFAULT_SOFTWARE_ENG_KPIS_EN : DEFAULT_SOFTWARE_ENG_KPIS_ES;
  }

  return [];
}

export function saveDepartmentKpis(
  tenantId: string,
  departmentId: string,
  kpis: DepartmentKPI[]
): void {
  const key = getDeptKpisStorageKey(tenantId, departmentId);
  localStorage.setItem(key, JSON.stringify(kpis));
}

export interface ContributingDepartmentObjective {
  departmentId: string;
  departmentName: string;
  objective: DepartmentObjective;
}

export function getContributingObjectivesForStrategicGoal(
  tenantId: string,
  strategicGoalId: string,
  departments: { departmentId: string; name: string }[]
): ContributingDepartmentObjective[] {
  const result: ContributingDepartmentObjective[] = [];
  for (const dept of departments) {
    const objs = getDepartmentObjectives(tenantId, dept.departmentId);
    for (const obj of objs) {
      if (obj.strategicGoalId === strategicGoalId) {
        result.push({
          departmentId: dept.departmentId,
          departmentName: dept.name,
          objective: obj,
        });
      }
    }
  }
  return result;
}
