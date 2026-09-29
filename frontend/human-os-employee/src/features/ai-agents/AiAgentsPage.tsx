import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import { Bot, BookOpen, Layers, MessageCircle, Search, Sparkles, Trophy } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { API_BASE_URL } from '@/features/capabilities/api/httpClient';
import { listAiAgentCourses, type AiAgentCourseCard } from './api/aiAgentsApi';

/** "AI Agents" (2026-09-04) — one card per enrolled course, each opening
 * that course's own chat agent (grounded ONLY in this course's engrams via
 * the existing StructuredLearningChatService retrieval, never the whole
 * catalog). Deliberately its own visual language (brand/accent gradient,
 * not human-os-web's cyan or capabilitystudio's monochrome) since this is
 * a separate app. */
export function AiAgentsPage() {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const tenantId = user?.tenantId ?? '';

  const [courses, setCourses] = useState<AiAgentCourseCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [startedOnly, setStartedOnly] = useState(false);

  useEffect(() => {
    if (!personId) return;
    setLoading(true);
    listAiAgentCourses(personId, tenantId)
      .then(setCourses)
      .catch((err) => setError(err instanceof Error ? err.message : 'Could not load your agents.'))
      .finally(() => setLoading(false));
  }, [personId, tenantId]);

  const totalEngramsCompleted = useMemo(
    () => courses.reduce((sum, course) => sum + course.masteredEngrams, 0),
    [courses],
  );

  const filteredCourses = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    return courses.filter((course) => {
      if (query && !course.documentName.toLowerCase().includes(query)) return false;
      if (startedOnly && course.masteredEngrams <= 0) return false;
      return true;
    });
  }, [courses, searchQuery, startedOnly]);

  return (
    <div className="mx-auto max-w-6xl space-y-6 p-4 pb-16 sm:p-8">
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-brand-600 via-indigo-600 to-accent-600 p-6 text-white shadow-lg shadow-brand-600/20 dark:border-white/10 sm:p-8">
        <div className="flex items-center gap-4">
          <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
            <Bot className="h-8 w-8 text-white" />
          </span>
          <div>
            <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-white/80">
              <Sparkles className="h-3.5 w-3.5" />
              {isEn ? 'Agent network' : 'Red de agentes'}
            </p>
            <h1 className="mt-1 text-2xl font-bold text-white">AI Agents</h1>
            <p className="mt-1 max-w-xl text-sm text-white/80">
              {isEn
                ? 'A specialized agent for each of your courses — grounded only in that course, aware of your own objectives, skills, and progress.'
                : 'Un agente especializado para cada uno de tus cursos — basado solo en ese curso, consciente de tus propios objetivos, habilidades y progreso.'}
            </p>
          </div>
        </div>
      </div>

      {!loading && !error && courses.length > 0 && (
        <div className="flex items-center gap-4 rounded-3xl border border-slate-200 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-white/[0.03]">
          <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-brand-500 to-accent-500 text-white">
            <Trophy className="h-7 w-7" />
          </span>
          <div>
            <p className="text-4xl font-bold text-slate-900 dark:text-white">{totalEngramsCompleted}</p>
            <p className="text-sm font-medium text-slate-500 dark:text-white/60">
              {isEn ? 'Total engrams completed' : 'Total de engramas completados'}
            </p>
          </div>
        </div>
      )}

      {!loading && !error && courses.length > 0 && (
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="relative max-w-md flex-1">
            <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              type="search"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={isEn ? 'Search by course name...' : 'Buscar por nombre de curso...'}
              className="w-full rounded-2xl border border-slate-200 bg-white py-2.5 pl-10 pr-4 text-sm text-slate-900 outline-none transition focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
            />
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            <button
              type="button"
              onClick={() => setStartedOnly((prev) => !prev)}
              className={`rounded-full px-3 py-1.5 text-xs font-semibold transition ${
                startedOnly
                  ? 'bg-brand-600 text-white shadow-sm'
                  : 'border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70'
              }`}
            >
              {isEn ? 'Started classes' : 'Clases iniciadas'}
            </button>
          </div>
        </div>
      )}

      {loading && (
        <LoadingBackdrop
          title={isEn ? 'Loading your agents...' : 'Cargando tus agentes...'}
          description={isEn ? 'Getting your enrolled courses ready.' : 'Preparando tus cursos inscritos.'}
        />
      )}
      {error && <div className="rounded-2xl border border-red-200 bg-red-50 p-5 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">{error}</div>}

      {!loading && !error && courses.length === 0 && (
        <Card className="flex flex-col items-center justify-center p-12 text-center border-dashed">
          <BookOpen className="h-10 w-10 text-slate-300 dark:text-white/20" />
          <h3 className="mt-3 text-base font-semibold text-slate-900 dark:text-white">
            {isEn ? "You haven't enrolled in any courses yet" : 'Aún no te has inscrito a ningún curso'}
          </h3>
          <Link
            to="/available-courses"
            className="mt-4 inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2 text-sm font-semibold text-white transition hover:opacity-90"
          >
            {isEn ? 'Browse available courses' : 'Ver cursos disponibles'}
          </Link>
        </Card>
      )}

      {!loading && !error && courses.length > 0 && filteredCourses.length === 0 && (
        <Card className="flex flex-col items-center justify-center p-12 text-center border-dashed">
          <Search className="h-10 w-10 text-slate-300 dark:text-white/20" />
          <h3 className="mt-3 text-base font-semibold text-slate-900 dark:text-white">
            {isEn ? 'No agents match those filters' : 'Ningún agente coincide con esos filtros'}
          </h3>
        </Card>
      )}

      {!loading && !error && filteredCourses.length > 0 && (
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {filteredCourses.map((course) => (
            <div
              key={course.documentId}
              className="group flex flex-col justify-between overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-xs transition-all hover:-translate-y-1 hover:border-slate-300 hover:shadow-xl dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-white/20"
            >
              <div className="relative h-24 w-full overflow-hidden bg-gradient-to-br from-brand-500 via-indigo-600 to-accent-600">
                {course.coverIllustrationId ? (
                  <img
                    src={`${API_BASE_URL}/illustrations/structured-learning/${course.coverIllustrationId}/image`}
                    alt=""
                    className="h-full w-full object-cover"
                  />
                ) : (
                  <div className="flex h-full w-full items-center justify-center">
                    <Bot className="h-9 w-9 text-white/70" strokeWidth={1.5} />
                  </div>
                )}
              </div>
              <div className="flex flex-1 flex-col p-5">
                <h2 className="text-lg font-bold text-slate-900 dark:text-white">{course.documentName}</h2>
                <div className="mt-2 flex items-center gap-1.5 text-xs text-slate-500 dark:text-white/50">
                  <Layers className="h-3.5 w-3.5" />
                  <span>
                    <strong className="font-semibold text-slate-700 dark:text-white/80">{course.masteredEngrams}</strong>
                    {' / '}
                    {course.totalEngrams} {isEn ? 'engrams mastered' : 'engramas dominados'}
                  </span>
                </div>
                <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-brand-500 to-accent-500"
                    style={{ width: `${course.totalEngrams === 0 ? 0 : Math.round((course.masteredEngrams / course.totalEngrams) * 100)}%` }}
                  />
                </div>
                <Link
                  to={`/ai-agents/${course.documentId}`}
                  className="mt-4 inline-flex items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2.5 text-sm font-semibold text-white transition hover:opacity-90"
                >
                  <MessageCircle className="h-4 w-4" /> {isEn ? 'Talk with the Agent' : 'Habla con el Agente'}
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
