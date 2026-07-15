import { en } from './en'
import { es } from './es'

export type Language = 'en' | 'es'

export const translations = {
  en,
  es
}

export const getTranslation = (language: Language = 'en') => {
  return translations[language] || translations.en
}
