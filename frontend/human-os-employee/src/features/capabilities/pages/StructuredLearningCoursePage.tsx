import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import {
  ArrowLeft,
  Lock,
  PlayCircle,
  Star,
  Menu,
  X,
  ListChecks,
  Search,
  Sparkles,
  RefreshCw,
  Flame,
  Zap,
  Target,
  Info,
  Check,
  Clock,
  ArrowRight,
  Layers,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import {
  getStructuredLearningDocument,
  getActiveStructuredLearningAssessmentRound,
  type StructuredLearningDocumentDetail,
  type StructuredLearningSectionDetail,
} from '../api/structuredLearningRuntimeApi';
import {
  getStructuredLearningCourseSessionCount,
} from '../api/runtimeSessionApi';
import {
  getEngramImpactScores,
  refreshEngramImpactScores,
  type EngramImpactScoreDto,
} from '../api/engramImpactApi';
import KnowledgeGraphSection from '../components/KnowledgeGraphSection';
import { useCourseFocusModeStore } from '@/store/useCourseFocusModeStore';

type SectionState = 'Locked' | 'Available' | 'Mastered';

const STATE_STYLES: Record<
  SectionState,
  { ring: string; bubble: string; icon: string; label: string; Icon: typeof Lock }
> = {
  Locked: {
    ring: 'border-slate-300 dark:border-white/10',
    bubble: 'bg-slate-100 dark:bg-white/[0.03]',
    icon: 'text-slate-400 dark:text-slate-500',
    label: 'text-slate-500 dark:text-slate-400 font-medium',
    Icon: Lock,
  },
  Available: {
    ring: 'border-transparent',
    bubble: 'bg-gradient-to-br from-indigo-500 to-purple-600 shadow-lg shadow-indigo-500/30 text-white',
    icon: 'text-white',
    label: 'text-slate-900 dark:text-white font-bold',
    Icon: PlayCircle,
  },
  Mastered: {
    ring: 'border-amber-400 dark:border-amber-300/50',
    bubble: 'bg-amber-100 dark:bg-amber-400/10',
    icon: 'text-amber-600 dark:text-amber-300',
    label: 'text-amber-900 dark:text-amber-100 font-semibold',
    Icon: Star,
  },
};

// Single calm, neutral card style for every chapter group — a different
// color per chapter (previously cycling through 6 hues) made the sidebar
// feel like a rainbow instead of a readable table of contents.
const LEVEL_ACCENT = {
  text: 'text-slate-500 dark:text-slate-400',
  dot: 'bg-indigo-500 dark:bg-indigo-400',
  border: 'border-slate-200 dark:border-white/10 bg-slate-50/60 dark:bg-white/[0.02]',
};


export default function StructuredLearningCoursePage() {
  const { documentId } = useParams<{ documentId: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();

  // Course focus mode (2026-10-02) — hides the sidebar and blocks all
  // navigation while the student is inside this course, released only via
  // the layout's "Exit course" button. Enabled for the whole lifetime of
  // this page (and any engram reached from it).
  const enterFocusMode = useCourseFocusModeStore((state) => state.enter);
  useEffect(() => {
    enterFocusMode();
  }, [enterFocusMode]);
  const { language } = useI18n();
  const isEn = language === 'en';
  const personId = user?.personId ?? '';

  const [document, setDocument] = useState<StructuredLearningDocumentDetail | null>(null);
  const [passedBySectionId, setPassedBySectionId] = useState<Record<string, boolean>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarQuery, setSidebarQuery] = useState('');
  const [showKnowledgeGraphPanel, setShowKnowledgeGraphPanel] = useState(false);

  // The overview only reads a session created after learning content opens.
  const [courseSessionId, setCourseSessionId] = useState<string | undefined>(undefined);
  const [courseSessionStartedDate, setCourseSessionStartedDate] = useState<string | undefined>(undefined);
  const [courseSessionCount, setCourseSessionCount] = useState<number | null>(null);

  useEffect(() => {
    if (!documentId) return;
    setCourseSessionCount(null);
    setCourseSessionId(undefined);
    setCourseSessionStartedDate(undefined);

    const storageKey = `structuredLearningCourseSession:${documentId}`;
    const cached = sessionStorage.getItem(storageKey);
    if (cached) {
      const parsed = JSON.parse(cached) as { EngramSessionId: string; StartedDate: string };
      setCourseSessionId(parsed.EngramSessionId);
      setCourseSessionStartedDate(parsed.StartedDate);
    }

    if (!personId) return;
    getStructuredLearningCourseSessionCount(personId, documentId)
      .then((res) => {
        setCourseSessionCount(res.SessionCount);
        if (!cached && res.EngramSessionId && res.StartedDate) {
          setCourseSessionId(res.EngramSessionId);
          setCourseSessionStartedDate(res.StartedDate);
        }
      })
      .catch((err) => console.warn('Could not load course session count:', err));
  }, [documentId, personId]);

  // Impact scores state
  const [impactScores, setImpactScores] = useState<EngramImpactScoreDto[] | null>(null);
  const [impactScoresLoading, setImpactScoresLoading] = useState(false);
  const [impactScoresError, setImpactScoresError] = useState<string | null>(null);
  const [selectedEngramForReason, setSelectedEngramForReason] = useState<EngramImpactScoreDto | null>(null);
  const [showImpactPurposeModal, setShowImpactPurposeModal] = useState(false);

  useEffect(() => {
    if (!documentId) return;
    setImpactScoresLoading(true);
    setImpactScoresError(null);
    getEngramImpactScores(documentId, personId)
      .then((scores) => {
        setImpactScores(scores);
      })
      .catch((err) => {
        const msg = err instanceof Error ? err.message : String(err);
        console.error('Failed to get engram impact scores:', err);
        setImpactScoresError(msg);
      })
      .finally(() => {
        setImpactScoresLoading(false);
      });
  }, [documentId, personId]);

  const handleRefreshImpact = useCallback(async () => {
    if (!documentId) return;
    setImpactScoresLoading(true);
    setImpactScoresError(null);
    try {
      const refreshed = await refreshEngramImpactScores(documentId, personId);
      setImpactScores(refreshed);
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      console.error('Failed to refresh engram impact scores:', err);
      setImpactScoresError(msg);
    } finally {
      setImpactScoresLoading(false);
    }
  }, [documentId, personId]);

  const impactScoresByEngramId = useMemo(() => {
    const map = new Map<string, EngramImpactScoreDto>();
    if (impactScores) {
      for (const item of impactScores) {
        map.set(item.engramId.toLowerCase(), item);
      }
    }
    return map;
  }, [impactScores]);

  const impactStats = useMemo(() => {
    if (!impactScores || impactScores.length === 0) return null;
    const critical = impactScores.filter((s) => s.tier === 'Critical').length;
    const important = impactScores.filter((s) => s.tier === 'Important').length;
    const refresher = impactScores.filter((s) => s.tier === 'Refresher').length;
    const optional = impactScores.filter((s) => s.tier === 'Optional').length;
    const avgScore = Math.round(impactScores.reduce((sum, s) => sum + s.score, 0) / impactScores.length);
    return { critical, important, refresher, optional, total: impactScores.length, avgScore };
  }, [impactScores]);

  const loadDocument = useCallback(() => {
    if (!documentId) return;
    setLoading(true);
    getStructuredLearningDocument(documentId)
      .then(async (detail) => {
        setDocument(detail);
        if (personId) {
          const entries = await Promise.all(
            detail.Sections.map(async (s) => {
              const round = await getActiveStructuredLearningAssessmentRound(s.StructuredLearningDocumentSectionId, personId).catch(() => null);
              return [s.StructuredLearningDocumentSectionId, round?.Status === 'Passed'] as const;
            })
          );
          setPassedBySectionId(Object.fromEntries(entries));
        }
      })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)))
      .finally(() => setLoading(false));
  }, [documentId, personId]);

  useEffect(() => {
    loadDocument();
  }, [loadDocument]);

  const sections = document?.Sections ?? [];

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
        <p className="text-red-400">{error ?? (isEn ? 'This course has no sections yet.' : 'Este curso aún no tiene secciones.')}</p>
        <Link to="/capabilities/job-skills" className="mt-4 inline-flex items-center gap-1.5 text-sm text-indigo-400 hover:text-indigo-300">
          <ArrowLeft className="h-4 w-4" /> {isEn ? 'Back to My Role' : 'Volver a Mi Puesto'}
        </Link>
      </div>
    );
  }

  if (loading && !document) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-20 text-center">
        <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-indigo-500 border-t-transparent" />
        <p className="mt-3 text-xs text-slate-400">{isEn ? 'Loading course map...' : 'Cargando mapa del curso...'}</p>
      </div>
    );
  }

  if (!document) return null;

  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 xl:max-w-7xl 2xl:max-w-[96rem]">
      {/* Persistent, well-sized session banner (2026-10-02) — shown inline
          in the page flow (not a small floating badge easily hidden behind
          other UI) so the session id + real server-recorded start time are
          always clearly visible while on this course. */}
      {(courseSessionId || courseSessionCount !== null) && (
        <div className="mt-4 flex flex-col items-center justify-center gap-1 rounded-2xl border border-indigo-200 bg-indigo-50/70 px-4 py-3 text-center dark:border-indigo-500/20 dark:bg-indigo-500/10">
          {courseSessionId && (
            <div className="flex flex-wrap items-center justify-center gap-2">
              <PlayCircle className="h-4 w-4 text-indigo-600 dark:text-indigo-300" />
              <span className="text-sm font-semibold text-indigo-900 dark:text-indigo-100">
                {isEn ? 'Session' : 'Sesión'} <span className="font-mono">{courseSessionId.slice(0, 8)}</span>
              </span>
              {courseSessionStartedDate && (
                <span className="text-sm text-indigo-700 dark:text-indigo-300">
                  · {isEn ? 'started at' : 'inició a las'}{' '}
                  {new Date(courseSessionStartedDate).toLocaleTimeString(isEn ? 'en-US' : 'es-MX', { hour: '2-digit', minute: '2-digit' })}
                </span>
              )}
            </div>
          )}
          {courseSessionCount !== null && (
            <span className="text-xs font-semibold text-indigo-700 dark:text-indigo-300">
              {isEn ? `Sessions with activity: ${courseSessionCount}` : `Sesiones con actividad: ${courseSessionCount}`}
            </span>
          )}
        </div>
      )}

      <Link to="/capabilities/job-skills" className="mt-4 inline-flex items-center gap-1.5 text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white transition">
        <ArrowLeft className="h-4 w-4" /> {isEn ? 'Back to My Role' : 'Volver a Mi Puesto'}
      </Link>

      <div className="mt-4 text-center">
        <p className="text-xs font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">STRUCTURED LEARNING PATH</p>
        <h1 className="mt-1 text-2xl sm:text-3xl font-extrabold text-slate-900 dark:text-white tracking-tight">{document.DocumentName}</h1>
        <p className="mt-1.5 text-xs sm:text-sm text-slate-600 dark:text-slate-400 font-medium">🏔️ {isEn ? 'Climb section by section all the way to the top' : 'Escala sección por sección hasta la cima'}</p>

        {/* Total Course Duration & Stats with 10% Overhead */}
        <div className="mt-4 flex flex-wrap items-center justify-center gap-2.5">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-indigo-200 dark:border-indigo-500/30 bg-indigo-50 dark:bg-indigo-950/40 px-3.5 py-1.5 text-xs font-bold text-indigo-700 dark:text-indigo-300 shadow-xs">
            <Layers className="h-4 w-4 text-indigo-500" />
            <span>{sections.length} Engram{sections.length === 1 ? '' : 's'}</span>
          </span>

          <span
            className="inline-flex items-center gap-1.5 rounded-full border border-amber-200 dark:border-amber-500/30 bg-amber-50 dark:bg-amber-950/40 px-3.5 py-1.5 text-xs font-bold text-amber-800 dark:text-amber-300 shadow-xs cursor-help"
            title={isEn ? `Base time: ~${courseDurationStats.rawMinutes} min + 10% overhead (~${courseDurationStats.overheadMinutes} min) for reflection, active recall & notes` : `Tiempo base: ~${courseDurationStats.rawMinutes} min + 10% de margen (~${courseDurationStats.overheadMinutes} min) para reflexión, active recall y notas`}
          >
            <Clock className="h-4 w-4 text-amber-500" />
            <span>{isEn ? 'Total duration:' : 'Duración total:'} <strong>~{courseDurationStats.durationFormatted}</strong></span>
            <span className="text-[10px] font-semibold text-amber-700/80 dark:text-amber-300/80">
              ({isEn ? 'incl. 10% overhead' : 'incl. 10% margen'})
            </span>
          </span>

          <span className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.03] px-3.5 py-1.5 text-xs font-semibold text-slate-700 dark:text-slate-300 shadow-xs">
            <Sparkles className="h-4 w-4 text-purple-500" />
            <span>{levels.length} {levels.length === 1 ? (isEn ? 'Chapter' : 'Capítulo') : (isEn ? 'Chapters' : 'Capítulos')}</span>
          </span>
        </div>
      </div>

      {/* Personalized Impact Analysis Banner */}
      {impactStats && (
        <div className="mt-6 overflow-hidden rounded-2xl border border-indigo-200/80 dark:border-indigo-500/20 bg-gradient-to-br from-indigo-50/70 via-white to-purple-50/40 dark:from-indigo-950/30 dark:via-slate-900/60 dark:to-purple-950/20 p-5 shadow-sm">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-start gap-3">
              <div className="flex h-10 w-10 flex-none items-center justify-center rounded-xl bg-gradient-to-br from-indigo-500 to-purple-600 text-white shadow-md shadow-indigo-500/20">
                <Sparkles className="h-5 w-5" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h2 className="text-sm font-bold text-slate-900 dark:text-white">
                    {isEn ? 'Personalized Impact Analysis' : 'Análisis de Impacto Personalizado'}
                  </h2>
                  <span className="rounded-full bg-indigo-100 dark:bg-indigo-900/40 px-2 py-0.5 text-[10px] font-semibold text-indigo-700 dark:text-indigo-300">
                    AI Curated
                  </span>
                </div>
                <p className="mt-0.5 text-xs text-slate-600 dark:text-slate-400">
                  {isEn
                    ? 'Engrams prioritised for your role, active Q4 SMART objectives, and performance KPIs.'
                    : 'Engramas priorizados para tu puesto, objetivos SMART Q4 y KPIs de desempeño.'}
                </p>
                {impactScoresError && (
                  <p className="mt-1 text-xs font-medium text-rose-600 dark:text-rose-400">
                    ⚠️ {impactScoresError}
                  </p>
                )}
              </div>
            </div>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setShowImpactPurposeModal(true)}
                className="inline-flex items-center justify-center gap-1.5 rounded-xl border border-indigo-200 dark:border-indigo-500/30 bg-indigo-50/70 dark:bg-indigo-950/40 px-3.5 py-2 text-xs font-semibold text-indigo-700 dark:text-indigo-300 shadow-sm hover:bg-indigo-100 dark:hover:bg-indigo-900/50 transition"
              >
                <Info className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                <span>{isEn ? 'How Scores Work' : 'Cómo Funciona el Score'}</span>
              </button>

              <button
                type="button"
                disabled={impactScoresLoading}
                onClick={handleRefreshImpact}
                className="inline-flex items-center justify-center gap-1.5 rounded-xl border border-indigo-200 dark:border-indigo-500/30 bg-white dark:bg-white/[0.04] px-3.5 py-2 text-xs font-semibold text-indigo-700 dark:text-indigo-300 shadow-sm hover:bg-indigo-50 dark:hover:bg-indigo-500/10 transition disabled:opacity-50"
              >
                <RefreshCw className={`h-3.5 w-3.5 ${impactScoresLoading ? 'animate-spin text-indigo-500' : ''}`} />
                <span>{impactScoresLoading ? (isEn ? 'Scoring with AI...' : 'Calculando...') : (isEn ? 'Recalculate with AI' : 'Recalcular con IA')}</span>
              </button>
            </div>
          </div>

          <div className="mt-4 flex flex-wrap items-center gap-2.5 pt-3 border-t border-indigo-100/80 dark:border-white/5">
            <div className="inline-flex items-center gap-1.5 rounded-lg bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-500/30 px-2.5 py-1 text-xs font-semibold text-rose-700 dark:text-rose-300">
              <span className="h-2 w-2 rounded-full bg-rose-500 animate-pulse" />
              <span>{impactStats.critical} {isEn ? 'Critical (Gap)' : 'Crítico (Brecha)'}</span>
              <span className="text-[10px] opacity-75">(80-100)</span>
            </div>

            <div className="inline-flex items-center gap-1.5 rounded-lg bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-500/30 px-2.5 py-1 text-xs font-semibold text-amber-700 dark:text-amber-300">
              <span className="h-2 w-2 rounded-full bg-amber-500" />
              <span>{impactStats.important} {isEn ? 'Important (Mastery)' : 'Importante (Maestría)'}</span>
              <span className="text-[10px] opacity-75">(55-79)</span>
            </div>

            <div className="inline-flex items-center gap-1.5 rounded-lg bg-sky-50 dark:bg-sky-950/40 border border-sky-200 dark:border-sky-500/30 px-2.5 py-1 text-xs font-semibold text-sky-700 dark:text-sky-300">
              <span className="h-2 w-2 rounded-full bg-sky-500" />
              <span>{impactStats.refresher} {isEn ? 'Refresher' : 'Repaso'}</span>
              <span className="text-[10px] opacity-75">(30-54)</span>
            </div>

            <div className="inline-flex items-center gap-1.5 rounded-lg bg-slate-100 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 px-2.5 py-1 text-xs font-semibold text-slate-600 dark:text-slate-400">
              <span className="h-2 w-2 rounded-full bg-slate-400" />
              <span>{impactStats.optional} {isEn ? 'Base / Foundational' : 'Base / Fundamental'}</span>
              <span className="text-[10px] opacity-75">(0-29)</span>
            </div>

            <div className="ml-auto text-[11px] text-slate-500 dark:text-slate-400 font-medium">
              {isEn ? `Average Impact: ${impactStats.avgScore}/100` : `Impacto Promedio: ${impactStats.avgScore}/100`}
            </div>
          </div>
        </div>
      )}

      {/* Engram Impact Detail Modal / Dialog */}
      {selectedEngramForReason && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-sm animate-in fade-in duration-150">
          <div className="relative w-full max-w-xl rounded-2xl border border-slate-200 dark:border-white/15 bg-white dark:bg-slate-900 p-6 shadow-2xl">
            <button
              type="button"
              onClick={() => setSelectedEngramForReason(null)}
              className="absolute right-4 top-4 rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white transition"
              aria-label="Close"
            >
              <X className="h-4 w-4" />
            </button>

            <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
              <Sparkles className="h-3.5 w-3.5" />
              <span>{isEn ? 'AI Personalization Rationale & Score Composition' : 'Justificación y Composición del Score con IA'}</span>
            </div>

            <h3 className="mt-2 text-lg font-bold text-slate-900 dark:text-white">
              {sections.find(
                (s) => s.StructuredLearningDocumentSectionId.toLowerCase() === selectedEngramForReason.engramId.toLowerCase()
              )?.SubchapterTitle ?? selectedEngramForReason.engramId}
            </h3>

            <div className="mt-3 flex flex-wrap items-center gap-2">
              <span
                className={`inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-bold ${
                  selectedEngramForReason.tier === 'Critical'
                    ? 'bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-200 border border-rose-300 dark:border-rose-500/30'
                    : selectedEngramForReason.tier === 'Important'
                    ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200 border border-amber-300 dark:border-amber-500/30'
                    : selectedEngramForReason.tier === 'Refresher'
                    ? 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200 border border-sky-300 dark:border-sky-500/30'
                    : 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300 border border-slate-200 dark:border-slate-700'
                }`}
              >
                {selectedEngramForReason.tier === 'Critical' && <Flame className="h-3.5 w-3.5 text-rose-600 dark:text-rose-400" />}
                {selectedEngramForReason.tier === 'Important' && <Zap className="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />}
                {selectedEngramForReason.tier === 'Refresher' && <RefreshCw className="h-3.5 w-3.5 text-sky-600 dark:text-sky-400" />}
                {selectedEngramForReason.score}/100 — {selectedEngramForReason.tier} {isEn ? 'Impact' : 'Impacto'}
              </span>

              <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-50 dark:bg-indigo-950/40 border border-indigo-200 dark:border-indigo-500/30 px-3 py-1 text-xs font-semibold text-indigo-700 dark:text-indigo-300">
                <Target className="h-3.5 w-3.5" />
                <span>
                  {isEn ? 'Primary Driver:' : 'Impulsor Principal:'} <strong>{selectedEngramForReason.primarySignal}</strong>
                </span>
              </span>
            </div>

            {/* AI Personalization Reason Narrative */}
            <div className="mt-4 rounded-xl border border-indigo-100 dark:border-indigo-500/20 bg-indigo-50/40 dark:bg-indigo-950/20 p-4 text-sm text-slate-800 dark:text-slate-200 leading-relaxed">
              <div className="text-[11px] font-bold uppercase tracking-wider text-indigo-700 dark:text-indigo-300 mb-1">
                {isEn ? 'Why this score was assigned to you:' : 'Por qué se te asignó este puntaje:'}
              </div>
              <p className="italic">"{selectedEngramForReason.reason}"</p>
            </div>

            {/* Why this Score vs Higher Scores Comparison Box */}
            <div className="mt-4 rounded-xl border border-slate-200 dark:border-slate-700/60 bg-slate-50 dark:bg-slate-950/40 p-4 text-xs space-y-2.5">
              <div className="font-bold text-slate-900 dark:text-white flex items-center gap-1.5">
                <Target className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                <span>{isEn ? `Why ${selectedEngramForReason.score}/100 vs other scores?` : `¿Por qué ${selectedEngramForReason.score}/100 vs otros puntajes?`}</span>
              </div>
              
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-[11px]">
                <div className="rounded-lg bg-sky-50 dark:bg-sky-950/30 border border-sky-200 dark:border-sky-500/20 p-2.5 space-y-1">
                  <div className="font-bold text-sky-800 dark:text-sky-300 flex items-center gap-1">
                    <RefreshCw className="h-3 w-3" />
                    <span>{isEn ? `This Engram (${selectedEngramForReason.score}/100):` : `Este Engrama (${selectedEngramForReason.score}/100):`}</span>
                  </div>
                  <p className="text-slate-600 dark:text-slate-300 leading-snug">
                    {selectedEngramForReason.tier === 'Refresher'
                      ? (isEn ? 'Required for your role, but your resume already demonstrates prior knowledge. Only light alignment is needed.' : 'Exigido para tu rol, pero tu CV ya demuestra conocimientos previos. Solo requiere repaso.')
                      : selectedEngramForReason.tier === 'Critical'
                      ? (isEn ? 'High-priority unverified skill gap directly linked to active Q4 objectives and KPIs.' : 'Brecha de alta prioridad directamente ligada a objetivos Q4 y KPIs.')
                      : selectedEngramForReason.tier === 'Important'
                      ? (isEn ? 'Important core capability to reinforce and demonstrate full mastery on current deliverables.' : 'Capacidad esencial para consolidar y demostrar maestría en entregables actuales.')
                      : (isEn ? 'Foundational background concepts providing context across domains.' : 'Conceptos base y prerrequisitos que aportan contexto general.')}
                  </p>
                </div>

                <div className="rounded-lg bg-indigo-50 dark:bg-indigo-950/30 border border-indigo-200 dark:border-indigo-500/20 p-2.5 space-y-1">
                  <div className="font-bold text-indigo-800 dark:text-indigo-300 flex items-center gap-1">
                    <Sparkles className="h-3 w-3" />
                    <span>{isEn ? 'Relative Priority Logic:' : 'Lógica de Prioridad Relativa:'}</span>
                  </div>
                  <p className="text-slate-600 dark:text-slate-300 leading-snug">
                    {isEn
                      ? 'Scores scale up (80–100) when a topic is required by your role BUT absent/unverified in your profile and measured by active KPIs.'
                      : 'Los puntajes suben (80–100) cuando un tema es exigido por tu puesto PERO no está validado en tu CV y lo miden tus KPIs activos.'}
                  </p>
                </div>
              </div>
            </div>

            {/* Score Composition & Signal Breakdown */}
            <div className="mt-4 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-4">
              <div className="text-[11px] font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400 mb-2.5">
                {isEn ? 'Scoring Signals & Weighting Framework' : 'Ponderación y Señales del Score'}
              </div>

              <div className="space-y-2">
                {/* Signal 1: Job Description & Core Responsibilities */}
                <div
                  className={`flex items-center justify-between p-2 rounded-lg text-xs transition ${
                    selectedEngramForReason.primarySignal === 'Role'
                      ? 'bg-indigo-100/70 dark:bg-indigo-900/30 font-bold text-indigo-900 dark:text-indigo-200 border border-indigo-200 dark:border-indigo-500/30'
                      : 'text-slate-600 dark:text-slate-400'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-sky-500" />
                    <span>{isEn ? '1. Job Description / Role' : '1. Descripción de Puesto / Rol'}</span>
                    <span className="text-[10px] text-slate-500 dark:text-slate-400">({isEn ? 'what role demands' : 'lo que el puesto exige'})</span>
                    {selectedEngramForReason.primarySignal === 'Role' && (
                      <span className="rounded bg-indigo-600 px-1.5 py-0.2 text-[9px] font-bold text-white uppercase">
                        {isEn ? 'Primary Driver' : 'Impulsor Principal'}
                      </span>
                    )}
                  </div>
                  <span className="font-mono font-bold">40%</span>
                </div>

                {/* Signal 2: KPIs & SMART Objectives */}
                <div
                  className={`flex items-center justify-between p-2 rounded-lg text-xs transition ${
                    selectedEngramForReason.primarySignal === 'KPI'
                      ? 'bg-indigo-100/70 dark:bg-indigo-900/30 font-bold text-indigo-900 dark:text-indigo-200 border border-indigo-200 dark:border-indigo-500/30'
                      : 'text-slate-600 dark:text-slate-400'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-indigo-500" />
                    <span>{isEn ? '2. KPIs & Active Objectives' : '2. KPIs y Objetivos Activos'}</span>
                    <span className="text-[10px] text-slate-500 dark:text-slate-400">({isEn ? 'what is measured today' : 'lo que miden hoy'})</span>
                    {selectedEngramForReason.primarySignal === 'KPI' && (
                      <span className="rounded bg-indigo-600 px-1.5 py-0.2 text-[9px] font-bold text-white uppercase">
                        {isEn ? 'Primary Driver' : 'Impulsor Principal'}
                      </span>
                    )}
                  </div>
                  <span className="font-mono font-bold">25%</span>
                </div>

                {/* Signal 3: Manager Coaching & Flagged Gaps */}
                <div
                  className={`flex items-center justify-between p-2 rounded-lg text-xs transition ${
                    selectedEngramForReason.primarySignal === 'Coaching'
                      ? 'bg-indigo-100/70 dark:bg-indigo-900/30 font-bold text-indigo-900 dark:text-indigo-200 border border-indigo-200 dark:border-indigo-500/30'
                      : 'text-slate-600 dark:text-slate-400'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-purple-500" />
                    <span>{isEn ? '3. Manager Coaching & Gaps' : '3. Coaching y Brechas'}</span>
                    <span className="text-[10px] text-slate-500 dark:text-slate-400">({isEn ? 'manager flagged areas' : 'gaps marcados por manager'})</span>
                    {selectedEngramForReason.primarySignal === 'Coaching' && (
                      <span className="rounded bg-indigo-600 px-1.5 py-0.2 text-[9px] font-bold text-white uppercase">
                        {isEn ? 'Primary Driver' : 'Impulsor Principal'}
                      </span>
                    )}
                  </div>
                  <span className="font-mono font-bold">15%</span>
                </div>

                {/* Signal 4: Career Growth & Promotion Roadmap */}
                <div
                  className={`flex items-center justify-between p-2 rounded-lg text-xs transition ${
                    selectedEngramForReason.primarySignal === 'CareerGrowth'
                      ? 'bg-indigo-100/70 dark:bg-indigo-900/30 font-bold text-indigo-900 dark:text-indigo-200 border border-indigo-200 dark:border-indigo-500/30'
                      : 'text-slate-600 dark:text-slate-400'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-amber-500" />
                    <span>{isEn ? '4. Career Growth & Promotion' : '4. Career Growth y Ascenso'}</span>
                    <span className="text-[10px] text-slate-500 dark:text-slate-400">({isEn ? 'future advancement' : 'crecimiento futuro'})</span>
                    {selectedEngramForReason.primarySignal === 'CareerGrowth' && (
                      <span className="rounded bg-indigo-600 px-1.5 py-0.2 text-[9px] font-bold text-white uppercase">
                        {isEn ? 'Primary Driver' : 'Impulsor Principal'}
                      </span>
                    )}
                  </div>
                  <span className="font-mono font-bold">12%</span>
                </div>

                {/* Signal 5: Assigned Skills & Tools */}
                <div
                  className={`flex items-center justify-between p-2 rounded-lg text-xs transition ${
                    selectedEngramForReason.primarySignal === 'Skill'
                      ? 'bg-indigo-100/70 dark:bg-indigo-900/30 font-bold text-indigo-900 dark:text-indigo-200 border border-indigo-200 dark:border-indigo-500/30'
                      : 'text-slate-600 dark:text-slate-400'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-emerald-500" />
                    <span>{isEn ? '5. Assigned Skills & Tools' : '5. Habilidades Asignadas y Herramientas'}</span>
                    <span className="text-[10px] text-slate-500 dark:text-slate-400">({isEn ? 'supporting signal' : 'señal de apoyo'})</span>
                    {selectedEngramForReason.primarySignal === 'Skill' && (
                      <span className="rounded bg-indigo-600 px-1.5 py-0.2 text-[9px] font-bold text-white uppercase">
                        {isEn ? 'Primary Driver' : 'Impulsor Principal'}
                      </span>
                    )}
                  </div>
                  <span className="font-mono font-bold">8%</span>
                </div>
              </div>
            </div>

            <div className="mt-6 flex justify-end">
              <button
                type="button"
                onClick={() => setSelectedEngramForReason(null)}
                className="rounded-xl bg-indigo-600 px-5 py-2 text-xs font-semibold text-white hover:bg-indigo-500 transition shadow-sm"
              >
                {isEn ? 'Close' : 'Cerrar'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Purpose / Philosophy of Engram Impact Scores Modal */}
      {showImpactPurposeModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-sm animate-in fade-in duration-150">
          <div className="relative w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-2xl border border-slate-200 dark:border-white/15 bg-white dark:bg-slate-900 p-6 shadow-2xl">
            <button
              type="button"
              onClick={() => setShowImpactPurposeModal(false)}
              className="absolute right-4 top-4 rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white transition"
              aria-label="Close"
            >
              <X className="h-4 w-4" />
            </button>

            <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
              <Target className="h-4 w-4" />
              <span>{isEn ? 'Engram Impact Scoring Purpose & Guidelines' : 'Propósito y Guía de Puntuación de Impacto'}</span>
            </div>

            <h3 className="mt-2 text-xl font-bold text-slate-900 dark:text-white">
              {isEn ? 'Why do Engrams have Impact Scores?' : '¿Por qué los Engramas tienen Puntuaciones de Impacto?'}
            </h3>

            {/* Core Principle Callout */}
            <div className="mt-4 rounded-xl border border-indigo-200 dark:border-indigo-500/30 bg-indigo-50/70 dark:bg-indigo-950/30 p-4">
              <p className="text-sm font-semibold text-indigo-950 dark:text-indigo-200">
                {isEn
                  ? '📌 Core Rule: Complete the full curriculum in order. No Engrams are skipped. Scores indicate where your effort delivers maximum strategic impact.'
                  : '📌 Regla Fundamental: Completa el curso completo en orden. No se salta ningún Engrama. El puntaje solo indica dónde tu esfuerzo tiene mayor impacto estratégico.'}
              </p>
              <p className="mt-1.5 text-xs text-indigo-800 dark:text-indigo-300">
                {isEn
                  ? 'All Engrams remain in their exact instructional sequence and all must be mastered to complete the course.'
                  : 'Todos los Engramas se mantienen en su secuencia curricular y todos deben completarse para finalizar el curso.'}
              </p>
            </div>

            {/* What Scores ARE vs ARE NOT */}
            <div className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
              <div className="rounded-xl border border-rose-200 dark:border-rose-500/30 bg-rose-50/60 dark:bg-rose-950/20 p-3.5">
                <div className="flex items-center gap-1.5 font-bold text-rose-800 dark:text-rose-300">
                  <span>❌</span>
                  <span>{isEn ? 'What the score is NOT:' : 'Lo que NO es el score:'}</span>
                </div>
                <p className="mt-1 text-slate-700 dark:text-slate-300">
                  {isEn
                    ? '"Low score = optional or can be skipped."'
                    : '"Puntaje bajo = opcional o puedes saltártelo."'}
                </p>
              </div>

              <div className="rounded-xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50/60 dark:bg-emerald-950/20 p-3.5">
                <div className="flex items-center gap-1.5 font-bold text-emerald-800 dark:text-emerald-300">
                  <span>✅</span>
                  <span>{isEn ? 'What the score IS:' : 'Lo que SÍ es el score:'}</span>
                </div>
                <p className="mt-1 text-slate-700 dark:text-slate-300">
                  {isEn
                    ? '"This is where your focus produces the highest return for your current role & career."'
                    : '"Aquí es donde tu esfuerzo tiene MÁS impacto en tu puesto y crecimiento profesional."'}
                </p>
              </div>
            </div>

            {/* The 3 Purposes */}
            <div className="mt-5 space-y-3">
              <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
                {isEn ? 'The 3 Strategic Purposes of the Score' : 'Los 3 Propósitos Estratégicos del Score'}
              </h4>

              <div className="space-y-2.5 text-xs text-slate-700 dark:text-slate-300">
                <div className="rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-3.5">
                  <div className="font-bold text-slate-900 dark:text-white flex items-center gap-2">
                    <span className="flex h-5 w-5 items-center justify-center rounded-full bg-indigo-100 dark:bg-indigo-900 text-indigo-700 dark:text-indigo-300 text-[11px]">1</span>
                    <span>{isEn ? 'Emphasis & Energy Allocation' : 'Énfasis y Asignación de Energía'}</span>
                  </div>
                  <p className="mt-1 text-slate-600 dark:text-slate-400 pl-7">
                    {isEn
                      ? '"This engram directly affects your active Q4 objectives — invest dedicated attention here." You see clearly where your time yields the highest leverage.'
                      : '"Este engrama impacta directamente tus objetivos Q4 activos — pon más energía aquí." Te ayuda a identificar dónde tu tiempo tiene mayor rendimiento.'}
                  </p>
                </div>

                <div className="rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-3.5">
                  <div className="font-bold text-slate-900 dark:text-white flex items-center gap-2">
                    <span className="flex h-5 w-5 items-center justify-center rounded-full bg-indigo-100 dark:bg-indigo-900 text-indigo-700 dark:text-indigo-300 text-[11px]">2</span>
                    <span>{isEn ? 'Points & Strategic Gamification' : 'Puntos y Recompensas Estratégicas'}</span>
                  </div>
                  <p className="mt-1 text-slate-600 dark:text-slate-400 pl-7">
                    {isEn
                      ? 'Mastering high-impact Critical engrams awards boosted progression points and recognition badges aligned with organizational needs.'
                      : 'Dominar engramas Críticos de alto impacto otorga mayor puntuación de progreso y recompensas alineadas con las metas estratégicas de la empresa.'}
                  </p>
                </div>

                <div className="rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-3.5">
                  <div className="font-bold text-slate-900 dark:text-white flex items-center gap-2">
                    <span className="flex h-5 w-5 items-center justify-center rounded-full bg-indigo-100 dark:bg-indigo-900 text-indigo-700 dark:text-indigo-300 text-[11px]">3</span>
                    <span>{isEn ? 'AI Tutor Adaptive Depth' : 'Profundidad Adaptativa del Tutor IA'}</span>
                  </div>
                  <p className="mt-1 text-slate-600 dark:text-slate-400 pl-7">
                    {isEn
                      ? 'The AI Tutor drills deeper into your Critical gap areas with tailored exercises while moving efficiently through Refresher concepts.'
                      : 'El Tutor con IA profundiza con mayor detalle y casos prácticos en tus brechas Críticas, mientras avanza con mayor agilidad en temas de repaso.'}
                  </p>
                </div>
              </div>
            </div>

            {/* Tier Legend */}
            <div className="mt-5 rounded-xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-4 text-xs">
              <div className="font-bold text-slate-900 dark:text-white mb-2.5">
                {isEn ? 'Score Tiers Overview' : 'Resumen de Niveles de Impacto'}
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <div className="flex items-center gap-2 p-2 rounded-lg bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-500/30 text-rose-900 dark:text-rose-200">
                  <span className="h-2 w-2 rounded-full bg-rose-500 animate-pulse" />
                  <span className="font-bold">Critical (80–100):</span>
                  <span>{isEn ? 'Required + unverified gap' : 'Exigido + brecha sin validar'}</span>
                </div>
                <div className="flex items-center gap-2 p-2 rounded-lg bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-500/30 text-amber-900 dark:text-amber-200">
                  <span className="h-2 w-2 rounded-full bg-amber-500" />
                  <span className="font-bold">Important (55–79):</span>
                  <span>{isEn ? 'Required + reinforce mastery' : 'Exigido + consolidar maestría'}</span>
                </div>
                <div className="flex items-center gap-2 p-2 rounded-lg bg-sky-50 dark:bg-sky-950/30 border border-sky-200 dark:border-sky-500/30 text-sky-900 dark:text-sky-200">
                  <span className="h-2 w-2 rounded-full bg-sky-500" />
                  <span className="font-bold">Refresher (30–54):</span>
                  <span>{isEn ? 'Already mastered in resume' : 'Dominado previamente en CV'}</span>
                </div>
                <div className="flex items-center gap-2 p-2 rounded-lg bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-800 dark:text-slate-300">
                  <span className="h-2 w-2 rounded-full bg-slate-400" />
                  <span className="font-bold">Base / Foundational (0–29):</span>
                  <span>{isEn ? 'Prerequisites / broad context' : 'Prerrequisitos / contexto base'}</span>
                </div>
              </div>
            </div>

            <div className="mt-6 flex justify-end">
              <button
                type="button"
                onClick={() => setShowImpactPurposeModal(false)}
                className="rounded-xl bg-indigo-600 px-5 py-2 text-xs font-semibold text-white hover:bg-indigo-500 transition shadow-sm"
              >
                {isEn ? 'Understood' : 'Entendido'}
              </button>
            </div>
          </div>
        </div>
      )}

      {documentId && (
        <div className="mt-6 space-y-3">
          {showKnowledgeGraphPanel ? (
            <div className="relative">
              <div className="mb-2 flex justify-end">
                <button
                  type="button"
                  onClick={() => setShowKnowledgeGraphPanel(false)}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-red-300 dark:border-red-400/40 bg-white dark:bg-slate-900/90 px-3.5 py-1.5 text-xs font-semibold text-red-700 dark:text-red-200 shadow-md hover:border-red-400 hover:bg-red-50 dark:hover:bg-red-500/20 hover:text-red-900 dark:hover:text-white transition"
                  aria-label={isEn ? 'Hide Knowledge Graph' : 'Ocultar Knowledge Graph'}
                >
                  <X className="h-4 w-4 text-red-600 dark:text-red-400" />
                  <span>{isEn ? 'Hide Knowledge Graph' : 'Ocultar Knowledge Graph'}</span>
                </button>
              </div>
              <KnowledgeGraphSection
                documentId={documentId}
                personId={personId}
                onClose={() => setShowKnowledgeGraphPanel(false)}
                onSelectEngram={(sec) => {
                  const scoreInfo = impactScoresByEngramId.get(sec.StructuredLearningDocumentSectionId.toLowerCase());
                  if (scoreInfo) {
                    setSelectedEngramForReason(scoreInfo);
                  } else {
                    const idx = sections.findIndex((s) => s.StructuredLearningDocumentSectionId === sec.StructuredLearningDocumentSectionId);
                    goToSection(sec, idx >= 0 ? idx : 0);
                  }
                }}
              />
            </div>
          ) : (
            <div className="flex justify-center">
              <button
                type="button"
                onClick={() => setShowKnowledgeGraphPanel(true)}
                className="inline-flex items-center gap-1.5 rounded-xl border border-indigo-200 dark:border-indigo-400/40 bg-indigo-50 dark:bg-indigo-500/10 px-4 py-2.5 text-sm font-semibold text-indigo-700 dark:text-indigo-200 hover:bg-indigo-100 dark:hover:bg-indigo-500/20 shadow-sm transition"
              >
                {isEn ? 'Show Knowledge Graph' : 'Mostrar Knowledge Graph'}
              </button>
            </div>
          )}
        </div>
      )}

      {levels.length > 0 && (
        <button
          type="button"
          onClick={() => setSidebarOpen((open) => !open)}
          className="mx-auto mt-6 flex items-center gap-2 rounded-xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] px-4 py-2.5 text-sm font-medium text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-white/[0.06] lg:hidden shadow-sm"
        >
          {sidebarOpen ? <X className="h-4 w-4" /> : <Menu className="h-4 w-4" />}
          {sidebarOpen ? (isEn ? 'Hide table of contents' : 'Ocultar índice') : (isEn ? 'Show table of contents' : 'Mostrar índice')}
        </button>
      )}

      <div className="mt-8 flex flex-col gap-8 lg:flex-row lg:items-start lg:gap-10">
        {levels.length > 0 && (
          <aside className={`${sidebarOpen ? 'block' : 'hidden'} w-full flex-none lg:sticky lg:top-6 lg:block lg:w-72 xl:w-80 2xl:w-96`}>
            <div className="rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-900/60 p-4 lg:flex lg:max-h-[calc(100vh-3rem)] lg:flex-col shadow-sm">
              <div className="flex items-center gap-2 text-sm font-bold text-slate-900 dark:text-white">
                <ListChecks className="h-4 w-4 text-indigo-600 dark:text-indigo-400" /> {isEn ? 'Course Contents' : 'Contenido del curso'}
              </div>
              <div className="mt-3">
                <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-indigo-500 to-purple-500 transition-all"
                    style={{ width: `${progress.percent}%` }}
                  />
                </div>
                <p className="mt-1.5 text-xs font-medium text-slate-600 dark:text-slate-400">
                  {isEn ? `Mastered: ${progress.mastered} of ${progress.total}` : `Dominados: ${progress.mastered} de ${progress.total}`} ({progress.percent}%)
                </p>
              </div>

              <div className="relative mt-3">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400 dark:text-slate-500" />
                <input
                  type="text"
                  value={sidebarQuery}
                  onChange={(e) => setSidebarQuery(e.target.value)}
                  placeholder={isEn ? 'Search section...' : 'Buscar sección...'}
                  className="w-full rounded-lg border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.04] py-1.5 pl-8 pr-7 text-xs text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-slate-500 focus:border-indigo-500 dark:focus:border-indigo-400/60 focus:outline-none"
                />
                {sidebarQuery && (
                  <button
                    type="button"
                    onClick={() => setSidebarQuery('')}
                    className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:text-slate-500 dark:hover:text-white"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                )}
              </div>

              <nav className="mt-3 space-y-3 overflow-y-auto pr-1 lg:min-h-0 lg:flex-1">
                {filteredLevels.length === 0 && (
                  <p className="px-1 text-xs text-slate-500">{isEn ? 'No sections found.' : 'No se encontraron secciones.'}</p>
                )}
                {filteredLevels.map(([chapterTitle, list], levelIndex) => {
                  const accent = LEVEL_ACCENT;
                  return (
                    <div key={chapterTitle} className={`rounded-xl border p-2.5 ${accent.border}`}>
                      <button
                        type="button"
                        onClick={() => scrollToLevel(levelIndex)}
                        className={`flex w-full items-center gap-1.5 truncate text-left text-[11px] font-bold uppercase tracking-wide hover:underline ${accent.text}`}
                      >
                        <span className={`h-1.5 w-1.5 flex-none rounded-full ${accent.dot}`} />
                        <span className="truncate">{chapterTitle}</span>
                      </button>
                      <ul className="mt-1.5 space-y-0.5">
                        {list.map((section) => {
                          const index = sections.indexOf(section);
                          const state = sectionState(section, index);
                          const clickable = state !== 'Locked';
                          const style = STATE_STYLES[state];
                          const Icon = style.Icon;
                          const isNextUp = section.StructuredLearningDocumentSectionId === nextUpSectionId;
                          const scoreInfo = impactScoresByEngramId.get(section.StructuredLearningDocumentSectionId.toLowerCase());
                          return (
                            <li key={section.StructuredLearningDocumentSectionId}>
                              <button
                                type="button"
                                disabled={!clickable}
                                onClick={() => goToSection(section, index)}
                                className={`flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-xs transition ${
                                  state !== 'Locked' ? 'cursor-pointer hover:bg-slate-100 dark:hover:bg-white/[0.06]' : 'cursor-not-allowed opacity-50'
                                } ${isNextUp ? 'bg-indigo-50 dark:bg-indigo-500/10 ring-1 ring-inset ring-indigo-300 dark:ring-indigo-400/40' : ''}`}
                              >
                                <Icon className={`h-3.5 w-3.5 flex-none ${style.icon}`} fill={state === 'Mastered' ? 'currentColor' : 'none'} />
                                <span className={`line-clamp-2 flex-1 ${state === 'Locked' ? 'text-slate-400 dark:text-slate-500' : 'text-slate-800 dark:text-slate-200 font-medium'}`}>
                                  {section.SubchapterTitle}
                                </span>
                                {scoreInfo && (
                                  <span
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      setSelectedEngramForReason(scoreInfo);
                                    }}
                                    title={`${scoreInfo.tier} Impact (${scoreInfo.score}/100) — ${scoreInfo.reason}`}
                                    className={`flex-none inline-flex items-center gap-1 rounded-full px-1.5 py-0.5 text-[9px] font-bold transition hover:scale-105 ${
                                      scoreInfo.tier === 'Critical'
                                        ? 'bg-rose-100 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300 border border-rose-300/60 dark:border-rose-500/30'
                                        : scoreInfo.tier === 'Important'
                                        ? 'bg-amber-100 text-amber-700 dark:bg-amber-950/60 dark:text-amber-300 border border-amber-300/60 dark:border-amber-500/30'
                                        : scoreInfo.tier === 'Refresher'
                                        ? 'bg-sky-100 text-sky-700 dark:bg-sky-950/60 dark:text-sky-300 border border-sky-300/60 dark:border-sky-500/30'
                                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-200 dark:border-slate-700'
                                    }`}
                                  >
                                    <span
                                      className={`h-1.5 w-1.5 rounded-full ${
                                        scoreInfo.tier === 'Critical'
                                          ? 'bg-rose-500'
                                          : scoreInfo.tier === 'Important'
                                          ? 'bg-amber-500'
                                          : scoreInfo.tier === 'Refresher'
                                          ? 'bg-sky-500'
                                          : 'bg-slate-400'
                                      }`}
                                    />
                                    <span>{scoreInfo.score}</span>
                                  </span>
                                )}
                                {isNextUp && (
                                  <span className="flex-none rounded-full bg-indigo-600 px-1.5 py-0.5 text-[9px] font-bold uppercase text-white shadow-sm">
                                    {isEn ? 'NEXT' : 'SIGUIENTE'}
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

        <div className="flex flex-1 flex-col rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-900/60 p-4 sm:p-8 shadow-sm">
          <div className="mx-auto w-full max-w-4xl space-y-10">
            {levels.map(([chapterTitle, list], levelIndex) => (
              <div
                key={chapterTitle}
                id={`structured-learning-level-${levelIndex}`}
                className="scroll-mt-6"
              >
                {/* Chapter Header */}
                <div className="mb-6 flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 dark:border-white/10 pb-3">
                  <div className="flex items-center gap-2.5">
                    <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-indigo-50 dark:bg-indigo-950/40 text-xs font-bold text-indigo-600 dark:text-indigo-400 border border-indigo-200 dark:border-indigo-500/30">
                      {levelIndex + 1}
                    </span>
                    <h2 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white tracking-tight">
                      {chapterTitle}
                    </h2>
                  </div>
                  <span className="text-xs font-medium text-slate-500 dark:text-slate-400">
                    {list.length} {list.length === 1 ? 'Engram' : 'Engrams'}
                  </span>
                </div>

                {/* Engram Stepper List */}
                <div className="space-y-4">
                  {list.map((section, sectionIdx) => {
                    const index = sections.indexOf(section);
                    const state = sectionState(section, index);
                    const clickable = state !== 'Locked';
                    const isNextUp = section.StructuredLearningDocumentSectionId === nextUpSectionId;
                    const isMastered = state === 'Mastered';
                    const isLocked = state === 'Locked';
                    const scoreInfo = impactScoresByEngramId.get(section.StructuredLearningDocumentSectionId.toLowerCase());
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
                            className={`flex h-10 w-10 sm:h-11 sm:w-11 shrink-0 items-center justify-center rounded-xl border-2 font-bold text-sm transition-all shadow-sm ${
                              isMastered
                                ? 'border-emerald-500 bg-emerald-50 text-emerald-700 dark:border-emerald-400/80 dark:bg-emerald-500/20 dark:text-emerald-300'
                                : isNextUp
                                ? 'border-indigo-500 bg-indigo-600 text-white shadow-lg shadow-indigo-500/30 ring-4 ring-indigo-500/20'
                                : clickable
                                ? 'border-indigo-300 dark:border-indigo-500/40 bg-indigo-50/50 dark:bg-white/[0.04] text-indigo-600 dark:text-indigo-400'
                                : 'border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] text-slate-400 dark:text-slate-500'
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
                            <div className="my-1.5 h-12 w-0.5 bg-slate-200 dark:bg-white/10 group-hover:bg-indigo-400/40 transition-colors" />
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
                              ? 'border-indigo-300 dark:border-indigo-500/50 bg-gradient-to-br from-indigo-50/80 via-white to-purple-50/50 dark:from-indigo-950/20 dark:via-slate-900 dark:to-purple-950/20 shadow-lg shadow-indigo-500/5 ring-1 ring-indigo-400/30 hover:-translate-y-0.5 hover:shadow-xl cursor-pointer'
                              : isMastered
                              ? 'border-emerald-200 dark:border-emerald-500/30 bg-emerald-50/30 dark:bg-emerald-500/[0.02] hover:-translate-y-0.5 hover:border-emerald-300 dark:hover:border-emerald-400/60 cursor-pointer shadow-xs'
                              : clickable
                              ? 'border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] hover:-translate-y-0.5 hover:border-indigo-300 dark:hover:border-white/20 hover:shadow-md cursor-pointer'
                              : 'border-slate-200/60 dark:border-white/5 bg-slate-50/50 dark:bg-white/[0.01] opacity-60 cursor-not-allowed'
                          }`}
                        >
                          {/* Header row: Index pill + Title + Status/Impact badge */}
                          <div className="flex flex-wrap items-center justify-between gap-2.5">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="inline-flex items-center gap-1 rounded-md bg-slate-100 dark:bg-white/5 border border-slate-200 dark:border-white/10 px-2 py-0.5 text-[11px] font-bold text-slate-700 dark:text-slate-300">
                                Engram {index + 1}
                              </span>
                              {isNextUp && (
                                <span className="inline-flex items-center gap-1 rounded-full bg-indigo-600 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white shadow-xs">
                                  {isEn ? 'START HERE' : 'INICIAR AQUÍ'}
                                </span>
                              )}
                              {scoreInfo && (
                                <button
                                  type="button"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setSelectedEngramForReason(scoreInfo);
                                  }}
                                  className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-bold transition hover:scale-105 shadow-xs ${
                                    scoreInfo.tier === 'Critical'
                                      ? 'bg-rose-100 text-rose-800 dark:bg-rose-950/60 dark:text-rose-300 border border-rose-300 dark:border-rose-500/30'
                                      : scoreInfo.tier === 'Important'
                                      ? 'bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300 border border-amber-300 dark:border-amber-500/30'
                                      : scoreInfo.tier === 'Refresher'
                                      ? 'bg-sky-100 text-sky-800 dark:bg-sky-950/60 dark:text-sky-300 border border-sky-300 dark:border-sky-500/30'
                                      : 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-400 border border-slate-200 dark:border-slate-700'
                                  }`}
                                  title={`${scoreInfo.tier} Impact (${scoreInfo.score}/100) — click for details`}
                                >
                                  {scoreInfo.tier === 'Critical' && <Flame className="h-3 w-3 text-rose-600 dark:text-rose-400" />}
                                  {scoreInfo.tier === 'Important' && <Zap className="h-3 w-3 text-amber-600 dark:text-amber-400" />}
                                  {scoreInfo.tier === 'Refresher' && <RefreshCw className="h-3 w-3 text-sky-600 dark:text-sky-400" />}
                                  <span>{scoreInfo.tier} ({scoreInfo.score})</span>
                                  <Info className="h-3 w-3 opacity-60" />
                                </button>
                              )}
                            </div>

                            <div className="flex items-center gap-2">
                              {isMastered && (
                                <span className="inline-flex items-center gap-1 rounded-full border border-emerald-300 dark:border-emerald-500/30 bg-emerald-100/70 dark:bg-emerald-500/10 px-2.5 py-0.5 text-xs font-semibold text-emerald-800 dark:text-emerald-300">
                                  <Check className="h-3.5 w-3.5" />
                                  {isEn ? 'Mastered' : 'Dominado'}
                                </span>
                              )}
                              {isLocked && (
                                <span className="inline-flex items-center gap-1 rounded-full border border-slate-200 dark:border-white/10 bg-slate-100 dark:bg-white/5 px-2.5 py-0.5 text-xs font-medium text-slate-500 dark:text-slate-400">
                                  <Lock className="h-3 w-3" />
                                  {isEn ? 'Locked' : 'Bloqueado'}
                                </span>
                              )}
                            </div>
                          </div>

                          {/* Title */}
                          <h3 className="mt-2 text-base sm:text-lg font-bold text-slate-900 dark:text-white leading-snug group-hover:text-indigo-600 dark:group-hover:text-indigo-400 transition-colors">
                            {section.SubchapterTitle}
                          </h3>

                          {/* Preview snippet */}
                          {previewSnippet && (
                            <p className="mt-1.5 text-xs sm:text-sm text-slate-600 dark:text-slate-400 line-clamp-2 leading-relaxed">
                              {previewSnippet}...
                            </p>
                          )}

                          {/* Footer with stages & action CTA */}
                          <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 dark:border-white/5 pt-3">
                            <div className="flex flex-wrap items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
                              <span className="inline-flex items-center gap-1 font-semibold text-slate-700 dark:text-slate-300">
                                <Clock className="h-3.5 w-3.5 text-amber-500" />
                                ~{estimatedMin} min
                              </span>
                              <span className="text-slate-300 dark:text-slate-600">·</span>
                              <div className="hidden sm:flex items-center gap-1.5 text-[11px]">
                                <span className="rounded bg-slate-100 dark:bg-white/5 px-1.5 py-0.5 text-slate-700 dark:text-slate-300 font-medium">💡 {isEn ? 'Hypothesis' : 'Hipótesis'}</span>
                                <span>›</span>
                                <span className="rounded bg-slate-100 dark:bg-white/5 px-1.5 py-0.5 text-slate-700 dark:text-slate-300 font-medium">📖 Deep Dive</span>
                                <span>›</span>
                                <span className="rounded bg-slate-100 dark:bg-white/5 px-1.5 py-0.5 text-slate-700 dark:text-slate-300 font-medium">🧠 Recall</span>
                                <span>›</span>
                                <span className="rounded bg-slate-100 dark:bg-white/5 px-1.5 py-0.5 text-slate-700 dark:text-slate-300 font-medium">📝 Quiz</span>
                              </div>
                            </div>

                            {clickable && (
                              <div className="inline-flex items-center gap-1.5 text-xs font-bold text-indigo-600 dark:text-indigo-400 group-hover:text-indigo-700 dark:group-hover:text-indigo-300 transition-colors">
                                <span>
                                  {isMastered ? (isEn ? 'Review' : 'Repasar') : isNextUp ? (isEn ? 'Continue Engram' : 'Continuar Engram') : (isEn ? 'Start' : 'Iniciar')}
                                </span>
                                <ArrowRight className="h-3.5 w-3.5 transition-transform group-hover:translate-x-1" />
                              </div>
                            )}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
