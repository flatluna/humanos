import { useTranslation } from 'react-i18next';
import { useAuth } from '@/auth/AuthContext';
import { updatePreferredLanguage } from '@/api/humanOsApi';

export function LanguageSwitcher() {
  const { i18n, t } = useTranslation();
  const { user } = useAuth();
  const isEnglish = i18n.language.startsWith('en');

  function changeLanguage(lang: 'en' | 'es') {
    void i18n.changeLanguage(lang);
    // Persist to the account so it "sticks" across devices/sessions —
    // fire-and-forget: a failed save shouldn't block switching the UI
    // language for the current session.
    if (user?.personId) {
      updatePreferredLanguage(user.personId, lang).catch((error) => {
        console.warn('Could not persist preferred language to the account.', error);
      });
    }
  }

  return (
    <div className="flex items-center rounded-full border border-slate-200 p-0.5 text-xs font-semibold dark:border-white/10">
      <button
        type="button"
        onClick={() => changeLanguage('en')}
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
        onClick={() => changeLanguage('es')}
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
