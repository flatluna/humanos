import { useState } from 'react';
import { Search, X } from 'lucide-react';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { useAuth } from '@/auth/AuthContext';
import { useGrowWithUsPlanStore } from '@/store/useGrowWithUsPlanStore';

/**
 * Full-width search bar above the "Advance Your Knowledge" hero card
 * (2026-09-28) — moved out of the left saved-searches panel to free up
 * vertical space there for the list itself.
 */
export function GrowWithUsSavedSearchesToolbar() {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const personId = user?.personId;
  const { loadSavedSearches, searchSavedSearches } = useGrowWithUsPlanStore();

  const [query, setQuery] = useState('');

  if (!personId) {
    return null;
  }

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    void searchSavedSearches(personId, query);
  };

  const handleClear = () => {
    setQuery('');
    void loadSavedSearches(personId);
  };

  return (
    <form onSubmit={handleSubmit} className="mb-4">
      <div className="mb-2 flex items-center justify-between gap-3">
        <h2 className="text-xl font-bold text-slate-950 dark:text-white">
          {isEn ? 'Search your Grow With Us history' : 'Busca tu historial de Grow With Us'}
        </h2>
        <button
          type="button"
          onClick={handleClear}
          className="flex shrink-0 items-center gap-1.5 rounded-full border border-slate-300 bg-slate-100 px-3.5 py-1.5 text-xs font-semibold text-slate-700 shadow-sm transition hover:bg-slate-200 dark:border-white/20 dark:bg-white/10 dark:text-white/85 dark:hover:bg-white/20"
        >
          <X className="h-3.5 w-3.5" aria-hidden="true" />
          {isEn ? 'Clear' : 'Limpiar'}
        </button>
      </div>
      <div className="relative">
        <Search className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden="true" />
        <input
          type="text"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder={isEn ? 'Search your past Grow With Us findings…' : 'Busca tus hallazgos pasados de Grow With Us…'}
          className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-11 pr-4 text-sm font-medium text-slate-700 shadow-sm outline-none transition focus:border-amber-400 focus:ring-2 focus:ring-amber-100 dark:border-white/10 dark:bg-white/[0.04] dark:text-white/80"
        />
      </div>
    </form>
  );
}
