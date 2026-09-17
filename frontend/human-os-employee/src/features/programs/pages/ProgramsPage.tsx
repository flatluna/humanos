import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import {
  GraduationCap,
  Layers,
  Search,
  CheckCircle2,
  PlayCircle,
  Info,
  X,
  Sparkles,
  AlertCircle,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import {
  getPrograms,
  getEnrolledPrograms,
  enrollProgram,
  getProgramLogoUrl,
  type ProgramCatalogEntry,
  type EnrolledProgramSummary,
} from '../api/programsApi';

const ACCENT_GRADIENTS = [
  'from-violet-600 via-indigo-600 to-purple-700',
  'from-blue-600 via-sky-600 to-cyan-700',
  'from-emerald-600 via-teal-600 to-cyan-700',
  'from-amber-600 via-orange-600 to-rose-700',
  'from-fuchsia-600 via-pink-600 to-rose-700',
];

function accentFor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return ACCENT_GRADIENTS[hash % ACCENT_GRADIENTS.length];
}

export function ProgramsPage() {
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === 'en';
  const personId = user?.personId ?? '';
  const navigate = useNavigate();

  const [programs, setPrograms] = useState<ProgramCatalogEntry[]>([]);
  const [enrolledMap, setEnrolledMap] = useState<Map<string, EnrolledProgramSummary>>(new Map());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedFilter, setSelectedFilter] = useState<'all' | 'enrolled' | 'available'>('all');
  const [detailModalProgram, setDetailModalProgram] = useState<ProgramCatalogEntry | null>(null);
  const [enrollingId, setEnrollingId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    Promise.all([
      getPrograms().catch((err) => {
        console.error('Error fetching programs:', err);
        return [] as ProgramCatalogEntry[];
      }),
      personId
        ? getEnrolledPrograms(personId).catch((err) => {
            console.error('Error fetching enrolled programs:', err);
            return [] as EnrolledProgramSummary[];
          })
        : Promise.resolve([] as EnrolledProgramSummary[]),
    ])
      .then(([allPrograms, enrolledList]) => {
        if (cancelled) return;
        setPrograms(allPrograms);
        const map = new Map<string, EnrolledProgramSummary>();
        enrolledList.forEach((p) => map.set(p.programId, p));
        setEnrolledMap(map);
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : isEn ? 'Failed to load programs' : 'Error al cargar los programas');
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [personId, isEn]);

  const handleEnrollAndStart = async (program: ProgramCatalogEntry) => {
    if (!personId || enrollingId) return;
    setEnrollingId(program.programId);

    try {
      if (!enrolledMap.has(program.programId)) {
        await enrollProgram(personId, program.programId);
        // Refresh enrolled map
        const updatedList = await getEnrolledPrograms(personId);
        const map = new Map<string, EnrolledProgramSummary>();
        updatedList.forEach((p) => map.set(p.programId, p));
        setEnrolledMap(map);
      }
      navigate(`/programs/${program.programId}`);
    } catch (err) {
      console.error('Failed to enroll program:', err);
    } finally {
      setEnrollingId(null);
    }
  };

  const filteredPrograms = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return programs.filter((p) => {
      const isEnrolled = enrolledMap.has(p.programId);
      if (selectedFilter === 'enrolled' && !isEnrolled) return false;
      if (selectedFilter === 'available' && isEnrolled) return false;

      if (!q) return true;
      return (
        p.name.toLowerCase().includes(q) ||
        (p.description ?? '').toLowerCase().includes(q) ||
        (p.objectives ?? '').toLowerCase().includes(q)
      );
    });
  }, [programs, enrolledMap, selectedFilter, searchQuery]);

  return (
    <div className="mx-auto max-w-6xl p-4 sm:p-8 space-y-8">
      {/* Page Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3.5">
          <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-purple-600 to-indigo-600 text-white shadow-lg shadow-purple-500/25">
            <GraduationCap className="h-6 w-6" />
          </div>
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
              {isEn ? 'Learning Programs' : 'Programas de Aprendizaje'}
            </h1>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              {isEn
                ? 'Curated series of structured courses with progression and prerequisites.'
                : 'Programas de formación integrados por secuencias de cursos y engramas cronológicos.'}
            </p>
          </div>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="relative flex-1 max-w-md">
          <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="search"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={isEn ? 'Search programs...' : 'Buscar programas...'}
            className="w-full rounded-xl border border-slate-200 bg-white pl-10 pr-4 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-purple-500 focus:outline-none focus:ring-2 focus:ring-purple-500/20 dark:border-white/10 dark:bg-white/[0.04] dark:text-white"
          />
        </div>

        <div className="flex items-center gap-1.5 rounded-xl border border-slate-200 bg-slate-50/50 p-1 dark:border-white/10 dark:bg-white/[0.02]">
          <button
            type="button"
            onClick={() => setSelectedFilter('all')}
            className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${
              selectedFilter === 'all'
                ? 'bg-white text-purple-700 shadow-sm dark:bg-white/10 dark:text-purple-300'
                : 'text-slate-600 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white'
            }`}
          >
            {isEn ? 'All' : 'Todos'} ({programs.length})
          </button>
          <button
            type="button"
            onClick={() => setSelectedFilter('enrolled')}
            className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${
              selectedFilter === 'enrolled'
                ? 'bg-white text-purple-700 shadow-sm dark:bg-white/10 dark:text-purple-300'
                : 'text-slate-600 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white'
            }`}
          >
            {isEn ? 'My Enrolled Programs' : 'Mis Inscripciones'} ({enrolledMap.size})
          </button>
          <button
            type="button"
            onClick={() => setSelectedFilter('available')}
            className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${
              selectedFilter === 'available'
                ? 'bg-white text-purple-700 shadow-sm dark:bg-white/10 dark:text-purple-300'
                : 'text-slate-600 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white'
            }`}
          >
            {isEn ? 'Available to Enroll' : 'Por Inscribir'} ({Math.max(0, programs.length - enrolledMap.size)})
          </button>
        </div>
      </div>

      {/* Loading Skeletons */}
      {loading && (
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <div
              key={i}
              className="h-72 animate-pulse rounded-2xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]"
            />
          ))}
        </div>
      )}

      {/* Error State */}
      {error && !loading && (
        <div className="flex items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-5 text-sm text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
          <AlertCircle className="h-5 w-5 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Empty State */}
      {!loading && !error && filteredPrograms.length === 0 && (
        <div className="rounded-2xl border border-slate-200 bg-white p-12 text-center dark:border-white/10 dark:bg-white/[0.02]">
          <GraduationCap className="mx-auto h-12 w-12 text-slate-300 dark:text-slate-600" />
          <h3 className="mt-4 text-base font-semibold text-slate-900 dark:text-white">
            {isEn ? 'No programs found' : 'No se encontraron programas'}
          </h3>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            {isEn
              ? 'Try modifying your search or filter options.'
              : 'Prueba cambiando tus criterios de búsqueda o filtro.'}
          </p>
        </div>
      )}

      {/* Program Cards Grid */}
      <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {filteredPrograms.map((program) => {
          const enrolled = enrolledMap.get(program.programId);
          const isEnrolled = !!enrolled;
          const isProcessing = enrollingId === program.programId;

          // Compute progress if enrolled
          let completedCount = 0;
          let totalCount = enrolled?.capabilities?.length ?? program.capabilityCount ?? 0;
          if (enrolled && enrolled.capabilities) {
            completedCount = enrolled.capabilities.filter((c) => c.status === 'Completed').length;
          }

          const percent = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

          return (
            <div
              key={program.programId}
              className="group flex flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-xs transition-all duration-200 hover:-translate-y-1 hover:border-purple-300 hover:shadow-xl hover:shadow-purple-500/10 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-purple-500/30"
            >
              {/* Card Banner / Logo */}
              <div className={`relative h-40 w-full overflow-hidden bg-gradient-to-br ${accentFor(program.programId)}`}>
                <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(255,255,255,0.2),transparent_60%)]" />
                {program.hasLogo ? (
                  <img
                    src={getProgramLogoUrl(program.programId)}
                    alt={program.name}
                    className="h-full w-full object-cover object-center transition-transform duration-500 group-hover:scale-105"
                    onError={(e) => {
                      (e.currentTarget as HTMLImageElement).style.display = 'none';
                    }}
                  />
                ) : (
                  <div className="flex h-full w-full items-center justify-center">
                    <GraduationCap className="h-14 w-14 text-white/80" strokeWidth={1.5} />
                  </div>
                )}

                {/* Status Badges */}
                <div className="absolute top-3 right-3 flex items-center gap-1.5">
                  {isEnrolled ? (
                    <span className="flex items-center gap-1 rounded-full bg-emerald-500/90 backdrop-blur-md px-2.5 py-1 text-xs font-semibold text-white shadow-xs">
                      <CheckCircle2 className="h-3.5 w-3.5" />
                      {percent === 100 ? (isEn ? 'Completed' : 'Completado') : (isEn ? 'Enrolled' : 'Inscrito')}
                    </span>
                  ) : (
                    <span className="flex items-center gap-1 rounded-full bg-black/45 backdrop-blur-md px-2.5 py-1 text-xs font-semibold text-white">
                      <Sparkles className="h-3.5 w-3.5 text-amber-300" />
                      {isEn ? 'Available' : 'Disponible'}
                    </span>
                  )}
                </div>

                <div className="absolute bottom-3 left-3 flex items-center gap-1 rounded-full bg-black/50 backdrop-blur-md px-2.5 py-1 text-xs font-medium text-white">
                  <Layers className="h-3.5 w-3.5" />
                  <span>
                    {totalCount} {totalCount === 1 ? (isEn ? 'course' : 'curso') : (isEn ? 'courses' : 'cursos')}
                  </span>
                </div>
              </div>

              {/* Card Body */}
              <div className="flex flex-1 flex-col p-5">
                <div className="flex-1">
                  <h3 className="font-semibold text-base text-slate-900 group-hover:text-purple-600 transition dark:text-white dark:group-hover:text-purple-400 line-clamp-2">
                    {program.name}
                  </h3>
                  <p className="mt-2 text-xs leading-relaxed text-slate-500 dark:text-slate-400 line-clamp-3">
                    {program.description || (isEn ? 'Curated structured program.' : 'Programa estructurado con secuencia de cursos.')}
                  </p>
                </div>

                {/* Progress bar if enrolled */}
                {isEnrolled && (
                  <div className="mt-4 pt-3 border-t border-slate-100 dark:border-white/5 space-y-1.5">
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-slate-500 dark:text-slate-400">
                        {isEn ? 'Progress' : 'Progreso'} ({completedCount}/{totalCount})
                      </span>
                      <span className="font-semibold text-purple-600 dark:text-purple-400">{percent}%</span>
                    </div>
                    <div className="h-1.5 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                      <div
                        className="h-full rounded-full bg-gradient-to-r from-purple-500 to-indigo-600 transition-all duration-500"
                        style={{ width: `${percent}%` }}
                      />
                    </div>
                  </div>
                )}

                {/* Actions */}
                <div className="mt-5 flex items-center justify-between gap-2 pt-3 border-t border-slate-100 dark:border-white/5">
                  <button
                    type="button"
                    onClick={() => setDetailModalProgram(program)}
                    className="flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-white/5 dark:hover:text-white"
                  >
                    <Info className="h-3.5 w-3.5" />
                    {isEn ? 'Details' : 'Ver detalle'}
                  </button>

                  {isEnrolled ? (
                    <Link
                      to={`/programs/${program.programId}`}
                      className="flex items-center gap-1.5 rounded-xl bg-purple-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-sm hover:bg-purple-700 transition"
                    >
                      <PlayCircle className="h-4 w-4" />
                      {isEn ? 'Go to Program' : 'Continuar'}
                    </Link>
                  ) : (
                    <button
                      type="button"
                      disabled={isProcessing}
                      onClick={() => handleEnrollAndStart(program)}
                      className="flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-purple-600 to-indigo-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-sm hover:from-purple-700 hover:to-indigo-700 transition disabled:opacity-60"
                    >
                      <GraduationCap className="h-4 w-4" />
                      {isProcessing
                        ? (isEn ? 'Enrolling...' : 'Inscribiendo...')
                        : (isEn ? 'Enroll in Program' : 'Inscribirme al Programa')}
                    </button>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Detail Modal */}
      {detailModalProgram && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
          onClick={() => setDetailModalProgram(null)}
        >
          <div
            className="max-h-[85vh] w-full max-w-lg overflow-y-auto rounded-3xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-center gap-2.5">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-purple-100 text-purple-700 dark:bg-purple-500/15 dark:text-purple-300">
                  <GraduationCap className="h-5 w-5" />
                </div>
                <div>
                  <h2 className="text-lg font-bold text-slate-900 dark:text-white">{detailModalProgram.name}</h2>
                  <p className="text-xs font-mono text-slate-400">{detailModalProgram.code}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setDetailModalProgram(null)}
                className="rounded-full p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-white/10 dark:hover:text-white"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <div className="mt-5 space-y-4 text-sm text-slate-600 dark:text-slate-300">
              {detailModalProgram.description ? (
                <div>
                  <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-1">
                    {isEn ? 'Description' : 'Descripción'}
                  </h4>
                  <p className="leading-relaxed">{detailModalProgram.description}</p>
                </div>
              ) : null}

              {detailModalProgram.objectives ? (
                <div>
                  <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-1">
                    {isEn ? 'Objectives' : 'Objetivos'}
                  </h4>
                  <p className="leading-relaxed">{detailModalProgram.objectives}</p>
                </div>
              ) : null}

              {detailModalProgram.requirements ? (
                <div>
                  <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-1">
                    {isEn ? 'Requirements / Prerequisites' : 'Requisitos / Prerrequisitos'}
                  </h4>
                  <p className="leading-relaxed">{detailModalProgram.requirements}</p>
                </div>
              ) : null}

              <div className="flex items-center gap-2 pt-2 text-xs font-medium text-purple-600 dark:text-purple-400">
                <Layers className="h-4 w-4" />
                <span>
                  {isEn
                    ? `Contains ${detailModalProgram.capabilityCount} sequential courses`
                    : `Contiene ${detailModalProgram.capabilityCount} cursos ordenados secuencialmente`}
                </span>
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-3 pt-4 border-t border-slate-100 dark:border-white/10">
              <button
                type="button"
                onClick={() => setDetailModalProgram(null)}
                className="rounded-xl px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-white/5"
              >
                {isEn ? 'Close' : 'Cerrar'}
              </button>

              <button
                type="button"
                onClick={() => {
                  const target = detailModalProgram;
                  setDetailModalProgram(null);
                  handleEnrollAndStart(target);
                }}
                className="rounded-xl bg-purple-600 px-4 py-2 text-xs font-semibold text-white shadow-sm hover:bg-purple-700 transition"
              >
                {enrolledMap.has(detailModalProgram.programId)
                  ? (isEn ? 'Open Program' : 'Abrir Programa')
                  : (isEn ? 'Enroll & Open' : 'Inscribirme y Abrir')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
