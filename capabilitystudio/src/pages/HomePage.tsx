import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Search, Sparkles, Layers, GitBranch, BookOpen } from 'lucide-react';
import { getCapabilities, type BackendCapability } from '../lib/api/capabilitiesApi';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getStructuredLearningDocuments, type StructuredLearningDocumentSummary } from '../lib/api/structuredLearningApi';
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
  const [activeLearningMode, setActiveLearningMode] = useState<BackendCapability['LearningMode'] | null>(null);
  const [search, setSearch] = useState('');

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

  const filtered = useMemo(() => {
    return capabilities.filter((c) => {
      if (activeSubject && c.SubjectCode !== activeSubject) return false;
      if (activeLanguage && (c.ContentLanguageCode ?? 'es') !== activeLanguage) return false;
      if (activeLearningMode && c.LearningMode !== activeLearningMode) return false;
      if (search.trim()) {
        const q = search.trim().toLowerCase();
        if (!c.Name.toLowerCase().includes(q) && !(c.LearningSummary ?? '').toLowerCase().includes(q)) {
          return false;
        }
      }
      return true;
    });
}, [capabilities, activeSubject, activeLanguage, activeLearningMode, search]);

  // Structured Learning documents have no Subject/Language/LearningMode
  // metadata yet (no Capability record — see repo memory), so they're only
  // excluded by the free-text search, and only shown under "All methods"/
  // "All subjects" (any other filter hides them, since they can't match).
  const filteredStructuredLearningDocuments = useMemo(() => {
    if (activeSubject || activeLearningMode) return [];
    if (!search.trim()) return structuredLearningDocuments;
    const q = search.trim().toLowerCase();
    return structuredLearningDocuments.filter((d) => d.DocumentName.toLowerCase().includes(q));
  }, [structuredLearningDocuments, activeSubject, activeLearningMode, search]);

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
          <p className="mx-auto mt-4 max-w-2xl text-lg text-slate-400">
            {t('home.subtitle')}
          </p>
          <p className="mx-auto mt-2 max-w-xl text-sm text-slate-500">
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
          <SubjectPill label={t('home.allModes')} active={activeLearningMode === null} onClick={() => setActiveLearningMode(null)} icon={<Layers className="h-3.5 w-3.5" />} />
          <SubjectPill
            label={t('home.graphMode')}
            active={activeLearningMode === 'GraphMemoryParadox'}
            onClick={() => setActiveLearningMode('GraphMemoryParadox')}
            icon={<GitBranch className="h-3.5 w-3.5" />}
          />
          <SubjectPill
            label={t('home.traditionalMode')}
            active={activeLearningMode === 'TraditionalChapters'}
            onClick={() => setActiveLearningMode('TraditionalChapters')}
            icon={<BookOpen className="h-3.5 w-3.5" />}
          />
        </div>

        <div className="mb-8 flex gap-2 overflow-x-auto pb-2">
          <SubjectPill label={t('home.allLanguages')} active={activeLanguage === null} onClick={() => setActiveLanguage(null)} />
          <SubjectPill label={t('home.spanish')} active={activeLanguage === 'es'} onClick={() => setActiveLanguage('es')} />
          <SubjectPill label={t('home.english')} active={activeLanguage === 'en'} onClick={() => setActiveLanguage('en')} />
        </div>

        {loading && <LoadingSpinner label={t('home.loading')} />}

        {!loading && error && (
          <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-center text-red-300">
            {t('home.loadError', { error })}
          </div>
        )}

        {!loading && !error && filtered.length === 0 && filteredStructuredLearningDocuments.length === 0 && (
          <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center text-slate-400">
            {t('home.noResults')}
          </div>
        )}

        {!loading && !error && (filtered.length > 0 || filteredStructuredLearningDocuments.length > 0) && (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {filteredStructuredLearningDocuments.map((document) => (
              <StructuredLearningDocumentCard key={document.StructuredLearningDocumentId} document={document} />
            ))}
            {filtered.map((capability) => (
              <CapabilityCard key={capability.CapabilityId} capability={capability} />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
