import { useTranslation } from 'react-i18next';
import type { LocalizedText } from './types';

/** Resolves a `LocalizedText` value to a plain string in the current
 *  i18next language, falling back to English if the current language
 *  is not (yet) supported for that piece of content.
 */
export function useLocalizedText(): (text: LocalizedText) => string {
  const { i18n } = useTranslation();

  return (text: LocalizedText) => {
    const lang = i18n.language.startsWith('es') ? 'es' : 'en';
    return text[lang] ?? text.en;
  };
}
