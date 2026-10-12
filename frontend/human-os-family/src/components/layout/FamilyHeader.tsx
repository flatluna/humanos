import { useState } from 'react';
import { Moon, Sun, Sparkles, ChevronDown, Heart, Shield } from 'lucide-react';
import { useLanguage } from '../../context/LanguageContext';
import { useTheme } from '../../context/ThemeContext';
import { useFamily } from '../../context/FamilyContext';

export function FamilyHeader() {
  const { lang, setLang, t } = useLanguage();
  const { theme, toggleTheme } = useTheme();
  const { activeCurrency, setActiveCurrency, setIsAiCoachOpen, householdName, householdCities } = useFamily();
  const [profileDropdownOpen, setProfileDropdownOpen] = useState(false);

  return (
    <header className="sticky top-0 z-30 flex h-20 items-center justify-between border-b border-warm-200/80 bg-white/90 px-6 backdrop-blur-md transition-colors dark:border-white/10 dark:bg-[#0b0f17]/90">
      {/* Brand & Household identity */}
      <div className="flex items-center gap-4">
        <div className="relative">
          <img
            src="/engramslogo.png"
            alt="EngramsIQ Family"
            className="h-11 w-11 rounded-2xl object-cover shadow-md shadow-emerald-500/20 ring-2 ring-emerald-500/20"
          />
          <span className="absolute -bottom-1 -right-1 flex h-4 w-4 items-center justify-center rounded-full bg-emerald-500 text-[10px] text-white">
            <Heart className="h-2.5 w-2.5 fill-white" />
          </span>
        </div>

        <div className="leading-tight">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-base font-bold tracking-tight text-warm-900 dark:text-white">
              {t('header.brand')}
            </span>
            <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/10 px-2.5 py-0.5 text-xs font-semibold text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300">
              <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse" />
              {householdName}
            </span>
            <span className="hidden sm:inline-flex items-center gap-1 rounded-full border border-warm-200 bg-warm-50 px-2 py-0.5 text-[11px] font-medium text-warm-600 dark:border-white/10 dark:bg-white/5 dark:text-warm-300">
              🇺🇸 Austin · 🇲🇽 CDMX
            </span>
          </div>
          <p className="mt-0.5 text-xs font-medium text-warm-500 dark:text-warm-400">
            {t('header.tagline')}
          </p>
        </div>
      </div>

      {/* Right controls */}
      <div className="flex items-center gap-2.5">
        {/* Currency Switcher (USD / MXN) */}
        <div className="hidden md:flex items-center rounded-full border border-warm-200 bg-warm-50 p-0.5 text-xs font-semibold dark:border-white/10 dark:bg-white/5">
          <button
            type="button"
            onClick={() => setActiveCurrency('USD')}
            className={`rounded-full px-2.5 py-1 transition ${
              activeCurrency === 'USD'
                ? 'bg-white text-warm-900 shadow-sm dark:bg-warm-800 dark:text-white'
                : 'text-warm-500 hover:text-warm-900 dark:text-warm-400 dark:hover:text-white'
            }`}
          >
            $ USD
          </button>
          <button
            type="button"
            onClick={() => setActiveCurrency('MXN')}
            className={`rounded-full px-2.5 py-1 transition ${
              activeCurrency === 'MXN'
                ? 'bg-white text-warm-900 shadow-sm dark:bg-warm-800 dark:text-white'
                : 'text-warm-500 hover:text-warm-900 dark:text-warm-400 dark:hover:text-white'
            }`}
          >
            $ MXN
          </button>
        </div>

        {/* Bilingual Language Switcher (ES / EN) */}
        <div
          className="flex items-center rounded-full border border-emerald-300/60 bg-emerald-50/50 p-0.5 text-xs font-bold dark:border-emerald-500/30 dark:bg-emerald-950/30"
          role="group"
          aria-label="Seleccionar idioma"
        >
          <button
            type="button"
            onClick={() => setLang('es')}
            aria-pressed={lang === 'es'}
            className={`flex items-center gap-1 rounded-full px-2.5 py-1 transition ${
              lang === 'es'
                ? 'bg-emerald-600 text-white shadow-sm shadow-emerald-600/30'
                : 'text-emerald-800 hover:text-emerald-950 dark:text-emerald-300 dark:hover:text-white'
            }`}
          >
            <span>🇲🇽</span> ES
          </button>
          <button
            type="button"
            onClick={() => setLang('en')}
            aria-pressed={lang === 'en'}
            className={`flex items-center gap-1 rounded-full px-2.5 py-1 transition ${
              lang === 'en'
                ? 'bg-emerald-600 text-white shadow-sm shadow-emerald-600/30'
                : 'text-emerald-800 hover:text-emerald-950 dark:text-emerald-300 dark:hover:text-white'
            }`}
          >
            <span>🇺🇸</span> EN
          </button>
        </div>

        {/* AI Family Coach button */}
        <button
          type="button"
          onClick={() => setIsAiCoachOpen(true)}
          className="hidden sm:inline-flex items-center gap-1.5 rounded-full bg-gradient-to-r from-emerald-600 via-teal-600 to-indigo-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-md shadow-emerald-600/20 transition hover:opacity-95 hover:shadow-lg"
        >
          <Sparkles className="h-3.5 w-3.5" />
          <span>{t('nav.coachButton')}</span>
        </button>

        {/* Theme Toggle */}
        <button
          type="button"
          onClick={toggleTheme}
          aria-label="Cambiar tema claro/oscuro"
          className="flex h-9 w-9 items-center justify-center rounded-full border border-warm-200 text-warm-600 transition hover:bg-warm-100 dark:border-white/10 dark:text-warm-300 dark:hover:bg-white/10"
        >
          {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
        </button>

        {/* User Profile dropdown */}
        <div className="relative">
          <button
            type="button"
            onClick={() => setProfileDropdownOpen((prev) => !prev)}
            className="flex items-center gap-2 rounded-full border border-warm-200 py-1 pl-1 pr-2.5 text-xs font-medium text-warm-700 transition hover:bg-warm-100 dark:border-white/10 dark:text-warm-200 dark:hover:bg-white/10"
          >
            <div className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-emerald-600 to-teal-700 text-xs font-bold text-white shadow-sm">
              C
            </div>
            <div className="hidden text-left sm:block">
              <span className="block font-semibold">Carlos Mendoza</span>
              <span className="block text-[10px] text-warm-400">Padre / Co-Admin</span>
            </div>
            <ChevronDown className="h-3 w-3 opacity-60" />
          </button>

          {profileDropdownOpen && (
            <div className="absolute right-0 mt-2 w-72 rounded-2xl border border-warm-200 bg-white p-3 shadow-xl dark:border-white/10 dark:bg-[#111827]">
              <div className="border-b border-warm-100 pb-3 dark:border-white/10">
                <p className="font-bold text-warm-900 dark:text-white">Carlos Mendoza, M.Sc.</p>
                <p className="text-xs text-warm-500 dark:text-warm-400">carlos.mendoza@engrams.family</p>
                <span className="mt-2 inline-flex items-center gap-1 rounded-md bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">
                  <Shield className="h-3 w-3" />
                  {t('header.role')}
                </span>
              </div>

              <div className="py-2 text-xs text-warm-600 dark:text-warm-300 space-y-1">
                <p className="font-semibold text-warm-800 dark:text-warm-200">{householdName}</p>
                <p className="text-warm-400">{householdCities}</p>
                <p className="text-emerald-600 dark:text-emerald-400 font-medium">{t('header.bilingualBadge')}</p>
              </div>

              <div className="mt-2 border-t border-warm-100 pt-2 dark:border-white/10">
                <button
                  type="button"
                  onClick={() => {
                    setIsAiCoachOpen(true);
                    setProfileDropdownOpen(false);
                  }}
                  className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 text-left text-xs font-medium text-warm-700 hover:bg-warm-100 dark:text-warm-200 dark:hover:bg-white/5"
                >
                  <span className="flex items-center gap-2">
                    <Sparkles className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />
                    Consultar Family Coach AI
                  </span>
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
