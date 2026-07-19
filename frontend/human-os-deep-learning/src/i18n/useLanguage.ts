import { useState } from 'react'

type Language = 'en' | 'es'

interface LanguageContextType {
  language: Language
  setLanguage: (lang: Language) => void
}

export function useLanguage(): LanguageContextType {
  const [language, setLanguage] = useState<Language>('en')
  
  return { language, setLanguage }
}

export function loadTranslations(namespace: string, language: Language) {
  try {
    return require(`./locales/${language}/${namespace}.json`)
  } catch (error) {
    console.warn(`Translation not found: ${namespace}.${language}`)
    return {}
  }
}
