// Bilingual strings for the application
export const translations = {
  en: {
    appName: 'Human OS',
    search: 'Search...',
    settings: 'Settings',
    signOut: 'Sign Out',
    home: 'Home',
    studio: 'Studio',
    capabilityLibrary: 'Capability Library',
    progress: 'Progress',
    language: 'Language',
    version: 'v0.1.0 — Scaffolding',
  },
  es: {
    appName: 'Human OS',
    search: 'Buscar...',
    settings: 'Configuración',
    signOut: 'Cerrar sesión',
    home: 'Inicio',
    studio: 'Studio',
    capabilityLibrary: 'Capability Library',
    progress: 'Progreso',
    language: 'Idioma',
    version: 'v0.1.0 — Andamiaje',
  },
};

export type Language = 'en' | 'es';

export const useTranslation = (lang: Language) => {
  return translations[lang];
};
