import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Lock, PlayCircle, Star, ChevronDown, Menu, X, ListChecks, Search, Plus, Trash2 } from 'lucide-react';
import {
  getStructuredLearningDocument,
  getActiveStructuredLearningAssessmentRound,
  deleteStructuredLearningSection,
  type StructuredLearningDocumentDetail,
  type StructuredLearningSectionDetail,
} from '../lib/api/structuredLearningApi';
import LoadingSpinner from '../components/LoadingSpinner';
import PreviewModeSwitcher from '../components/PreviewModeSwitcher';
import AddStructuredLearningSectionModal from '../components/AddStructuredLearningSectionModal';
import DeleteStructuredLearningSectionModal from '../components/DeleteStructuredLearningSectionModal';
import KnowledgeGraphSection from '../components/KnowledgeGraphSection';
import StructuredLearningCostSection from '../components/StructuredLearningCostSection';
import { getStructuredLearningCostDetail, type StructuredLearningCostDetail } from '../lib/api/costsApi';
import { usePreviewMode, withPreviewMode } from '../lib/previewMode';

type SectionState = 'Locked' | 'Available' | 'Mastered';

const STATE_STYLES: Record<
  SectionState,
  { ring: string; bubble: string; icon: string; label: string; Icon: typeof Lock }
> = {
  Locked: {
    ring: 'border-white/10',
    bubble: 'bg-white/[0.03]',
    icon: 'text-slate-500',
    label: 'text-slate-500',
    Icon: Lock,
  },
  Available: {
    ring: 'border-transparent',
    bubble: 'bg-gradient-to-br from-brand-500 to-accent-500 shadow-lg shadow-brand-500/30',
    icon: 'text-[#fff]',
    label: 'text-white font-semibold',
    Icon: PlayCircle,
  },
  Mastered: {
    ring: 'border-amber-300/50',
    bubble: 'bg-amber-400/10',
    icon: 'text-amber-300',
    label: 'text-amber-100',
    Icon: Star,
  },
};

const LEVEL_ACCENTS = [
  { text: 'text-brand-300', dot: 'bg-brand-400', border: 'border-brand-400/50' },
  { text: 'text-sky-300', dot: 'bg-sky-400', border: 'border-sky-400/50' },
  { text: 'text-emerald-300', dot: 'bg-emerald-400', border: 'border-emerald-400/50' },
  { text: 'text-amber-300', dot: 'bg-amber-400', border: 'border-amber-400/50' },
  { text: 'text-fuchsia-300', dot: 'bg-fuchsia-400', border: 'border-fuchsia-400/50' },
  { text: 'text-rose-300', dot: 'bg-rose-400', border: 'border-rose-400/50' },
];

/**
 * Structured Learning's "climb the document" map (2026-08-10, rewritten
 * per explicit user feedback: reuse as much of PreviewGraphPage's proven
 * visual language as possible instead of inventing a distinct look).
 * Chapters become "levels" (exactly like Graph's Levels), sections become
 * the bubble "nodes" — same Locked/Available/Mastered states, same
 * pyramid-with-chevron-connectors layout, same Course-contents sidebar
 * with search + progress bar. Since Structured Learning's own document is
 * inherently LINEAR (no dependency graph), a section unlocks once the
 * PREVIOUS section's Assessment round is Passed (sequential gating,
 * mirroring Graph's prerequisite-edge gating with the one edge shape this
 * document actually has: "comes after"). Clicking an unlocked section
 * bubble goes to StructuredLearningSectionPage (mirrors PreviewNodePage).
 */
export default function StructuredLearningCoursePage() {
  const { t } = useTranslation();
  const { documentId } = useParams<{ documentId: string }>();
  const navigate = useNavigate();
  const [mode, setMode] = usePreviewMode();

  const [document, setDocument] = useState<StructuredLearningDocumentDetail | null>(null);
  const [passedBySectionId, setPassedBySectionId] = useState<Record<string, boolean>>({});
  const [error, setError] = useState<string | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarQuery, setSidebarQuery] = useState('');
  // "Edit mode" add-section feature (2026-08-11) — the chapter currently
  // being added to (null when the modal is closed).
  const [addSectionChapter, setAddSectionChapter] = useState<string | null>(null);
  // "Edit mode" delete-section feature (2026-08-11) — the section pending
  // type-to-confirm deletion (null when the modal is closed).
  const [deletingSection, setDeletingSection] = useState<StructuredLearningSectionDetail | null>(null);
  // Knowledge Graph panel visibility (2026-08-26) — previously always shown
  // with no way to close it, permanently taking up space on this page.
  const [showKnowledgeGraphPanel, setShowKnowledgeGraphPanel] = useState(false);
  // Total Engrams (sections) + token usage summary shown at the top of the
  // page (2026-08-26 request) — token totals only fetched/shown in edit
  // mode, same data StructuredLearningCostSection already reads.
  const [costSummary, setCostSummary] = useState<StructuredLearningCostDetail | null>(null);

  const loadDocument = useCallback(() => {
    if (!documentId) return;
    getStructuredLearningDocument(documentId)
      .then(async (detail) => {
        setDocument(detail);
        const entries = await Promise.all(
          detail.Sections.map(async (s) => {
            const round = await getActiveStructuredLearningAssessmentRound(s.StructuredLearningDocumentSectionId).catch(() => null);
            return [s.StructuredLearningDocumentSectionId, round?.Status === 'Passed'] as const;
          })
        );
        setPassedBySectionId(Object.fromEntries(entries));
      })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
  }, [documentId]);

  useEffect(() => {
    loadDocument();
  }, [loadDocument]);

  useEffect(() => {
    if (!documentId || mode !== 'edit') {
      setCostSummary(null);
      return;
    }
    let cancelled = false;
    getStructuredLearningCostDetail(documentId)
      .then((result) => { if (!cancelled) setCostSummary(result); })
      .catch(() => { if (!cancelled) setCostSummary(null); });
    return () => { cancelled = true; };
  }, [documentId, mode]);

  const sections = document?.Sections ?? [];

  // Groups sections back into their own Chapter ("level"), preserving document order.
  const levels = useMemo(() => {
    const map = new Map<string, StructuredLearningSectionDetail[]>();
    sections.forEach((s) => {
      const list = map.get(s.ChapterTitle) ?? [];
      list.push(s);
      map.set(s.ChapterTitle, list);
    });
    return Array.from(map.entries());
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

  // Review & acceptance / Edit modes are reviewer-facing (not a real
  // student's progression) — no section should ever look or behave as
  // locked in either, mirroring PreviewGraphPage's own nodeEffectiveState/
  // isSectionClickable split exactly.
  const sectionEffectiveState = useCallback(
    (section: StructuredLearningSectionDetail, index: number): SectionState => {
      const real = sectionState(section, index);
      return mode !== 'real' && real === 'Locked' ? 'Available' : real;
    },
    [sectionState, mode]
  );
  const isSectionClickable = useCallback(
    (section: StructuredLearningSectionDetail, index: number) => sectionState(section, index) !== 'Locked' || mode !== 'real',
    [sectionState, mode]
  );

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
      .map(([chapterTitle, list]): [string, StructuredLearningSectionDetail[]] => {
        const chapterMatches = normalizeForSearch(chapterTitle).includes(query);
        const matching = chapterMatches ? list : list.filter((s) => normalizeForSearch(s.SubchapterTitle).includes(query));
        return [chapterTitle, matching];
      })
      .filter(([, list]) => list.length > 0);
  }, [levels, sidebarQuery, normalizeForSearch]);

  const goToSection = useCallback(
    (section: StructuredLearningSectionDetail, index: number) => {
      if (!isSectionClickable(section, index)) return;
      navigate(withPreviewMode(`/structured-learning/${documentId}/course/sections/${section.StructuredLearningDocumentSectionId}`, mode));
    },
    [navigate, documentId, isSectionClickable, mode]
  );

  const scrollToLevel = useCallback((levelIndex: number) => {
    globalThis.document.getElementById(`structured-learning-level-${levelIndex}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, []);

  if (error || (document && sections.length === 0)) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16 text-center">
        <p className="text-red-300">{error ?? t('structuredLearningMap.emptyDocument')}</p>
        <Link to="/" className="mt-4 inline-flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300">
          <ArrowLeft className="h-4 w-4" /> {t('structuredLearningMap.back')}
        </Link>
      </div>
    );
  }

  if (!document) {
    return <LoadingSpinner label={t('structuredLearningMap.loading')} />;
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 xl:max-w-7xl 2xl:max-w-[96rem]">
      <Link to="/" className="inline-flex items-center gap-1.5 text-sm text-slate-400 hover:text-white">
        <ArrowLeft className="h-4 w-4" /> {t('structuredLearningMap.back')}
      </Link>

      <div className="mt-4 text-center">
        <p className="text-xs font-medium uppercase tracking-wide text-brand-400">{t('structuredLearningMap.studentPreviewLabel')}</p>
        <h1 className="mt-1 text-2xl font-bold text-white">{document.DocumentName}</h1>
        <p className="mt-1 text-xs text-slate-500">{t('structuredLearningMap.climbHint')}</p>
        <p className="mt-2 text-xs font-medium text-slate-400">
          {sections.length} Engram{sections.length === 1 ? '' : 's'}
          {mode === 'edit' && costSummary && (
            <span className="ml-2 text-amber-300">
              · {costSummary.InputTokens.toLocaleString()} Tokens in · {costSummary.OutputTokens.toLocaleString()} Tokens out
            </span>
          )}
        </p>
        <div className="mt-4 flex justify-center">
          <PreviewModeSwitcher mode={mode} onChange={setMode} />
        </div>
        {mode === 'demo' && <p className="mt-2 text-xs text-amber-300">{t('structuredLearningMap.demoModeNotice')}</p>}
      </div>

      {documentId && (
        <div className="mt-8 space-y-3">
          {showKnowledgeGraphPanel ? (
            <div className="relative">
              <button
                type="button"
                onClick={() => setShowKnowledgeGraphPanel(false)}
                className="absolute right-3 top-3 z-10 rounded-lg p-1.5 text-slate-400 hover:bg-white/10 hover:text-white"
                aria-label="Cerrar Knowledge Graph"
              >
                <X className="h-4 w-4" />
              </button>
              <KnowledgeGraphSection documentId={documentId} />
            </div>
          ) : (
            <button
              type="button"
              onClick={() => setShowKnowledgeGraphPanel(true)}
              className="inline-flex items-center gap-1.5 rounded-xl border border-brand-400/40 bg-brand-500/10 px-4 py-2.5 text-sm font-semibold text-brand-200 hover:bg-brand-500/20"
            >
              Mostrar Knowledge Graph
            </button>
          )}
          {mode === 'edit' && <StructuredLearningCostSection documentId={documentId} />}
        </div>
      )}

      {levels.length > 0 && (
        <button
          type="button"
          onClick={() => setSidebarOpen((open) => !open)}
          className="mx-auto mt-6 flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-4 py-2.5 text-sm font-medium text-slate-200 hover:bg-white/[0.06] lg:hidden"
        >
          {sidebarOpen ? <X className="h-4 w-4" /> : <Menu className="h-4 w-4" />}
          {sidebarOpen ? t('structuredLearningMap.sidebarToggleHide') : t('structuredLearningMap.sidebarToggleShow')}
        </button>
      )}

      <div className="mt-8 flex flex-col gap-8 lg:flex-row lg:items-start lg:gap-10">
        {levels.length > 0 && (
          <aside className={`${sidebarOpen ? 'block' : 'hidden'} w-full flex-none lg:sticky lg:top-6 lg:block lg:w-72 xl:w-80 2xl:w-96`}>
            <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4 lg:flex lg:max-h-[calc(100vh-3rem)] lg:flex-col">
              <div className="flex items-center gap-2 text-sm font-semibold text-white">
                <ListChecks className="h-4 w-4 text-brand-400" /> {t('structuredLearningMap.sidebarTitle')}
              </div>
              <div className="mt-3">
                <div className="h-1.5 w-full overflow-hidden rounded-full bg-white/10">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-brand-500 to-accent-500 transition-all"
                    style={{ width: `${progress.percent}%` }}
                  />
                </div>
                <p className="mt-1.5 text-xs text-slate-400">
                  {t('structuredLearningMap.sidebarProgress', { done: progress.mastered, total: progress.total })}
                </p>
              </div>

              <div className="relative mt-3">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-500" />
                <input
                  type="text"
                  value={sidebarQuery}
                  onChange={(e) => setSidebarQuery(e.target.value)}
                  placeholder={t('structuredLearningMap.sidebarSearchPlaceholder')}
                  className="w-full rounded-lg border border-white/10 bg-white/[0.04] py-1.5 pl-8 pr-7 text-xs text-white placeholder:text-slate-500 focus:border-brand-400/60 focus:outline-none"
                />
                {sidebarQuery && (
                  <button
                    type="button"
                    onClick={() => setSidebarQuery('')}
                    className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-500 hover:text-white"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                )}
              </div>

              <nav className="mt-3 space-y-3 overflow-y-auto pr-1 lg:min-h-0 lg:flex-1">
                {filteredLevels.length === 0 && <p className="px-1 text-xs text-slate-500">{t('structuredLearningMap.sidebarNoResults')}</p>}
                {filteredLevels.map(([chapterTitle, list], levelIndex) => {
                  const accent = LEVEL_ACCENTS[levelIndex % LEVEL_ACCENTS.length];
                  return (
                    <div key={chapterTitle} className={`rounded-xl border bg-white/[0.02] p-2.5 ${accent.border}`}>
                      <button
                        type="button"
                        onClick={() => scrollToLevel(levelIndex)}
                        className={`flex w-full items-center gap-1.5 truncate text-left text-[11px] font-semibold uppercase tracking-wide hover:brightness-125 ${accent.text}`}
                      >
                        <span className={`h-1.5 w-1.5 flex-none rounded-full ${accent.dot}`} />
                        <span className="truncate">{chapterTitle}</span>
                      </button>
                      <ul className="mt-1.5 space-y-0.5">
                        {list.map((section) => {
                          const index = sections.indexOf(section);
                          const state = sectionEffectiveState(section, index);
                          const clickable = isSectionClickable(section, index);
                          const style = STATE_STYLES[state];
                          const Icon = style.Icon;
                          const isNextUp = section.StructuredLearningDocumentSectionId === nextUpSectionId;
                          return (
                            <li key={section.StructuredLearningDocumentSectionId}>
                              <button
                                type="button"
                                disabled={!clickable}
                                onClick={() => goToSection(section, index)}
                                className={`flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-xs transition ${
                                  state !== 'Locked' ? 'cursor-pointer hover:bg-white/[0.06]' : 'cursor-not-allowed opacity-50'
                                } ${isNextUp ? 'bg-brand-500/10 ring-1 ring-inset ring-brand-400/40' : ''}`}
                              >
                                <Icon className={`h-3.5 w-3.5 flex-none ${style.icon}`} fill={state === 'Mastered' ? 'currentColor' : 'none'} />
                                <span className={`line-clamp-2 flex-1 ${state === 'Locked' ? 'text-slate-500' : 'text-slate-200'}`}>
                                  {section.SubchapterTitle}
                                </span>
                                {isNextUp && (
                                  <span className="flex-none rounded-full bg-brand-500 px-1.5 py-0.5 text-[9px] font-semibold uppercase text-[#fff]">
                                    {t('structuredLearningMap.sidebarNextBadge')}
                                  </span>
                                )}
                              </button>
                            </li>
                          );
                        })}
                      </ul>
                    </div>
                  );
                })}
              </nav>
            </div>
          </aside>
        )}

        <div className="flex flex-1 flex-col items-center rounded-2xl border border-white/10 bg-white/[0.02] p-6 sm:p-8">
          {levels.map(([chapterTitle, list], levelIndex) => (
            <div key={chapterTitle} id={`structured-learning-level-${levelIndex}`} className="flex w-full scroll-mt-6 flex-col items-center">
              <div className="relative z-10 mb-6">
                <span className="inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-white/[0.03] px-3 py-1 text-[11px] font-medium uppercase tracking-wide text-slate-400">
                  {chapterTitle}
                </span>
              </div>

              <div className="flex flex-wrap justify-center gap-x-6 gap-y-8">
                {list.map((section) => {
                  const index = sections.indexOf(section);
                  const state = sectionEffectiveState(section, index);
                  const clickable = isSectionClickable(section, index);
                  const style = STATE_STYLES[state];
                  const Icon = style.Icon;
                  const isNextUp = section.StructuredLearningDocumentSectionId === nextUpSectionId;

                  return (
                    <div key={section.StructuredLearningDocumentSectionId} className="group relative flex w-24 flex-col items-center gap-2 text-center">
                      {mode === 'edit' && (
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            setDeletingSection(section);
                          }}
                          title={t('structuredLearningMap.deleteSectionButton')}
                          className="absolute -top-2 -right-2 z-20 rounded-full border border-white/20 bg-slate-800 p-1.5 text-slate-200 opacity-70 shadow-md transition-opacity hover:border-red-500 hover:bg-red-500/20 hover:text-red-400 hover:opacity-100 group-hover:opacity-100"
                        >
                          <Trash2 className="h-4 w-4" />
                        </button>
                      )}
                      <button
                        type="button"
                        disabled={!clickable}
                        onClick={() => goToSection(section, index)}
                        className={`flex w-24 flex-col items-center gap-2 text-center transition ${
                          clickable ? 'cursor-pointer' : 'cursor-not-allowed'
                        }`}
                      >
                        <div className="relative">
                          {isNextUp && (
                            <span className="pointer-events-none absolute -top-7 left-1/2 z-0 -translate-x-1/2 whitespace-nowrap rounded-full bg-brand-500 px-2 py-0.5 text-[10px] font-semibold text-[#fff] shadow-md">
                              {t('structuredLearningMap.startHere')}
                            </span>
                          )}
                          {isNextUp && <span className="pointer-events-none absolute inset-0 -m-1.5 rounded-full border-2 border-brand-400 animate-ping" />}
                          <div
                            className={`relative flex h-16 w-16 items-center justify-center rounded-full border-2 sm:h-20 sm:w-20 ${style.ring} ${style.bubble} ${
                              state !== 'Locked' ? 'transition-transform group-hover:scale-110' : 'opacity-60'
                            }`}
                          >
                            <Icon className={`h-6 w-6 sm:h-7 sm:w-7 ${style.icon}`} fill={state === 'Mastered' ? 'currentColor' : 'none'} />
                          </div>
                        </div>
                        <p className={`line-clamp-2 text-xs leading-tight ${style.label}`}>{section.SubchapterTitle}</p>
                      </button>
                    </div>
                  );
                })}
                {mode === 'edit' && (
                  <button
                    type="button"
                    onClick={() => setAddSectionChapter(chapterTitle)}
                    className="flex w-24 flex-col items-center gap-2 text-center"
                  >
                    <div className="flex h-16 w-16 items-center justify-center rounded-full border-2 border-dashed border-white/15 text-slate-400 transition hover:border-brand-400/60 hover:text-brand-300 sm:h-20 sm:w-20">
                      <Plus className="h-6 w-6 sm:h-7 sm:w-7" />
                    </div>
                    <p className="text-xs leading-tight text-slate-400">{t('structuredLearningMap.addSectionButton')}</p>
                  </button>
                )}
              </div>

              {levelIndex < levels.length - 1 && (
                <div className="my-4 flex w-full max-w-xs items-center gap-3">
                  <div className="h-px flex-1 bg-amber-400/40" />
                  <ChevronDown className="h-4 w-4 flex-none text-amber-400/70" />
                  <div className="h-px flex-1 bg-amber-400/40" />
                </div>
              )}
            </div>
          ))}
        </div>
      </div>

      {addSectionChapter && (
        <AddStructuredLearningSectionModal
          documentId={documentId!}
          chapterTitle={addSectionChapter}
          chapterSections={levels.find(([title]) => title === addSectionChapter)?.[1] ?? []}
          languageCode="en"
          onClose={() => setAddSectionChapter(null)}
          onCreated={() => {
            setAddSectionChapter(null);
            loadDocument();
          }}
        />
      )}

      {deletingSection && documentId && (
        <DeleteStructuredLearningSectionModal
          sectionName={deletingSection.SubchapterTitle}
          onCancel={() => setDeletingSection(null)}
          onConfirm={async () => {
            await deleteStructuredLearningSection(documentId, deletingSection.StructuredLearningDocumentSectionId);
            setDeletingSection(null);
            loadDocument();
          }}
        />
      )}
    </div>
  );
}
