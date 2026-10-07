import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, Briefcase, CheckCircle2, ChevronRight, Clock3, Cpu, Handshake, ShieldCheck, Sparkles, X, type LucideIcon } from 'lucide-react';
import { Link } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getJobRole } from '@/features/profile/api/jobRoleApi';
import { getProgramById, getPrograms } from '@/features/programs/api/programsApi';

export function InYourRolePage() {
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === 'en';
  const [activeRequirement, setActiveRequirement] = useState<{ title: string; description: string; href?: string; external?: boolean; available?: boolean; engramProgress?: { mastered: number; total: number } } | null>(null);

  const jobRoleQuery = useQuery({
    queryKey: ['job-role-in-your-role', user?.jobRoleId],
    queryFn: () => getJobRole(user!.jobRoleId!),
    enabled: Boolean(user?.jobRoleId),
  });
  const roleTitle = jobRoleQuery.data?.title || user?.jobRoleTitle || (isEn ? 'Your role' : 'Tu puesto');
  const scheduledProgramQuery = useQuery({
    queryKey: ['scheduled-every-day-program', roleTitle],
    queryFn: async () => {
      const programs = await getPrograms();
      const program = programs.find((item) => item.name === `${roleTitle} · Every day`);
      if (!program) return null;
      const detail = await getProgramById(program.programId);
      let pendingCourses: Array<{ capabilityId: string; capabilityName: string; capabilityCode: string; sortOrder: number }> = [];
      if (detail.requirements) {
        try {
          const metadata = JSON.parse(detail.requirements) as { scheduledRoleSkills?: Array<{ capabilityId: string; capabilityName: string; capabilityCode: string; sortOrder: number }> };
          pendingCourses = (metadata.scheduledRoleSkills ?? []).filter((course) => Boolean(course.capabilityId && course.capabilityName));
        } catch (error) {
          console.error('Could not parse scheduled every-day courses:', error);
        }
      }
      const existingIds = new Set(detail.capabilities.map((course) => course.capabilityId));
      return {
        ...detail,
        capabilities: [
          ...detail.capabilities,
          ...pendingCourses
            .filter((course) => !existingIds.has(course.capabilityId))
            .map((course) => ({
              ...course,
              programCapabilityId: `pending-${course.capabilityId}`,
              subjectCode: null,
              isRequired: true,
              phaseLabel: null,
              objectives: null,
              requirements: null,
              levelCount: 0,
              nodeCount: 0,
              hasCoverImage: false,
              courseType: 'RoleSkill',
            })),
        ].sort((a, b) => a.sortOrder - b.sortOrder),
      };
    },
    enabled: Boolean(roleTitle),
  });

  const scheduledCourses = useMemo(() => (scheduledProgramQuery.data?.capabilities ?? []).map((course) => ({
    name: course.capabilityName,
    description: course.objectives ?? course.requirements ?? null,
    matchedCapabilityId: course.capabilityId,
    matchedCourseType: course.courseType === 'StructuredLearning' ? 'structured-learning' as const : 'graph' as const,
    status: undefined,
  })), [scheduledProgramQuery.data?.capabilities]);

  const technicalCapabilities = useMemo(() => {
    const softNames = new Set((jobRoleQuery.data?.softSkills ?? []).map((name) => name.trim().toLowerCase()));
    return scheduledCourses.filter((course) => !softNames.has(course.name.trim().toLowerCase())).sort(sortReadyFirst);
  }, [jobRoleQuery.data?.softSkills, scheduledCourses]);

  const softCapabilities = useMemo(() => {
    const softNames = new Set((jobRoleQuery.data?.softSkills ?? []).map((name) => name.trim().toLowerCase()));
    return scheduledCourses.filter((course) => softNames.has(course.name.trim().toLowerCase())).sort(sortReadyFirst);
  }, [jobRoleQuery.data?.softSkills, scheduledCourses]);

  const roleProcesses: RequirementMatch[] = [];
  const hrProcesses: RequirementMatch[] = [];

  if (jobRoleQuery.isLoading || scheduledProgramQuery.isLoading) {
    return <LoadingBackdrop title={isEn ? 'Loading your role capabilities...' : 'Cargando las capacidades de tu puesto...'} description={isEn ? 'Loading required skills for your role.' : 'Cargando las habilidades requeridas para tu puesto.'} />;
  }

  const metrics: { label: string; value: string; icon: LucideIcon }[] = [
    { label: isEn ? 'Technical capabilities' : 'Capacidades técnicas', value: String(technicalCapabilities.length), icon: Cpu },
    { label: isEn ? 'Soft capabilities' : 'Capacidades blandas', value: String(softCapabilities.length), icon: Handshake },
    { label: isEn ? 'Department processes' : 'Procesos del puesto', value: String(roleProcesses.length), icon: Briefcase },
    { label: isEn ? 'HR & compliance' : 'Políticas y RRHH', value: String(hrProcesses.length), icon: ShieldCheck },
  ];

  return (
    <div className="min-h-screen bg-[#f5f7fb] pb-16 dark:bg-[#05060a]">
      <main className="mx-auto max-w-6xl px-6 pt-8 sm:pt-12">
        <Link to="/" className="inline-flex items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-blue-600 dark:text-white/60 dark:hover:text-blue-300">
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          {isEn ? 'Back to your journey' : 'Volver a tu recorrido'}
        </Link>

        <section className="mt-6 overflow-hidden rounded-3xl bg-gradient-to-br from-blue-600 via-indigo-600 to-violet-700 p-7 text-white shadow-xl shadow-blue-900/20 sm:p-10">
          <div className="flex flex-wrap items-start justify-between gap-6">
            <div className="max-w-3xl">
              <div className="flex items-center gap-2 text-sm font-semibold text-blue-100">
                <Sparkles className="h-4 w-4" aria-hidden="true" />
                {isEn ? 'After 90 days · Every day' : 'Después de 90 días · Cada día'}
              </div>
              <h1 className="mt-4 text-3xl font-bold tracking-tight sm:text-5xl">{isEn ? 'In Your Role' : 'En tu puesto'}</h1>
              <p className="mt-4 max-w-2xl text-base leading-7 text-blue-50 sm:text-lg">
                {isEn
                  ? 'Grow in your day-to-day: assigned courses, real objectives, and evidence of your progress.'
                  : 'Crece en tu día a día: cursos asignados, objetivos reales y evidencia de tu progreso.'}
              </p>
            </div>
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
              <Briefcase className="h-8 w-8" aria-hidden="true" />
            </div>
          </div>
        </section>

        <section className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
          {metrics.map(({ label, value, icon: MetricIcon }) => (
            <Card key={String(label)} className="border-slate-200/80 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
              <MetricIcon className="h-5 w-5 text-indigo-600 dark:text-indigo-400" aria-hidden="true" />
              <p className="mt-4 text-xs font-semibold uppercase tracking-[0.16em] text-slate-400 dark:text-white/45">{label}</p>
              <p className="mt-2 text-lg font-bold text-slate-950 dark:text-white">{value}</p>
            </Card>
          ))}
        </section>

        {/* Bloque 1: Technical & Soft Skills */}
        <div className="mt-6 grid gap-6 lg:grid-cols-2">
          {/* Card Habilidades Técnicas */}
          <section className="flex flex-col overflow-hidden rounded-3xl border border-sky-200/80 bg-white shadow-md shadow-sky-900/5 dark:border-sky-400/20 dark:bg-white/[0.04]" aria-labelledby="technical-capabilities-title">
            <div className="flex items-center gap-3 border-b border-sky-100 bg-sky-50/60 px-6 py-4 dark:border-white/5 dark:bg-sky-500/10">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-sky-600 text-white shadow-sm">
                <Cpu className="h-5 w-5" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-sky-600 dark:text-sky-300">
                  {isEn ? 'Ongoing Role Capabilities' : 'Capacidades Continuas del Puesto'}
                </p>
                <h2 id="technical-capabilities-title" className="truncate text-sm font-bold text-slate-950 dark:text-white">
                  {isEn ? 'Technical Learning Plan' : 'Plan de Aprendizaje Técnico'}
                </h2>
              </div>
            </div>
            <div className="flex-1 space-y-2.5 p-5">
              {technicalCapabilities.length === 0 ? (
                <p className="text-sm font-medium text-slate-500 dark:text-white/60">
                  {isEn ? 'No technical capabilities have been assigned yet.' : 'Aún no se han asignado capacidades técnicas.'}
                </p>
              ) : (
                technicalCapabilities.map((requirement) => (
                  <RequirementRow key={requirement.name} requirement={requirement} isEn={isEn} accent="sky" onSelect={setActiveRequirement} />
                ))
              )}
            </div>
          </section>

          {/* Card Habilidades Blandas */}
          <section className="flex flex-col overflow-hidden rounded-3xl border border-violet-200/80 bg-white shadow-md shadow-violet-900/5 dark:border-violet-400/20 dark:bg-white/[0.04]" aria-labelledby="soft-capabilities-title">
            <div className="flex items-center gap-3 border-b border-violet-100 bg-violet-50/60 px-6 py-4 dark:border-white/5 dark:bg-violet-500/10">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-violet-600 text-white shadow-sm">
                <Handshake className="h-5 w-5" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-violet-600 dark:text-violet-300">
                  {isEn ? 'Ongoing Leadership & Soft Skills' : 'Liderazgo y Habilidades Blandas'}
                </p>
                <h2 id="soft-capabilities-title" className="truncate text-sm font-bold text-slate-950 dark:text-white">
                  {isEn ? 'Soft Skills Learning Plan' : 'Plan de Habilidades Blandas'}
                </h2>
              </div>
            </div>
            <div className="flex-1 space-y-2.5 p-5">
              {softCapabilities.length === 0 ? (
                <p className="text-sm font-medium text-slate-500 dark:text-white/60">
                  {isEn ? 'No soft capabilities have been assigned yet.' : 'Aún no se han asignado capacidades blandas.'}
                </p>
              ) : (
                softCapabilities.map((requirement) => (
                  <RequirementRow key={requirement.name} requirement={requirement} isEn={isEn} accent="violet" onSelect={setActiveRequirement} />
                ))
              )}
            </div>
          </section>
        </div>

        {/* Bloque 2: Department & HR Processes */}
        {(roleProcesses.length > 0 || hrProcesses.length > 0) && (
          <div className="mt-6 grid gap-6 lg:grid-cols-2">
            {roleProcesses.length > 0 && (
              <section className="flex flex-col overflow-hidden rounded-3xl border border-amber-200/80 bg-white shadow-md shadow-amber-900/5 dark:border-amber-400/20 dark:bg-white/[0.04]">
                <div className="flex items-center gap-3 border-b border-amber-100 bg-amber-50/50 px-6 py-4 dark:border-white/5 dark:bg-amber-500/10">
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-amber-600 text-white shadow-sm">
                    <Briefcase className="h-5 w-5" aria-hidden="true" />
                  </span>
                  <div>
                    <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-amber-700 dark:text-amber-300">
                      {isEn ? 'Role & Department Processes' : 'Procesos del Puesto y Departamento'}
                    </p>
                    <h3 className="text-sm font-bold text-slate-950 dark:text-white">
                      {isEn ? 'Ongoing Operational Workflows' : 'Flujos de Trabajo Continuos'}
                    </h3>
                  </div>
                </div>
                <div className="flex-1 space-y-2.5 p-5">
                  {roleProcesses.map((requirement) => (
                    <RequirementRow key={requirement.name} requirement={requirement} isEn={isEn} accent="amber" onSelect={setActiveRequirement} />
                  ))}
                </div>
              </section>
            )}

            {hrProcesses.length > 0 && (
              <section className="flex flex-col overflow-hidden rounded-3xl border border-emerald-200/80 bg-white shadow-md shadow-emerald-900/5 dark:border-emerald-400/20 dark:bg-white/[0.04]">
                <div className="flex items-center gap-3 border-b border-emerald-100 bg-emerald-50/50 px-6 py-4 dark:border-white/5 dark:bg-emerald-500/10">
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-emerald-600 text-white shadow-sm">
                    <ShieldCheck className="h-5 w-5" aria-hidden="true" />
                  </span>
                  <div>
                    <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-emerald-700 dark:text-emerald-300">
                      {isEn ? 'HR & Corporate Policies' : 'Políticas y Procesos de RRHH'}
                    </p>
                    <h3 className="text-sm font-bold text-slate-950 dark:text-white">
                      {isEn ? 'Compliance & Culture' : 'Cumplimiento y Cultura Corporativa'}
                    </h3>
                  </div>
                </div>
                <div className="flex-1 space-y-2.5 p-5">
                  {hrProcesses.map((requirement) => (
                    <RequirementRow key={requirement.name} requirement={requirement} isEn={isEn} accent="emerald" onSelect={setActiveRequirement} />
                  ))}
                </div>
              </section>
            )}
          </div>
        )}
      </main>

      {activeRequirement && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4"
          role="dialog"
          aria-modal="true"
          aria-labelledby="requirement-modal-title"
          onClick={() => setActiveRequirement(null)}
        >
          <div
            className="w-full max-w-lg rounded-3xl bg-white p-6 shadow-2xl dark:bg-slate-900 sm:p-8"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <h3 id="requirement-modal-title" className="text-xl font-bold text-slate-950 dark:text-white">
                {activeRequirement.title}
              </h3>
              <button
                type="button"
                onClick={() => setActiveRequirement(null)}
                aria-label={isEn ? 'Close' : 'Cerrar'}
                className="shrink-0 rounded-full p-1 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-white/10 dark:hover:text-white"
              >
                <X className="h-5 w-5" aria-hidden="true" />
              </button>
            </div>
            <p className="mt-4 text-sm leading-6 text-slate-600 dark:text-white/70">{activeRequirement.description}</p>
            <div className="mt-5 flex items-center gap-2 text-xs font-semibold">
              {activeRequirement.available ? (
                <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-2.5 py-1 text-emerald-800 dark:bg-emerald-400/15 dark:text-emerald-300">
                  <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />
                  {isEn ? 'Available' : 'Disponible'}
                </span>
              ) : (
                <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-100 px-2.5 py-1 text-amber-800 dark:bg-amber-400/15 dark:text-amber-300">
                  <Clock3 className="h-3.5 w-3.5" aria-hidden="true" />
                  {isEn ? 'Coming soon' : 'Próximamente'}
                </span>
              )}
              <span className="text-slate-400 dark:text-white/40">{isEn ? 'Ongoing, every day' : 'En curso, cada día'}</span>
            </div>
            {activeRequirement.engramProgress && (
              <div className="mt-3">
                <div className="flex items-center justify-between text-xs font-semibold text-slate-500 dark:text-white/55">
                  <span>{isEn ? 'Engrams mastered (tracked in Brain)' : 'Engramas dominados (registrado en Brain)'}</span>
                  <span>{activeRequirement.engramProgress.mastered}/{activeRequirement.engramProgress.total}</span>
                </div>
                <div className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-violet-500 to-indigo-600"
                    style={{ width: `${activeRequirement.engramProgress.total > 0 ? Math.round((activeRequirement.engramProgress.mastered / activeRequirement.engramProgress.total) * 100) : 0}%` }}
                  />
                </div>
              </div>
            )}
            {activeRequirement.available && activeRequirement.href ? (
              activeRequirement.external ? (
                <a
                  href={activeRequirement.href}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-6 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-indigo-600 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-indigo-600/20 transition hover:bg-indigo-700"
                >
                  {isEn ? 'Go to class' : 'Ir a la clase'}
                  <ArrowRight className="h-4 w-4" aria-hidden="true" />
                </a>
              ) : (
                <Link
                  to={activeRequirement.href}
                  onClick={() => setActiveRequirement(null)}
                  className="mt-6 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-indigo-600 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-indigo-600/20 transition hover:bg-indigo-700"
                >
                  {isEn ? 'Go to class' : 'Ir a la clase'}
                  <ArrowRight className="h-4 w-4" aria-hidden="true" />
                </Link>
              )
            ) : (
              <a
                href="#"
                onClick={(event) => event.preventDefault()}
                aria-disabled="true"
                className="mt-6 inline-flex w-full cursor-not-allowed items-center justify-center gap-2 rounded-xl bg-slate-200 px-5 py-3 text-sm font-semibold text-slate-500 dark:bg-white/10 dark:text-white/50"
              >
                {isEn ? 'Go to class · Coming soon' : 'Ir a la clase · Próximamente'}
                <ArrowRight className="h-4 w-4" aria-hidden="true" />
              </a>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

interface RequirementMatch {
  name: string;
  description: string | null;
  matchedCapabilityId: string | null;
  matchedCourseType?: 'structured-learning' | 'graph' | null;
  status?: 'NotStarted' | 'InProgress' | 'Completed';
  engramProgress?: { mastered: number; total: number };
}

/** Ready-to-take lessons (a matched capability already exists) sort ahead of
 * "Coming soon" ones, preserving each group's original relative order. */
function sortReadyFirst(a: RequirementMatch, b: RequirementMatch): number {
  return Number(Boolean(b.matchedCapabilityId)) - Number(Boolean(a.matchedCapabilityId));
}

/** One row in the Technical/Soft Capabilities lists — matches
 * StartInYourRolePage's requiredTraining row styling exactly, parameterized
 * by accent color since the two cards use different color families. */
function RequirementRow({
  requirement,
  isEn,
  accent,
  onSelect,
}: {
  requirement: RequirementMatch;
  isEn: boolean;
  accent: 'sky' | 'violet' | 'amber' | 'emerald';
  onSelect: (requirement: { title: string; description: string; href?: string; available?: boolean; engramProgress?: { mastered: number; total: number } }) => void;
}) {
  const { name, description, matchedCapabilityId, matchedCourseType, status, engramProgress } = requirement;
  const statusLabel = status === 'Completed'
    ? (isEn ? 'Completed' : 'Completado')
    : status === 'InProgress'
      ? (isEn ? 'In progress' : 'En progreso')
      : matchedCapabilityId
        ? (isEn ? 'Not started' : 'Sin empezar')
        : (isEn ? 'Coming soon' : 'Próximamente');
  const isDone = status === 'Completed';
  const engramLabel = engramProgress
    ? (isEn ? `${engramProgress.mastered}/${engramProgress.total} engrams` : `${engramProgress.mastered}/${engramProgress.total} engramas`)
    : null;
  const hoverBorder =
    accent === 'sky'
      ? 'hover:border-sky-300 dark:hover:border-sky-400/40'
      : accent === 'violet'
        ? 'hover:border-violet-300 dark:hover:border-violet-400/40'
        : accent === 'amber'
          ? 'hover:border-amber-300 dark:hover:border-amber-400/40'
          : 'hover:border-emerald-300 dark:hover:border-emerald-400/40';

  return (
    <button
      type="button"
      onClick={() => onSelect({
        title: name,
        description: description
          ?? (isEn
            ? `Understand the practices and standards covered by ${name}.`
            : `Comprende las prácticas y estándares cubiertos por ${name}.`),
        href: matchedCapabilityId
          ? (matchedCourseType === 'graph' ? `/capabilities/${matchedCapabilityId}` : `/structured-learning/${matchedCapabilityId}`)
          : undefined,
        available: Boolean(matchedCapabilityId),
        engramProgress,
      })}
      className={`flex w-full items-center gap-3 rounded-xl border border-slate-200/70 bg-white p-3 text-left transition hover:-translate-y-0.5 hover:shadow-md dark:border-white/10 dark:bg-white/[0.03] ${hoverBorder}`}
    >
      <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${isDone ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-400/15 dark:text-emerald-300' : matchedCapabilityId ? 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/70' : 'bg-amber-100 text-amber-700 dark:bg-amber-400/15 dark:text-amber-300'}`}>
        {isDone ? <CheckCircle2 className="h-4.5 w-4.5" aria-hidden="true" /> : matchedCapabilityId ? <ShieldCheck className="h-4.5 w-4.5" aria-hidden="true" /> : <Clock3 className="h-4.5 w-4.5" aria-hidden="true" />}
      </span>
      <span className="min-w-0 flex-1">
        <span
          className={`block truncate text-sm font-semibold ${
            isDone
              ? 'text-emerald-700 dark:text-emerald-300'
              : matchedCapabilityId
                ? accent === 'sky'
                  ? 'text-sky-700 dark:text-sky-300'
                  : 'text-violet-700 dark:text-violet-300'
                : 'text-slate-900 dark:text-white'
          }`}
        >
          {name}
        </span>
        <span className={`mt-0.5 flex items-center gap-1.5 text-[11px] font-semibold ${isDone ? 'text-emerald-700 dark:text-emerald-300' : matchedCapabilityId ? 'text-slate-500 dark:text-white/55' : 'text-amber-700 dark:text-amber-300'}`}>
          {statusLabel}
          {engramLabel && <span className="text-slate-400 dark:text-white/40">· {engramLabel}</span>}
        </span>
      </span>
      <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
    </button>
  );
}
