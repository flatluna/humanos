import { useEffect, useMemo, useState } from 'react';
import {
  Search,
  GraduationCap,
  Layers,
  Hash,
  ExternalLink,
  ChevronRight,
  BookOpen,
  Sparkles,
  Quote,
  GitMerge,
  CheckSquare,
  Square,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getEnrolledStructuredLearningDocuments } from '@/features/capabilities/api/enrolledStructuredLearningApi';
import {
  getCrossCourseEntities,
  type CrossCourseEntitiesResponse,
  type CrossCourseEntityOccurrence,
} from './api/crossCourseEntitiesApi';
import { EngramDetailPanel } from '@/features/brain/EngramDetailPanel';
import type { SelectedNeuron } from '@/features/brain/types';

/** "Cruzar Conocimiento" — lets the user pick any subset of their own
 * enrolled courses and see which entities/concepts repeat across them, with
 * verbatim source quotes. Deliberately separate from Program Entities
 * (/program-entities), which is scoped to a curated Program instead of a
 * free course selection. */
export function CrossCourseEntitiesPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const { language } = useI18n();
  const isEn = language === 'en';

  const [enrolledCourses, setEnrolledCourses] = useState<{ id: string; name: string }[]>([]);
  const [coursesLoading, setCoursesLoading] = useState(true);
  const [selectedCourseIds, setSelectedCourseIds] = useState<string[]>([]);

  const [data, setData] = useState<CrossCourseEntitiesResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [openEngram, setOpenEngram] = useState<CrossCourseEntityOccurrence | null>(null);

  function toSelectedNeuron(occ: CrossCourseEntityOccurrence): SelectedNeuron {
    return {
      capabilityGraphNodeId: occ.engramId,
      name: occ.engramTitle,
      state: 'Mastered',
      isStructuredLearning: true,
      structuredLearningDocumentId: occ.courseId,
      structuredLearningSectionId: occ.engramId,
      capabilityId: occ.courseId,
      capabilityName: occ.courseName,
    };
  }

  const [searchTerm, setSearchTerm] = useState('');
  const [selectedType, setSelectedType] = useState<string>('ALL');
  const [selectedCourseCountFacet, setSelectedCourseCountFacet] = useState<number | 'ALL'>('ALL');
  const [selectedEntityId, setSelectedEntityId] = useState<string | null>(null);

  useEffect(() => {
    if (!personId) return;
    let cancelled = false;
    setCoursesLoading(true);
    getEnrolledStructuredLearningDocuments(personId)
      .then((docs) => {
        if (!cancelled) {
          setEnrolledCourses(docs.map((d) => ({ id: d.structuredLearningDocumentId, name: d.documentName })));
        }
      })
      .catch(() => {
        if (!cancelled) setEnrolledCourses([]);
      })
      .finally(() => {
        if (!cancelled) setCoursesLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [personId]);

  function toggleCourse(courseId: string) {
    setSelectedCourseIds((prev) =>
      prev.includes(courseId) ? prev.filter((id) => id !== courseId) : [...prev, courseId],
    );
  }

  function handleCrossCourses() {
    if (!personId || selectedCourseIds.length < 2) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    getCrossCourseEntities(personId, selectedCourseIds)
      .then((res) => {
        if (!cancelled) {
          setData(res);
          setSelectedEntityId(res.entities[0]?.entityId ?? null);
        }
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
  }

  const entityTypes = useMemo(() => {
    if (!data?.entities) return [];
    const set = new Set<string>();
    for (const e of data.entities) {
      if (e.entityType) set.add(e.entityType);
    }
    return Array.from(set).sort();
  }, [data]);

  // Facet: how many of the crossed courses each entity repeats in — e.g.
  // "in all 3 courses", "in 2 of 3", "in only 1". Always shows every count
  // from 1 up to the number of crossed courses (even 0-entity buckets like
  // "En 3 cursos (0)"), not just the counts that happen to have hits.
  const courseCountFacets = useMemo(() => {
    if (!data?.entities) return [];
    const counts = new Map<number, number>();
    for (const e of data.entities) {
      counts.set(e.distinctCoursesCount, (counts.get(e.distinctCoursesCount) ?? 0) + 1);
    }
    const maxCount = Math.max(selectedCourseIds.length, ...counts.keys(), 0);
    const result: { count: number; entityCount: number }[] = [];
    for (let count = maxCount; count >= 1; count--) {
      result.push({ count, entityCount: counts.get(count) ?? 0 });
    }
    return result;
  }, [data, selectedCourseIds.length]);

  const filteredEntities = useMemo(() => {
    if (!data?.entities) return [];
    return data.entities.filter((item) => {
      if (selectedType !== 'ALL' && item.entityType !== selectedType) return false;
      if (selectedCourseCountFacet !== 'ALL' && item.distinctCoursesCount !== selectedCourseCountFacet) return false;
      if (searchTerm.trim()) {
        const q = searchTerm.toLowerCase().trim();
        const matchesName = item.canonicalName.toLowerCase().includes(q);
        const matchesType = item.entityType.toLowerCase().includes(q);
        const matchesAlias = item.aliases?.some((a) => a.toLowerCase().includes(q));
        const matchesOcc = item.occurrences.some(
          (o) => o.engramTitle.toLowerCase().includes(q) || o.sourceText.toLowerCase().includes(q),
        );
        return matchesName || matchesType || matchesAlias || matchesOcc;
      }
      return true;
    });
  }, [data, selectedType, selectedCourseCountFacet, searchTerm]);

  const activeEntity = useMemo(() => {
    if (!data?.entities || !selectedEntityId) return null;
    return data.entities.find((e) => e.entityId === selectedEntityId) ?? null;
  }, [data, selectedEntityId]);

  return (
    <div className="mx-auto max-w-7xl space-y-6 p-4 pb-16 sm:p-8">
      {/* Header Banner */}
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-sky-600 via-indigo-700 to-purple-800 p-6 text-white shadow-xl dark:border-white/10 sm:p-8">
        <div className="flex items-center gap-4">
          <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
            <GitMerge className="h-8 w-8 text-sky-200" />
          </span>
          <div>
            <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-white/80">
              <Sparkles className="h-3.5 w-3.5 text-sky-300" />
              {isEn ? 'Cross-Course Knowledge' : 'Conocimiento Cruzado'}
            </p>
            <h1 className="mt-1 text-2xl sm:text-3xl font-extrabold text-white">
              Knowledge Graph
            </h1>
            <p className="mt-1 max-w-2xl text-sm text-white/85">
              {isEn
                ? 'Pick two or more of your enrolled courses to see which entities and concepts repeat between them, with verbatim source quotes.'
                : 'Elige dos o más de tus cursos inscritos para ver qué entidades y conceptos se repiten entre ellos, con citas textuales de origen.'}
            </p>
          </div>
        </div>
      </div>

      {/* Course Picker */}
      <div className="rounded-3xl border border-slate-200 bg-white dark:border-white/10 dark:bg-slate-900/60 p-5 sm:p-6 space-y-4">
        <h2 className="text-sm font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
          {isEn ? 'Select courses to cross' : 'Selecciona los cursos a cruzar'}
        </h2>

        {coursesLoading ? (
          <div className="h-20 animate-pulse rounded-2xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]" />
        ) : enrolledCourses.length === 0 ? (
          <div className="rounded-2xl border border-slate-200 bg-slate-50 dark:border-white/10 dark:bg-white/[0.02] p-6 text-center text-sm text-slate-500">
            {isEn ? "You aren't enrolled in any courses yet." : 'Aún no estás inscrito en ningún curso.'}
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {enrolledCourses.map((course) => {
              const checked = selectedCourseIds.includes(course.id);
              return (
                <button
                  key={course.id}
                  type="button"
                  onClick={() => toggleCourse(course.id)}
                  className={`flex items-center gap-2.5 rounded-xl border px-3.5 py-2.5 text-left text-sm font-medium transition-all ${
                    checked
                      ? 'border-sky-500 bg-sky-500/10 text-slate-900 dark:text-white'
                      : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300 dark:border-white/10 dark:bg-slate-900/60 dark:text-slate-300 dark:hover:border-white/20'
                  }`}
                >
                  {checked ? (
                    <CheckSquare className="h-4 w-4 shrink-0 text-sky-500" />
                  ) : (
                    <Square className="h-4 w-4 shrink-0 text-slate-400" />
                  )}
                  <span className="truncate">{course.name}</span>
                </button>
              );
            })}
          </div>
        )}

        <div className="flex items-center justify-between gap-3">
          <span className="text-xs text-slate-500 dark:text-slate-400">
            {selectedCourseIds.length < 2
              ? isEn
                ? 'Select at least 2 courses.'
                : 'Selecciona al menos 2 cursos.'
              : isEn
                ? `${selectedCourseIds.length} courses selected.`
                : `${selectedCourseIds.length} cursos seleccionados.`}
          </span>
          <button
            type="button"
            disabled={selectedCourseIds.length < 2 || loading}
            onClick={handleCrossCourses}
            className="inline-flex items-center gap-2 rounded-xl bg-sky-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-sky-500 disabled:cursor-not-allowed disabled:opacity-40"
          >
            <GitMerge className="h-4 w-4" />
            {loading ? (isEn ? 'Synthesizing...' : 'Sintetizando...') : isEn ? 'Synthesize knowledge graph' : 'Sintetizar grafo de conocimiento'}
          </button>
        </div>
      </div>

      {error && (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
          <p>{error}</p>
        </div>
      )}

      {data && (
        <>
          {/* Metrics */}
          <div className="flex flex-wrap items-center gap-3 text-xs font-medium">
            <span className="flex items-center gap-1.5 rounded-lg bg-slate-100 dark:bg-white/10 px-3 py-1.5 text-slate-700 dark:text-slate-200">
              <GraduationCap className="h-4 w-4 text-purple-600 dark:text-purple-300" />
              <strong>{data.totalCourses}</strong> {isEn ? 'Courses' : 'Cursos'}
            </span>
            <span className="flex items-center gap-1.5 rounded-lg bg-slate-100 dark:bg-white/10 px-3 py-1.5 text-slate-700 dark:text-slate-200">
              <Layers className="h-4 w-4 text-emerald-600 dark:text-emerald-300" />
              <strong>{data.totalEngrams}</strong> Engrams
            </span>
            <span className="flex items-center gap-1.5 rounded-lg bg-slate-100 dark:bg-white/10 px-3 py-1.5 text-slate-700 dark:text-slate-200">
              <Hash className="h-4 w-4 text-amber-600 dark:text-amber-300" />
              <strong>{data.totalEntities}</strong> {isEn ? 'Unique Entities' : 'Entidades Únicas'}
            </span>
            <span className="flex items-center gap-1.5 rounded-lg bg-slate-100 dark:bg-white/10 px-3 py-1.5 text-slate-700 dark:text-slate-200">
              <Quote className="h-4 w-4 text-sky-600 dark:text-sky-300" />
              <strong>{data.totalOccurrences}</strong> {isEn ? 'Citations across engrams' : 'Apariciones en engrams'}
            </span>
          </div>

          <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
            {/* Left: Entities List */}
            <div className="flex flex-col lg:col-span-5 xl:col-span-4 space-y-3">
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <input
                  type="text"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  placeholder={isEn ? 'Search entity, type, engram, quote...' : 'Buscar entidad, tipo, engrama o texto...'}
                  className="w-full rounded-2xl border border-slate-200 bg-white dark:border-white/10 dark:bg-slate-900/80 pl-9 pr-8 py-2.5 text-xs sm:text-sm text-slate-900 dark:text-white placeholder-slate-400 focus:border-sky-500/50 focus:outline-none focus:ring-1 focus:ring-sky-500/50 shadow-sm"
                />
              </div>

              <select
                value={selectedType}
                onChange={(e) => setSelectedType(e.target.value)}
                className="w-full truncate rounded-xl border border-slate-200 bg-white dark:border-white/10 dark:bg-slate-900/80 px-2.5 py-1.5 text-xs text-slate-700 dark:text-slate-300 focus:border-sky-500/50 focus:outline-none shadow-xs"
              >
                <option value="ALL">
                  {isEn ? `All types (${data.entities.length})` : `Todos los tipos (${data.entities.length})`}
                </option>
                {entityTypes.map((type) => {
                  const count = data.entities.filter((e) => e.entityType === type).length;
                  return (
                    <option key={type} value={type}>
                      {type} ({count})
                    </option>
                  );
                })}
              </select>

              {/* Recurrence facet: how many of the crossed courses each
                  entity repeats in — the core "cuántos se repiten en los
                  tres/dos/uno" question. */}
              {courseCountFacets.length > 0 && (
                <div className="flex flex-wrap gap-1.5">
                  <button
                    type="button"
                    onClick={() => setSelectedCourseCountFacet('ALL')}
                    className={`rounded-full px-3 py-1 text-[11px] font-semibold transition ${
                      selectedCourseCountFacet === 'ALL'
                        ? 'bg-sky-600 text-white'
                        : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-white/10 dark:text-slate-300 dark:hover:bg-white/15'
                    }`}
                  >
                    {isEn ? `All (${data.entities.length})` : `Todas (${data.entities.length})`}
                  </button>
                  {courseCountFacets.map(({ count, entityCount }) => (
                    <button
                      key={count}
                      type="button"
                      onClick={() => setSelectedCourseCountFacet(count)}
                      className={`rounded-full px-3 py-1 text-[11px] font-semibold transition ${
                        selectedCourseCountFacet === count
                          ? 'bg-sky-600 text-white'
                          : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-white/10 dark:text-slate-300 dark:hover:bg-white/15'
                      }`}
                    >
                      {count === 1
                        ? isEn
                          ? `In 1 course (${entityCount})`
                          : `En 1 curso (${entityCount})`
                        : isEn
                          ? `In ${count} courses (${entityCount})`
                          : `En ${count} cursos (${entityCount})`}
                    </button>
                  ))}
                </div>
              )}

              <div className="max-h-[calc(100vh-420px)] space-y-1.5 overflow-y-auto pr-1">
                {filteredEntities.length === 0 ? (
                  <div className="rounded-2xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-white/[0.02] p-8 text-center text-xs text-slate-500">
                    {isEn ? 'No entities match the chosen filters.' : 'No se encontraron entidades con los filtros seleccionados.'}
                  </div>
                ) : (
                  filteredEntities.map((item) => {
                    const isSelected = selectedEntityId === item.entityId;
                    return (
                      <button
                        key={item.entityId}
                        type="button"
                        onClick={() => setSelectedEntityId(item.entityId)}
                        className={`w-full text-left rounded-2xl border px-3.5 py-3 transition-all flex items-center justify-between gap-3 ${
                          isSelected
                            ? 'border-sky-500 bg-sky-500/10 dark:bg-sky-500/15 text-slate-900 dark:text-white shadow-md'
                            : 'border-slate-200 dark:border-white/5 bg-white dark:bg-slate-900/60 text-slate-700 dark:text-slate-300 hover:border-slate-300 dark:hover:border-white/15'
                        }`}
                      >
                        <div className="min-w-0 flex-1">
                          <div className="flex items-center gap-2">
                            <span className="text-[10px] font-semibold uppercase tracking-wider rounded px-1.5 py-0.5 bg-slate-100 dark:bg-white/10 text-slate-600 dark:text-slate-300">
                              {item.entityType || 'Concept'}
                            </span>
                            {item.distinctCoursesCount > 1 && (
                              <span className="text-[10px] font-medium text-purple-700 dark:text-purple-300 rounded px-1.5 py-0.2 bg-purple-100 dark:bg-purple-500/20">
                                {item.distinctCoursesCount} {isEn ? 'courses' : 'cursos'}
                              </span>
                            )}
                          </div>
                          <div className="mt-1.5 truncate text-xs sm:text-sm font-semibold">{item.canonicalName}</div>
                          {/* Which specific course(s) this entity belongs to
                              — the "1 curso (1180)" facet bucket alone
                              doesn't say which of the crossed courses, so
                              name it explicitly here. */}
                          <div className="mt-0.5 truncate text-[11px] font-medium text-sky-700 dark:text-sky-400">
                            {Array.from(new Set(item.occurrences.map((o) => o.courseName))).join(' · ')}
                          </div>
                          {item.description && (
                            <div className="mt-0.5 truncate text-[11px] text-slate-500 dark:text-slate-400">
                              {item.description}
                            </div>
                          )}
                        </div>
                        <div className="flex shrink-0 items-center gap-1.5">
                          <span
                            className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${
                              isSelected
                                ? 'bg-sky-500 text-slate-950'
                                : 'bg-slate-100 text-slate-700 dark:bg-white/10 dark:text-sky-300'
                            }`}
                          >
                            ({item.totalOccurrences})
                          </span>
                          <ChevronRight className={`h-4 w-4 ${isSelected ? 'text-sky-500' : 'text-slate-400 dark:text-slate-600'}`} />
                        </div>
                      </button>
                    );
                  })
                )}
              </div>
            </div>

            {/* Right: Entity Detail */}
            <div className="lg:col-span-7 xl:col-span-8">
              {activeEntity ? (
                <div className="rounded-3xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-900/70 p-6 sm:p-7 shadow-lg space-y-6">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between border-b border-slate-200 dark:border-white/10 pb-5">
                    <div className="space-y-2">
                      <div className="flex items-center gap-2">
                        <span className="rounded-lg bg-sky-500/15 px-2.5 py-1 text-xs font-semibold uppercase tracking-wider text-sky-700 dark:text-sky-300 border border-sky-500/30">
                          {activeEntity.entityType || 'Entity'}
                        </span>
                      </div>
                      <h2 className="text-xl sm:text-2xl font-bold text-slate-900 dark:text-white">
                        {activeEntity.canonicalName}
                      </h2>
                      {activeEntity.description && (
                        <p className="text-sm text-slate-600 dark:text-slate-300 leading-relaxed">
                          {activeEntity.description}
                        </p>
                      )}
                    </div>
                    <div className="shrink-0 rounded-2xl bg-sky-500/10 dark:bg-sky-500/15 border border-sky-500/20 p-4 text-center sm:text-right min-w-[130px]">
                      <div className="text-3xl sm:text-4xl font-extrabold text-sky-600 dark:text-sky-400">
                        {activeEntity.totalOccurrences}
                      </div>
                      <div className="text-xs text-slate-600 dark:text-slate-400 font-medium">
                        {isEn ? 'total citations' : 'menciones en total'}
                      </div>
                      <div className="mt-1 text-[11px] text-purple-700 dark:text-purple-300 font-semibold">
                        {activeEntity.distinctCoursesCount} {isEn ? 'crossed courses' : 'cursos cruzados'}
                      </div>
                    </div>
                  </div>

                  <div>
                    <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-4">
                      {isEn ? 'Occurrences by Course & Engram' : 'Apariciones por Curso y Engrama'}
                    </h3>
                    <div className="space-y-6">
                      {Array.from(new Set(activeEntity.occurrences.map((o) => o.courseId))).map((courseId) => {
                        const courseOccs = activeEntity.occurrences.filter((o) => o.courseId === courseId);
                        const courseName = courseOccs[0]?.courseName ?? 'Course';
                        return (
                          <div key={courseId} className="rounded-2xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-slate-950/60 p-4 sm:p-5">
                            <div className="flex items-center justify-between border-b border-slate-200 dark:border-white/10 pb-3 mb-3">
                              <div className="flex items-center gap-2">
                                <GraduationCap className="h-4 w-4 text-purple-600 dark:text-purple-400" />
                                <span className="font-bold text-slate-900 dark:text-white text-sm sm:text-base">
                                  {courseName}
                                </span>
                              </div>
                              <span className="text-xs text-sky-700 dark:text-sky-300 font-bold bg-sky-100 dark:bg-sky-500/10 px-2.5 py-0.5 rounded-full border border-sky-300/60 dark:border-sky-500/20">
                                {courseOccs.length} {courseOccs.length === 1 ? (isEn ? 'citation' : 'aparición') : (isEn ? 'citations' : 'apariciones')}
                              </span>
                            </div>
                            <div className="space-y-3">
                              {courseOccs.map((occ, idx) => (
                                <div
                                  key={`${occ.engramId}-${idx}`}
                                  className="rounded-xl border border-slate-200/80 dark:border-white/5 bg-white dark:bg-white/[0.02] p-3.5 transition-colors hover:border-slate-300 dark:hover:border-white/10"
                                >
                                  <div className="flex items-start justify-between gap-3">
                                    <button
                                      type="button"
                                      onClick={() => setOpenEngram(occ)}
                                      className="group flex items-start gap-2.5 text-left text-xs sm:text-sm font-semibold text-slate-800 dark:text-slate-200 hover:text-sky-600 dark:hover:text-white cursor-pointer"
                                    >
                                      <BookOpen className="h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400 mt-0.5 group-hover:scale-110 transition-transform" />
                                      <div>
                                        <span className="group-hover:text-sky-600 dark:group-hover:text-sky-300 transition-colors">
                                          {occ.engramTitle}
                                        </span>
                                        {occ.chapterTitle && (
                                          <div className="text-[11px] font-normal text-slate-500 dark:text-slate-400">
                                            {occ.chapterTitle}
                                          </div>
                                        )}
                                      </div>
                                    </button>
                                    <button
                                      type="button"
                                      onClick={() => setOpenEngram(occ)}
                                      className="shrink-0 inline-flex items-center gap-1 rounded-lg bg-purple-50 dark:bg-purple-500/10 px-2.5 py-1 text-[11px] font-semibold text-purple-700 dark:text-purple-300 hover:bg-purple-100 dark:hover:bg-purple-500/20 transition cursor-pointer"
                                    >
                                      <span>{isEn ? 'View Engram' : 'Ver Engrama'}</span>
                                      <ExternalLink className="h-3 w-3" />
                                    </button>
                                  </div>
                                  {occ.sourceText && (
                                    <div className="mt-2.5 rounded-lg bg-slate-100 dark:bg-black/40 p-3 border-l-2 border-sky-500 text-xs italic text-slate-700 dark:text-slate-300 leading-relaxed">
                                      "{occ.sourceText}"
                                    </div>
                                  )}
                                </div>
                              ))}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                </div>
              ) : (
                <div className="rounded-3xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-slate-900/40 p-12 text-center text-sm text-slate-500">
                  {isEn
                    ? 'Select an entity from the list to view its occurrences and verbatim source quotes.'
                    : 'Selecciona una entidad de la lista para ver sus apariciones y citas textuales de origen.'}
                </div>
              )}
            </div>
          </div>
        </>
      )}

      {openEngram && (
        <EngramDetailPanel
          personId={personId}
          neuron={toSelectedNeuron(openEngram)}
          initialEngramText={openEngram.sourceText}
          onClose={() => setOpenEngram(null)}
        />
      )}
    </div>
  );
}
