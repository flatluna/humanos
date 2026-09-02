import { useTranslation } from 'react-i18next';
import i18n from '@/i18n';
import { translations, type Language } from './translations';

const LANGUAGE_KEY = 'humanos_employee_language';

export function setLanguage(language: Language) {
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(LANGUAGE_KEY, language);
    window.localStorage.setItem('humanos.employee.language', language);
  }
  void i18n.changeLanguage(language);
}

export function useI18n() {
  const { i18n: i18nInstance } = useTranslation();
  const currentLang = (i18nInstance.language?.startsWith('en') ? 'en' : 'es') as Language;
  return {
    t: translations[currentLang],
    language: currentLang,
  };
}
