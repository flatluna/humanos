/** Curated "we don't have this yet, but plan to build it" capability
 *  suggestions per Subject, shown as clickable chips in the Step 3
 *  "Starting Point" gap-capability box (see StartingPointPage.tsx).
 *
 *  IMPORTANT — granularity rule (agreed 2026-07-22): a suggestion here
 *  must be narrow enough to become ONE real Capability on its own (same
 *  size as the one real Capability that exists today, "Ley del IVA
 *  Mexicana" — a single bounded, teachable, assessable topic). A mega-
 *  umbrella like "Cloud/Azure" or "Programación" is NOT itself a
 *  suggestion — it's a `clusterLabel` grouping many narrow suggestions
 *  (e.g. clusterLabel "Nube" contains "Azure Storage y Data Lake",
 *  "Azure Functions y Serverless", etc. as separate items). Clusters are
 *  a pure UI grouping for scannability — they do NOT create a new
 *  taxonomy layer in the backend (CapabilityDomain -> Subject ->
 *  Capability stays exactly as-is; every item below becomes a flat,
 *  sibling Capability under its Subject if it's ever built for real).
 *
 *  Content is Spanish-only for now (the language actually in active use
 *  across this session) — add an `en` variant per item if/when the
 *  English locale needs its own wording instead of falling back to these
 *  Spanish strings.
 */

export interface GapSuggestionCluster {
  clusterLabel: string;
  items: string[];
}

export const SUBJECT_GAP_SUGGESTIONS: Record<string, GapSuggestionCluster[]> = {
  tecnologia: [
    {
      clusterLabel: 'Nube',
      items: [
        'Fundamentos de la Nube',
        'Azure Storage y Data Lake',
        'Azure Functions y Serverless',
        'Azure SQL y Bases de Datos en la Nube',
        'Contenedores y Kubernetes (AKS)',
        'Redes en Azure (VNets, Load Balancers)',
        'Identidad y Seguridad en la Nube (Entra ID, RBAC)',
        'Monitoreo y Observabilidad (Azure Monitor)',
        'Cosmos DB y Bases NoSQL',
        'Costos y FinOps en la Nube',
      ],
    },
    {
      clusterLabel: 'Programación',
      items: [
        'Python Básico',
        'JavaScript Esencial',
        'Estructuras de Datos y Algoritmos',
        'Control de Versiones con Git',
        'Bases de Datos SQL',
        'APIs y Servicios Web',
        'Testing y Depuración de Código',
        'Programación Orientada a Objetos',
        'Desarrollo Web Frontend',
        'Desarrollo Móvil Básico',
      ],
    },
    {
      clusterLabel: 'IA',
      items: [
        'Prompting Efectivo',
        'Agentes de IA',
        'Automatización con IA',
        'Generación de Imágenes con IA',
        'Generación de Video y Audio con IA',
        'Análisis de Datos con IA',
        'Asistentes de Código (Copilots)',
        'Chatbots para Negocio',
        'Ética y Uso Responsable de IA',
        'Herramientas No-Code con IA',
      ],
    },
    {
      clusterLabel: 'Seguridad',
      items: [
        'Ciberseguridad Básica',
        'Gestión de Contraseñas y Autenticación',
        'Phishing y Fraude Digital',
        'Seguridad en Redes Wi-Fi',
        'Privacidad de Datos Personales',
        'Copias de Seguridad (Backups)',
        'Seguridad en Dispositivos Móviles',
        'Cumplimiento y Normativas de Datos',
      ],
    },
    {
      clusterLabel: 'DevOps',
      items: [
        'Integración Continua (CI)',
        'Entrega Continua (CD)',
        'Contenedores con Docker',
        'Automatización de Infraestructura (IaC)',
        'GitHub: Colaboración y Pull Requests',
        'GitHub Actions',
        'Gestión de Configuración',
        'Cultura y Prácticas DevOps',
      ],
    },
  ],
  oficios: [
    {
      clusterLabel: 'Autos',
      items: [
        'Mecánica Básica del Automóvil',
        'Cambio de Aceite y Filtros',
        'Sistema de Frenos',
        'Diagnóstico con Scanner OBD-II',
        'Neumáticos y Alineación',
        'Sistema Eléctrico del Auto',
        'Mantenimiento Preventivo',
        'Reparación de Carrocería Básica',
      ],
    },
    {
      clusterLabel: 'Carpintería',
      items: [
        'Carpintería Básica',
        'Uso y Cuidado de Herramientas',
        'Acabados y Barnizado',
        'Muebles a Medida',
      ],
    },
    {
      clusterLabel: 'Electricidad y Plomería',
      items: [
        'Electricidad Básica del Hogar',
        'Instalación de Contactos e Iluminación',
        'Plomería Básica del Hogar',
        'Reparación de Fugas',
      ],
    },
  ],
  animales: [
    {
      clusterLabel: 'Mascotas',
      items: [
        'Comportamiento Canino Básico',
        'Cuidado de Gatos',
        'Primeros Auxilios para Mascotas',
        'Nutrición Animal',
        'Adiestramiento Básico',
        'Cuidado de Aves',
        'Cuidado de Animales de Granja',
        'Bienestar Animal',
      ],
    },
  ],
  'arte-creatividad': [
    {
      clusterLabel: 'Arte y Creatividad',
      items: [
        'Dibujo Básico',
        'Pintura con Acrílico',
        'Fotografía Digital',
        'Edición de Video',
        'Diseño Gráfico Básico',
        'Escritura Creativa',
        'Música y Teoría Básica',
        'Cerámica y Escultura',
      ],
    },
  ],
  negocios: [
    {
      clusterLabel: 'Negocios y Emprendimiento',
      items: [
        'Plan de Negocios',
        'Marketing Digital Básico',
        'Ventas y Negociación',
        'Contabilidad para Emprendedores',
        'Atención al Cliente',
        'Networking Profesional',
        'E-commerce Básico',
        'Modelos de Negocio (Canvas)',
      ],
    },
  ],
  cocina: [
    {
      clusterLabel: 'Cocina',
      items: [
        'Técnicas de Cuchillo',
        'Repostería Básica',
        'Cocina Mexicana Tradicional',
        'Panificación',
        'Cocina Saludable',
        'Fermentación',
        'Parrilla y Asados',
        'Higiene y Seguridad Alimentaria',
      ],
    },
  ],
  finanzas: [
    {
      clusterLabel: 'Finanzas Personales',
      items: [
        'Presupuesto Personal',
        'Ahorro e Inversión Básica',
        'Crédito y Manejo de Deudas',
        'Fondos de Retiro',
        'Impuestos Personales',
        'Finanzas para Freelancers',
        'Bolsa de Valores Básica',
        'Seguros',
      ],
    },
  ],
  geografia: [
    {
      clusterLabel: 'Geografía',
      items: [
        'Geografía de México',
        'Mapas y Cartografía',
        'Geopolítica Básica',
        'Climas y Ecosistemas',
        'Geografía Económica',
        'Migraciones y Población',
        'Geografía Urbana',
        'Recursos Naturales',
      ],
    },
  ],
  'salud-bienestar': [
    {
      clusterLabel: 'Salud y Bienestar',
      items: [
        'Nutrición Básica',
        'Ejercicio y Acondicionamiento Físico',
        'Salud Mental Básica',
        'Primeros Auxilios',
        'Manejo del Estrés',
        'Sueño y Descanso',
        'Salud Preventiva',
        'Meditación y Mindfulness',
      ],
    },
  ],
  historia: [
    {
      clusterLabel: 'Historia',
      items: [
        'Historia de México',
        'Historia Universal Contemporánea',
        'Civilizaciones Antiguas',
        'Historia de las Revoluciones',
        'Historia del Arte',
        'Historia Económica',
        'Historia de la Ciencia',
        'Historia Regional y Local',
      ],
    },
  ],
  'recursos-humanos': [
    {
      clusterLabel: 'Recursos Humanos',
      items: [
        'Reclutamiento y Selección',
        'Evaluación de Desempeño',
        'Cultura Organizacional',
        'Compensaciones y Beneficios',
        'Capacitación y Desarrollo',
        'Manejo de Conflictos',
        'Legislación Laboral Básica',
        'Onboarding de Personal',
      ],
    },
  ],
  idiomas: [
    {
      clusterLabel: 'Idiomas',
      items: [
        'Inglés Básico',
        'Inglés de Negocios',
        'Francés Básico',
        'Gramática Española Avanzada',
        'Pronunciación y Fonética',
        'Preparación para Certificaciones (TOEFL/IELTS)',
        'Conversación Práctica',
        'Traducción Básica',
      ],
    },
  ],
  matematicas: [
    {
      clusterLabel: 'Matemáticas',
      items: [
        'Álgebra Básica',
        'Geometría',
        'Estadística y Probabilidad',
        'Cálculo Diferencial',
        'Aritmética Financiera',
        'Lógica y Razonamiento',
        'Trigonometría',
        'Matemáticas para Programadores',
      ],
    },
  ],
  'desarrollo-personal': [
    {
      clusterLabel: 'Desarrollo Personal',
      items: [
        'Gestión del Tiempo',
        'Hábitos y Productividad',
        'Comunicación Efectiva',
        'Liderazgo Personal',
        'Inteligencia Emocional',
        'Toma de Decisiones',
        'Establecimiento de Metas',
        'Resiliencia',
      ],
    },
  ],
  ciencia: [
    {
      clusterLabel: 'Ciencia',
      items: [
        'Método Científico',
        'Física Básica',
        'Química Básica',
        'Biología Básica',
        'Astronomía',
        'Ecología',
        'Ciencia de Datos Básica',
        'Pensamiento Crítico Científico',
      ],
    },
  ],
};
