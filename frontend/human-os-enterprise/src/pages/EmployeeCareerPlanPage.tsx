import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router';
import {
  ArrowLeft,
  BookOpen,
  Briefcase,
  Building2,
  CheckCircle2,
  Clock3,
  ExternalLink,
  GraduationCap,
  Hourglass,
  LayoutDashboard,
  PlayCircle,
  ShieldCheck,
  Sparkles,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  getPersonCareerPlan,
  getTenantPeople,
  type CareerPlanRoadmapStepDto,
} from '@/api/orgStructureApi';

// Capability Studio (:3002) is the AUTHORING app — where HR/designers
// generate brand-new courses. Matching a roadmap step to a real course is
// fully automatic (see PersonCareerPlanService.AutoMatchCapabilitiesAsync
// on the backend) — HR never manually links anything. A roadmap step
// shows exactly one action: generate it (no real course exists yet), or
// take it.
//
// "Taking" a course does NOT send employees to Engram (human-os-web,
// :5178) — that app is branded/aimed at independent consumer students,
// which the user explicitly rejected for the corporate audience. Instead:
//  - standalone Capability matches open in human-os-employee (:5180), a
//    THIRD, separate app built specifically for company employees (same
//    ported runtime UI, own shell/branding, own CIAM login) — see
//    /memories/repo/multi-frontend-app-landscape.md.
//  - Program matches still point at Engram for now — Program support
//    hasn't been ported to human-os-employee yet (no employee career step
//    has matched a Program so far; revisit if/when one does).
const CAPABILITY_STUDIO_URL = 'http://localhost:3002';
const ENGRAM_APP_URL = 'http://localhost:5178';
const EMPLOYEE_PORTAL_URL = 'http://localhost:5180';

function buildGenerateCourseUrl(course: Pick<CareerPlanRoadmapStepDto, 'title' | 'description'>): string {
  const params = new URLSearchParams();
  if (course.title) params.set('name', course.title);
  if (course.description) params.set('description', course.description);
  return `${CAPABILITY_STUDIO_URL}/new?${params.toString()}`;
}

function buildEngramProgramUrl(programId: string): string {
  return `${ENGRAM_APP_URL}/programs/${programId}`;
}

function buildEmployeePortalCapabilityUrl(capabilityId: string): string {
  return `${EMPLOYEE_PORTAL_URL}/capabilities/${capabilityId}`;
}

const ORG_ROLE_LABELS: Record<string, string> = {
  Employee: 'Empleado',
  Manager: 'Gerente',
  HRAdmin: 'Administrador RRHH',
};

// PRIVACY: mirrors the same restriction applied to the "Plan de
// capacitación" button in PeoplePage.tsx — an employee's training
// progress is private, so this detail route is only allowed for the
// seeded demo account until a real permission model exists. Guarded here
// too (not just by hiding the button) so a direct URL visit can't bypass it.
const DEMO_CAREER_PLAN_PERSON_ID = '562cb73a-414b-47a8-957e-9f1950ae9622';

type Track = 'technical' | 'soft' | 'department' | 'hr';

interface RoadmapCourse extends CareerPlanRoadmapStepDto {
  track: Track;
}

/** The same 4 course categories shown as separate pages in the employee
 * portal (human-os-employee's CareerPlanPage.tsx — see /memories/repo).
 * This HR-facing detail view is a single read-only overview page (no
 * per-employee routing needed here), so all 4 categories are shown
 * stacked with their real titles instead of split into separate routes —
 * HR wants to see everything at a glance for one employee. All 4
 * categories are now real, backend-driven tracks (2026-08-01 "cursos de
 * empresa" feature). */
const CATEGORY_META = {
  jobSkills: { title: 'Mi Puesto', icon: Briefcase, accent: 'sky' as const },
  softSkills: { title: 'Soft Skills', icon: Sparkles, accent: 'violet' as const },
  departmentProcesses: { title: 'Procesos de Depto.', icon: Building2, accent: 'slate' as const },
  hrProcesses: { title: 'Procesos de RRHH', icon: ShieldCheck, accent: 'slate' as const },
};

/** Per-card status badge (Completado/En progreso/Pendiente) — needed now
 * that cards are grouped by CATEGORY instead of by status, so at-a-glance
 * progress info isn't lost (mirrors STATUS_META in the employee portal's
 * CareerPlanPage.tsx). */
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

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return `${parts[0]![0]}${parts[parts.length - 1]![0]}`.toUpperCase();
}

export function EmployeeCareerPlanPage() {
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const navigate = useNavigate();
  const params = useParams<{ personId: string }>();
  const personId = params.personId!;

  const peopleQuery = useQuery({
    queryKey: ['tenant-people', tenantId],
    queryFn: () => getTenantPeople(tenantId),
  });

  const person = useMemo(() => peopleQuery.data?.find((p) => p.personId === personId), [peopleQuery.data, personId]);

  const planQuery = useQuery({
    queryKey: ['person-career-plan', personId],
    queryFn: () => getPersonCareerPlan(personId),
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

  const jobSkillsCourses = allCourses.filter((c) => c.track === 'technical');
  const softSkillsCourses = allCourses.filter((c) => c.track === 'soft');
  const departmentProcessesCourses = allCourses.filter((c) => c.track === 'department');
  const hrProcessesCourses = allCourses.filter((c) => c.track === 'hr');

  const total = allCourses.length;
  const completionRate = total === 0 ? 0 : Math.round((completed.length / total) * 100);

  const displayName = person?.displayName || person?.email || 'Empleado';

  if (personId !== DEMO_CAREER_PLAN_PERSON_ID) {
    return (
      <div className="mx-auto max-w-2xl space-y-4 pb-16">
        <button
          type="button"
          onClick={() => navigate('/personal')}
          className="inline-flex items-center gap-1.5 text-sm font-medium text-slate-500 hover:text-slate-700 dark:text-white/50 dark:hover:text-white"
        >
          <ArrowLeft className="h-4 w-4" />
          Volver a Personal
        </button>
        <div className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-10 text-center dark:border-white/10 dark:bg-white/5">
          <span className="flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-slate-500 dark:bg-white/10 dark:text-white/60">
            <GraduationCap className="h-6 w-6" />
          </span>
          <p className="text-sm text-slate-500 dark:text-white/40">
            El plan de capacitación de un colaborador es información privada. Este directorio no permite consultar el
            de otros empleados.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl space-y-6 pb-16">
      <button
        type="button"
        onClick={() => navigate('/personal')}
        className="inline-flex items-center gap-1.5 text-sm font-medium text-slate-500 hover:text-slate-700 dark:text-white/50 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" />
        Volver a Personal
      </button>

      {/* Header */}
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-indigo-600 via-indigo-500 to-violet-600 p-6 text-white shadow-lg shadow-indigo-500/20 dark:border-white/10 sm:p-8">
        <div className="flex flex-col gap-6 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 text-xl font-bold ring-1 ring-white/25 backdrop-blur">
              {initialsOf(displayName)}
            </span>
            <div>
              <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-white/70">
                <LayoutDashboard className="h-3.5 w-3.5" />
                Dashboard de capacitación
              </p>
              <h1 className="mt-1 text-2xl font-semibold">{displayName}</h1>
              <p className="mt-0.5 text-sm text-white/80">
                {person?.jobRoleTitle ?? 'Sin puesto asignado'}
                {person?.departmentName ? ` · ${person.departmentName}` : ''}
                {person ? ` · ${ORG_ROLE_LABELS[person.orgRole] ?? person.orgRole}` : ''}
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

      {planQuery.isLoading && <p className="text-sm text-slate-400">Cargando plan de capacitación…</p>}

      {planQuery.isSuccess && !plan?.hasPlan && (
        <div className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-indigo-200 bg-indigo-50/50 p-10 text-center dark:border-indigo-500/20 dark:bg-indigo-500/5">
          <span className="flex h-12 w-12 items-center justify-center rounded-full bg-indigo-100 text-indigo-600 dark:bg-indigo-500/15 dark:text-indigo-300">
            <GraduationCap className="h-6 w-6" />
          </span>
          <p className="text-sm text-slate-500 dark:text-white/40">
            Este empleado aún no tiene un plan de capacitación generado.
          </p>
          <button
            type="button"
            onClick={() => navigate('/roadmaps')}
            className="mt-1 inline-flex items-center gap-1.5 rounded-full bg-indigo-600 px-4 py-2 text-xs font-semibold text-white hover:bg-indigo-500"
          >
            <Sparkles className="h-3.5 w-3.5" />
            Ir a Rutas de Carrera
          </button>
        </div>
      )}

      {plan?.hasPlan && (
        <>
          {/* Stat cards */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatCard label="Total" value={total} icon={LayoutDashboard} accent="slate" />
            <StatCard label="Tomados" value={completed.length} icon={CheckCircle2} accent="indigo" />
            <StatCard label="En proceso" value={inProgress.length} icon={Clock3} accent="amber" />
            <StatCard label="Pendientes" value={pending.length} icon={Hourglass} accent="rose" />
          </div>

          {plan.overallRationale && (
            <p className="rounded-2xl border border-indigo-200 bg-indigo-50/60 p-4 text-sm text-indigo-900 dark:border-indigo-500/20 dark:bg-indigo-500/10 dark:text-indigo-200">
              {plan.overallRationale}
            </p>
          )}

          <CourseSection
            title={CATEGORY_META.jobSkills.title}
            icon={CATEGORY_META.jobSkills.icon}
            accent={CATEGORY_META.jobSkills.accent}
            courses={jobSkillsCourses}
            emptyMessage="No hay cursos de su descripción de puesto todavía."
          />
          <CourseSection
            title={CATEGORY_META.softSkills.title}
            icon={CATEGORY_META.softSkills.icon}
            accent={CATEGORY_META.softSkills.accent}
            courses={softSkillsCourses}
            emptyMessage="No hay cursos de soft skills todavía."
          />
          <CourseSection
            title={CATEGORY_META.departmentProcesses.title}
            icon={CATEGORY_META.departmentProcesses.icon}
            accent={CATEGORY_META.departmentProcesses.accent}
            courses={departmentProcessesCourses}
            emptyMessage="No hay cursos de procesos del departamento todavía."
          />
          <CourseSection
            title={CATEGORY_META.hrProcesses.title}
            icon={CATEGORY_META.hrProcesses.icon}
            accent={CATEGORY_META.hrProcesses.accent}
            courses={hrProcessesCourses}
            emptyMessage="No hay cursos de procesos de Recursos Humanos todavía."
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
  indigo: {
    icon: 'bg-indigo-100 text-indigo-600 dark:bg-indigo-500/15 dark:text-indigo-300',
    heading: 'text-indigo-600 dark:text-indigo-300',
    badge: 'bg-indigo-100 text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300',
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
    <div className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5">
      <div className="flex items-center gap-3">
        <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl ${styles.icon}`}>
          <Icon className="h-4.5 w-4.5" />
        </span>
        <div>
          <p className="text-2xl font-semibold text-slate-900 dark:text-white">{value}</p>
          <p className="text-xs text-slate-400 dark:text-white/40">{label}</p>
        </div>
      </div>
    </div>
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
                // Real course already exists as a Program in the catalog —
                // employees take it in Engram, not Capability Studio.
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
                // The system auto-detected a standalone Capability whose
                // name matches this step — the employee is auto-enrolled
                // server-side, so they can go straight to taking it inside
                // the dedicated employee portal (not the consumer app).
                <a
                  href={buildEmployeePortalCapabilityUrl(course.matchedCapabilityId)}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-1 inline-flex w-fit items-center gap-1.5 rounded-full bg-emerald-600 px-2.5 py-1 text-xs font-medium text-white transition hover:bg-emerald-500"
                >
                  <PlayCircle className="h-3 w-3" />
                  Tomar el curso
                  <ExternalLink className="h-3 w-3" />
                </a>
              ) : (
                // No matching course exists yet — HR needs to generate one
                // in Capability Studio. Once its name matches this step's
                // title, the system picks it up automatically next time
                // this plan is loaded — no manual linking needed.
                <a
                  href={buildGenerateCourseUrl(course)}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-1 inline-flex w-fit items-center gap-1.5 rounded-full bg-indigo-600 px-2.5 py-1 text-xs font-medium text-white transition hover:bg-indigo-500"
                >
                  <BookOpen className="h-3 w-3" />
                  Generar curso
                  <ExternalLink className="h-3 w-3" />
                </a>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
