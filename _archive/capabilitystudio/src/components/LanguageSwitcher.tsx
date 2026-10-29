import { useTranslation } from 'react-i18next';

/**
 * Compact ES/EN toggle for Capability Studio's header (2026-07-26).
 * Capability Studio has no student login — it's used by
 * instructors/professors authoring content — so there is no per-account
 * preference to sync to the backend here (unlike human-os-web's
 * LanguageSwitcher, which persists to PersonProfile.PreferredLanguage).
 * The choice is just remembered locally via i18next-browser-languagedetector's
 * localStorage cache (see src/i18n/index.ts).
 */
export default function LanguageSwitcher() {
  const { i18n, t } = useTranslation();
  const current = i18n.language?.startsWith('en') ? 'en' : 'es';

  function changeLanguage(lang: 'es' | 'en') {
    void i18n.changeLanguage(lang);
  }

  return (
    <div
      className="flex items-center gap-0.5 rounded-lg border border-white/10 bg-white/[0.03] p-0.5"
      role="group"
      aria-label={t('common.changeLanguage')}
    >
      <button
        type="button"
        onClick={() => changeLanguage('es')}
        aria-pressed={current === 'es'}
        title={t('common.languageSpanish')}
        className={`rounded-md px-2 py-1.5 text-xs font-semibold transition-colors ${
          current === 'es' ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white'
        }`}
      >
        ES
      </button>
      <button
        type="button"
        onClick={() => changeLanguage('en')}
        aria-pressed={current === 'en'}
        title={t('common.languageEnglish')}
        className={`rounded-md px-2 py-1.5 text-xs font-semibold transition-colors ${
          current === 'en' ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white'
        }`}
      >
        EN
      </button>
    </div>
  );
}
