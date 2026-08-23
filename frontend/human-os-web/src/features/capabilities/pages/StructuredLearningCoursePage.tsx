import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { Lock, PlayCircle, Star, Menu, X, ListChecks, Search } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  getStructuredLearningDocument,
  getActiveStructuredLearningAssessmentRound,
  type StructuredLearningDocumentDetail,
  type StructuredLearningSectionDetail,
} from '../api/structuredLearningRuntimeApi';

type SectionState = 'Locked' | 'Available' | 'Mastered';

/** Same state palette as CapabilityGraphMapPage's STATE_STYLES — reused
 * verbatim so a Structured Learning "engrama" reads visually identical to
 * a Capability Graph course. */
const STATE_STYLES: Record<SectionState, { ring: string; bubble: string; icon: string; label: string; Icon: typeof Lock }> = {
  Locked: {
    ring: 'border-slate-200 dark:border-white/10',
    bubble: 'bg-white dark:bg-white/[0.03]',
    icon: 'text-slate-500',
    label: 'text-slate-500',
    Icon: Lock,
  },
  Available: {
    ring: 'border-transparent',
    bubble: 'bg-gradient-to-br from-brand-500 to-accent-500 shadow-lg shadow-brand-500/30',
    icon: 'text-white',
    label: 'text-slate-900 dark:text-white font-semibold',
    Icon: PlayCircle,
  },
  Mastered: {
    ring: 'border-amber-300/50',
    bubble: 'bg-amber-400/10',
    icon: 'text-amber-600 dark:text-amber-300',
    label: 'text-amber-700 dark:text-amber-100',
    Icon: Star,
  },
};

const LEVEL_ACCENT = { text: 'text-brand-600 dark:text-brand-300', dot: 'bg-brand-500 dark:bg-brand-400' };

/**
 * Structured Learning's real student-facing "climb the document" map
 * (2026-08-14, restyled to match CapabilityGraphMapPage.tsx exactly —
 * course-contents sidebar on the LEFT with search + progress bar, main
 * pyramid area in its own bordered card, same Locked/Available/Mastered
 * bubble palette and pulsing "Start here!" badge). Every section's state
 * is driven by the real signed-in student's own assessment rounds.
 */
export default function StructuredLearningCoursePage() {
  const { documentId } = useParams<{ documentId: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const [document, setDocument] = useState<StructuredLearningDocumentDetail | null>(null);
  const [passedBySectionId, setPassedBySectionId] = useState<Record<string, boolean>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarQuery, setSidebarQuery] = useState('');

  useEffect(() => {
    if (!documentId || !personId) return;
    let cancelled = false;
    setLoading(true);
    getStructuredLearningDocument(documentId)
      .then(async (detail) => {
        if (cancelled) return;
        setDocument(detail);
        const entries = await Promise.all(
          detail.Sections.map(async (s) => {
            const round = await getActiveStructuredLearningAssessmentRound(s.StructuredLearningDocumentSectionId, personId).catch(() => null);
            return [s.StructuredLearningDocumentSectionId, round?.Status === 'Passed'] as const;
          })
        );
        if (!cancelled) setPassedBySectionId(Object.fromEntries(entries));
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [documentId, personId]);

  const sections = document?.Sections ?? [];

  const levels = useMemo(() => {
    const map = new Map<string, StructuredLearningSectionDetail[]>();
    sections.forEach((s) => {
      const list = map.get(s.ChapterTitle) ?? [];
      list.push(s);
      map.set(s.ChapterTitle, list);
    });
    return Array.from(map.entries()).map(([chapterTitle, list], levelIndex) => ({ levelIndex, chapterTitle, list }));
  }, [sections]);

  const sectionState = useCallback(
    (section: StructuredLearningSectionDetail, index: number): SectionState => {
      if (passedBySectionId[section.StructuredLearningDocumentSectionId]) return 'Mastered';
      if (index === 0) return 'Available';
      const previous = sections[index - 1];
      return passedBySectionId[previous.StructuredLearningDocumentSectionId] ? 'Available' : 'Locked';
    },
    [passedBySectionId, sections]
  );

  const nextUpSectionId = useMemo(() => {
    const found = sections.find((s, i) => sectionState(s, i) === 'Available');
    return found?.StructuredLearningDocumentSectionId ?? null;
  }, [sections, sectionState]);

  const progress = useMemo(() => {
    const total = sections.length;
    const mastered = sections.filter((s) => passedBySectionId[s.StructuredLearningDocumentSectionId]).length;
    return { total, mastered, percent: total > 0 ? Math.round((mastered / total) * 100) : 0 };
  }, [sections, passedBySectionId]);

  const normalizeForSearch = useCallback(
    (value: string) => value.toLocaleLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, ''),
    []
  );
  const filteredLevels = useMemo(() => {
    const query = normalizeForSearch(sidebarQuery.trim());
    if (!query) return levels;
    return levels
      .map(({ levelIndex, chapterTitle, list }) => {
        const chapterMatches = normalizeForSearch(chapterTitle).includes(query);
        const matching = chapterMatches ? list : list.filter((s) => normalizeForSearch(s.SubchapterTitle).includes(query));
        return { levelIndex, chapterTitle, list: matching };
      })
      .filter(({ list }) => list.length > 0);
  }, [levels, sidebarQuery, normalizeForSearch]);

  const goToSection = useCallback(
    (section: StructuredLearningSectionDetail, index: number) => {
      if (sectionState(section, index) === 'Locked') return;
      navigate(`/structured-learning/${documentId}/sections/${section.StructuredLearningDocumentSectionId}`);
    },
    [navigate, documentId, sectionState]
  );

  const scrollToLevel = useCallback((levelIndex: number) => {
    globalThis.document.getElementById(`structured-learning-level-${levelIndex}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, []);

  if (error || (document && sections.length === 0)) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16 text-center">
        <p className="text-red-600 dark:text-red-400">{error ?? 'This course has no sections yet.'}</p>
        <Link to="/my-capabilities" className="mt-4 inline-flex items-center gap-1.5 text-sm text-brand-600 dark:text-brand-400 hover:underline">
          ← Back
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl p-4 sm:p-8 xl:max-w-7xl 2xl:max-w-[96rem]">
      <Link to="/my-capabilities" className="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:underline">
        ← Back to my Engrams
      </Link>

      {document && (
        <div className="mt-4 text-center">
          <h1 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white">{document.DocumentName}</h1>
          {document.ExecutiveSummary && (
            <p className="mx-auto mt-2 max-w-2xl text-sm text-slate-500 dark:text-slate-400">{document.ExecutiveSummary}</p>
          )}
        </div>
      )}

      {loading && <p className="mt-6 text-center text-slate-500 dark:text-slate-400">Loading...</p>}

      {!loading && document && (
        <>
          {levels.length > 0 && (
            <div className="mt-6 flex justify-center lg:hidden">
              <button
                type="button"
                onClick={() => setSidebarOpen((open) => !open)}
                className="flex items-center gap-1.5 rounded-lg border border-slate-200 dark:border-white/10 bg-slate-100 dark:bg-white/[0.04] px-3 py-1.5 text-xs font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-white/[0.08]"
              >
                {sidebarOpen ? <X className="h-3.5 w-3.5" /> : <Menu className="h-3.5 w-3.5" />}
                {sidebarOpen ? 'Hide course contents' : 'Show course contents'}
              </button>
            </div>
          )}

          <div className="mt-8 flex flex-col gap-8 lg:flex-row lg:items-start lg:gap-10">
            {levels.length > 0 && (
              <aside className={`w-full flex-none lg:sticky lg:top-20 lg:block lg:w-72 xl:w-80 2xl:w-96 ${sidebarOpen ? 'block' : 'hidden'}`}>
                <div className="rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] p-4 lg:max-h-[calc(100vh-6rem)] lg:overflow-y-auto">
                  <div className="flex items-center gap-2 text-sm font-semibold text-slate-900 dark:text-white">
                    <ListChecks className="h-4 w-4 text-brand-500" />
                    Course contents
                  </div>

                  <div className="mt-2 flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
                    <span>Mastered: {progress.mastered}/{progress.total}</span>
                    <span>{progress.percent}%</span>
                  </div>
                  <div className="mt-1.5 h-1.5 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                    <div className="h-full rounded-full bg-gradient-to-r from-brand-500 to-accent-500" style={{ width: `${progress.percent}%` }} />
                  </div>

                  <div className="relative mt-4">
                    <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
                    <input
                      type="text"
                      value={sidebarQuery}
                      onChange={(e) => setSidebarQuery(e.target.value)}
                      placeholder="Search sections..."
                      className="w-full rounded-lg border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.04] py-1.5 pl-8 pr-7 text-xs text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-brand-400/50"
                    />
                    {sidebarQuery && (
                      <button
                        type="button"
                        onClick={() => setSidebarQuery('')}
                        className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    )}
                  </div>

                  <nav className="mt-3 space-y-3">
                    {filteredLevels.map(({ levelIndex, chapterTitle, list }) => (
                      <div key={chapterTitle} className="rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-2.5">
                        <button
                          type="button"
                          onClick={() => scrollToLevel(levelIndex)}
                          className={`flex w-full items-center gap-2 text-left text-xs font-semibold uppercase tracking-wide ${LEVEL_ACCENT.text} hover:underline`}
                        >
                          <span className={`h-1.5 w-1.5 rounded-full ${LEVEL_ACCENT.dot}`} />
                          {chapterTitle}
                        </button>
                        <ul className="mt-1.5 space-y-1">
                          {list.map((section) => {
                            const index = sections.indexOf(section);
                            const state = sectionState(section, index);
                            const style = STATE_STYLES[state];
                            const Icon = style.Icon;
                            const clickable = state !== 'Locked';
                            const isNextUp = section.StructuredLearningDocumentSectionId === nextUpSectionId;
                            return (
                              <li key={section.StructuredLearningDocumentSectionId}>
                                <button
                                  type="button"
                                  disabled={!clickable}
                                  onClick={() => goToSection(section, index)}
                                  className={`flex w-full items-center gap-1.5 rounded-md px-1.5 py-1 text-left text-xs ${
                                    clickable ? 'text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-white/[0.06]' : 'cursor-not-allowed text-slate-400 dark:text-slate-500 opacity-60'
                                  }`}
                                >
                                  <Icon className={`h-3.5 w-3.5 flex-none ${style.icon}`} fill={state === 'Mastered' ? 'currentColor' : 'none'} />
                                  <span className="line-clamp-1 flex-1">{section.SubchapterTitle}</span>
                                  {isNextUp && <span className="flex-none rounded-full bg-brand-500 px-1.5 py-0.5 text-[9px] font-semibold text-white">Next</span>}
                                </button>
                              </li>
                            );
                          })}
                        </ul>
                      </div>
                    ))}
                    {filteredLevels.length === 0 && <p className="text-xs text-slate-500 dark:text-slate-400">No sections match your search.</p>}
                  </nav>
                </div>
              </aside>
            )}

            <div className="flex flex-1 flex-col items-center rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.02] p-6 sm:p-8">
              {levels.map(({ levelIndex, chapterTitle, list }) => (
                <div key={chapterTitle} id={`structured-learning-level-${levelIndex}`} className="flex w-full scroll-mt-24 flex-col items-center">
                  <span className="mb-4 rounded-full border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] px-3 py-1 text-[11px] font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    {chapterTitle}
                  </span>

                  <div className="mb-8 flex flex-wrap justify-center gap-x-6 gap-y-8">
                    {list.map((section) => {
                      const index = sections.indexOf(section);
                      const state = sectionState(section, index);
                      const style = STATE_STYLES[state];
                      const Icon = style.Icon;
                      const clickable = state !== 'Locked';
                      const isNextUp = section.StructuredLearningDocumentSectionId === nextUpSectionId;

                      return (
                        <button
                          key={section.StructuredLearningDocumentSectionId}
                          type="button"
                          disabled={!clickable}
                          onClick={() => goToSection(section, index)}
                          className={`group flex w-24 flex-col items-center gap-2 text-center transition ${clickable ? 'cursor-pointer' : 'cursor-not-allowed'}`}
                        >
                          <div className="relative">
                            {isNextUp && (
                              <span className="absolute -top-7 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full bg-brand-500 px-2 py-0.5 text-[10px] font-semibold text-white shadow-md">
                                Start here!
                              </span>
                            )}
                            {isNextUp && <span className="absolute inset-0 -m-1.5 rounded-full border-2 border-brand-400 animate-ping" />}
                            <div
                              className={`relative flex h-16 w-16 items-center justify-center rounded-full border-2 sm:h-20 sm:w-20 ${style.ring} ${style.bubble} ${
                                clickable ? 'transition-transform group-hover:scale-110' : 'opacity-60'
                              }`}
                            >
                              <Icon className={`h-6 w-6 sm:h-7 sm:w-7 ${style.icon}`} fill={state === 'Mastered' ? 'currentColor' : 'none'} />
                            </div>
                          </div>
                          <span className={`line-clamp-2 text-xs leading-tight ${style.label}`}>{section.SubchapterTitle}</span>
                        </button>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
