import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Search, GraduationCap, Trash2, CheckCheck } from 'lucide-react';
import { deleteProgram, getPrograms, type BackendProgram } from '../lib/api/programsApi';
import ProgramCard from '../components/ProgramCard';
import LoadingSpinner from '../components/LoadingSpinner';

export default function ProgramsHomePage() {
  const { t } = useTranslation();
  const [programs, setPrograms] = useState<BackendProgram[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    getPrograms()
      .then((progs) => {
        if (!cancelled) setPrograms(progs);
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return programs;
    return programs.filter(
      (p) => p.Name.toLowerCase().includes(q) || (p.Description ?? '').toLowerCase().includes(q),
    );
  }, [programs, search]);

  const selectedPrograms = useMemo(
    () => programs.filter((program) => selectedIds.includes(program.ProgramId)),
    [programs, selectedIds],
  );

  function toggleProgramSelection(programId: string) {
    setSelectedIds((current) =>
      current.includes(programId) ? current.filter((item) => item !== programId) : [...current, programId],
    );
  }

  function selectVisiblePrograms() {
    const visibleIds = filtered.map((program) => program.ProgramId);
    setSelectedIds((current) => {
      const next = new Set(current);
      for (const id of visibleIds) next.add(id);
      return [...next];
    });
  }

  function clearSelection() {
    setSelectedIds([]);
  }

  async function handleDeleteSelected() {
    if (selectedIds.length === 0) return;

    setDeleting(true);
    try {
      await Promise.all(selectedIds.map((id) => deleteProgram(id)));
      setPrograms((current) => current.filter((program) => !selectedIds.includes(program.ProgramId)));
      setSelectedIds([]);
      setShowDeleteConfirm(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('programsHome.deleteSelectedErrorFallback'));
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div>
      <section className="relative overflow-hidden border-b border-white/10">
        <div className="absolute inset-0 bg-mesh-gradient opacity-60" />
        <div className="relative mx-auto max-w-7xl px-6 py-20 text-center">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-white/5 px-3.5 py-1.5 text-xs font-medium text-slate-300">
            <GraduationCap className="h-3.5 w-3.5 text-brand-400" />
            {t('programsHome.badge')}
          </span>
          <h1 className="mt-6 text-4xl sm:text-5xl font-bold tracking-tight text-white">
            {t('programsHome.title')} <span className="shimmer-text">{t('programsHome.titleHighlight')}</span>
          </h1>
          <p className="mx-auto mt-4 max-w-2xl text-lg text-slate-400">
            {t('programsHome.subtitle')}
          </p>

          <div className="mx-auto mt-8 max-w-xl">
            <div className="relative">
              <Search className="pointer-events-none absolute left-4 top-1/2 h-4.5 w-4.5 -translate-y-1/2 text-slate-500" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={t('programsHome.searchPlaceholder')}
                className="w-full rounded-2xl border border-white/10 bg-white/[0.04] py-3.5 pl-11 pr-4 text-sm text-white placeholder:text-slate-500 backdrop-blur-xl focus:border-brand-400/50 focus:outline-none focus:ring-2 focus:ring-brand-500/30"
              />
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-6 py-10">
        {loading && <LoadingSpinner label={t('programsHome.loading')} />}

        {!loading && error && (
          <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-center text-red-300">
            {t('programsHome.loadError', { error })}
          </div>
        )}

        {!loading && !error && programs.length > 0 && (
          <div className="mb-6 flex flex-col gap-3 rounded-2xl border border-white/10 bg-white/[0.02] p-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-2 text-sm text-slate-300">
              <CheckCheck className="h-4 w-4 text-brand-400" />
              <span>
                {selectedIds.length > 0
                  ? t('programsHome.selectedCount', { count: selectedIds.length })
                  : t('programsHome.noSelection')}
              </span>
            </div>

            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={selectVisiblePrograms}
                className="rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-slate-200 transition hover:border-white/20 hover:bg-white/[0.06]"
              >
                {t('programsHome.selectVisible')}
              </button>
              <button
                type="button"
                onClick={clearSelection}
                disabled={selectedIds.length === 0}
                className="rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-sm text-slate-200 transition hover:border-white/20 hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-40"
              >
                {t('programsHome.clearSelection')}
              </button>
              <button
                type="button"
                onClick={() => setShowDeleteConfirm(true)}
                disabled={selectedIds.length === 0 || deleting}
                className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm font-medium text-red-300 transition hover:border-red-500/50 hover:bg-red-500/15 disabled:cursor-not-allowed disabled:opacity-40"
              >
                {deleting ? t('programsHome.deletingSelected') : t('programsHome.deleteSelected')}
              </button>
            </div>
          </div>
        )}

        {!loading && !error && filtered.length === 0 && (
          <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center text-slate-400">
            {t('programsHome.noResults')}
          </div>
        )}

        {!loading && !error && filtered.length > 0 && (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {filtered.map((program) => (
              <ProgramCard
                key={program.ProgramId}
                program={program}
                selected={selectedIds.includes(program.ProgramId)}
                onToggleSelect={() => toggleProgramSelection(program.ProgramId)}
              />
            ))}
          </div>
        )}
      </section>

      {showDeleteConfirm && selectedPrograms.length > 0 && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" role="dialog" aria-modal="true">
          <div className="w-full max-w-lg rounded-2xl border-2 border-red-600 bg-slate-900 p-6 shadow-2xl">
            <div className="mb-4 flex items-start gap-3">
              <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full border border-red-500/30 bg-red-500/10">
                <Trash2 className="text-red-500" size={28} />
              </div>
              <div>
                <h2 className="text-xl font-bold text-red-500">{t('programsHome.deleteSelectedModalTitle')}</h2>
                <p className="mt-1 text-sm text-slate-400">
                  {t('programsHome.deleteSelectedModalBody', { count: selectedPrograms.length })}
                </p>
              </div>
            </div>

            <div className="max-h-52 space-y-2 overflow-auto rounded-xl border border-white/10 bg-black/20 p-3 text-sm text-slate-300">
              {selectedPrograms.map((program) => (
                <div key={program.ProgramId} className="truncate">
                  • {program.Name}
                </div>
              ))}
            </div>

            <div className="mt-6 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setShowDeleteConfirm(false)}
                disabled={deleting}
                className="rounded-lg px-4 py-2 font-medium text-slate-300 transition-all hover:bg-white/5 disabled:opacity-50"
              >
                {t('programDetail.cancel')}
              </button>
              <button
                type="button"
                onClick={handleDeleteSelected}
                disabled={deleting}
                className="rounded-lg bg-red-600 px-4 py-2 font-medium text-white transition-all hover:bg-red-500 disabled:opacity-50"
              >
                {deleting ? t('programsHome.deletingSelected') : t('programsHome.confirmDeleteSelected')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
