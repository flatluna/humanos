import { useEffect } from 'react';
import { Clock, Sparkles } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { useAuth } from '@/auth/AuthContext';
import { useGrowWithUsPlanStore } from '@/store/useGrowWithUsPlanStore';

/**
 * Left-side "Advance Your Knowledge" saved-searches panel (2026-09-28) —
 * lists the learner's past "Grow With Us" searches by date/name (most
 * recent first) and lets clicking a saved search re-open its full plan
 * without regenerating it. The search box itself moved to
 * <see cref="GrowWithUsSavedSearchesToolbar"/> (top of page, 2026-09-28)
 * to free up vertical space here for the list.
 */
export function GrowWithUsSavedSearchesPanel() {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const personId = user?.personId;

  const { savedSearches, activeSavedSearchId, isLoadingSavedSearches, isLoadingSavedSearch, loadSavedSearches, openSavedSearch } =
    useGrowWithUsPlanStore();

  useEffect(() => {
    if (personId) {
      void loadSavedSearches(personId);
    }
  }, [personId, loadSavedSearches]);

  if (!personId) {
    return null;
  }

  return (
    <Card className="h-fit border-slate-200/80 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
      <div className="flex items-center gap-2">
        <Sparkles className="h-4 w-4 text-amber-600 dark:text-amber-400" aria-hidden="true" />
        <h2 className="text-sm font-bold text-slate-950 dark:text-white">{isEn ? 'Saved searches' : 'Búsquedas guardadas'}</h2>
      </div>

      <div className="mt-3 space-y-1.5">
        {isLoadingSavedSearches ? (
          <p className="px-1 text-xs font-medium text-slate-400 dark:text-white/45">{isEn ? 'Loading…' : 'Cargando…'}</p>
        ) : savedSearches.length === 0 ? (
          <p className="px-1 text-xs font-medium text-slate-400 dark:text-white/45">
            {isEn ? 'No saved searches yet.' : 'Aún no hay búsquedas guardadas.'}
          </p>
        ) : (
          savedSearches.map((item) => (
            <button
              key={item.growWithUsSavedSearchId}
              type="button"
              disabled={isLoadingSavedSearch}
              onClick={() => void openSavedSearch(personId, item.growWithUsSavedSearchId)}
              className={`w-full rounded-xl px-3 py-2 text-left text-xs transition disabled:opacity-60 ${
                activeSavedSearchId === item.growWithUsSavedSearchId
                  ? 'bg-amber-50 ring-1 ring-amber-300 dark:bg-amber-400/10 dark:ring-amber-400/30'
                  : 'hover:bg-slate-50 dark:hover:bg-white/[0.04]'
              }`}
            >
              <p className="truncate font-semibold text-slate-800 dark:text-white/85">{item.topic}</p>
              <p className="mt-0.5 flex items-center gap-1 text-[10px] font-medium text-slate-400 dark:text-white/45">
                <Clock className="h-3 w-3" aria-hidden="true" />
                {new Date(item.createdDate).toLocaleDateString(isEn ? 'en-US' : 'es-ES', { year: 'numeric', month: 'short', day: 'numeric' })}
              </p>
            </button>
          ))
        )}
      </div>
    </Card>
  );
}

