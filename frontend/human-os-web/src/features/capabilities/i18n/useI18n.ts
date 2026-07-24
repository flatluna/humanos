import { useTranslation } from 'react-i18next';
import { translations, type Language } from './translations';

/**
 * Bridges the ported (from humanlearn) capability-learning pages' own
 * flat `t.xxx` translation dictionaries to the app's real language
 * selection (react-i18next / the sidebar's Inglés/Español switcher),
 * instead of merging ~90 keys into src/locales/{en,es}.ts one by one.
 * Keeps every existing `t.someKey` call in the ported pages working
 * unchanged while staying in sync with the rest of the app's language.
 */
export function useI18n() {
  const { i18n } = useTranslation();
  const language: Language = i18n.language?.toLowerCase().startsWith('es') ? 'es' : 'en';
  return { t: translations[language], language };
}
