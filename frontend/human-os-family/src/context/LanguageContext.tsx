import React, { createContext, useContext, useState } from 'react';
import { Language } from '../types';

interface LanguageContextType {
  lang: Language;
  setLang: (lang: Language) => void;
  t: (key: string, defaultText?: string) => string;
}

const LanguageContext = createContext<LanguageContextType | undefined>(undefined);

export const DICTIONARY: Record<Language, Record<string, string>> = {
  es: {
    // Header
    'header.brand': 'EngramsIQ Family',
    'header.tagline': 'Inteligencia humana y armonía para familias profesionales (USA / LATAM)',
    'header.household': 'Familia Mendoza-Ríos',
    'header.location': 'Austin, TX · CDMX',
    'header.plan': 'Family Enterprise Plan',
    'header.themeLight': 'Modo Claro',
    'header.themeDark': 'Modo Oscuro',
    'header.profile': 'Portal Familiar',
    'header.role': 'Co-Administrador del Hogar',
    'header.bilingualBadge': 'Hogar Bilingüe Bicultural',

    // Nav
    'nav.sectionTitle': 'Ecosistema Familiar',
    'nav.overview': 'Overview',
    'nav.family': 'Family',
    'nav.personalDev': 'Personal Development',
    'nav.capabilities': 'Capabilities / Skills',
    'nav.learning': 'Learning',
    'nav.insights': 'Insights',
    'nav.admin': 'Admin',
    'nav.coachButton': 'Family Coach AI',
    'nav.householdLabel': 'Núcleo Familiar',
    'nav.switchMember': 'Filtrar por integrante',
    'nav.allMembers': 'Toda la Familia',

    // Overview Page
    'overview.greeting': '¡Hola, Carlos! 👋',
    'overview.subgreeting': 'Panel de crecimiento integral y balance para la Familia Mendoza-Ríos.',
    'overview.kpi.familyBalance': 'Índice de Armonía Familiar',
    'overview.kpi.familyBalanceSub': '9.4 / 10 · Excelente equilibrio vida-estudio',
    'overview.kpi.activeGoals': 'Metas de Crecimiento Activas',
    'overview.kpi.activeGoalsSub': '5 metas estratégicas en curso (78% de avance)',
    'overview.kpi.weeklyHours': 'Horas de Crecimiento Semanales',
    'overview.kpi.weeklyHoursSub': '34.5 hrs invertidas en estudio, carrera y salud',
    'overview.kpi.budget': 'Fondo Educativo Familiar 2026',
    'overview.kpi.budgetSub': '$18,400 USD (~$368,000 MXN) ejecutado al 68%',
    'overview.familyPulse': 'Pulso Semanal del Hogar',
    'overview.upcomingMilestones': 'Próximos Hitos & Compromisos Clave',
    'overview.viewAllMilestones': 'Ver agenda completa',
    'overview.aiInsightTitle': 'Recomendación Inteligente del Family Coach AI',
    'overview.aiInsightText': 'Sofía tiene su simulacro SAT este sábado. Basado en su carga de AP Biology, se sugiere agendar la cena familiar de desconexión el viernes a las 7:30 PM para asegurar 8.5 horas de sueño reparador y reducir cortisol.',
    'overview.membersSnapshot': 'Estado de los Miembros de la Familia',
    'overview.viewProfile': 'Ver Perfil Completo',

    // Family Page
    'family.title': 'Núcleo Familiar & Proyecto de Vida',
    'family.subtitle': 'Miembros, fortalezas, acuerdos de convivencia y proyectos que unen nuestras generaciones en EE.UU. y América Latina.',
    'family.addMember': 'Invitar Familiar / Tutor',
    'family.valuesTitle': 'Valores & Acuerdos del Hogar',
    'family.value1.title': 'Bilingüismo & Orgullo por Nuestras Raíces',
    'family.value1.desc': 'Honramos nuestras raíces hispanas con el mismo rigor que dominamos el inglés académico y profesional global.',
    'family.value2.title': 'Curiosidad Intelectual Sin Presión Tóxica',
    'family.value2.desc': 'Fomentamos el amor genuino por aprender, la resiliencia ante el error y la salud mental antes que las notas numéricas.',
    'family.value3.title': 'Solidaridad Intergeneracional',
    'family.value3.desc': 'La sabiduría de los abuelos guía a los jóvenes; la energía y destreza tecnológica de los hijos empodera a los mayores.',
    'family.projectsTitle': 'Proyectos Familiares Colectivos',
    'family.membersList': 'Integrantes de la Familia Mendoza-Ríos',

    // Personal Development Page
    'pdev.title': 'Personal Development & Bienestar',
    'pdev.subtitle': 'Crecimiento profesional para padres, liderazgo socioemocional para adolescentes y hábitos de vida plena.',
    'pdev.tabParents': 'Carrera & Liderazgo (Padres)',
    'pdev.tabYouth': 'Inteligencia Emocional & Jóvenes',
    'pdev.tabWellness': 'Salud, Hábitos & Desconexión',
    'pdev.tabFinance': 'Educación Financiera & Patrimonio',

    // Learning Page
    'learning.title': 'Learning & Rutas Académicas',
    'learning.subtitle': 'Planes de estudio personalizados, preparación universitaria (USA / LATAM), idiomas y certificaciones de vanguardia.',
    'learning.tabAll': 'Todos los Programas',
    'learning.tabCollege': 'Admisiones & SAT / IB',
    'learning.tabStem': 'STEM, IA & Programación',
    'learning.tabLanguages': 'Idiomas & Humanidades',
    'learning.activeTracks': 'Rutas de Aprendizaje en Progreso',
    'learning.extracurricular': 'Actividades Extracurriculares & Portafolio',
    'learning.suggested': 'Oportunidades Recomendadas por IA para 2027',

    // Insights Page
    'insights.title': 'Insights & Analítica Familiar',
    'insights.subtitle': 'Métricas transparentes sobre equilibrio de tiempo, hábitos de estudio, bienestar emocional y proyecciones universitarias.',
    'insights.radarTitle': 'Radar de Desarrollo Holístico Familiar',
    'insights.screenVsStudy': 'Tiempo de Estudio vs Pantallas Recreativas',
    'insights.collegeReadiness': 'Preparación para Universidades Top (Stanford, Columbia, Tec, UNAM)',
    'insights.gapsTitle': 'Áreas de Refuerzo & Detección Temprana',

    // Admin Page
    'admin.title': 'Administración del Hogar',
    'admin.subtitle': 'Configuración de membresías familiares, asignación presupuestal, permisos y sincronización de herramientas educativas.',
    'admin.budgetSection': 'Presupuesto Educativo & Desglose Anual (USD / MXN)',
    'admin.membersPermissions': 'Permisos & Supervisión de Menores',
    'admin.biculturalSettings': 'Configuración Regional & Bicultural',
    'admin.integrations': 'Integraciones con Escuelas & Calendarios',
  },
  en: {
    // Header
    'header.brand': 'EngramsIQ Family',
    'header.tagline': 'Human intelligence and harmony for professional families (USA / LATAM)',
    'header.household': 'Mendoza-Ríos Family',
    'header.location': 'Austin, TX · Mexico City',
    'header.plan': 'Family Enterprise Plan',
    'header.themeLight': 'Light Mode',
    'header.themeDark': 'Dark Mode',
    'header.profile': 'Family Portal',
    'header.role': 'Household Co-Administrator',
    'header.bilingualBadge': 'Bilingual Bicultural Household',

    // Nav
    'nav.sectionTitle': 'Family Ecosystem',
    'nav.overview': 'Overview',
    'nav.family': 'Family',
    'nav.personalDev': 'Personal Development',
    'nav.capabilities': 'Capabilities / Skills',
    'nav.learning': 'Learning',
    'nav.insights': 'Insights',
    'nav.admin': 'Admin',
    'nav.coachButton': 'Family Coach AI',
    'nav.householdLabel': 'Household Core',
    'nav.switchMember': 'Filter by member',
    'nav.allMembers': 'All Family Members',

    // Overview Page
    'overview.greeting': 'Welcome back, Carlos! 👋',
    'overview.subgreeting': 'Holistic growth, wellness, and learning dashboard for the Mendoza-Ríos Family.',
    'overview.kpi.familyBalance': 'Family Harmony Index',
    'overview.kpi.familyBalanceSub': '9.4 / 10 · Outstanding work-study-life balance',
    'overview.kpi.activeGoals': 'Active Strategic Growth Goals',
    'overview.kpi.activeGoalsSub': '5 active family roadmaps (78% milestone completion)',
    'overview.kpi.weeklyHours': 'Weekly Growth Hours',
    'overview.kpi.weeklyHoursSub': '34.5 total hours logged across study, career & wellness',
    'overview.kpi.budget': '2026 Educational Investment Fund',
    'overview.kpi.budgetSub': '$18,400 USD (~$368,000 MXN) · 68% utilized',
    'overview.familyPulse': 'Weekly Household Pulse',
    'overview.upcomingMilestones': 'Upcoming Milestones & High-Impact Events',
    'overview.viewAllMilestones': 'View complete family schedule',
    'overview.aiInsightTitle': 'Smart Recommendation from Family Coach AI',
    'overview.aiInsightText': "Sofía has her official SAT mock exam this Saturday. Given her AP Biology homework load, we recommend scheduling Friday's digital-free dinner at 7:30 PM to guarantee 8.5 hours of restorative sleep and optimize focus.",
    'overview.membersSnapshot': 'Family Members Current Snapshot',
    'overview.viewProfile': 'View Full Profile',

    // Family Page
    'family.title': 'Family Core & Life Vision',
    'family.subtitle': 'Members, Gallup/VIA strengths, household agreements, and shared initiatives bridging our generations in the US and Latin America.',
    'family.addMember': 'Invite Member / Tutor',
    'family.valuesTitle': 'Household Values & Agreements',
    'family.value1.title': 'Academic Bilingualism & Heritage Pride',
    'family.value1.desc': 'We honor our Latin American roots with the same intellectual rigor with which we master global professional English.',
    'family.value2.title': 'Intellectual Curiosity without Toxic Pressure',
    'family.value2.desc': 'We foster a genuine love of learning, resilience in the face of setbacks, and mental health above mere grades.',
    'family.value3.title': 'Intergenerational Solidarity',
    'family.value3.desc': 'Grandparents wisdom illuminates the youth; our childrens technological agility empowers the elder generation.',
    'family.projectsTitle': 'Collective Family Projects',
    'family.membersList': 'Mendoza-Ríos Family Members',

    // Personal Development Page
    'pdev.title': 'Personal Development & Well-being',
    'pdev.subtitle': 'Executive career growth for parents, social-emotional leadership for teens, and holistic flourishing habits.',
    'pdev.tabParents': 'Career & Executive Leadership (Parents)',
    'pdev.tabYouth': 'Emotional Intelligence & Teen Leadership',
    'pdev.tabWellness': 'Health, Sleep & Digital Sunset',
    'pdev.tabFinance': 'Financial Literacy & Generational Wealth',

    // Learning Page
    'learning.title': 'Learning & Academic Roadmaps',
    'learning.subtitle': 'Customized curricula, elite university admissions prep (USA & LATAM), world languages, and cutting-edge certifications.',
    'learning.tabAll': 'All Programs',
    'learning.tabCollege': 'Admissions & SAT / IB',
    'learning.tabStem': 'STEM, AI & Robotics',
    'learning.tabLanguages': 'Languages & Humanities',
    'learning.activeTracks': 'Active Learning Pathways',
    'learning.extracurricular': 'Extracurricular Activities & Leadership Portfolio',
    'learning.suggested': 'AI Recommended Opportunities for 2027',

    // Insights Page
    'insights.title': 'Insights & Family Analytics',
    'insights.subtitle': 'Transparent analytics on time allocation, study habits, emotional well-being, and competitive college readiness.',
    'insights.radarTitle': 'Family Holistic Growth Radar',
    'insights.screenVsStudy': 'Focused Study Time vs Recreative Screen Time',
    'insights.collegeReadiness': 'Top University Admissions Readiness (Stanford, Columbia, Tec, UNAM)',
    'insights.gapsTitle': 'Growth Areas & Early Skill Gap Detection',

    // Admin Page
    'admin.title': 'Family Household Administration',
    'admin.subtitle': 'Subscription settings, educational budget allocation, parental permissions, and school LMS integrations.',
    'admin.budgetSection': 'Educational Budget & Annual Breakdown (USD / MXN)',
    'admin.membersPermissions': 'Permissions & Minor Protection Controls',
    'admin.biculturalSettings': 'Regional, Bicultural & Timezone Preferences',
    'admin.integrations': 'School & Calendar Integrations',
  },
};

export function LanguageProvider({ children }: { children: React.ReactNode }) {
  const [lang, setLangState] = useState<Language>(() => {
    const saved = localStorage.getItem('engrams_family_lang');
    return saved === 'en' ? 'en' : 'es';
  });

  const setLang = (newLang: Language) => {
    setLangState(newLang);
    localStorage.setItem('engrams_family_lang', newLang);
  };

  const t = (key: string, defaultText?: string): string => {
    return DICTIONARY[lang][key] || defaultText || key;
  };

  return (
    <LanguageContext.Provider value={{ lang, setLang, t }}>
      {children}
    </LanguageContext.Provider>
  );
}

export function useLanguage() {
  const context = useContext(LanguageContext);
  if (!context) {
    throw new Error('useLanguage must be used within a LanguageProvider');
  }
  return context;
}
