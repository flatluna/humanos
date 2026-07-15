import { useTranslation } from 'react-i18next';

export function LanguageSwitcher() {
  const { i18n, t } = useTranslation();
  const isEnglish = i18n.language.startsWith('en');

  return (
    <div className="flex items-center rounded-full border border-slate-200 p-0.5 text-xs font-semibold dark:border-white/10">
      <button
        type="button"
        onClick={() => i18n.changeLanguage('en')}
        aria-pressed={isEnglish}
        aria-label={t('common.languageEnglish')}
        className={
          isEnglish
            ? 'rounded-full bg-slate-900 px-2.5 py-1 text-white dark:bg-white dark:text-slate-900'
            : 'rounded-full px-2.5 py-1 text-slate-400 hover:text-slate-700 dark:text-white/40 dark:hover:text-white'
        }
      >
        EN
      </button>
      <button
        type="button"
        onClick={() => i18n.changeLanguage('es')}
        aria-pressed={!isEnglish}
        aria-label={t('common.languageSpanish')}
        className={
          !isEnglish
            ? 'rounded-full bg-slate-900 px-2.5 py-1 text-white dark:bg-white dark:text-slate-900'
            : 'rounded-full px-2.5 py-1 text-slate-400 hover:text-slate-700 dark:text-white/40 dark:hover:text-white'
        }
      >
        ES
      </button>
    </div>
  );
}
