import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, Link } from 'react-router-dom';
import {
  ArrowLeft,
  Search,
  GraduationCap,
  Layers,
  Hash,
  ExternalLink,
  ChevronRight,
  BookOpen,
  Sparkles,
  Quote,
} from 'lucide-react';
import {
  getProgramEntities,
  type ProgramEntitiesResponse,
  type ProgramEntityOccurrence,
} from '../lib/api/programsApi';
import LoadingSpinner from '../components/LoadingSpinner';
import {
  getStructuredLearningDocument,
  listStructuredLearningSectionVideos,
  type StructuredLearningSectionVideo,
} from '../lib/api/structuredLearningApi';
import { apiImageUrl } from '../lib/api/httpClient';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import { X } from 'lucide-react';

export default function ProgramEntitiesPage() {
  const { i18n } = useTranslation();
  const isEs = (i18n.language || 'es').startsWith('es');
  const { programId } = useParams<{ programId: string }>();

  const [data, setData] = useState<ProgramEntitiesResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Selected engram modal
  const [openEngram, setOpenEngram] = useState<ProgramEntityOccurrence | null>(null);

  // Search & Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedType, setSelectedType] = useState<string>('ALL');
  const [selectedCourseId, setSelectedCourseId] = useState<string>('ALL');

  // Selected Entity for Right-hand Detail Panel
  const [selectedEntityId, setSelectedEntityId] = useState<string | null>(null);

  useEffect(() => {
    if (!programId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    getProgramEntities(programId)
      .then((res) => {
        if (!cancelled) {
          setData(res);
          if (res.Entities.length > 0) {
            setSelectedEntityId(res.Entities[0].EntityId);
          }
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
  }, [programId]);

  // Distinct entity types
  const entityTypes = useMemo(() => {
    if (!data) return [];
    const set = new Set<string>();
    for (const e of data.Entities) {
      if (e.EntityType) set.add(e.EntityType);
    }
    return Array.from(set).sort();
  }, [data]);

  // Filtered entities
  const filteredEntities = useMemo(() => {
    if (!data) return [];
    return data.Entities.filter((item) => {
      // Type filter
      if (selectedType !== 'ALL' && item.EntityType !== selectedType) {
        return false;
      }

      // Course filter
      if (
        selectedCourseId !== 'ALL' &&
        !item.Occurrences.some((o) => o.CourseId === selectedCourseId)
      ) {
        return false;
      }

      // Search term
      if (searchTerm.trim()) {
        const q = searchTerm.toLowerCase().trim();
        const matchesName = item.CanonicalName.toLowerCase().includes(q);
        const matchesType = item.EntityType.toLowerCase().includes(q);
        const matchesAlias = item.Aliases?.some((a) => a.toLowerCase().includes(q));
        const matchesOcc = item.Occurrences.some(
          (o) =>
            o.EngramTitle.toLowerCase().includes(q) ||
            o.SourceText.toLowerCase().includes(q),
        );
        return matchesName || matchesType || matchesAlias || matchesOcc;
      }

      return true;
    });
  }, [data, selectedType, selectedCourseId, searchTerm]);

  // Active selected entity object
  const activeEntity = useMemo(() => {
    if (!data || !selectedEntityId) return null;
    return data.Entities.find((e) => e.EntityId === selectedEntityId) ?? null;
  }, [data, selectedEntityId]);

  if (loading) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <LoadingSpinner />
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="mx-auto max-w-4xl px-4 py-12">
        <div className="rounded-2xl border border-red-500/20 bg-red-500/10 p-6 text-center text-red-300">
          <p className="font-semibold">{isEs ? 'Error al cargar entidades' : 'Error loading entities'}</p>
          <p className="mt-2 text-sm text-red-400">{error ?? 'No data'}</p>
          <Link
            to={programId ? `/programs/${programId}` : '/programs'}
            className="mt-4 inline-flex items-center gap-2 rounded-lg bg-white/10 px-4 py-2 text-sm text-white hover:bg-white/20"
          >
            <ArrowLeft className="h-4 w-4" />
            {isEs ? 'Volver al programa' : 'Back to program'}
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="px-4 py-6 sm:px-6 lg:px-8">
      {/* Top Header */}
      <div className="mb-6 flex flex-col gap-4 border-b border-white/10 pb-6 lg:flex-row lg:items-center lg:justify-between">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-amber-400">
            <Link
              to={`/programs/${programId}`}
              className="flex items-center gap-1.5 text-slate-400 hover:text-white transition-colors"
            >
              <ArrowLeft className="h-4 w-4" />
              <span>{isEs ? 'Programa' : 'Program'}</span>
            </Link>
            <span className="text-slate-600">/</span>
            <span className="flex items-center gap-1.5">
              <Sparkles className="h-3.5 w-3.5" />
              {isEs ? 'Entidades del Programa' : 'Program Entities'}
            </span>
          </div>

          <h1 className="mt-2 truncate text-2xl font-bold tracking-tight text-white sm:text-3xl">
            {data.ProgramName}
          </h1>

          {data.Description && (
            <p className="mt-1 line-clamp-2 text-xs sm:text-sm text-slate-400">
              {data.Description}
            </p>
          )}

          {/* Metrics summary */}
          <div className="mt-3 flex flex-wrap items-center gap-3 text-xs text-slate-400">
            <span className="flex items-center gap-1 rounded-md bg-white/5 px-2.5 py-1">
              <GraduationCap className="h-3.5 w-3.5 text-violet-400" />
              <strong className="text-white">{data.TotalCourses}</strong> {isEs ? 'Cursos' : 'Courses'}
            </span>
            <span className="flex items-center gap-1 rounded-md bg-white/5 px-2.5 py-1">
              <Layers className="h-3.5 w-3.5 text-emerald-400" />
              <strong className="text-white">{data.TotalEngrams}</strong> Engrams
            </span>
            <span className="flex items-center gap-1 rounded-md bg-white/5 px-2.5 py-1">
              <Hash className="h-3.5 w-3.5 text-amber-400" />
              <strong className="text-white">{data.TotalEntities}</strong> {isEs ? 'Entidades únicas' : 'Unique Entities'}
            </span>
            <span className="flex items-center gap-1 rounded-md bg-white/5 px-2.5 py-1">
              <Quote className="h-3.5 w-3.5 text-sky-400" />
              <strong className="text-white">{data.TotalOccurrences}</strong> {isEs ? 'Menciones totales' : 'Total occurrences'}
            </span>
          </div>
        </div>

        {/* Quick action: View Program Detail */}
        <div className="flex shrink-0 items-center gap-3">
          <Link
            to={`/programs/${programId}`}
            className="flex items-center gap-2 rounded-xl border border-white/10 bg-white/5 px-3.5 py-2 text-xs font-medium text-slate-300 transition-colors hover:border-white/20 hover:bg-white/10 hover:text-white"
          >
            <GraduationCap className="h-4 w-4 text-violet-400" />
            {isEs ? 'Ver programa' : 'View Program'}
          </Link>
        </div>
      </div>

      {/* Main Two-Column Layout */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
        {/* Left Column: Entities List with Search & Filters (5 cols) */}
        <div className="flex flex-col lg:col-span-5 xl:col-span-4 space-y-3">
          {/* Search box */}
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder={isEs ? 'Buscar entidad, tipo, engrama o texto...' : 'Search entity, type, engram, quote...'}
              className="w-full rounded-xl border border-white/10 bg-slate-900/80 pl-9 pr-4 py-2 text-xs sm:text-sm text-white placeholder-slate-500 focus:border-amber-500/50 focus:outline-none focus:ring-1 focus:ring-amber-500/50"
            />
            {searchTerm && (
              <button
                type="button"
                onClick={() => setSearchTerm('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-slate-500 hover:text-white"
              >
                ✕
              </button>
            )}
          </div>

          {/* Filters Row */}
          <div className="flex gap-2">
            {/* Entity Type Filter */}
            <div className="flex-1 min-w-0">
              <select
                value={selectedType}
                onChange={(e) => setSelectedType(e.target.value)}
                className="w-full truncate rounded-xl border border-white/10 bg-slate-900/80 px-2.5 py-1.5 text-xs text-slate-300 focus:border-amber-500/50 focus:outline-none"
              >
                <option value="ALL">
                  {isEs ? `Todos los tipos (${data.Entities.length})` : `All types (${data.Entities.length})`}
                </option>
                {entityTypes.map((type) => {
                  const count = data.Entities.filter((e) => e.EntityType === type).length;
                  return (
                    <option key={type} value={type}>
                      {type} ({count})
                    </option>
                  );
                })}
              </select>
            </div>

            {/* Course Filter */}
            {data.Courses.length > 1 && (
              <div className="flex-1 min-w-0">
                <select
                  value={selectedCourseId}
                  onChange={(e) => setSelectedCourseId(e.target.value)}
                  className="w-full truncate rounded-xl border border-white/10 bg-slate-900/80 px-2.5 py-1.5 text-xs text-slate-300 focus:border-amber-500/50 focus:outline-none"
                >
                  <option value="ALL">
                    {isEs ? `Todos los cursos (${data.Courses.length})` : `All courses (${data.Courses.length})`}
                  </option>
                  {data.Courses.map((c) => (
                    <option key={c.CourseId} value={c.CourseId}>
                      {c.CourseName}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          {/* Counter info */}
          <div className="flex items-center justify-between px-1 text-[11px] text-slate-400">
            <span>
              {isEs ? 'Mostrando' : 'Showing'} <strong className="text-white">{filteredEntities.length}</strong> {isEs ? 'entidades' : 'entities'}
            </span>
            {(selectedType !== 'ALL' || selectedCourseId !== 'ALL' || searchTerm) && (
              <button
                type="button"
                onClick={() => {
                  setSelectedType('ALL');
                  setSelectedCourseId('ALL');
                  setSearchTerm('');
                }}
                className="text-amber-400 hover:underline"
              >
                {isEs ? 'Limpiar filtros' : 'Clear filters'}
              </button>
            )}
          </div>

          {/* Scrollable Entities List */}
          <div className="max-h-[calc(100vh-290px)] space-y-1.5 overflow-y-auto pr-1">
            {filteredEntities.length === 0 ? (
              <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-8 text-center text-xs text-slate-500">
                {isEs ? 'No se encontraron entidades con los filtros seleccionados.' : 'No entities match the chosen filters.'}
              </div>
            ) : (
              filteredEntities.map((item) => {
                const isSelected = selectedEntityId === item.EntityId;
                return (
                  <button
                    key={item.EntityId}
                    type="button"
                    onClick={() => setSelectedEntityId(item.EntityId)}
                    className={`w-full text-left rounded-xl border px-3 py-2.5 transition-all flex items-center justify-between gap-3 ${
                      isSelected
                        ? 'border-amber-500/50 bg-amber-500/15 text-white shadow-lg shadow-amber-500/5'
                        : 'border-white/5 bg-slate-900/60 text-slate-300 hover:border-white/15 hover:bg-slate-900/90 hover:text-white'
                    }`}
                  >
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <span className="text-[10px] font-semibold uppercase tracking-wider rounded px-1.5 py-0.5 bg-white/10 text-slate-300">
                          {item.EntityType || 'Concept'}
                        </span>
                        {item.DistinctCoursesCount > 1 && (
                          <span className="text-[10px] font-medium text-violet-300 rounded px-1 py-0.2 bg-violet-500/20" title={`${item.DistinctCoursesCount} cursos distintos`}>
                            {item.DistinctCoursesCount} {isEs ? 'cursos' : 'courses'}
                          </span>
                        )}
                      </div>
                      <div className="mt-1 truncate text-xs sm:text-sm font-medium">
                        {item.CanonicalName}
                      </div>
                      {item.Description && (
                        <div className="mt-0.5 truncate text-[11px] text-slate-400">
                          {item.Description}
                        </div>
                      )}
                    </div>

                    {/* Occurrences Pill */}
                    <div className="flex shrink-0 items-center gap-1.5">
                      <span
                        className={`rounded-full px-2 py-0.5 text-xs font-bold ${
                          isSelected
                            ? 'bg-amber-400 text-slate-950'
                            : 'bg-white/10 text-amber-300'
                        }`}
                        title={`${item.TotalOccurrences} ${isEs ? 'apariciones en engrams' : 'occurrences across engrams'}`}
                      >
                        ({item.TotalOccurrences})
                      </span>
                      <ChevronRight className={`h-4 w-4 ${isSelected ? 'text-amber-400' : 'text-slate-600'}`} />
                    </div>
                  </button>
                );
              })
            )}
          </div>
        </div>

        {/* Right Column: Entity Occurrences & Detail Breakdown (7 cols) */}
        <div className="lg:col-span-7 xl:col-span-8">
          {activeEntity ? (
            <div className="rounded-2xl border border-white/10 bg-slate-900/70 p-5 sm:p-6 backdrop-blur-xl">
              {/* Header */}
              <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between border-b border-white/10 pb-4">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="rounded-lg bg-amber-500/20 px-2.5 py-1 text-xs font-semibold uppercase tracking-wider text-amber-300 border border-amber-500/30">
                      {activeEntity.EntityType || 'Entity'}
                    </span>
                    {activeEntity.OntologicalClass && (
                      <span className="rounded-lg bg-emerald-500/15 px-2 py-1 text-xs font-medium text-emerald-300">
                        {isEs ? 'Clase:' : 'Class:'} {activeEntity.OntologicalClass}
                      </span>
                    )}
                  </div>
                  <h2 className="mt-2 text-xl font-bold text-white sm:text-2xl">
                    {activeEntity.CanonicalName}
                  </h2>
                  {activeEntity.Description && (
                    <p className="mt-1 text-sm text-slate-300">{activeEntity.Description}</p>
                  )}
                  {activeEntity.Aliases && activeEntity.Aliases.length > 0 && (
                    <div className="mt-2 flex flex-wrap items-center gap-1.5 text-xs text-slate-400">
                      <span className="font-semibold text-slate-500">{isEs ? 'Alias / Formas:' : 'Aliases:'}</span>
                      {activeEntity.Aliases.map((alias, idx) => (
                        <span key={idx} className="rounded bg-white/5 px-2 py-0.5 text-slate-300">
                          {alias}
                        </span>
                      ))}
                    </div>
                  )}
                </div>

                {/* Big count badge */}
                <div className="shrink-0 text-left sm:text-right">
                  <div className="text-3xl font-extrabold text-amber-400 sm:text-4xl">
                    {activeEntity.TotalOccurrences}
                  </div>
                  <div className="text-xs text-slate-400">
                    {isEs ? 'menciones en total' : 'total occurrences'}
                  </div>
                  <div className="mt-1 text-[11px] text-violet-400 font-medium">
                    {activeEntity.DistinctCoursesCount} {isEs ? 'cursos del programa' : 'program courses'}
                  </div>
                </div>
              </div>

              {/* Occurrences Grouped by Course */}
              <div className="mt-6">
                <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-400 mb-4">
                  {isEs ? 'Apariciones por Curso y Engram' : 'Occurrences by Course & Engram'}
                </h3>

                <div className="space-y-6">
                  {/* Group occurrences by CourseId */}
                  {Array.from(
                    new Set(activeEntity.Occurrences.map((o) => o.CourseId)),
                  ).map((courseId) => {
                    const courseOccs = activeEntity.Occurrences.filter((o) => o.CourseId === courseId);
                    const courseName = courseOccs[0]?.CourseName ?? 'Course';

                    return (
                      <div key={courseId} className="rounded-xl border border-white/10 bg-slate-950/60 p-4">
                        <div className="flex items-center justify-between border-b border-white/10 pb-2 mb-3">
                          <div className="flex items-center gap-2">
                            <GraduationCap className="h-4 w-4 text-violet-400" />
                            <Link
                              to={`/structured-learning/${courseId}/course?mode=edit`}
                              className="font-semibold text-white hover:text-amber-300 transition-colors flex items-center gap-1.5"
                            >
                              <span>{courseName}</span>
                              <ExternalLink className="h-3 w-3 text-slate-500" />
                            </Link>
                          </div>
                          <span className="text-xs text-amber-300 font-semibold bg-amber-500/10 px-2 py-0.5 rounded-full border border-amber-500/20">
                            {courseOccs.length} {courseOccs.length === 1 ? (isEs ? 'aparición' : 'occurrence') : (isEs ? 'apariciones' : 'occurrences')}
                          </span>
                        </div>

                        {/* List of engrams */}
                        <div className="space-y-2.5">
                          {courseOccs.map((occ, idx) => (
                            <div
                              key={`${occ.EngramId}-${idx}`}
                              className="rounded-lg border border-white/5 bg-white/[0.02] p-3 transition-colors hover:border-white/10 hover:bg-white/[0.04]"
                            >
                              <div className="flex items-start justify-between gap-2">
                                <button
                                  type="button"
                                  onClick={() => setOpenEngram(occ)}
                                  className="group flex items-start gap-2 text-left text-xs sm:text-sm font-medium text-slate-200 hover:text-white cursor-pointer"
                                >
                                  <BookOpen className="h-4 w-4 shrink-0 text-emerald-400 mt-0.5 group-hover:scale-110 transition-transform" />
                                  <div>
                                    <span className="group-hover:text-amber-300 transition-colors">
                                      {occ.EngramTitle}
                                    </span>
                                    {occ.ChapterTitle && (
                                      <div className="text-[11px] text-slate-500">{occ.ChapterTitle}</div>
                                    )}
                                  </div>
                                </button>

                                <button
                                  type="button"
                                  onClick={() => setOpenEngram(occ)}
                                  className="shrink-0 text-[11px] text-slate-500 hover:text-amber-300 flex items-center gap-1 cursor-pointer"
                                >
                                  <span>{isEs ? 'Ver engrama' : 'View engram'}</span>
                                  <ExternalLink className="h-3 w-3" />
                                </button>
                              </div>

                              {/* Verbatim quote evidence if present */}
                              {occ.SourceText && (
                                <div className="mt-2 rounded bg-black/40 p-2.5 border-l-2 border-amber-400/70 text-xs italic text-slate-300">
                                  "{occ.SourceText}"
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
            <div className="rounded-2xl border border-white/10 bg-slate-900/40 p-12 text-center text-sm text-slate-500">
              {isEs ? 'Selecciona una entidad de la lista para ver su detalle y apariciones.' : 'Select an entity from the list to view its occurrences and source quotes.'}
            </div>
          )}
        </div>
      </div>

      {openEngram && (
        <StudioEngramModal engram={openEngram} onClose={() => setOpenEngram(null)} />
      )}
    </div>
  );
}

function StudioEngramModal({ engram, onClose }: { engram: ProgramEntityOccurrence; onClose: () => void }) {
  const [teachingStepId, setTeachingStepId] = useState<string | null>(null);
  const [illustrationId, setIllustrationId] = useState<string | null>(null);
  const [sectionText, setSectionText] = useState<string>('');
  const [videos, setVideos] = useState<StructuredLearningSectionVideo[]>([]);

  useEffect(() => {
    let cancelled = false;
    getStructuredLearningDocument(engram.CourseId)
      .then((doc) => {
        if (cancelled) return;
        const section = doc.Sections.find((s) => s.StructuredLearningDocumentSectionId === engram.EngramId);
        setSectionText(section?.Text || engram.SourceText || '');
        const teachingStep = section?.Steps.find((s) => s.StepType === 'Teaching') ?? null;
        setTeachingStepId(teachingStep?.StructuredLearningSectionStepId ?? null);
        try {
          const parsed = teachingStep?.StepDataJson ? (JSON.parse(teachingStep.StepDataJson) as { IllustrationId?: string | null }) : null;
          setIllustrationId(parsed?.IllustrationId ?? null);
        } catch {
          setIllustrationId(null);
        }
      })
      .catch(() => !cancelled && setTeachingStepId(null));
    listStructuredLearningSectionVideos(engram.EngramId)
      .then((list) => !cancelled && setVideos(list))
      .catch(() => !cancelled && setVideos([]));
    return () => {
      cancelled = true;
    };
  }, [engram.CourseId, engram.EngramId, engram.SourceText]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/75 p-4 backdrop-blur-md" onClick={onClose}>
      <div
        className="max-h-[85vh] w-full max-w-xl overflow-y-auto rounded-3xl border border-white/10 bg-slate-900 p-6 sm:p-7 shadow-2xl shadow-black/60"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-3 border-b border-white/10 pb-4">
          <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-amber-400">
            <BookOpen className="h-4 w-4" />
            <span className="truncate max-w-sm">{engram.CourseName}</span>
          </div>
          <button type="button" onClick={onClose} className="rounded-full p-1.5 text-slate-400 hover:bg-white/10 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>

        {videos.length > 0 ? (
          <div className="mt-4 space-y-3">
            {videos.map((video) => (
              <video
                key={video.IllustrationId}
                src={apiImageUrl(`/illustrations/structured-learning/${video.IllustrationId}/image`)}
                controls
                className="w-full rounded-2xl border border-white/10 bg-black/40 shadow-md max-h-72 object-contain"
              />
            ))}
          </div>
        ) : illustrationId ? (
          <div className="mt-4">
            <img
              src={apiImageUrl(`/illustrations/structured-learning/${illustrationId}/image`)}
              alt={engram.EngramTitle}
              className="w-full rounded-2xl border border-white/10 bg-black/40 shadow-md max-h-72 object-contain"
            />
          </div>
        ) : null}

        <h2 className="mt-4 text-xl font-bold text-white">{engram.EngramTitle}</h2>
        {engram.ChapterTitle && (
          <p className="mt-0.5 text-xs text-slate-400">{engram.ChapterTitle}</p>
        )}

        <div className="mt-4 rounded-2xl bg-black/30 p-4 border border-white/5 text-sm leading-relaxed text-slate-200 whitespace-pre-wrap max-h-80 overflow-y-auto">
          {sectionText || engram.SourceText}
        </div>

        {teachingStepId && (
          <div className="mt-5 border-t border-white/10 pt-4">
            <VoiceTutorAgent structuredLearningSectionStepId={teachingStepId} />
          </div>
        )}
      </div>
    </div>
  );
}
