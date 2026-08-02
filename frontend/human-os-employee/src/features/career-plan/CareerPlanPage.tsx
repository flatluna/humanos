import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useLocation } from 'react-router';
import {
  Briefcase,
  Building2,
  CheckCircle2,
  Clock3,
  ExternalLink,
  GraduationCap,
  Hammer,
  Hourglass,
  LayoutDashboard,
  PlayCircle,
  ShieldCheck,
  Sparkles,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { getMyCareerPlan, type CareerPlanRoadmapStepDto } from './api/careerPlanApi';

// Program matches still point at Engram (human-os-web, :5178) — Program
// support hasn't been ported into this employee portal yet (see
// EmployeeCareerPlanPage.tsx in human-os-enterprise, the HR-facing
// twin of this page, for the same convention/rationale).
const ENGRAM_APP_URL = 'http://localhost:5178';

function buildEngramProgramUrl(programId: string): string {
  return `${ENGRAM_APP_URL}/programs/${programId}`;
}

type Track = 'technical' | 'soft' | 'department' | 'hr';

interface RoadmapCourse extends CareerPlanRoadmapStepDto {
  track: Track;
}

/** The 4 course categories (2026-08-01, take 3): each is a REAL, separate
 * ROUTE/page — `/capabilities/job-skills`, `/capabilities/soft-skills`,
 * `/capabilities/department-processes`, `/capabilities/hr-processes` (see
 * routes/index.tsx) — not a query-param "tab" on one shared URL. The user
 * explicitly rejected both the earlier #hash-anchor-scroll version AND a
 * `?tab=` query-param version ("lo veo igual... quiero que cada uno tenga
 * su propia página"): they want distinct, bookmarkable/linkable pages,
 * each with its own filters. `CareerPlanPage` is still one shared
 * component (rendered by all 4 routes) — it derives which category is
 * active from the current URL path, and each category page also gets a
 * local status filter (Todos/Completado/En progreso/Pendiente). All 4
 * categories are now real, backend-driven tracks (2026-08-01 "cursos de
 * empresa" feature — department/HR processes roadmaps come from
 * PersonCareerPlanDto.departmentProcessesRoadmap/hrProcessesRoadmap). */
const CATEGORY_TABS = [
  { id: 'job-skills', label: 'Mi Puesto', icon: Briefcase, accent: 'sky' as const },
  { id: 'soft-skills', label: 'Soft Skills', icon: Sparkles, accent: 'violet' as const },
  { id: 'department-processes', label: 'Procesos de Depto.', icon: Building2, accent: 'slate' as const },
  { id: 'hr-processes', label: 'Procesos de RRHH', icon: ShieldCheck, accent: 'slate' as const },
] as const;

type CategoryTabId = (typeof CATEGORY_TABS)[number]['id'];

function isCategoryTabId(value: string | undefined): value is CategoryTabId {
  return CATEGORY_TABS.some((tab) => tab.id === value);
}

/** Status filter shown on each category page ("con filtros" per the user's
 * request) — narrows the visible course list by training status without
 * needing a separate route/page per status. */
const STATUS_FILTERS = [
  { id: 'all', label: 'Todos' },
  { id: 'Pending', label: 'Pendiente' },
  { id: 'InProgress', label: 'En progreso' },
  { id: 'Completed', label: 'Completado' },
] as const;

type StatusFilterId = (typeof STATUS_FILTERS)[number]['id'];

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return `${parts[0]![0]}${parts[parts.length - 1]![0]}`.toUpperCase();
}

/** Small per-card badge showing where a course stands (Completed/InProgress/
 * Pending) — needed now that cards are grouped by CATEGORY (job
 * description / soft skills / dept. / HR processes) instead of by status,
 * so that at-a-glance progress info isn't lost. */
const STATUS_META: Record<string, { label: string; className: string }> = {
  Completed: {
    label: 'Completado',
    className: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  },
  InProgress: {
    label: 'En progreso',
    className: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  },
  Pending: {
    label: 'Pendiente',
    className: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/50',
  },
};

/** "Mi Plan de Capacitación" — the employee's own AI-generated career
 * development roadmap (technical + soft-skill gaps for their job role),
 * with real progress against each step. Ported from human-os-enterprise's
 * `EmployeeCareerPlanPage.tsx` (the HR-facing view of any employee's plan)
 * but scoped to ONLY the signed-in employee's own plan — no personId
 * route param, no "generate a course" action (that's an HR/Capability
 * Studio action, out of scope here), no navigation to other employees'
 * data (see /memories/repo for the privacy rule this mirrors on the HR
 * side: HR can only open this detail view for the demo account). */
export function CareerPlanPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const location = useLocation();
  const [statusFilter, setStatusFilter] = useState<StatusFilterId>('all');

  const pathSegment = location.pathname.split('/').filter(Boolean).pop();
  const activeTab: CategoryTabId = isCategoryTabId(pathSegment) ? pathSegment : 'job-skills';

  const planQuery = useQuery({
    queryKey: ['my-career-plan', personId],
    queryFn: () => getMyCareerPlan(personId),
    enabled: !!personId,
  });

  const plan = planQuery.data;

  const allCourses: RoadmapCourse[] = useMemo(() => {
    if (!plan?.hasPlan) return [];
    return [
      ...plan.technicalRoadmap.map((step) => ({ ...step, track: 'technical' as const })),
      ...plan.softSkillsRoadmap.map((step) => ({ ...step, track: 'soft' as const })),
      ...plan.departmentProcessesRoadmap.map((step) => ({ ...step, track: 'department' as const })),
      ...plan.hrProcessesRoadmap.map((step) => ({ ...step, track: 'hr' as const })),
    ];
  }, [plan]);

  const completed = allCourses.filter((c) => c.trainingStatus === 'Completed');
  const inProgress = allCourses.filter((c) => c.trainingStatus === 'InProgress');
  const pending = allCourses.filter((c) => c.trainingStatus === 'Pending');

  const jobDescriptionCourses = allCourses.filter((c) => c.track === 'technical');
  const softSkillCourses = allCourses.filter((c) => c.track === 'soft');
  const departmentProcessCourses = allCourses.filter((c) => c.track === 'department');
  const hrProcessCourses = allCourses.filter((c) => c.track === 'hr');

  const total = allCourses.length;
  const completionRate = total === 0 ? 0 : Math.round((completed.length / total) * 100);

  const coursesByTab: Record<CategoryTabId, RoadmapCourse[]> = {
    'job-skills': jobDescriptionCourses,
    'soft-skills': softSkillCourses,
    'department-processes': departmentProcessCourses,
    'hr-processes': hrProcessCourses,
  };

  const emptyMessageByTab: Record<CategoryTabId, string> = {
    'job-skills': 'No hay cursos de tu descripción de puesto todavía.',
    'soft-skills': 'No hay cursos de soft skills todavía.',
    'department-processes': 'No hay cursos de procesos de tu departamento todavía.',
    'hr-processes': 'No hay cursos de procesos de Recursos Humanos todavía.',
  };

  const activeCourses = coursesByTab[activeTab];
  const filteredCourses =
    statusFilter === 'all' ? activeCourses : activeCourses.filter((c) => c.trainingStatus === statusFilter);

  return (
    <div className="mx-auto max-w-6xl space-y-6 p-4 pb-16 sm:p-8">
      {/* Gradient header banner — mirrors the HR-facing dashboard at
       * EmployeeCareerPlanPage.tsx (human-os-enterprise) 1:1, per explicit
       * user request ("ese banner no lo veo aquí"). */}
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-indigo-600 via-indigo-500 to-violet-600 p-6 text-white shadow-lg shadow-indigo-500/20 dark:border-white/10 sm:p-8">
        <div className="flex flex-col gap-6 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 text-xl font-bold ring-1 ring-white/25 backdrop-blur">
              {initialsOf(user?.name ?? 'Empleado')}
            </span>
            <div>
              <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-white/70">
                <LayoutDashboard className="h-3.5 w-3.5" />
                Dashboard de capacitación
              </p>
              <h1 className="mt-1 text-2xl font-semibold">{user?.name ?? 'Empleado'}</h1>
              <p className="mt-0.5 text-sm text-white/80">
                {plan?.jobRoleTitle ?? 'Sin puesto asignado'}
              </p>
            </div>
          </div>

          {total > 0 && (
            <div className="flex items-center gap-4 rounded-2xl bg-white/10 px-5 py-4 ring-1 ring-white/20 backdrop-blur">
              <div className="relative flex h-16 w-16 shrink-0 items-center justify-center">
                <svg viewBox="0 0 36 36" className="h-16 w-16 -rotate-90">
                  <circle cx="18" cy="18" r="15.5" fill="none" stroke="rgba(255,255,255,0.2)" strokeWidth="3" />
                  <circle
                    cx="18"
                    cy="18"
                    r="15.5"
                    fill="none"
                    stroke="white"
                    strokeWidth="3"
                    strokeDasharray={`${completionRate * 0.974} 1000`}
                    strokeLinecap="round"
                  />
                </svg>
                <span className="absolute text-sm font-bold">{completionRate}%</span>
              </div>
              <div className="text-sm">
                <p className="font-semibold">Progreso general</p>
                <p className="text-white/70">
                  {completed.length} de {total} entrenamientos completados
                </p>
              </div>
            </div>
          )}
        </div>
      </div>

      {planQuery.isLoading && (
        <LoadingBackdrop title="Cargando tu plan..." description="Reuniendo tu ruta de capacitación recomendada." />
      )}

      {planQuery.isSuccess && !plan?.hasPlan && (
        <Card className="flex flex-col items-center gap-3 border-dashed p-10 text-center">
          <span className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-100 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
            <GraduationCap className="h-6 w-6" />
          </span>
          <p className="text-sm text-slate-500 dark:text-white/40">
            Todavía no tienes un plan de capacitación generado. Tu gerente o RRHH puede generarlo por ti.
          </p>
        </Card>
      )}

      {plan?.hasPlan && (
        <>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatCard label="Total" value={total} icon={LayoutDashboard} accent="slate" />
            <StatCard label="Tomados" value={completed.length} icon={CheckCircle2} accent="brand" />
            <StatCard label="En proceso" value={inProgress.length} icon={Clock3} accent="amber" />
            <StatCard label="Pendientes" value={pending.length} icon={Hourglass} accent="rose" />
          </div>

          {plan.overallRationale && (
            <p className="rounded-2xl border border-brand-200 bg-brand-50/60 p-4 text-sm text-brand-900 dark:border-brand-500/20 dark:bg-brand-500/10 dark:text-brand-200">
              {plan.overallRationale}
            </p>
          )}

          <div className="flex flex-wrap items-center gap-1.5 rounded-2xl border border-slate-200 bg-white p-1.5 dark:border-white/10 dark:bg-white/5">
            {CATEGORY_TABS.map((tab) => {
              const TabIcon = tab.icon;
              const isActive = tab.id === activeTab;
              return (
                <Link
                  key={tab.id}
                  to={`/capabilities/${tab.id}`}
                  className={`flex items-center gap-1.5 rounded-xl px-3 py-1.5 text-xs font-semibold transition ${
                    isActive
                      ? ACCENT_STYLES[tab.accent].badge
                      : 'text-slate-500 hover:bg-slate-100 dark:text-white/50 dark:hover:bg-white/5'
                  }`}
                >
                  <TabIcon className="h-3.5 w-3.5" />
                  {tab.label}
                  {coursesByTab[tab.id].length > 0 && (
                    <span className="rounded-full bg-black/10 px-1.5 py-0.5 text-[10px] dark:bg-white/10">
                      {coursesByTab[tab.id].length}
                    </span>
                  )}
                </Link>
              );
            })}
          </div>

          <div className="flex flex-wrap items-center gap-1.5">
            {STATUS_FILTERS.map((filter) => (
              <button
                key={filter.id}
                type="button"
                onClick={() => setStatusFilter(filter.id)}
                className={`rounded-full border px-2.5 py-1 text-[11px] font-medium transition ${
                  statusFilter === filter.id
                    ? 'border-brand-500 bg-brand-500 text-white'
                    : 'border-slate-200 text-slate-500 hover:bg-slate-100 dark:border-white/10 dark:text-white/50 dark:hover:bg-white/5'
                }`}
              >
                {filter.label}
              </button>
            ))}
          </div>

          <CourseSection
            title={CATEGORY_TABS.find((t) => t.id === activeTab)!.label}
            icon={CATEGORY_TABS.find((t) => t.id === activeTab)!.icon}
            accent={CATEGORY_TABS.find((t) => t.id === activeTab)!.accent}
            courses={filteredCourses}
            emptyMessage={
              activeCourses.length === 0
                ? emptyMessageByTab[activeTab]
                : 'Ningún curso coincide con este filtro.'
            }
          />
        </>
      )}
    </div>
  );
}

const ACCENT_STYLES = {
  slate: {
    icon: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/70',
    heading: 'text-slate-600 dark:text-white/70',
    badge: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/60',
  },
  brand: {
    icon: 'bg-brand-100 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300',
    heading: 'text-brand-600 dark:text-brand-300',
    badge: 'bg-brand-100 text-brand-700 dark:bg-brand-500/15 dark:text-brand-300',
  },
  amber: {
    icon: 'bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300',
    heading: 'text-amber-600 dark:text-amber-300',
    badge: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  },
  rose: {
    icon: 'bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300',
    heading: 'text-rose-500 dark:text-rose-300',
    badge: 'bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300',
  },
  sky: {
    icon: 'bg-sky-100 text-sky-600 dark:bg-sky-500/15 dark:text-sky-300',
    heading: 'text-sky-600 dark:text-sky-300',
    badge: 'bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300',
  },
  violet: {
    icon: 'bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300',
    heading: 'text-violet-600 dark:text-violet-300',
    badge: 'bg-violet-100 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300',
  },
} as const;

type Accent = keyof typeof ACCENT_STYLES;

interface StatCardProps {
  label: string;
  value: number;
  icon: typeof CheckCircle2;
  accent: Accent;
}

function StatCard({ label, value, icon: Icon, accent }: StatCardProps) {
  const styles = ACCENT_STYLES[accent];
  return (
    <Card className="p-4">
      <div className="flex items-center gap-3">
        <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl ${styles.icon}`}>
          <Icon className="h-4.5 w-4.5" />
        </span>
        <div>
          <p className="text-2xl font-semibold text-slate-900 dark:text-white">{value}</p>
          <p className="text-xs text-slate-400 dark:text-white/40">{label}</p>
        </div>
      </div>
    </Card>
  );
}

interface CourseSectionProps {
  title: string;
  icon: typeof CheckCircle2;
  accent: Accent;
  courses: RoadmapCourse[];
  emptyMessage: string;
}

function CourseSection({ title, icon: Icon, accent, courses, emptyMessage }: CourseSectionProps) {
  const styles = ACCENT_STYLES[accent];

  return (
    <div>
      <h2 className={`flex items-center gap-2 text-sm font-semibold ${styles.heading}`}>
        <Icon className="h-4 w-4" />
        {title}
        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-500 dark:bg-white/10 dark:text-white/50">
          {courses.length}
        </span>
      </h2>

      {courses.length === 0 ? (
        <p className="mt-2 rounded-xl border border-dashed border-slate-200 p-4 text-sm text-slate-400 dark:border-white/10 dark:text-white/40">
          {emptyMessage}
        </p>
      ) : (
        <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {courses.map((course) => (
            <div
              key={`${course.track}-${course.order}-${course.title}`}
              className="flex flex-col gap-2 rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5"
            >
              <div className="flex items-center justify-between gap-2">
                <span
                  className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${
                    STATUS_META[course.trainingStatus]?.className ?? STATUS_META.Pending!.className
                  }`}
                >
                  {STATUS_META[course.trainingStatus]?.label ?? course.trainingStatus}
                </span>
                <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${styles.badge}`}>
                  #{course.order}
                </span>
              </div>

              <p className="text-sm font-semibold text-slate-900 dark:text-white">{course.title}</p>
              <p className="text-xs text-slate-400 dark:text-white/40">Cierra: {course.skill}</p>
              <p className="text-sm text-slate-600 dark:text-white/60">{course.description}</p>
              {(course.matchedProgramName || course.matchedCapabilityName) && (
                <p className="text-xs font-medium text-emerald-600 dark:text-emerald-400">
                  Curso en catálogo: {course.matchedProgramName ?? course.matchedCapabilityName}
                </p>
              )}

              {course.matchedProgramId ? (
                <a
                  href={buildEngramProgramUrl(course.matchedProgramId)}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-1 inline-flex w-fit items-center gap-1.5 rounded-full bg-emerald-600 px-2.5 py-1 text-xs font-medium text-white transition hover:bg-emerald-500"
                >
                  <PlayCircle className="h-3 w-3" />
                  Abrir en Engram
                  <ExternalLink className="h-3 w-3" />
                </a>
              ) : course.matchedCapabilityId ? (
                <Link
                  to={`/capabilities/${course.matchedCapabilityId}`}
                  className="mt-1 inline-flex w-fit items-center gap-1.5 rounded-full bg-emerald-600 px-2.5 py-1 text-xs font-medium text-white transition hover:bg-emerald-500"
                >
                  <PlayCircle className="h-3 w-3" />
                  Tomar el curso
                </Link>
              ) : (
                <span className="mt-1 inline-flex w-fit items-center gap-1.5 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-500 dark:bg-white/10 dark:text-white/50">
                  <Hammer className="h-3 w-3" />
                  En proceso / Desarrollo
                </span>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
