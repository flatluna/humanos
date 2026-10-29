import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Search, Sparkles, Layers, Trash2, AlertTriangle, X } from 'lucide-react';
import { deleteCapability, getCapabilities, type BackendCapability } from '../lib/api/capabilitiesApi';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { deleteStructuredLearningDocument, getStructuredLearningDocuments, type StructuredLearningDocumentSummary } from '../lib/api/structuredLearningApi';
import CapabilityCard from '../components/CapabilityCard';
import StructuredLearningDocumentCard from '../components/StructuredLearningDocumentCard';
import SubjectPill from '../components/SubjectPill';
import LoadingSpinner from '../components/LoadingSpinner';
import { getSubjectIcon } from '../lib/subjectVisuals';

export default function HomePage() {
  const { t } = useTranslation();
  const [capabilities, setCapabilities] = useState<BackendCapability[]>([]);
  const [structuredLearningDocuments, setStructuredLearningDocuments] = useState<StructuredLearningDocumentSummary[]>([]);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeSubject, setActiveSubject] = useState<string | null>(null);
  const [activeLanguage, setActiveLanguage] = useState<'es' | 'en' | null>(null);
  const [ownershipFilter, setOwnershipFilter] = useState<'all' | 'mine' | 'global'>('all');
  const [search, setSearch] = useState('');
  const [selectedDocumentIds, setSelectedDocumentIds] = useState<Set<string>>(new Set());
  const [selectedCapabilityIds, setSelectedCapabilityIds] = useState<Set<string>>(new Set());
  const [deletingDocuments, setDeletingDocuments] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [deleteConfirmation, setDeleteConfirmation] = useState('');

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    Promise.all([getCapabilities(), getSubjects(), getStructuredLearningDocuments().catch(() => [])])
      .then(([caps, subs, structuredDocs]) => {
        if (cancelled) return;
        setCapabilities(caps);
        setSubjects(subs);
        setStructuredLearningDocuments(structuredDocs);
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

  const subjectsInUse = useMemo(() => {
    const codes = new Set(capabilities.map((c) => c.SubjectCode).filter(Boolean));
    return subjects.filter((s) => codes.has(s.Code));
  }, [capabilities, subjects]);

  const filteredCapabilities = useMemo(() => {
    return capabilities.filter((c) => {
      if (activeSubject && c.SubjectCode !== activeSubject) return false;
      if (activeLanguage && (c.ContentLanguageCode ?? 'es') !== activeLanguage) return false;
      const isMine = c.OwnerPersonId !== null || c.OwnerTenantId !== null;
      if (ownershipFilter === 'mine' && !isMine) return false;
      if (ownershipFilter === 'global' && isMine) return false;
      if (search.trim()) {
        const q = search.trim().toLowerCase();
        if (!c.Name.toLowerCase().includes(q) && !(c.LearningSummary ?? '').toLowerCase().includes(q)) {
          return false;
        }
      }
      return true;
    });
  }, [capabilities, activeSubject, activeLanguage, ownershipFilter, search]);

  const filteredStructuredLearningDocuments = useMemo(() => {
    if (activeSubject) return [];
    let docs = structuredLearningDocuments;
    if (activeLanguage) {
      docs = docs.filter((d) => (d.Language ?? 'es') === activeLanguage);
    }
    if (ownershipFilter === 'mine') {
      docs = docs.filter((d) => !d.IsGlobal);
    } else if (ownershipFilter === 'global') {
      docs = docs.filter((d) => d.IsGlobal);
    }
    if (!search.trim()) return docs;
    const q = search.trim().toLowerCase();
    return docs.filter((d) => d.DocumentName.toLowerCase().includes(q));
  }, [structuredLearningDocuments, activeSubject, activeLanguage, ownershipFilter, search]);

  const unifiedCatalog = useMemo(() => {
    const items = [
      ...filteredCapabilities.map((capability) => ({
        kind: 'capability' as const,
        sortDate: capability.UpdatedDate || capability.CreatedDate,
        item: capability,
      })),
      ...filteredStructuredLearningDocuments.map((document) => ({
        kind: 'structured-learning' as const,
        sortDate: document.CreatedDate,
        item: document,
      })),
    ];

    return items.sort((a, b) => new Date(b.sortDate).getTime() - new Date(a.sortDate).getTime());
  }, [filteredCapabilities, filteredStructuredLearningDocuments]);

  const toggleDocument = (documentId: string) => {
    setSelectedDocumentIds((current) => {
      const next = new Set(current);
      if (next.has(documentId)) next.delete(documentId);
      else next.add(documentId);
      return next;
    });
  };

  const deleteSelectedDocuments = async () => {
    const documentIds = Array.from(selectedDocumentIds);
    const capabilityIds = Array.from(selectedCapabilityIds);
    if ((documentIds.length === 0 && capabilityIds.length === 0) || deletingDocuments) return;

    setDeletingDocuments(true);
    setError(null);
    try {
      await Promise.all([
        ...documentIds.map((id) => deleteStructuredLearningDocument(id)),
        ...capabilityIds.map((id) => deleteCapability(id)),
      ]);
      setStructuredLearningDocuments((documents) => documents.filter((document) => !selectedDocumentIds.has(document.StructuredLearningDocumentId)));
      setCapabilities((capabilities) => capabilities.filter((capability) => !selectedCapabilityIds.has(capability.CapabilityId)));
      setSelectedDocumentIds(new Set());
      setSelectedCapabilityIds(new Set());
      setDeleteConfirmation('');
      setDeleteModalOpen(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete the selected courses.');
    } finally {
      setDeletingDocuments(false);
    }
  };

  return (
    <div>
      <section className="relative overflow-hidden border-b border-white/10">
        <div className="absolute inset-0 bg-mesh-gradient opacity-60" />
        <div className="relative mx-auto max-w-7xl px-6 py-20 text-center">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-white/5 px-3.5 py-1.5 text-xs font-medium text-slate-300">
            <Sparkles className="h-3.5 w-3.5 text-brand-400" />
            {t('home.badge')}
          </span>
          <h1 className="mt-6 text-4xl sm:text-5xl font-bold tracking-tight text-white">
            {t('home.titlePrefix')}{' '}
            <span className="shimmer-text">{t('home.titleHighlight')}</span>
          </h1>
          <p className="mx-auto mt-4 max-w-3xl text-base sm:text-lg text-slate-300 leading-relaxed">
            {t('home.subtitle')}
          </p>
          <p className="mx-auto mt-3 max-w-2xl text-sm sm:text-base text-slate-400">
            {t('home.subtitleResult')}
          </p>
          <p className="mx-auto mt-4 max-w-xl text-xs sm:text-sm font-medium text-brand-400">
            {t('home.subtitleFunctional')}
          </p>

          <div className="mx-auto mt-8 max-w-xl">
            <div className="relative">
              <Search className="pointer-events-none absolute left-4 top-1/2 h-4.5 w-4.5 -translate-y-1/2 text-slate-500" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={t('home.searchPlaceholder')}
                className="w-full rounded-2xl border border-white/10 bg-white/[0.04] py-3.5 pl-11 pr-4 text-sm text-white placeholder:text-slate-500 backdrop-blur-xl focus:border-brand-400/50 focus:outline-none focus:ring-2 focus:ring-brand-500/30"
              />
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-6 py-10">
        {subjectsInUse.length > 0 && (
          <div className="mb-4 flex gap-2 overflow-x-auto pb-2">
            <SubjectPill label={t('home.allSubjects')} active={activeSubject === null} onClick={() => setActiveSubject(null)} icon={<Layers className="h-3.5 w-3.5" />} />
            {subjectsInUse.map((subject) => {
              const Icon = getSubjectIcon(subject.Code);
              return (
                <SubjectPill
                  key={subject.SubjectId}
                  label={subject.Name}
                  active={activeSubject === subject.Code}
                  onClick={() => setActiveSubject(subject.Code)}
                  icon={<Icon className="h-3.5 w-3.5" />}
                />
              );
            })}
          </div>
        )}

        <div className="mb-4 flex gap-2 overflow-x-auto pb-2">
          <SubjectPill label={t('home.ownershipAll')} active={ownershipFilter === 'all'} onClick={() => setOwnershipFilter('all')} />
          <SubjectPill label={t('home.ownershipMine')} active={ownershipFilter === 'mine'} onClick={() => setOwnershipFilter('mine')} />
          <SubjectPill label={t('home.ownershipGlobal')} active={ownershipFilter === 'global'} onClick={() => setOwnershipFilter('global')} />
        </div>

        <div className="mb-8 flex gap-2 overflow-x-auto pb-2">
          <SubjectPill label={t('home.allLanguages')} active={activeLanguage === null} onClick={() => setActiveLanguage(null)} />
          <SubjectPill label={t('home.spanish')} active={activeLanguage === 'es'} onClick={() => setActiveLanguage('es')} />
          <SubjectPill label={t('home.english')} active={activeLanguage === 'en'} onClick={() => setActiveLanguage('en')} />
        </div>

        {(selectedDocumentIds.size > 0 || selectedCapabilityIds.size > 0) && (
          <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-red-500/30 bg-red-500/10 px-4 py-3">
            <span className="text-sm text-red-100">{selectedDocumentIds.size + selectedCapabilityIds.size} course{selectedDocumentIds.size + selectedCapabilityIds.size === 1 ? '' : 's'} selected</span>
            <button
              type="button"
              onClick={() => {
                setDeleteConfirmation('');
                setDeleteModalOpen(true);
              }}
              disabled={deletingDocuments}
              className="inline-flex items-center gap-2 rounded-lg bg-red-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-red-500 disabled:cursor-not-allowed disabled:opacity-50"
            >
              <Trash2 className="h-4 w-4" />
              {deletingDocuments ? 'Deleting...' : 'Delete selected'}
            </button>
          </div>
        )}

        {loading && <LoadingSpinner label={t('home.loading')} />}

        {!loading && error && (
          <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-center text-red-300">
            {t('home.loadError', { error })}
          </div>
        )}

        {!loading && !error && unifiedCatalog.length === 0 && (
          <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center text-slate-400">
            {t('home.noResults')}
          </div>
        )}

        {!loading && !error && unifiedCatalog.length > 0 && (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {unifiedCatalog.map((entry) =>
              entry.kind === 'structured-learning' ? (
                <StructuredLearningDocumentCard
                  key={entry.item.StructuredLearningDocumentId}
                  document={entry.item}
                  selected={selectedDocumentIds.has(entry.item.StructuredLearningDocumentId)}
                  onToggle={() => toggleDocument(entry.item.StructuredLearningDocumentId)}
                />
              ) : (
                  <CapabilityCard
                    key={entry.item.CapabilityId}
                    capability={entry.item}
                    selected={selectedCapabilityIds.has(entry.item.CapabilityId)}
                    onToggle={() => setSelectedCapabilityIds((current) => {
                      const next = new Set(current);
                      if (next.has(entry.item.CapabilityId)) next.delete(entry.item.CapabilityId);
                      else next.add(entry.item.CapabilityId);
                      return next;
                    })}
                  />
              )
            )}
          </div>
        )}
      </section>

      {deleteModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/75 p-4 backdrop-blur-sm" role="dialog" aria-modal="true" aria-labelledby="delete-courses-title">
          <div className="w-full max-w-md overflow-hidden rounded-2xl border border-red-400/25 bg-slate-900 shadow-2xl shadow-black/50">
            <div className="border-b border-white/10 px-6 py-5">
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-red-500/15 text-red-300 ring-1 ring-red-400/25">
                    <AlertTriangle className="h-5 w-5" />
                  </div>
                  <div>
                    <h2 id="delete-courses-title" className="text-lg font-semibold text-white">Delete selected courses</h2>
                    <p className="mt-0.5 text-sm text-slate-400">This action cannot be undone.</p>
                  </div>
                </div>
                <button type="button" onClick={() => setDeleteModalOpen(false)} disabled={deletingDocuments} className="rounded-lg p-1.5 text-slate-400 transition hover:bg-white/10 hover:text-white disabled:opacity-50" aria-label="Close">
                  <X className="h-5 w-5" />
                </button>
              </div>
            </div>
            <div className="space-y-4 px-6 py-5">
              <div className="rounded-xl border border-red-400/20 bg-red-500/10 px-4 py-3 text-sm text-red-100">
                You are about to permanently delete <strong>{selectedDocumentIds.size + selectedCapabilityIds.size}</strong> course{selectedDocumentIds.size + selectedCapabilityIds.size === 1 ? '' : 's'}, including their sections, progress, illustrations, and generated content.
              </div>
              <div>
                <label htmlFor="delete-confirmation" className="block text-sm font-medium text-slate-200">Type <strong className="text-red-300">Delete</strong> to confirm</label>
                <input
                  id="delete-confirmation"
                  type="text"
                  value={deleteConfirmation}
                  onChange={(event) => setDeleteConfirmation(event.target.value)}
                  disabled={deletingDocuments}
                  autoFocus
                  placeholder="Delete"
                  className="mt-2 w-full rounded-xl border border-white/10 bg-white/[0.04] px-3.5 py-2.5 text-white outline-none transition placeholder:text-slate-600 focus:border-red-400/60 focus:ring-2 focus:ring-red-500/20 disabled:opacity-50"
                />
              </div>
              <div className="flex justify-end gap-3 pt-1">
                <button type="button" onClick={() => setDeleteModalOpen(false)} disabled={deletingDocuments} className="rounded-xl px-4 py-2.5 text-sm font-medium text-slate-300 transition hover:bg-white/10 hover:text-white disabled:opacity-50">Cancel</button>
                <button type="button" onClick={deleteSelectedDocuments} disabled={deleteConfirmation !== 'Delete' || deletingDocuments} className="inline-flex items-center gap-2 rounded-xl bg-red-600 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-red-500 disabled:cursor-not-allowed disabled:opacity-40">
                  <Trash2 className="h-4 w-4" />
                  {deletingDocuments ? 'Deleting...' : 'Delete permanently'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
