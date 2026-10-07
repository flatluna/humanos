import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, Briefcase, BriefcaseBusiness, CheckCircle2, ChevronRight, Clock3, Cpu, Handshake, ShieldCheck, Sparkles, X, type LucideIcon } from 'lucide-react';
import { Link } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getMyCareerPlan } from '@/features/career-plan/api/careerPlanApi';
import { getJobRole } from '@/features/profile/api/jobRoleApi';
import { getPerson } from '@/features/profile/api/personProfileApi';
import { getSkills } from '@/features/capabilities/api/skillsApi';
import { getEnrolledCapabilities } from '@/features/capabilities/api/enrolledCapabilitiesApi';
import { getStructuredLearningDocuments } from '@/features/capabilities/api/structuredLearningRuntimeApi';
import { getProgramById, getPrograms } from '@/features/programs/api/programsApi';

interface RequirementMatch {
  name: string;
  description: string | null;
  matchedCapabilityId: string | null;
  matchedCourseType?: 'structured-learning' | 'graph' | null;
  status?: 'NotStarted' | 'InProgress' | 'Completed';
  engramProgress?: { mastered: number; total: number };
}

function sortReadyFirst(a: RequirementMatch, b: RequirementMatch): number {
  return Number(Boolean(b.matchedCapabilityId)) - Number(Boolean(a.matchedCapabilityId));
}

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
                  : accent === 'violet'
                    ? 'text-violet-700 dark:text-violet-300'
                    : 'text-amber-700 dark:text-amber-300'
                : 'text-slate-900 dark:text-white'
          }`}
        >
          {name}
        </span>
        <span
          className={`mt-0.5 flex items-center gap-1.5 text-[11px] font-semibold ${
            isDone
              ? 'text-emerald-700 dark:text-emerald-300'
              : matchedCapabilityId
                ? 'text-slate-500 dark:text-white/55'
                : 'text-amber-700 dark:text-amber-300'
          }`}
        >
          {statusLabel}
          {engramLabel && <span className="text-slate-400 dark:text-white/40">· {engramLabel}</span>}
        </span>
      </span>
      <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
    </button>
  );
}

export function StartInYourRolePage() {
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === 'en';
  const personId = user?.personId ?? '';
  const [activeRequirement, setActiveRequirement] = useState<{ title: string; description: string; href?: string; external?: boolean; available?: boolean; engramProgress?: { mastered: number; total: number } } | null>(null);
  const planQuery = useQuery({
    queryKey: ['my-career-plan-start-in-your-role', personId],
    queryFn: () => getMyCareerPlan(personId),
    enabled: Boolean(personId),
  });
  const roleQuery = useQuery({
    queryKey: ['my-job-role-start-in-your-role', user?.jobRoleId],
    queryFn: () => getJobRole(user!.jobRoleId!),
    enabled: Boolean(user?.jobRoleId),
  });
  const personQuery = useQuery({
    queryKey: ['person-start-in-your-role', personId],
    queryFn: () => getPerson(personId),
    enabled: Boolean(personId),
  });
  const skillsQuery = useQuery({
    queryKey: ['skills', user?.tenantId, 'start-in-your-role'],
    queryFn: () => getSkills(user!.tenantId!),
    enabled: Boolean(user?.tenantId),
  });
  const enrolledQuery = useQuery({
    queryKey: ['enrolled-capabilities', personId, 'start-in-your-role'],
    queryFn: () => getEnrolledCapabilities(personId),
    enabled: Boolean(personId),
  });

  const plan = planQuery.data;
  const effectiveJobRoleId = user?.jobRoleId || plan?.jobRoleId || personQuery.data?.jobRoleId;
  const effectiveRoleQuery = useQuery({
    queryKey: ['my-job-role-start-in-your-role-effective', effectiveJobRoleId],
    queryFn: () => getJobRole(effectiveJobRoleId!),
    enabled: Boolean(effectiveJobRoleId),
  });
  const currentRole = effectiveRoleQuery.data ?? roleQuery.data;

  const roleTitle = currentRole?.title || plan?.jobRoleTitle || user?.jobRoleTitle || (isEn ? 'Your role' : 'Tu puesto');
  const scheduledProgramQuery = useQuery({
    queryKey: ['scheduled-first-90-days-program', roleTitle],
    queryFn: async () => {
      const programs = await getPrograms();
      const program = programs.find((item) => item.name === `${roleTitle} · First 90 days`);
      if (!program) return null;
      const detail = await getProgramById(program.programId);
      let pendingCourses: Array<{ capabilityId: string; capabilityName: string; capabilityCode: string; sortOrder: number }> = [];
      if (detail.requirements) {
        try {
          const metadata = JSON.parse(detail.requirements) as { scheduledRoleSkills?: Array<{ capabilityId: string; capabilityName: string; capabilityCode: string; sortOrder: number }> };
          pendingCourses = (metadata.scheduledRoleSkills ?? []).filter((course) =>
            Boolean(course.capabilityId && course.capabilityName),
          );
        } catch (error) {
          console.error('Could not parse scheduled first-90-days courses:', error);
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
  const structuredDocumentsQuery = useQuery({
    queryKey: ['structured-learning-catalog-start-in-your-role'],
    queryFn: getStructuredLearningDocuments,
  });
  const dueDate = useMemo(() => {
    const createdDate = personQuery.data?.createdDate;
    if (!createdDate) return null;
    const date = new Date(createdDate);
    if (Number.isNaN(date.getTime())) return null;
    date.setDate(date.getDate() + 90);
    return new Intl.DateTimeFormat(isEn ? 'en-US' : 'es-MX', { dateStyle: 'medium' }).format(date);
  }, [isEn, personQuery.data?.createdDate]);
  const dueDateLabel = dueDate
    ? (isEn ? `Due by ${dueDate}` : `Fecha límite: ${dueDate}`)
    : (isEn ? 'Due within your first 90 days' : 'Fecha límite dentro de tus primeros 90 días');

  const matchRequirement = useMemo(() => {
    const enrolledByCapabilityId = new Map((enrolledQuery.data ?? []).map((item) => [item.capabilityId, item]));
    const skillsByLowerName = new Map((skillsQuery.data ?? []).map((skill) => [skill.name.trim().toLowerCase(), skill]));
    return (name: string) => {
      const skill = skillsByLowerName.get(name.trim().toLowerCase());
      const enrolled = skill?.matchedCapabilityId ? enrolledByCapabilityId.get(skill.matchedCapabilityId) : undefined;
      return {
        name,
        description: skill?.description ?? null,
        matchedCapabilityId: skill?.matchedCapabilityId ?? null,
        matchedCourseType: skill?.matchedCourseType ?? null,
        status: enrolled?.status,
        engramProgress: enrolled ? { mastered: enrolled.masteredNodes, total: enrolled.totalNodes } : undefined,
      };
    };
  }, [enrolledQuery.data, skillsQuery.data]);

  const technicalFirst90Days = useMemo(() => {
    const softNames = new Set((currentRole?.softSkills ?? []).map((name) => name.trim().toLowerCase()));
    return (scheduledProgramQuery.data?.capabilities ?? [])
      .filter((course) => !softNames.has(course.capabilityName.trim().toLowerCase()))
      .map((course) => {
        const structuredDocument = structuredDocumentsQuery.data?.find((document) => document.documentName.trim().toLowerCase() === course.capabilityName.trim().toLowerCase());
        const isStructuredLearning = course.courseType === 'StructuredLearning' || Boolean(structuredDocument);
        return {
          name: course.capabilityName,
          description: course.objectives ?? course.requirements ?? null,
          matchedCapabilityId: structuredDocument?.structuredLearningDocumentId ?? (isStructuredLearning ? course.capabilityId : null),
          matchedCourseType: isStructuredLearning ? 'structured-learning' as const : 'graph' as const,
          status: undefined,
        };
      })
      .sort(sortReadyFirst);
  }, [currentRole?.softSkills, scheduledProgramQuery.data?.capabilities, structuredDocumentsQuery.data]);

  const softSkillsFirst90Days = useMemo(() => {
    const softNames = new Set((currentRole?.softSkills ?? []).map((name) => name.trim().toLowerCase()));
    return (scheduledProgramQuery.data?.capabilities ?? [])
      .filter((course) => softNames.has(course.capabilityName.trim().toLowerCase()))
      .map((course) => {
        const structuredDocument = structuredDocumentsQuery.data?.find((document) => document.documentName.trim().toLowerCase() === course.capabilityName.trim().toLowerCase());
        const isStructuredLearning = course.courseType === 'StructuredLearning' || Boolean(structuredDocument);
        return {
          name: course.capabilityName,
          description: course.objectives ?? course.requirements ?? null,
          matchedCapabilityId: structuredDocument?.structuredLearningDocumentId ?? (isStructuredLearning ? course.capabilityId : null),
          matchedCourseType: isStructuredLearning ? 'structured-learning' as const : 'graph' as const,
          status: undefined,
        };
      })
      .sort(sortReadyFirst);
  }, [currentRole?.softSkills, scheduledProgramQuery.data?.capabilities, structuredDocumentsQuery.data]);

  const roleProcessesFirst90Days = useMemo(() => {
    const selected = currentRole?.startInYourRoleDepartmentProcesses ?? [];
    return selected.map(matchRequirement).sort(sortReadyFirst);
  }, [currentRole?.startInYourRoleDepartmentProcesses, matchRequirement]);

  const hrProcessesFirst90Days = useMemo(() => {
    const selected = currentRole?.startInYourRoleHrProcesses ?? [];
    return selected.map(matchRequirement).sort(sortReadyFirst);
  }, [currentRole?.startInYourRoleHrProcesses, matchRequirement]);

  if (planQuery.isLoading || effectiveRoleQuery.isLoading || scheduledProgramQuery.isLoading) {
    return <LoadingBackdrop title={isEn ? 'Preparing your first step...' : 'Preparando tu primer paso...'} description={isEn ? 'Loading your role and available courses.' : 'Cargando tu puesto y cursos disponibles.'} />;
  }

  const metrics: { label: string; value: string; icon: LucideIcon }[] = [
    { label: isEn ? 'Role' : 'Puesto', value: roleTitle, icon: BriefcaseBusiness },
    { label: isEn ? 'Technical plan (90d)' : 'Plan técnico (90d)', value: String(technicalFirst90Days.length), icon: Cpu },
    { label: isEn ? 'Soft skills plan (90d)' : 'Plan habilidades blandas (90d)', value: String(softSkillsFirst90Days.length), icon: Handshake },
  ];

  return (
    <div className="min-h-screen bg-[#f5f7fb] pb-16 dark:bg-[#05060a]">
      <main className="mx-auto max-w-6xl px-6 pt-8 sm:pt-12">
        <Link to="/" className="inline-flex items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-blue-600 dark:text-white/60 dark:hover:text-blue-300">
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          {isEn ? 'Back to your journey' : 'Volver a tu recorrido'}
        </Link>

        <section className="mt-6 overflow-hidden rounded-3xl bg-gradient-to-br from-sky-600 via-blue-600 to-indigo-700 p-7 text-white shadow-xl shadow-blue-900/20 sm:p-10">
          <div className="flex flex-wrap items-start justify-between gap-6">
            <div className="max-w-3xl">
              <div className="flex items-center gap-2 text-sm font-semibold text-blue-100">
                <Sparkles className="h-4 w-4" aria-hidden="true" />
                {isEn ? 'Your first 90 days' : 'Tus primeros 90 días'}
              </div>
              <h1 className="mt-4 text-3xl font-bold tracking-tight sm:text-5xl">{isEn ? 'Start in Your Role' : 'Comienza en tu rol'}</h1>
              <p className="mt-4 max-w-2xl text-base leading-7 text-blue-50 sm:text-lg">
                {isEn
                  ? 'Get to know the company, complete your essential training, and begin developing the knowledge required for your role.'
                  : 'Conoce la empresa, completa tu capacitación esencial y comienza a desarrollar los conocimientos que requiere tu puesto.'}
              </p>
            </div>
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
              <BriefcaseBusiness className="h-8 w-8" aria-hidden="true" />
            </div>
          </div>
        </section>

        <section className="mt-6 grid gap-4 sm:grid-cols-3">
          {metrics.map(({ label, value, icon: MetricIcon }) => {
            return (
              <Card key={String(label)} className="border-slate-200/80 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
                <MetricIcon className="h-5 w-5 text-blue-600 dark:text-blue-300" aria-hidden="true" />
                <p className="mt-4 text-xs font-semibold uppercase tracking-[0.16em] text-slate-400 dark:text-white/45">{label}</p>
                <p className="mt-2 text-lg font-bold text-slate-950 dark:text-white">{value}</p>
              </Card>
            );
          })}
        </section>

        <div className="mt-6 grid gap-6 lg:grid-cols-2">
          {/* Technical Plan Card for First 90 Days */}
          <section className="flex flex-col overflow-hidden rounded-3xl border border-sky-200/80 bg-white shadow-md shadow-sky-900/5 dark:border-sky-400/20 dark:bg-white/[0.04]" aria-labelledby="technical-plan-title">
            <div className="flex items-center justify-between border-b border-sky-100 bg-gradient-to-r from-sky-50 to-blue-50/50 px-6 py-5 dark:border-white/5 dark:from-sky-500/10 dark:to-blue-500/10">
              <div className="flex items-center gap-3">
                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-sky-600 text-white shadow-md shadow-sky-600/20">
                  <Cpu className="h-6 w-6" aria-hidden="true" />
                </span>
                <div>
                  <div className="flex items-center gap-2">
                    <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-sky-700 dark:text-sky-300">
                      {isEn ? 'First 90 Days Plan' : 'Plan Primeros 90 Días'}
                    </p>
                    <span className="rounded-full bg-sky-200/60 px-2 py-0.5 text-[10px] font-bold text-sky-800 dark:bg-sky-400/20 dark:text-sky-200">
                      {technicalFirst90Days.length}
                    </span>
                  </div>
                  <h2 id="technical-plan-title" className="text-base font-bold text-slate-950 dark:text-white">
                    {isEn ? 'Technical Learning Plan' : 'Plan de Aprendizaje Técnico'}
                  </h2>
                </div>
              </div>
            </div>
            <div className="flex-1 space-y-2.5 p-5">
              {technicalFirst90Days.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-slate-200 p-8 text-center text-xs text-slate-500 dark:border-white/10 dark:text-white/50">
                  <Cpu className="mx-auto mb-2 h-7 w-7 text-slate-400" />
                  <p className="font-semibold">{isEn ? 'No technical requirements selected for first 90 days.' : 'Sin requerimientos técnicos seleccionados para los primeros 90 días.'}</p>
                </div>
              ) : (
                technicalFirst90Days.map((requirement) => (
                  <RequirementRow key={requirement.name} requirement={requirement} isEn={isEn} accent="sky" onSelect={setActiveRequirement} />
                ))
              )}
            </div>
          </section>

          {/* Soft Learning Plan Card for First 90 Days */}
          <section className="flex flex-col overflow-hidden rounded-3xl border border-violet-200/80 bg-white shadow-md shadow-violet-900/5 dark:border-violet-400/20 dark:bg-white/[0.04]" aria-labelledby="soft-plan-title">
            <div className="flex items-center justify-between border-b border-violet-100 bg-gradient-to-r from-violet-50 to-purple-50/50 px-6 py-5 dark:border-white/5 dark:from-violet-500/10 dark:to-purple-500/10">
              <div className="flex items-center gap-3">
                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-violet-600 text-white shadow-md shadow-violet-600/20">
                  <Handshake className="h-6 w-6" aria-hidden="true" />
                </span>
                <div>
                  <div className="flex items-center gap-2">
                    <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-violet-700 dark:text-violet-300">
                      {isEn ? 'First 90 Days Plan' : 'Plan Primeros 90 Días'}
                    </p>
                    <span className="rounded-full bg-violet-200/60 px-2 py-0.5 text-[10px] font-bold text-violet-800 dark:bg-violet-400/20 dark:text-violet-200">
                      {softSkillsFirst90Days.length}
                    </span>
                  </div>
                  <h2 id="soft-plan-title" className="text-base font-bold text-slate-950 dark:text-white">
                    {isEn ? 'Soft Skills Learning Plan' : 'Plan de Habilidades Blandas'}
                  </h2>
                </div>
              </div>
            </div>
            <div className="flex-1 space-y-2.5 p-5">
              {softSkillsFirst90Days.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-slate-200 p-8 text-center text-xs text-slate-500 dark:border-white/10 dark:text-white/50">
                  <Handshake className="mx-auto mb-2 h-7 w-7 text-slate-400" />
                  <p className="font-semibold">{isEn ? 'No soft skills selected for first 90 days.' : 'Sin habilidades blandas seleccionadas para los primeros 90 días.'}</p>
                </div>
              ) : (
                softSkillsFirst90Days.map((requirement) => (
                  <RequirementRow key={requirement.name} requirement={requirement} isEn={isEn} accent="violet" onSelect={setActiveRequirement} />
                ))
              )}
            </div>
          </section>
        </div>

        {/* Process and HR Policies if configured in StartInYourRole */}
        {(roleProcessesFirst90Days.length > 0 || hrProcessesFirst90Days.length > 0) && (
          <div className="mt-6 grid gap-6 lg:grid-cols-2">
            {roleProcessesFirst90Days.length > 0 && (
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
                      {isEn ? 'Essential Operational Workflows' : 'Flujos de Trabajo Operativos'}
                    </h3>
                  </div>
                </div>
                <div className="flex-1 space-y-2.5 p-5">
                  {roleProcessesFirst90Days.map((requirement) => (
                    <RequirementRow key={requirement.name} requirement={requirement} isEn={isEn} accent="amber" onSelect={setActiveRequirement} />
                  ))}
                </div>
              </section>
            )}

            {hrProcessesFirst90Days.length > 0 && (
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
                      {isEn ? 'Company Standards' : 'Estándares y Políticas de la Empresa'}
                    </h3>
                  </div>
                </div>
                <div className="flex-1 space-y-2.5 p-5">
                  {hrProcessesFirst90Days.map((requirement) => (
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
              <span className="text-slate-400 dark:text-white/40">{dueDateLabel}</span>
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
