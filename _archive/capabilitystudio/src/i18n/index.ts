import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import es from './locales/es';
import en from './locales/en';

/**
 * Capability Studio i18n (2026-07-26). Unlike human-os-web (the real
 * student app, whose default is English), Capability Studio's existing
 * UI has always been 100% Spanish for its authoring team — so Spanish
 * stays the default/fallback here. Detection order still lets an
 * explicit prior choice (localStorage) or the browser's own language
 * win, but falls back to Spanish rather than English when neither is
 * conclusive.
 */
void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      es: { translation: es },
      en: { translation: en },
    },
    fallbackLng: 'es',
    supportedLngs: ['es', 'en'],
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
      lookupLocalStorage: 'capabilitystudio_language',
    },
  });

export default i18n;
