import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Lock, PlayCircle, Star, Menu, X, ListChecks, Search, Plus, Trash2, Settings2, ArrowRight, Check, Clock, Sparkles, Layers, Info } from 'lucide-react';
import {
  getStructuredLearningDocument,
  getActiveStructuredLearningAssessmentRound,
  deleteStructuredLearningSection,
  updateStructuredLearningPedagogyConfig,
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

  const courseDurationStats = useMemo(() => {
    const rawMinutes = sections.reduce((acc, s) => acc + (s.EstimatedMinutesToComplete || 6), 0);
    const overheadMinutes = Math.round(rawMinutes * 0.10);
    const totalMinutes = rawMinutes + overheadMinutes;
    const hours = Math.floor(totalMinutes / 60);
    const mins = totalMinutes % 60;
    const durationFormatted = hours > 0 ? (mins > 0 ? `${hours}h ${mins}m` : `${hours}h`) : `${totalMinutes} min`;
    return { rawMinutes, overheadMinutes, totalMinutes, durationFormatted };
  }, [sections]);

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

        {/* Total Course Duration & Stats with 10% Overhead */}
        <div className="mt-4 flex flex-wrap items-center justify-center gap-2.5">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-brand-500/30 bg-brand-500/10 px-3.5 py-1.5 text-xs font-bold text-brand-300 shadow-xs">
            <Layers className="h-4 w-4 text-brand-400" />
            <span>{sections.length} Engram{sections.length === 1 ? '' : 's'}</span>
          </span>

          <span
            className="inline-flex items-center gap-1.5 rounded-full border border-amber-500/30 bg-amber-500/10 px-3.5 py-1.5 text-xs font-bold text-amber-300 shadow-xs cursor-help"
            title={`Tiempo base: ~${courseDurationStats.rawMinutes} min + 10% de margen (~${courseDurationStats.overheadMinutes} min) para reflexión, active recall y notas`}
          >
            <Clock className="h-4 w-4 text-amber-400" />
            <span>Duración total: <strong>~{courseDurationStats.durationFormatted}</strong></span>
            <span className="text-[10px] font-semibold text-amber-300/80">
              (incl. 10% margen)
            </span>
          </span>

          <span className="inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-white/[0.03] px-3.5 py-1.5 text-xs font-semibold text-slate-300 shadow-xs">
            <Sparkles className="h-4 w-4 text-purple-400" />
            <span>{levels.length} {levels.length === 1 ? 'Capítulo' : 'Capítulos'}</span>
          </span>
        </div>

        {mode === 'edit' && costSummary && (
          <p className="mt-3 text-xs font-medium text-amber-300">
            {costSummary.InputTokens.toLocaleString()} Tokens in · {costSummary.OutputTokens.toLocaleString()} Tokens out
          </p>
        )}
        <div className="mt-4 flex justify-center">
          <PreviewModeSwitcher mode={mode} onChange={setMode} />
        </div>
        {mode === 'demo' && <p className="mt-2 text-xs text-amber-300">{t('structuredLearningMap.demoModeNotice')}</p>}
      </div>

      {documentId && (
        <div className="mt-8 space-y-3">
          {document && <CourseOriginPanel document={document} />}
          {mode === 'edit' && document && (
            <CourseDesignSummaryPanel
              document={document}
              onSaved={(updated) => setDocument((prev) => (prev ? { ...prev, ...updated } : prev))}
            />
          )}
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

        <div className="flex flex-1 flex-col rounded-2xl border border-white/10 bg-white/[0.02] p-4 sm:p-8">
          <div className="mx-auto w-full max-w-4xl space-y-10">
            {levels.map(([chapterTitle, list], levelIndex) => (
              <div
                key={chapterTitle}
                id={`structured-learning-level-${levelIndex}`}
                className="scroll-mt-6"
              >
                {/* Chapter Header */}
                <div className="mb-6 flex flex-wrap items-center justify-between gap-3 border-b border-white/10 pb-3">
                  <div className="flex items-center gap-2.5">
                    <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-brand-500/15 text-xs font-bold text-brand-300">
                      {levelIndex + 1}
                    </span>
                    <h2 className="text-base sm:text-lg font-bold text-white tracking-tight">
                      {chapterTitle}
                    </h2>
                  </div>
                  <span className="text-xs font-medium text-slate-400">
                    {list.length} {list.length === 1 ? 'Engram' : 'Engrams'}
                  </span>
                </div>

                {/* Engram Stepper List */}
                <div className="space-y-4">
                  {list.map((section, sectionIdx) => {
                    const index = sections.indexOf(section);
                    const state = sectionEffectiveState(section, index);
                    const clickable = isSectionClickable(section, index);
                    const isNextUp = section.StructuredLearningDocumentSectionId === nextUpSectionId;
                    const isMastered = state === 'Mastered';
                    const isLocked = state === 'Locked';
                    const estimatedMin = section.EstimatedMinutesToComplete || 6;
                    const previewSnippet = section.Text
                      ? section.Text.replace(/[#*`_>]+/g, ' ').trim().slice(0, 150)
                      : null;

                    return (
                      <div
                        key={section.StructuredLearningDocumentSectionId}
                        className="group relative flex items-start gap-3 sm:gap-4"
                      >
                        {/* Stepper node indicator */}
                        <div className="flex flex-col items-center">
                          <div
                            className={`flex h-10 w-10 sm:h-11 sm:w-11 shrink-0 items-center justify-center rounded-xl border-2 font-bold text-sm transition-all ${
                              isMastered
                                ? 'border-emerald-400/80 bg-emerald-500/20 text-emerald-300 shadow-md shadow-emerald-500/20'
                                : isNextUp
                                ? 'border-brand-400 bg-brand-500 text-white shadow-lg shadow-brand-500/30 ring-4 ring-brand-400/20'
                                : clickable
                                ? 'border-brand-400/50 bg-white/[0.04] text-brand-300'
                                : 'border-white/10 bg-white/[0.02] text-slate-500'
                            }`}
                          >
                            {isMastered ? (
                              <Check className="h-5 w-5 stroke-[2.5]" />
                            ) : isLocked ? (
                              <Lock className="h-4 w-4" />
                            ) : isNextUp ? (
                              <Sparkles className="h-5 w-5" />
                            ) : (
                              <span>{String(index + 1).padStart(2, '0')}</span>
                            )}
                          </div>
                          {sectionIdx < list.length - 1 && (
                            <div className="my-1.5 h-12 w-0.5 bg-white/10 group-hover:bg-brand-500/30 transition-colors" />
                          )}
                        </div>

                        {/* Engram Card */}
                        <div
                          role={clickable ? 'button' : undefined}
                          tabIndex={clickable ? 0 : undefined}
                          onClick={() => clickable && goToSection(section, index)}
                          onKeyDown={(e) => {
                            if (clickable && (e.key === 'Enter' || e.key === ' ')) {
                              e.preventDefault();
                              goToSection(section, index);
                            }
                          }}
                          className={`relative flex-1 rounded-2xl border p-4 sm:p-5 text-left transition-all duration-200 ${
                            isNextUp
                              ? 'border-brand-400/80 bg-gradient-to-br from-brand-500/10 via-white/[0.03] to-accent-500/10 shadow-xl shadow-brand-500/10 ring-1 ring-brand-400/40 hover:-translate-y-0.5 hover:border-brand-300 cursor-pointer'
                              : isMastered
                              ? 'border-emerald-500/30 bg-emerald-500/[0.02] hover:-translate-y-0.5 hover:border-emerald-400/60 hover:bg-emerald-500/[0.05] cursor-pointer'
                              : clickable
                              ? 'border-white/10 bg-white/[0.03] hover:-translate-y-0.5 hover:border-white/20 hover:bg-white/[0.06] hover:shadow-lg cursor-pointer'
                              : 'border-white/5 bg-white/[0.01] opacity-60 cursor-not-allowed'
                          }`}
                        >
                          {/* Header row: Index pill + Title + Status badge */}
                          <div className="flex flex-wrap items-center justify-between gap-2.5">
                            <div className="flex items-center gap-2">
                              <span className="inline-flex items-center gap-1 rounded-md bg-white/5 border border-white/10 px-2 py-0.5 text-[11px] font-semibold text-slate-300">
                                Engram {index + 1}
                              </span>
                              {isNextUp && (
                                <span className="inline-flex items-center gap-1 rounded-full bg-brand-500 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white shadow-sm">
                                  {t('structuredLearningMap.startHere')}
                                </span>
                              )}
                            </div>

                            <div className="flex items-center gap-2">
                              {isMastered && (
                                <span className="inline-flex items-center gap-1 rounded-full border border-emerald-500/30 bg-emerald-500/10 px-2.5 py-0.5 text-xs font-semibold text-emerald-300">
                                  <Check className="h-3.5 w-3.5" />
                                  Dominado
                                </span>
                              )}
                              {isLocked && (
                                <span className="inline-flex items-center gap-1 rounded-full border border-white/10 bg-white/5 px-2.5 py-0.5 text-xs font-medium text-slate-400">
                                  <Lock className="h-3 w-3" />
                                  Bloqueado
                                </span>
                              )}

                              {mode === 'edit' && (
                                <button
                                  type="button"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setDeletingSection(section);
                                  }}
                                  title={t('structuredLearningMap.deleteSectionButton')}
                                  className="rounded-lg border border-red-500/20 bg-red-500/10 p-1.5 text-red-300 hover:bg-red-500/20 hover:text-red-200 transition-colors"
                                >
                                  <Trash2 className="h-4 w-4" />
                                </button>
                              )}
                            </div>
                          </div>

                          {/* Title */}
                          <h3 className="mt-2 text-base sm:text-lg font-bold text-white leading-snug group-hover:text-brand-300 transition-colors">
                            {section.SubchapterTitle}
                          </h3>

                          {/* Description snippet */}
                          {previewSnippet && (
                            <p className="mt-1.5 text-xs sm:text-sm text-slate-400 line-clamp-2 leading-relaxed">
                              {previewSnippet}...
                            </p>
                          )}

                          {/* Footer with 4 Engram stages & CTA */}
                          <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-white/5 pt-3">
                            <div className="flex flex-wrap items-center gap-2 text-xs text-slate-400">
                              <span className="inline-flex items-center gap-1 font-medium text-slate-300">
                                <Clock className="h-3.5 w-3.5 text-amber-400" />
                                ~{estimatedMin} min
                              </span>
                              <span className="text-slate-600">·</span>
                              <div className="hidden sm:flex items-center gap-1.5 text-[11px] text-slate-400">
                                <span className="rounded bg-white/5 px-1.5 py-0.5 text-slate-300">💡 Hipótesis</span>
                                <span>›</span>
                                <span className="rounded bg-white/5 px-1.5 py-0.5 text-slate-300">📖 Deep Dive</span>
                                <span>›</span>
                                <span className="rounded bg-white/5 px-1.5 py-0.5 text-slate-300">🧠 Recall</span>
                                <span>›</span>
                                <span className="rounded bg-white/5 px-1.5 py-0.5 text-slate-300">📝 Quiz</span>
                              </div>
                            </div>

                            {clickable && (
                              <div className="inline-flex items-center gap-1.5 text-xs font-semibold text-brand-400 group-hover:text-brand-300 transition-colors">
                                <span>
                                  {isMastered ? 'Repasar' : isNextUp ? 'Continuar Engram' : 'Iniciar'}
                                </span>
                                <ArrowRight className="h-3.5 w-3.5 transition-transform group-hover:translate-x-1" />
                              </div>
                            )}
                          </div>
                        </div>
                      </div>
                    );
                  })}

                  {/* Add section button in Edit mode */}
                  {mode === 'edit' && (
                    <button
                      type="button"
                      onClick={() => setAddSectionChapter(chapterTitle)}
                      className="flex w-full items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-white/15 bg-white/[0.01] py-4 text-xs font-semibold text-slate-400 transition hover:border-brand-400/60 hover:bg-brand-500/5 hover:text-brand-300"
                    >
                      <Plus className="h-4 w-4" />
                      {t('structuredLearningMap.addSectionButton')}
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
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

/** Editable (2026-10-06) Course design panel — the designer re-opening an
 * already-created course in Edit mode can tweak the learner-facing agent
 * triggers via updateStructuredLearningPedagogyConfig: QA Professor
 * (+its voice opt-in), Deep Dive's "speak with your agent" (+its voice
 * opt-in), and "In your role" (the student-facing name for the Production
 * step — ONE single feature, gated solely by IncludeWorkplaceApplicationStep;
 * there is no separate "Production" toggle to confuse it with). Everything
 * else (Max Deep Dive cards, Recall, Assessment — including their
 * item/question counts and Static/Dynamic mode) is shown READ-ONLY: per
 * explicit product decision, those always exist and, once generated in
 * Static mode, their item/question counts are permanently fixed — editing
 * them here wouldn't regenerate or remove anything already baked into the DB. */
/**
 * Read-only "how was this course created" panel (2026-10-27) — surfaces
 * the authoring config captured at creation time that previously only
 * lived in the database (StructuredLearningDocument columns), with no UI
 * anywhere. Shown in every preview mode (unlike CourseDesignSummaryPanel,
 * which is edit-only and lets you change a few toggles after the fact) —
 * this one is purely informational and never calls the API.
 */
function CourseOriginPanel({ document }: { document: StructuredLearningDocumentDetail }) {
  const [expanded, setExpanded] = useState(false);

  const hasDomainCatalogLink = Boolean(document.SubjectId || document.CapabilityDomainId || document.Category || document.TargetSkill);
  const createdDateFormatted = document.CreatedDate
    ? new Date(document.CreatedDate).toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' })
    : null;

  const stepRows: Array<{ label: string; value: string; on: boolean }> = [
    { label: 'Configuration (audience / level)', value: `${document.PedagogicalAudience ?? 'Standard'} · ${document.CourseLevel ?? 'L100'}`, on: true },
    { label: 'Hypothesis', value: document.IncludeHypothesisStep ? 'Generated for every section' : 'Off', on: document.IncludeHypothesisStep },
    {
      label: 'Learn (Q&A with professor)',
      value: document.ShowQaProfessorStep ? (document.EnableQaProfessorVoice ? 'Text + Voice' : 'Text only') : 'Off',
      on: document.ShowQaProfessorStep,
    },
    {
      label: 'In your role',
      value: document.IncludeWorkplaceApplicationStep ? 'Enabled' : 'Off',
      on: document.IncludeWorkplaceApplicationStep,
    },
    {
      label: 'Recall',
      value: document.IncludeRecallStep
        ? `${document.RecallGenerationMode ?? 'Dynamic'} · ${document.RecallItemsRequired ?? 3} items`
        : 'Off',
      on: document.IncludeRecallStep,
    },
    {
      label: 'Assessment',
      value: document.IncludeAssessmentStep
        ? `${document.AssessmentGenerationMode ?? 'Dynamic'} · ${document.AssessmentQuestionsPerRound ?? 3} ${document.AssessmentQuestionType}`
        : 'Off',
      on: document.IncludeAssessmentStep,
    },
  ];

  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
      <button
        type="button"
        onClick={() => setExpanded((v) => !v)}
        className="flex w-full items-center justify-between gap-2 text-left"
      >
        <span className="flex items-center gap-2">
          <Info className="h-4 w-4 text-brand-400" />
          <h3 className="text-sm font-semibold text-white">How this course was created</h3>
        </span>
        <span className="text-xs text-slate-400">{expanded ? 'Hide' : 'Show'}</span>
      </button>

      {expanded && (
        <div className="mt-3 space-y-3">
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            <div className="rounded-xl border border-white/10 bg-white/[0.02] px-3 py-2 text-xs text-slate-300">
              <span className="font-medium text-white">Origin code:</span>{' '}
              {document.SourceCapabilityCode ?? 'None (authored directly in Studio)'}
            </div>
            <div className="rounded-xl border border-white/10 bg-white/[0.02] px-3 py-2 text-xs text-slate-300">
              <span className="font-medium text-white">Created:</span> {createdDateFormatted ?? 'Unknown'}
            </div>
            <div className="rounded-xl border border-white/10 bg-white/[0.02] px-3 py-2 text-xs text-slate-300 sm:col-span-2">
              <span className="font-medium text-white">Catalog domain:</span>{' '}
              {hasDomainCatalogLink
                ? [document.Category, document.TargetSkill].filter(Boolean).join(' / ') || 'Linked to Subject/Domain catalog'
                : 'No domain/subject set — this course was not linked to the Studio catalog taxonomy.'}
            </div>
            {document.FamilyOrigin && (
              <div className="rounded-xl border border-white/10 bg-white/[0.02] px-3 py-2 text-xs text-slate-300 sm:col-span-2">
                <span className="font-medium text-white">Created from Family recommendation:</span>{' '}
                {[
                  document.FamilyOrigin.CountryNameEn,
                  document.FamilyOrigin.EducationSystemTypeNameEn,
                  document.FamilyOrigin.StageNameEn,
                  document.FamilyOrigin.SubjectNameEn,
                ]
                  .filter(Boolean)
                  .join(' · ')}
                {document.FamilyOrigin.AgeRange && (
                  <span className="text-slate-400"> (ages {document.FamilyOrigin.AgeRange})</span>
                )}
              </div>
            )}
          </div>

          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            {stepRows.map((row) => (
              <div
                key={row.label}
                className={`flex items-center justify-between gap-2 rounded-xl border px-3 py-2 text-xs ${
                  row.on ? 'border-white/10 bg-white/[0.02] text-slate-300' : 'border-white/5 bg-white/[0.01] text-slate-500'
                }`}
              >
                <span className="font-medium text-white">{row.label}</span>
                <span>{row.value}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function CourseDesignSummaryPanel({
  document,
  onSaved,
}: {
  document: StructuredLearningDocumentDetail;
  onSaved: (updated: Partial<StructuredLearningDocumentDetail>) => void;
}) {
  const [showQaProfessorStep, setShowQaProfessorStep] = useState(document.ShowQaProfessorStep);
  const [enableQaProfessorVoice, setEnableQaProfessorVoice] = useState(document.EnableQaProfessorVoice);
  const [showDeepDiveAgentChat, setShowDeepDiveAgentChat] = useState(document.ShowDeepDiveAgentChat);
  const [enableDeepDiveAgentVoice, setEnableDeepDiveAgentVoice] = useState(document.EnableDeepDiveAgentVoice);
  const [includeWorkplaceApplicationStep, setIncludeWorkplaceApplicationStep] = useState(document.IncludeWorkplaceApplicationStep);
  const [includeHypothesisStep, setIncludeHypothesisStep] = useState(document.IncludeHypothesisStep);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedAt, setSavedAt] = useState<number | null>(null);

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      await updateStructuredLearningPedagogyConfig(document.StructuredLearningDocumentId, {
        // Read-only here on purpose — resent unchanged (see the function's doc comment).
        pedagogicalAudience: document.PedagogicalAudience,
        maxDeepDiveCardsPerNode: document.MaxDeepDiveCardsPerNode,
        includeProductionStep: document.IncludeProductionStep,
        includeRecallStep: document.IncludeRecallStep,
        recallItemsRequired: document.RecallItemsRequired,
        includeAssessmentStep: document.IncludeAssessmentStep,
        assessmentQuestionType: document.AssessmentQuestionType,
        assessmentQuestionsPerRound: document.AssessmentQuestionsPerRound,
        recallGenerationMode: document.RecallGenerationMode,
        assessmentGenerationMode: document.AssessmentGenerationMode,
        // Actually editable here:
        showQaProfessorStep,
        showDeepDiveAgentChat,
        enableQaProfessorVoice,
        enableDeepDiveAgentVoice,
        includeWorkplaceApplicationStep,
        includeHypothesisStep,
      });
      onSaved({
        ShowQaProfessorStep: showQaProfessorStep,
        EnableQaProfessorVoice: enableQaProfessorVoice,
        ShowDeepDiveAgentChat: showDeepDiveAgentChat,
        EnableDeepDiveAgentVoice: enableDeepDiveAgentVoice,
        IncludeWorkplaceApplicationStep: includeWorkplaceApplicationStep,
        IncludeHypothesisStep: includeHypothesisStep,
      });
      setSavedAt(Date.now());
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
      <div className="mb-3 flex items-center gap-2">
        <Settings2 className="h-4 w-4 text-brand-400" />
        <h3 className="text-sm font-semibold text-white">Course design</h3>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div className="rounded-xl border border-white/10 bg-white/[0.02] p-3">
          <label className="flex items-center gap-2 text-xs font-medium text-slate-200">
            <input
              type="checkbox"
              checked={showQaProfessorStep}
              onChange={(e) => setShowQaProfessorStep(e.target.checked)}
              className="h-3.5 w-3.5 shrink-0 rounded border-white/20 bg-white/[0.02] accent-brand-500 checked:!border-brand-500 checked:!bg-brand-500 focus:ring-2 focus:ring-brand-400/50"
            />
            Q&amp;A with the professor (Learn)
          </label>
          <label className={`mt-2 flex items-center gap-2 pl-5 text-[11px] ${showQaProfessorStep ? 'text-slate-400' : 'text-slate-600'}`}>
            <input
              type="checkbox"
              checked={enableQaProfessorVoice}
              disabled={!showQaProfessorStep}
              onChange={(e) => setEnableQaProfessorVoice(e.target.checked)}
              className="h-3 w-3 shrink-0 rounded border-white/20 bg-white/[0.02] accent-accent-500 checked:!border-accent-500 checked:!bg-accent-500 focus:ring-2 focus:ring-accent-400/50 disabled:opacity-40"
            />
            Also allow real-time voice (higher cost)
          </label>
        </div>

        <div className="rounded-xl border border-white/10 bg-white/[0.02] p-3">
          <label className="flex items-center gap-2 text-xs font-medium text-slate-200">
            <input
              type="checkbox"
              checked={showDeepDiveAgentChat}
              onChange={(e) => setShowDeepDiveAgentChat(e.target.checked)}
              className="h-3.5 w-3.5 shrink-0 rounded border-white/20 bg-white/[0.02] accent-brand-500 checked:!border-brand-500 checked:!bg-brand-500 focus:ring-2 focus:ring-brand-400/50"
            />
            Speak with your agent (Deep Dive)
          </label>
          <label className={`mt-2 flex items-center gap-2 pl-5 text-[11px] ${showDeepDiveAgentChat ? 'text-slate-400' : 'text-slate-600'}`}>
            <input
              type="checkbox"
              checked={enableDeepDiveAgentVoice}
              disabled={!showDeepDiveAgentChat}
              onChange={(e) => setEnableDeepDiveAgentVoice(e.target.checked)}
              className="h-3 w-3 shrink-0 rounded border-white/20 bg-white/[0.02] accent-accent-500 checked:!border-accent-500 checked:!bg-accent-500 focus:ring-2 focus:ring-accent-400/50 disabled:opacity-40"
            />
            Also allow real-time voice (higher cost)
          </label>
        </div>

        <div className="rounded-xl border border-white/10 bg-white/[0.02] p-3">
          <label className="flex items-center gap-2 text-xs font-medium text-slate-200">
            <input
              type="checkbox"
              checked={includeWorkplaceApplicationStep}
              onChange={(e) => setIncludeWorkplaceApplicationStep(e.target.checked)}
              className="h-3.5 w-3.5 shrink-0 rounded border-white/20 bg-white/[0.02] accent-brand-500 checked:!border-brand-500 checked:!bg-brand-500 focus:ring-2 focus:ring-brand-400/50"
            />
            In your role
          </label>
        </div>

        <div className="rounded-xl border border-white/10 bg-white/[0.02] p-3">
          <label className="flex items-center gap-2 text-xs font-medium text-slate-200">
            <input
              type="checkbox"
              checked={includeHypothesisStep}
              onChange={(e) => setIncludeHypothesisStep(e.target.checked)}
              className="h-3.5 w-3.5 shrink-0 rounded border-white/20 bg-white/[0.02] accent-brand-500 checked:!border-brand-500 checked:!bg-brand-500 focus:ring-2 focus:ring-brand-400/50"
            />
            Hypothesis
          </label>
          <p className="mt-1 pl-5 text-[11px] text-slate-500">
            Typically off for young-learner / pre-reader courses.
          </p>
        </div>
      </div>

      {/* Read-only — see the component's doc comment: these always exist
          and, once generated in Static mode, their counts are permanently
          fixed, so there's nothing safe to edit here. ("In your role" above
          IS the Production step — same single feature, not a separate one,
          so it isn't repeated down here.) */}
      <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-3">
        <div className="rounded-xl border border-white/10 bg-white/[0.02] px-3 py-2 text-xs text-slate-300" title="Fixed — cannot be changed after course generation.">
          <span className="font-medium text-white">Deep Dive cards per Engram:</span> {document.MaxDeepDiveCardsPerNode ?? 3} 🔒
        </div>
        <div className="rounded-xl border border-white/10 bg-white/[0.02] px-3 py-2 text-xs text-slate-300" title="Fixed — cannot be changed after course generation.">
          <span className="font-medium text-white">Recall:</span>{' '}
          {document.IncludeRecallStep ? `${document.RecallGenerationMode} (${document.RecallItemsRequired ?? 3} items to master)` : 'Off'} 🔒
        </div>
        <div className="rounded-xl border border-white/10 bg-white/[0.02] px-3 py-2 text-xs text-slate-300" title="Fixed — cannot be changed after course generation.">
          <span className="font-medium text-white">Assessment:</span>{' '}
          {document.IncludeAssessmentStep
            ? `${document.AssessmentGenerationMode} (${document.AssessmentQuestionsPerRound ?? 3} questions, ${document.AssessmentQuestionType})`
            : 'Off'}{' '}
          🔒
        </div>
      </div>

      {error && <p className="mt-3 text-xs text-red-400">{error}</p>}

      <div className="mt-4 flex items-center gap-3">
        <button
          type="button"
          onClick={() => void handleSave()}
          disabled={saving}
          className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2 text-xs font-semibold text-white shadow-lg shadow-brand-500/20 disabled:opacity-50"
        >
          {saving ? 'Saving…' : 'Save changes'}
        </button>
        {savedAt && !saving && <span className="text-xs text-emerald-400">Saved ✓</span>}
      </div>
    </div>
  );
}

