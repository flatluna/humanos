import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router';
import {
  ArrowLeft,
  CalendarDays,
  CheckCircle2,
  CircleDashed,
  GraduationCap,
  Lock,
  PlayCircle,
  AlertCircle,
  Layers,
  Sparkles,
  BookOpen,
  Award,
  Brain,
  Bot,
  Network,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import {
  getProgramById,
  getEnrolledPrograms,
  enrollProgram,
  type ProgramCatalogDetail,
  type EnrolledProgramSummary,
} from '../api/programsApi';
import {
  getStructuredLearningDocument,
  getActiveStructuredLearningAssessmentRound,
} from '@/features/capabilities/api/structuredLearningRuntimeApi';
import { getCapabilityGraph, type BackendCapabilityGraphNode } from '@/features/capabilities/api/capabilityGraphApi';

export function ProgramDetailPage() {
  const { programId } = useParams<{ programId: string }>();
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === 'en';
  const personId = user?.personId ?? '';

  const [programDetail, setProgramDetail] = useState<ProgramCatalogDetail | null>(null);
  const [enrolledSummary, setEnrolledSummary] = useState<EnrolledProgramSummary | null>(null);
  const [isEnrolled, setIsEnrolled] = useState(false);
  const [courseCompletionMap, setCourseCompletionMap] = useState<Record<string, { status: 'NotStarted' | 'InProgress' | 'Completed'; mastered: number; total: number }>>({});
  const [loading, setLoading] = useState(true);
  const [enrolling, setEnrolling] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    if (!programId) return;
    setLoading(true);
    setError(null);

    try {
      // 1. Fetch program detail
      const detail = await getProgramById(programId);
      setProgramDetail(detail);

      // 2. Check enrollment
      let enrolled: EnrolledProgramSummary | null = null;
      if (personId) {
        const enrolledList = await getEnrolledPrograms(personId).catch(() => []);
        enrolled = enrolledList.find((p) => p.programId === programId) ?? null;
      }
      setEnrolledSummary(enrolled);
      setIsEnrolled(!!enrolled);

      // 3. For each course in the program, calculate real mastery
      // StructuredLearning: checks section assessment rounds
      // Capability: checks graph node mastery
      const completion: Record<string, { status: 'NotStarted' | 'InProgress' | 'Completed'; mastered: number; total: number }> = {};

      if (personId && detail.capabilities && detail.capabilities.length > 0) {
        await Promise.all(
          detail.capabilities.map(async (course) => {
            const isStructured = course.courseType === 'StructuredLearning' || course.capabilityCode.startsWith('SL-');
            if (isStructured) {
              try {
                const doc = await getStructuredLearningDocument(course.capabilityId);
                const sections = doc.Sections ?? [];
                let masteredCount = 0;
                let inProgress = false;

                await Promise.all(
                  sections.map(async (sec) => {
                    const round = await getActiveStructuredLearningAssessmentRound(sec.StructuredLearningDocumentSectionId, personId).catch(() => null);
                    if (round?.Status === 'Passed') {
                      masteredCount++;
                    } else if (round) {
                      inProgress = true;
                    }
                  })
                );

                const total = sections.length;
                let status: 'NotStarted' | 'InProgress' | 'Completed' = 'NotStarted';
                if (total > 0 && masteredCount === total) {
                  status = 'Completed';
                } else if (masteredCount > 0 || inProgress) {
                  status = 'InProgress';
                }
                completion[course.capabilityId] = { status, mastered: masteredCount, total };
              } catch {
                completion[course.capabilityId] = { status: 'NotStarted', mastered: 0, total: course.nodeCount || 1 };
              }
            } else {
              // Standard capability graph
              try {
                const graph = await getCapabilityGraph(course.capabilityId, personId);
                const nodes = graph.nodes ?? [];
                const total = nodes.length;
                const masteredCount = nodes.filter((n: BackendCapabilityGraphNode) => n.state === 'Mastered').length;
                let status: 'NotStarted' | 'InProgress' | 'Completed' = 'NotStarted';
                if (total > 0 && masteredCount === total) {
                  status = 'Completed';
                } else if (masteredCount > 0) {
                  status = 'InProgress';
                }
                completion[course.capabilityId] = { status, mastered: masteredCount, total };
              } catch {
                completion[course.capabilityId] = { status: 'NotStarted', mastered: 0, total: course.nodeCount || 1 };
              }
            }
          })
        );
      }

      setCourseCompletionMap(completion);
    } catch (err) {
      console.error('Error loading program detail:', err);
      setError(err instanceof Error ? err.message : isEn ? 'Failed to load program' : 'Error al cargar el programa');
    } finally {
      setLoading(false);
    }
  }, [programId, personId, isEn]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleEnroll = async () => {
    if (!personId || !programId || enrolling) return;
    setEnrolling(true);
    try {
      await enrollProgram(personId, programId);
      await loadData();
    } catch (err) {
      console.error('Failed to enroll:', err);
    } finally {
      setEnrolling(false);
    }
  };

  const capabilities = useMemo(() => {
    return (programDetail?.capabilities ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder);
  }, [programDetail]);

  // Chronological Progression Gate Calculation:
  // Cannot proceed to next course if the previous required course is not completed!
  const coursesWithGate = useMemo(() => {
    let previousCompleted = true; // First course is always open if enrolled

    return capabilities.map((course, index) => {
      const stats = courseCompletionMap[course.capabilityId] ?? {
        status: 'NotStarted',
        mastered: 0,
        total: course.nodeCount || 1,
      };

      // Is this course locked by chronological order?
      // A course is locked if not enrolled, OR if index > 0 and previous required course wasn't completed
      const isLocked = !isEnrolled || (index > 0 && !previousCompleted);

      // Update previousCompleted for next course in chain
      if (course.isRequired) {
        previousCompleted = previousCompleted && stats.status === 'Completed';
      }

      const isStructured = course.courseType === 'StructuredLearning' || course.capabilityCode.startsWith('SL-');
      const targetUrl = isStructured
        ? `/structured-learning/${course.capabilityId}`
        : `/capabilities/${course.capabilityId}?programId=${programId}`;

      return {
        ...course,
        stats,
        isLocked,
        isStructured,
        targetUrl,
      };
    });
  }, [capabilities, courseCompletionMap, isEnrolled, programId]);

  const totalCourses = coursesWithGate.length;
  const completedCourses = coursesWithGate.filter((c) => c.stats.status === 'Completed').length;
  const overallPercent = totalCourses > 0 ? Math.round((completedCourses / totalCourses) * 100) : 0;

  const totalEngrams = useMemo(() => {
    return coursesWithGate.reduce((sum, c) => sum + (c.stats.total || c.nodeCount || c.levelCount || 0), 0);
  }, [coursesWithGate]);

  const masteredEngrams = useMemo(() => {
    return coursesWithGate.reduce((sum, c) => sum + (c.stats.mastered || 0), 0);
  }, [coursesWithGate]);

  return (
    <div className="mx-auto max-w-4xl p-4 sm:p-8 space-y-8">
      {/* Back button */}
      <div>
        <Link
          to="/programs"
          className="inline-flex items-center gap-2 text-sm font-medium text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-white transition"
        >
          <ArrowLeft className="h-4 w-4" />
          {isEn ? 'Back to Programs' : 'Volver a Programas'}
        </Link>
      </div>

      {loading && (
        <div className="space-y-6">
          <div className="h-64 animate-pulse rounded-3xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]" />
          <div className="space-y-4">
            {[1, 2, 3].map((i) => (
              <div
                key={i}
                className="h-24 animate-pulse rounded-2xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]"
              />
            ))}
          </div>
        </div>
      )}

      {error && !loading && (
        <div className="flex items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-5 text-sm text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
          <AlertCircle className="h-5 w-5 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {!loading && programDetail && (
        <>
          {/* Header Banner */}
          <div className="relative overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-purple-700 via-indigo-700 to-blue-800 p-6 sm:p-8 text-white shadow-xl dark:border-white/10">
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(255,255,255,0.18),transparent_70%)]" />

            <div className="relative z-10 flex flex-col md:flex-row md:items-center md:justify-between gap-6">
              <div className="space-y-3 max-w-2xl">
                <div className="flex items-center gap-2 text-white/80">
                  <GraduationCap className="h-5 w-5" />
                  <span className="text-xs font-semibold uppercase tracking-wider">
                    {isEn ? 'Official Training Program' : 'Programa de Capacitación Oficial'}
                  </span>
                  <span className="text-xs font-mono bg-white/20 px-2 py-0.5 rounded-full">{programDetail.code}</span>
                </div>

                <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight">{programDetail.name}</h1>

                {programDetail.description && (
                  <p className="text-sm leading-relaxed text-white/90 line-clamp-3">{programDetail.description}</p>
                )}

                <div className="flex flex-wrap items-center gap-4 pt-2 text-xs font-medium text-white/85">
                  <div className="flex items-center gap-1.5">
                    <Layers className="h-4 w-4" />
                    <span>
                      {totalCourses} {totalCourses === 1 ? (isEn ? 'Course' : 'Curso') : (isEn ? 'Courses' : 'Cursos')}
                    </span>
                  </div>

                  <div className="flex items-center gap-1.5">
                    <Brain className="h-4 w-4 text-fuchsia-300" />
                    <span>
                      {isEn
                        ? isEnrolled
                          ? `${masteredEngrams} / ${totalEngrams} Engrams mastered`
                          : `${totalEngrams} Engrams`
                        : isEnrolled
                          ? `${masteredEngrams} / ${totalEngrams} Engrams dominados`
                          : `${totalEngrams} Engramas`}
                    </span>
                  </div>

                  {isEnrolled && enrolledSummary?.acceptedDate && (
                    <div className="flex items-center gap-1.5">
                      <CalendarDays className="h-4 w-4" />
                      <span>
                        {isEn ? 'Started on' : 'Iniciado el'}{' '}
                        {new Date(enrolledSummary.acceptedDate).toLocaleDateString(isEn ? 'en-US' : 'es-ES', {
                          year: 'numeric',
                          month: 'short',
                          day: 'numeric',
                        })}
                      </span>
                    </div>
                  )}

                  {isEnrolled && (
                    <div className="flex items-center gap-1.5">
                      <Award className="h-4 w-4 text-amber-300" />
                      <span>
                        {completedCourses} / {totalCourses} {isEn ? 'completed' : 'completados'} ({overallPercent}%)
                      </span>
                    </div>
                  )}
                </div>
              </div>

              {/* Action buttons in header */}
              <div className="relative z-10 flex flex-col items-start md:items-end gap-3 shrink-0">
                <div className="flex flex-wrap items-center gap-2">
                  <Link
                    to={`/programs/${programDetail.programId}/entities`}
                    className="flex items-center gap-2 rounded-2xl bg-amber-500/20 backdrop-blur-md border border-amber-300/40 px-4 py-2.5 text-xs font-bold text-amber-200 hover:bg-amber-500/30 transition shadow-sm"
                  >
                    <Network className="h-4 w-4 text-amber-300" />
                    <span>{isEn ? 'Program Entities' : 'Entidades del Programa'}</span>
                  </Link>

                  <Link
                    to={`/program-agents/${programDetail.programId}`}
                    className="flex items-center gap-2 rounded-2xl bg-white/20 backdrop-blur-md border border-white/30 px-4 py-2.5 text-xs font-bold text-white hover:bg-white/30 transition shadow-sm"
                  >
                    <Bot className="h-4 w-4 text-purple-200" />
                    <span>{isEn ? 'Consult Program Agent' : 'Preguntar al Agente'}</span>
                  </Link>
                </div>

                {!isEnrolled ? (
                  <button
                    type="button"
                    disabled={enrolling}
                    onClick={handleEnroll}
                    className="flex items-center gap-2 rounded-2xl bg-white px-5 py-3 text-sm font-bold text-purple-700 shadow-lg shadow-black/20 hover:bg-purple-50 transition active:scale-95 disabled:opacity-60"
                  >
                    <GraduationCap className="h-5 w-5 text-purple-600" />
                    {enrolling
                      ? (isEn ? 'Enrolling...' : 'Inscribiendo...')
                      : (isEn ? 'Enroll in Program' : 'Inscribirme al Programa')}
                  </button>
                ) : (
                  <div className="flex items-center gap-2 rounded-2xl bg-emerald-500/20 backdrop-blur-md border border-emerald-400/40 px-4 py-2 text-xs font-semibold text-emerald-200">
                    <CheckCircle2 className="h-4 w-4 text-emerald-400" />
                    <span>{isEn ? 'Enrolled Student' : 'Estudiante Inscrito'}</span>
                  </div>
                )}
              </div>
            </div>

            {/* Overall Progress Bar */}
            {isEnrolled && (
              <div className="relative z-10 mt-6 pt-4 border-t border-white/15 space-y-2">
                <div className="flex items-center justify-between text-xs font-semibold text-white/90">
                  <span>{isEn ? 'Program Chronological Completion' : 'Avance Cronológico del Programa'}</span>
                  <span>{overallPercent}%</span>
                </div>
                <div className="h-2.5 w-full overflow-hidden rounded-full bg-black/30 backdrop-blur-xs">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-emerald-400 to-teal-300 transition-all duration-700"
                    style={{ width: `${overallPercent}%` }}
                  />
                </div>
              </div>
            )}
          </div>

          {/* Program Information Accordion / Cards */}
          {(programDetail.objectives || programDetail.requirements) && (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {programDetail.objectives && (
                <div className="rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/[0.03]">
                  <h3 className="text-xs font-bold uppercase tracking-wider text-purple-600 dark:text-purple-400 flex items-center gap-1.5">
                    <Sparkles className="h-4 w-4" />
                    {isEn ? 'Learning Objectives' : 'Objetivos de Aprendizaje'}
                  </h3>
                  <p className="mt-2 text-xs leading-relaxed text-slate-600 dark:text-slate-300">
                    {programDetail.objectives}
                  </p>
                </div>
              )}

              {programDetail.requirements && (
                <div className="rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/[0.03]">
                  <h3 className="text-xs font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400 flex items-center gap-1.5">
                    <BookOpen className="h-4 w-4" />
                    {isEn ? 'Requirements & Prerequisites' : 'Requisitos y Perfil'}
                  </h3>
                  <p className="mt-2 text-xs leading-relaxed text-slate-600 dark:text-slate-300">
                    {programDetail.requirements}
                  </p>
                </div>
              )}
            </div>
          )}

          {/* Chronological Course Progression List */}
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
                  <Layers className="h-5 w-5 text-purple-600 dark:text-purple-400" />
                  {isEn ? 'Course Sequence (Chronological)' : 'Secuencia de Cursos (Cronológica)'}
                </h2>
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  {isEn
                    ? 'Courses must be completed in order. Complete each course to unlock the next.'
                    : 'Los cursos deben completarse en orden. Debes terminar el curso actual para desbloquear el siguiente.'}
                </p>
              </div>
            </div>

            <div className="space-y-3">
              {coursesWithGate.map((course, index) => {
                const isCompleted = course.stats.status === 'Completed';
                const isInProgress = course.stats.status === 'InProgress';
                const percent =
                  course.stats.total > 0 ? Math.round((course.stats.mastered / course.stats.total) * 100) : 0;

                return (
                  <div
                    key={course.capabilityId}
                    className={`group relative flex flex-col sm:flex-row sm:items-center justify-between gap-4 rounded-2xl border p-5 transition-all duration-200 ${
                      course.isLocked
                        ? 'border-slate-200 bg-slate-50/70 opacity-70 dark:border-white/5 dark:bg-white/[0.01]'
                        : isCompleted
                        ? 'border-emerald-200 bg-emerald-50/30 dark:border-emerald-500/20 dark:bg-emerald-500/5 hover:border-emerald-300 hover:shadow-md'
                        : isInProgress
                        ? 'border-purple-300 bg-purple-50/30 shadow-md shadow-purple-500/5 dark:border-purple-500/30 dark:bg-purple-500/5 hover:border-purple-400'
                        : 'border-slate-200 bg-white hover:border-purple-200 hover:shadow-md dark:border-white/10 dark:bg-white/[0.03]'
                    }`}
                  >
                    {/* Course sequence indicator & main info */}
                    <div className="flex items-start gap-4 flex-1">
                      <div
                        className={`flex h-12 w-12 flex-none items-center justify-center rounded-2xl font-bold text-sm transition-all ${
                          course.isLocked
                            ? 'bg-slate-200 text-slate-500 dark:bg-white/10 dark:text-slate-400'
                            : isCompleted
                            ? 'bg-emerald-500 text-white shadow-md shadow-emerald-500/20'
                            : isInProgress
                            ? 'bg-purple-600 text-white shadow-md shadow-purple-500/25 ring-4 ring-purple-500/20'
                            : 'bg-purple-100 text-purple-700 dark:bg-purple-500/20 dark:text-purple-300'
                        }`}
                      >
                        {course.isLocked ? (
                          <Lock className="h-5 w-5" />
                        ) : isCompleted ? (
                          <CheckCircle2 className="h-6 w-6" />
                        ) : (
                          <span>{index + 1}</span>
                        )}
                      </div>

                      <div className="min-w-0 flex-1 space-y-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="text-[11px] font-mono text-purple-600 dark:text-purple-400 font-semibold">
                            {course.capabilityCode}
                          </span>
                          {course.isRequired && (
                            <span className="rounded-md bg-amber-100 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-amber-800 dark:bg-amber-500/15 dark:text-amber-300">
                              {isEn ? 'Required' : 'Obligatorio'}
                            </span>
                          )}
                          <span className="text-[11px] text-slate-400 dark:text-slate-500">
                            • {course.nodeCount || course.levelCount}{' '}
                            {isEn ? 'Engrams / Sections' : 'Engramas / Secciones'}
                          </span>
                        </div>

                        <h3 className="font-bold text-base text-slate-900 dark:text-white leading-snug">
                          {course.capabilityName}
                        </h3>

                        {/* Status label and node progress */}
                        <div className="flex flex-wrap items-center gap-3 pt-1 text-xs">
                          {course.isLocked ? (
                            <span className="flex items-center gap-1 font-medium text-slate-400 dark:text-slate-500">
                              <Lock className="h-3.5 w-3.5" />
                              {isEn
                                ? 'Locked (complete previous course)'
                                : 'Bloqueado (completa el curso anterior)'}
                            </span>
                          ) : isCompleted ? (
                            <span className="flex items-center gap-1 font-semibold text-emerald-600 dark:text-emerald-400">
                              <CheckCircle2 className="h-3.5 w-3.5" />
                              {isEn ? 'Completed' : 'Completado'} ({course.stats.mastered}/{course.stats.total})
                            </span>
                          ) : isInProgress ? (
                            <span className="flex items-center gap-1 font-semibold text-purple-600 dark:text-purple-400">
                              <PlayCircle className="h-3.5 w-3.5" />
                              {isEn ? 'In Progress' : 'En Progreso'} ({course.stats.mastered}/{course.stats.total})
                            </span>
                          ) : (
                            <span className="flex items-center gap-1 font-medium text-slate-500 dark:text-slate-400">
                              <CircleDashed className="h-3.5 w-3.5" />
                              {isEn ? 'Available to start' : 'Listo para iniciar'}
                            </span>
                          )}

                          {!course.isLocked && course.stats.total > 0 && (
                            <div className="flex items-center gap-2">
                              <div className="h-1.5 w-24 overflow-hidden rounded-full bg-slate-200 dark:bg-white/10">
                                <div
                                  className="h-full rounded-full bg-gradient-to-r from-purple-500 to-indigo-600"
                                  style={{ width: `${percent}%` }}
                                />
                              </div>
                              <span className="text-[11px] font-semibold text-slate-500 dark:text-slate-400">
                                {percent}%
                              </span>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>

                    {/* Action Button */}
                    <div className="sm:self-center shrink-0 pt-2 sm:pt-0">
                      {course.isLocked ? (
                        <button
                          type="button"
                          disabled
                          className="flex items-center gap-1.5 rounded-xl border border-slate-200 bg-slate-100 px-4 py-2 text-xs font-semibold text-slate-400 dark:border-white/5 dark:bg-white/5 dark:text-slate-500 cursor-not-allowed"
                        >
                          <Lock className="h-3.5 w-3.5" />
                          {isEn ? 'Locked' : 'Bloqueado'}
                        </button>
                      ) : (
                        <Link
                          to={course.targetUrl}
                          className={`flex items-center gap-2 rounded-xl px-4 py-2 text-xs font-semibold shadow-xs transition ${
                            isCompleted
                              ? 'bg-emerald-600 text-white hover:bg-emerald-700'
                              : isInProgress
                              ? 'bg-purple-600 text-white shadow-purple-500/20 hover:bg-purple-700'
                              : 'bg-gradient-to-r from-purple-600 to-indigo-600 text-white hover:from-purple-700 hover:to-indigo-700'
                          }`}
                        >
                          <PlayCircle className="h-4 w-4" />
                          {isCompleted
                            ? (isEn ? 'Review Course' : 'Repasar Curso')
                            : isInProgress
                            ? (isEn ? 'Continue Course' : 'Continuar Curso')
                            : (isEn ? 'Start Course' : 'Comenzar Curso')}
                        </Link>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
