import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router';
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
  Network,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import {
  getProgramEntities,
  type ProgramEntitiesResponse,
  type ProgramEntityOccurrence,
} from '@/features/programs/api/programsApi';
import { EngramDetailPanel } from '@/features/brain/EngramDetailPanel';
import type { SelectedNeuron } from '@/features/brain/types';

export function ProgramEntitiesDetailPage() {
  const { programId } = useParams<{ programId: string }>();
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const { language } = useI18n();
  const isEn = language === 'en';

  const [data, setData] = useState<ProgramEntitiesResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Selected Engram Modal (same as Brain page)
  const [openEngram, setOpenEngram] = useState<ProgramEntityOccurrence | null>(null);

  function toSelectedNeuron(occ: ProgramEntityOccurrence): SelectedNeuron {
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
          if (res.entities && res.entities.length > 0) {
            setSelectedEntityId(res.entities[0].entityId);
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
    if (!data?.entities) return [];
    const set = new Set<string>();
    for (const e of data.entities) {
      if (e.entityType) set.add(e.entityType);
    }
    return Array.from(set).sort();
  }, [data]);

  // Filtered entities
  const filteredEntities = useMemo(() => {
    if (!data?.entities) return [];
    return data.entities.filter((item) => {
      // Type filter
      if (selectedType !== 'ALL' && item.entityType !== selectedType) {
        return false;
      }

      // Course filter
      if (
        selectedCourseId !== 'ALL' &&
        !item.occurrences.some((o) => o.courseId === selectedCourseId)
      ) {
        return false;
      }

      // Search term
      if (searchTerm.trim()) {
        const q = searchTerm.toLowerCase().trim();
        const matchesName = item.canonicalName.toLowerCase().includes(q);
        const matchesType = item.entityType.toLowerCase().includes(q);
        const matchesAlias = item.aliases?.some((a) => a.toLowerCase().includes(q));
        const matchesOcc = item.occurrences.some(
          (o) =>
            o.engramTitle.toLowerCase().includes(q) ||
            o.sourceText.toLowerCase().includes(q),
        );
        return matchesName || matchesType || matchesAlias || matchesOcc;
      }

      return true;
    });
  }, [data, selectedType, selectedCourseId, searchTerm]);

  // Active selected entity object
  const activeEntity = useMemo(() => {
    if (!data?.entities || !selectedEntityId) return null;
    return data.entities.find((e) => e.entityId === selectedEntityId) ?? null;
  }, [data, selectedEntityId]);

  if (loading) {
    return (
      <div className="mx-auto max-w-7xl p-4 sm:p-8 space-y-6">
        <div className="h-40 animate-pulse rounded-3xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]" />
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
          <div className="h-96 animate-pulse rounded-2xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04] lg:col-span-5" />
          <div className="h-96 animate-pulse rounded-2xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04] lg:col-span-7" />
        </div>
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="mx-auto max-w-4xl p-6 sm:p-12">
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
          <p className="font-semibold">{isEn ? 'Error loading entities' : 'Error al cargar entidades'}</p>
          <p className="mt-2 text-sm">{error ?? 'No data'}</p>
          <Link
            to={programId ? `/programs/${programId}` : '/programs'}
            className="mt-4 inline-flex items-center gap-2 rounded-xl bg-slate-900 px-4 py-2 text-sm text-white hover:bg-slate-800 dark:bg-white/10 dark:hover:bg-white/20"
          >
            <ArrowLeft className="h-4 w-4" />
            {isEn ? 'Back to program' : 'Volver al programa'}
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-7xl p-4 sm:p-8 space-y-6">
      {/* Top Breadcrumb & Actions */}
      <div className="flex items-center justify-between">
        <Link
          to={`/programs/${programId}`}
          className="inline-flex items-center gap-2 text-sm font-medium text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-white transition"
        >
          <ArrowLeft className="h-4 w-4" />
          {isEn ? 'Back to Program' : 'Volver al Programa'}
        </Link>

        <Link
          to={`/program-agents/${programId}`}
          className="inline-flex items-center gap-1.5 rounded-xl border border-purple-200 bg-purple-50 px-3 py-1.5 text-xs font-semibold text-purple-700 hover:bg-purple-100 dark:border-purple-500/30 dark:bg-purple-500/15 dark:text-purple-300 dark:hover:bg-purple-500/25 transition"
        >
          <Sparkles className="h-3.5 w-3.5" />
          <span>{isEn ? 'Program AI Agent' : 'Agente del Programa'}</span>
        </Link>
      </div>

      {/* Program Header Banner */}
      <div className="relative overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-amber-600 via-purple-700 to-indigo-800 p-6 sm:p-8 text-white shadow-xl dark:border-white/10">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(255,255,255,0.18),transparent_70%)]" />

        <div className="relative z-10 flex flex-col md:flex-row md:items-center md:justify-between gap-6">
          <div className="space-y-3 max-w-3xl">
            <div className="flex items-center gap-2 text-white/80">
              <Network className="h-5 w-5 text-amber-300" />
              <span className="text-xs font-semibold uppercase tracking-wider">
                {isEn ? 'Program Entities & Knowledge Map' : 'Entidades y Mapa de Conocimiento del Programa'}
              </span>
              {data.programCode && (
                <span className="text-xs font-mono bg-white/20 px-2 py-0.5 rounded-full">{data.programCode}</span>
              )}
            </div>

            <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight">{data.programName}</h1>

            {data.description && (
              <p className="text-sm leading-relaxed text-white/90 line-clamp-2">{data.description}</p>
            )}

            {/* Metrics */}
            <div className="flex flex-wrap items-center gap-3 pt-1 text-xs font-medium text-white/90">
              <span className="flex items-center gap-1.5 rounded-lg bg-black/20 px-3 py-1 backdrop-blur-sm">
                <GraduationCap className="h-4 w-4 text-purple-200" />
                <strong className="text-white">{data.totalCourses}</strong> {isEn ? 'Courses' : 'Cursos'}
              </span>
              <span className="flex items-center gap-1.5 rounded-lg bg-black/20 px-3 py-1 backdrop-blur-sm">
                <Layers className="h-4 w-4 text-emerald-300" />
                <strong className="text-white">{data.totalEngrams}</strong> Engrams
              </span>
              <span className="flex items-center gap-1.5 rounded-lg bg-black/20 px-3 py-1 backdrop-blur-sm">
                <Hash className="h-4 w-4 text-amber-300" />
                <strong className="text-white">{data.totalEntities}</strong> {isEn ? 'Unique Entities' : 'Entidades Únicas'}
              </span>
              <span className="flex items-center gap-1.5 rounded-lg bg-black/20 px-3 py-1 backdrop-blur-sm">
                <Quote className="h-4 w-4 text-sky-300" />
                <strong className="text-white">{data.totalOccurrences}</strong> {isEn ? 'Citations across engrams' : 'Apariciones en engrams'}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Main Two-Column Layout */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
        {/* Left Column: Entities List with Search & Filters (5 cols) */}
        <div className="flex flex-col lg:col-span-5 xl:col-span-4 space-y-3">
          {/* Search box */}
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder={isEn ? 'Search entity, type, engram, quote...' : 'Buscar entidad, tipo, engrama o texto...'}
              className="w-full rounded-2xl border border-slate-200 bg-white dark:border-white/10 dark:bg-slate-900/80 pl-9 pr-8 py-2.5 text-xs sm:text-sm text-slate-900 dark:text-white placeholder-slate-400 focus:border-amber-500/50 focus:outline-none focus:ring-1 focus:ring-amber-500/50 shadow-sm"
            />
            {searchTerm && (
              <button
                type="button"
                onClick={() => setSearchTerm('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-slate-400 hover:text-slate-600 dark:hover:text-white"
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
                className="w-full truncate rounded-xl border border-slate-200 bg-white dark:border-white/10 dark:bg-slate-900/80 px-2.5 py-1.5 text-xs text-slate-700 dark:text-slate-300 focus:border-amber-500/50 focus:outline-none shadow-xs"
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
            </div>

            {/* Course Filter */}
            {data.courses.length > 1 && (
              <div className="flex-1 min-w-0">
                <select
                  value={selectedCourseId}
                  onChange={(e) => setSelectedCourseId(e.target.value)}
                  className="w-full truncate rounded-xl border border-slate-200 bg-white dark:border-white/10 dark:bg-slate-900/80 px-2.5 py-1.5 text-xs text-slate-700 dark:text-slate-300 focus:border-amber-500/50 focus:outline-none shadow-xs"
                >
                  <option value="ALL">
                    {isEn ? `All courses (${data.courses.length})` : `Todos los cursos (${data.courses.length})`}
                  </option>
                  {data.courses.map((c) => (
                    <option key={c.courseId} value={c.courseId}>
                      {c.courseName}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          {/* Counter info */}
          <div className="flex items-center justify-between px-1 text-[11px] text-slate-500 dark:text-slate-400">
            <span>
              {isEn ? 'Showing' : 'Mostrando'} <strong className="text-slate-800 dark:text-white">{filteredEntities.length}</strong> {isEn ? 'entities' : 'entidades'}
            </span>
            {(selectedType !== 'ALL' || selectedCourseId !== 'ALL' || searchTerm) && (
              <button
                type="button"
                onClick={() => {
                  setSelectedType('ALL');
                  setSelectedCourseId('ALL');
                  setSearchTerm('');
                }}
                className="text-amber-600 dark:text-amber-400 font-medium hover:underline"
              >
                {isEn ? 'Clear filters' : 'Limpiar filtros'}
              </button>
            )}
          </div>

          {/* Scrollable Entities List */}
          <div className="max-h-[calc(100vh-320px)] space-y-1.5 overflow-y-auto pr-1">
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
                        ? 'border-amber-500 bg-amber-500/10 dark:bg-amber-500/15 text-slate-900 dark:text-white shadow-md shadow-amber-500/5'
                        : 'border-slate-200 dark:border-white/5 bg-white dark:bg-slate-900/60 text-slate-700 dark:text-slate-300 hover:border-slate-300 dark:hover:border-white/15 hover:bg-slate-50 dark:hover:bg-slate-900/90'
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
                      <div className="mt-1.5 truncate text-xs sm:text-sm font-semibold">
                        {item.canonicalName}
                      </div>
                      {item.description && (
                        <div className="mt-0.5 truncate text-[11px] text-slate-500 dark:text-slate-400">
                          {item.description}
                        </div>
                      )}
                    </div>

                    {/* Occurrences Pill */}
                    <div className="flex shrink-0 items-center gap-1.5">
                      <span
                        className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${
                          isSelected
                            ? 'bg-amber-500 text-slate-950'
                            : 'bg-slate-100 text-slate-700 dark:bg-white/10 dark:text-amber-300'
                        }`}
                        title={`${item.totalOccurrences} ${isEn ? 'occurrences across engrams' : 'apariciones en engrams'}`}
                      >
                        ({item.totalOccurrences})
                      </span>
                      <ChevronRight className={`h-4 w-4 ${isSelected ? 'text-amber-500' : 'text-slate-400 dark:text-slate-600'}`} />
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
            <div className="rounded-3xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-900/70 p-6 sm:p-7 shadow-lg dark:backdrop-blur-xl space-y-6">
              {/* Header */}
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between border-b border-slate-200 dark:border-white/10 pb-5">
                <div className="space-y-2">
                  <div className="flex items-center gap-2">
                    <span className="rounded-lg bg-amber-500/15 px-2.5 py-1 text-xs font-semibold uppercase tracking-wider text-amber-700 dark:text-amber-300 border border-amber-500/30">
                      {activeEntity.entityType || 'Entity'}
                    </span>
                    {activeEntity.ontologicalClass && (
                      <span className="rounded-lg bg-emerald-500/15 px-2 py-1 text-xs font-medium text-emerald-700 dark:text-emerald-300">
                        {isEn ? 'Class:' : 'Clase:'} {activeEntity.ontologicalClass}
                      </span>
                    )}
                  </div>

                  <h2 className="text-xl sm:text-2xl font-bold text-slate-900 dark:text-white">
                    {activeEntity.canonicalName}
                  </h2>

                  {activeEntity.description && (
                    <p className="text-sm text-slate-600 dark:text-slate-300 leading-relaxed">{activeEntity.description}</p>
                  )}

                  {activeEntity.aliases && activeEntity.aliases.length > 0 && (
                    <div className="flex flex-wrap items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400 pt-1">
                      <span className="font-semibold text-slate-600 dark:text-slate-400">{isEn ? 'Aliases:' : 'Alias / Formas:'}</span>
                      {activeEntity.aliases.map((alias, idx) => (
                        <span key={idx} className="rounded-md bg-slate-100 dark:bg-white/10 px-2 py-0.5 text-slate-700 dark:text-slate-300">
                          {alias}
                        </span>
                      ))}
                    </div>
                  )}
                </div>

                {/* Big count badge */}
                <div className="shrink-0 rounded-2xl bg-amber-500/10 dark:bg-amber-500/15 border border-amber-500/20 p-4 text-center sm:text-right min-w-[130px]">
                  <div className="text-3xl sm:text-4xl font-extrabold text-amber-600 dark:text-amber-400">
                    {activeEntity.totalOccurrences}
                  </div>
                  <div className="text-xs text-slate-600 dark:text-slate-400 font-medium">
                    {isEn ? 'total citations' : 'menciones en total'}
                  </div>
                  <div className="mt-1 text-[11px] text-purple-700 dark:text-purple-300 font-semibold">
                    {activeEntity.distinctCoursesCount} {isEn ? 'program courses' : 'cursos del programa'}
                  </div>
                </div>
              </div>

              {/* Occurrences Grouped by Course */}
              <div>
                <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-4">
                  {isEn ? 'Occurrences by Course & Engram' : 'Apariciones por Curso y Engrama'}
                </h3>

                <div className="space-y-6">
                  {/* Group occurrences by CourseId */}
                  {Array.from(
                    new Set(activeEntity.occurrences.map((o) => o.courseId)),
                  ).map((courseId) => {
                    const courseOccs = activeEntity.occurrences.filter((o) => o.courseId === courseId);
                    const courseName = courseOccs[0]?.courseName ?? 'Course';

                    return (
                      <div key={courseId} className="rounded-2xl border border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-slate-950/60 p-4 sm:p-5">
                        <div className="flex items-center justify-between border-b border-slate-200 dark:border-white/10 pb-3 mb-3">
                          <div className="flex items-center gap-2">
                            <GraduationCap className="h-4 w-4 text-purple-600 dark:text-purple-400" />
                            <Link
                              to={`/structured-learning/${courseId}`}
                              className="font-bold text-slate-900 dark:text-white hover:text-amber-600 dark:hover:text-amber-300 transition-colors flex items-center gap-1.5 text-sm sm:text-base"
                            >
                              <span>{courseName}</span>
                              <ExternalLink className="h-3.5 w-3.5 text-slate-400" />
                            </Link>
                          </div>
                          <span className="text-xs text-amber-700 dark:text-amber-300 font-bold bg-amber-100 dark:bg-amber-500/10 px-2.5 py-0.5 rounded-full border border-amber-300/60 dark:border-amber-500/20">
                            {courseOccs.length} {courseOccs.length === 1 ? (isEn ? 'citation' : 'aparición') : (isEn ? 'citations' : 'apariciones')}
                          </span>
                        </div>

                        {/* List of engrams */}
                        <div className="space-y-3">
                          {courseOccs.map((occ, idx) => (
                            <div
                              key={`${occ.engramId}-${idx}`}
                              className="rounded-xl border border-slate-200/80 dark:border-white/5 bg-white dark:bg-white/[0.02] p-3.5 transition-colors hover:border-slate-300 dark:hover:border-white/10 shadow-2xs"
                            >
                              <div className="flex items-start justify-between gap-3">
                                <button
                                  type="button"
                                  onClick={() => setOpenEngram(occ)}
                                  className="group flex items-start gap-2.5 text-left text-xs sm:text-sm font-semibold text-slate-800 dark:text-slate-200 hover:text-amber-600 dark:hover:text-white cursor-pointer"
                                >
                                  <BookOpen className="h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400 mt-0.5 group-hover:scale-110 transition-transform" />
                                  <div>
                                    <span className="group-hover:text-amber-600 dark:group-hover:text-amber-300 transition-colors">
                                      {occ.engramTitle}
                                    </span>
                                    {occ.chapterTitle && (
                                      <div className="text-[11px] font-normal text-slate-500 dark:text-slate-400">{occ.chapterTitle}</div>
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

                              {/* Verbatim quote evidence */}
                              {occ.sourceText && (
                                <div className="mt-2.5 rounded-lg bg-slate-100 dark:bg-black/40 p-3 border-l-2 border-amber-500 text-xs italic text-slate-700 dark:text-slate-300 leading-relaxed">
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

      {/* Engram Detail Panel (Same as Brain page) */}
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
