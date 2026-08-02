import { translations, type Language } from './translations';

/**
 * Human OS Employee Portal is Spanish-only for now (no language switcher,
 * mirrors human-os-enterprise) — kept the same `{ t, language }` shape the
 * ported capabilities pages (copied from human-os-web) already expect,
 * without pulling in the full react-i18next setup those pages depended on
 * over there. Revisit if this app ever needs a language toggle too.
 */
export function useI18n() {
  const language: Language = 'es';
  return { t: translations[language], language };
}
