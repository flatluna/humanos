import { useState } from 'react'
import { loadTranslations } from '@i18n/useLanguage'

export function useTranslation(namespace: string) {
  const [language] = useState<'en' | 'es'>('en')
  const translations = loadTranslations(namespace, language)
  
  return translations
}
