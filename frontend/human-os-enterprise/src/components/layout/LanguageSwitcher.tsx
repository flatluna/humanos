import { useTranslation } from 'react-i18next';

export function LanguageSwitcher() {
  const { i18n, t } = useTranslation();
  const current = i18n.language?.startsWith('en') ? 'en' : 'es';

  function changeLanguage(lang: 'es' | 'en') {
    void i18n.changeLanguage(lang);
  }

  return (
    <div
      className="flex items-center rounded-full border border-slate-200 p-0.5 text-xs font-semibold dark:border-white/10"
      role="group"
      aria-label={t('common.changeLanguage')}
    >
      <button
        type="button"
        onClick={() => changeLanguage('es')}
        aria-pressed={current === 'es'}
        title={t('common.languageSpanish')}
        className={`rounded-full px-2.5 py-1 transition ${
          current === 'es'
            ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
            : 'text-slate-400 hover:text-slate-700 dark:text-white/40 dark:hover:text-white'
        }`}
      >
        ES
      </button>
      <button
        type="button"
        onClick={() => changeLanguage('en')}
        aria-pressed={current === 'en'}
        title={t('common.languageEnglish')}
        className={`rounded-full px-2.5 py-1 transition ${
          current === 'en'
            ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
            : 'text-slate-400 hover:text-slate-700 dark:text-white/40 dark:hover:text-white'
        }`}
      >
        EN
      </button>
    </div>
  );
}
