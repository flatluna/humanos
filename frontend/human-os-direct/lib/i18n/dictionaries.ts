export type Locale = "en" | "es";

export const locales: Locale[] = ["en", "es"];

export const defaultLocale: Locale = "en";

/**
 * Minimal in-app dictionary-based i18n (no URL locale prefix), so the
 * `app/` route structure stays exactly as specified — language is a user
 * preference (stored client-side), not a routing concern.
 */
export const dictionaries = {
  en: {
    common: {
      appName: "Human OS",
      today: "Today",
      language: "Language",
    },
    nav: {
      today: "Today",
      universe: "My Universe",
      capabilities: "Capabilities",
      portfolio: "My Portfolio",
      goals: "My Goals",
      agents: "My Agents",
      evolution: "My Evolution",
      settings: "Settings",
      collapse: "Collapse menu",
      expand: "Expand menu",
    },
    topbar: {
      searchPlaceholder: "Search...",
      notifications: "Notifications",
      toggleTheme: "Toggle theme",
      openProfile: "Open profile menu",
    },
    profile: {
      title: "Profile",
      email: "Email",
      objectId: "Account ID",
      settings: "Settings",
      signOut: "Sign out",
      close: "Close",
    },
    page: {
      placeholder: "Page content goes here",
      universe: "My Universe",
      capabilities: "Capabilities",
      portfolio: "My Portfolio",
      goals: "My Goals",
      agents: "My Agents",
      evolution: "My Evolution",
    },
    goals: {
      header: {
        subtitle: "Where you're growing towards",
        newGoal: "+ New goal",
      },
      stats: {
        active: "Active",
        achieved: "Achieved",
        average: "Average",
      },
      capabilityList: {
        title: "Capabilities that take you there:",
        more: "and more...",
      },
      card: {
        progress: "Progress",
        details: "View details →",
      },
      achieved: {
        title: "Achieved Goals",
      },
      mock: {
        invest: "Invest my money wisely",
        invest_desc: "Learn to manage and grow my wealth",
        firstJob: "Get my first job",
        firstJob_desc: "Secure my first professional position",
        timeManagement: "Learn to organize my time",
        timeManagement_desc: "Master time management",
      },
    },
    capabilities: {
      mock: {
        financial: "Financial Clarity",
        vision: "Vision Setting",
        deepFocus: "Deep Focus",
        energy: "Energy Management",
      },
    },
  },
  es: {
    common: {
      appName: "Human OS",
      today: "Hoy",
      language: "Idioma",
    },
    nav: {
      today: "Hoy",
      universe: "Mi Universo",
      capabilities: "Capacidades",
      portfolio: "Mi Portafolio",
      goals: "Mis Metas",
      agents: "Mis Agentes",
      evolution: "Mi Evolución",
      settings: "Ajustes",
      collapse: "Contraer menú",
      expand: "Expandir menú",
    },
    topbar: {
      searchPlaceholder: "Buscar...",
      notifications: "Notificaciones",
      toggleTheme: "Cambiar tema",
      openProfile: "Abrir menú de perfil",
    },
    profile: {
      title: "Perfil",
      email: "Correo",
      objectId: "ID de cuenta",
      settings: "Ajustes",
      signOut: "Cerrar sesión",
      close: "Cerrar",
    },
    page: {
      placeholder: "Contenido de la página aquí",
      universe: "Mi Universo",
      capabilities: "Capacidades",
      portfolio: "Mi Portafolio",
      goals: "Mis Metas",
      agents: "Mis Agentes",
      evolution: "Mi Evolución",
    },
    goals: {
      header: {
        subtitle: "Hacia dónde estás creciendo",
        newGoal: "+ Nueva meta",
      },
      stats: {
        active: "Activas",
        achieved: "Logradas",
        average: "Promedio",
      },
      capabilityList: {
        title: "Capacidades que te llevan ahí:",
        more: "y más...",
      },
      card: {
        progress: "Progreso",
        details: "Ver detalle →",
      },
      achieved: {
        title: "Metas Logradas",
      },
      mock: {
        invest: "Invertir bien mi dinero",
        invest_desc: "Aprender a gestionar y hacer crecer mi patrimonio",
        firstJob: "Conseguir mi primer trabajo",
        firstJob_desc: "Asegurar mi primer empleo profesional",
        timeManagement: "Aprender a organizar mi tiempo",
        timeManagement_desc: "Dominar la gestión del tiempo",
      },
    },
    capabilities: {
      mock: {
        financial: "Finanzas Personales",
        vision: "Visión Estratégica",
        deepFocus: "Concentración Profunda",
        energy: "Gestión de Energía",
      },
    },
  },
} as const satisfies Record<Locale, any>;
