import { createBrowserRouter, redirect } from 'react-router';
import { EmployeeAppLayout } from '@/layouts/EmployeeAppLayout';
import { withAcademicGuard } from '@/components/RequireAcademicTenant';

/** Routes deliberately mirror human-os-web's /capabilities/* structure
 * (same ported feature module, see src/features/capabilities) so a
 * personId-scoped runtime session behaves identically here — just under
 * this app's own shell/branding instead of the consumer app's.
 *
 * 2026-08-01: added the dashboard ("Hoy") as the new index route + "Mi
 * Cerebro" (mastered-engrams/fading-memory dashboard), both ported from
 * human-os-web — see src/features/today and src/features/brain. The
 * capability catalog moved from `/` to `/capabilities` to make room.
 *
 * 2026-08-01 (later same day): "Mis Cursos" (`/capabilities` index) now
 * renders the employee's own career-plan roadmap (`CareerPlanPage`,
 * previously only reachable at `/plan-capacitacion`) instead of the
 * generic "browse every published capability" catalog — per explicit
 * product decision: this portal should only ever show the employee their
 * ASSIGNED training (matched courses are playable now, unmatched ones
 * show an "En proceso / Desarrollo" badge), never a free-browse catalog.
 * `/capabilities/:capabilityId(/nodes/:nodeId)` still route to the real
 * catalog/graph/runtime pages — CareerPlanPage's "Tomar el curso" links
 * navigate straight there. The standalone `/plan-capacitacion` route and
 * sidebar entry were removed since they'd now be an exact duplicate.
 *
 * 2026-08-01 (evening, take 3): course categories are now REAL separate
 * routes — `/capabilities/job-skills`, `/capabilities/soft-skills`,
 * `/capabilities/department-processes`, `/capabilities/hr-processes` —
 * each its own bookmarkable/linkable "page" (all rendered by the same
 * `CareerPlanPage` component, which derives the active category from the
 * URL path — see CareerPlanPage.tsx). The user explicitly rejected both
 * an earlier #hash-anchor-scroll version AND a `?tab=` query-param
 * version before landing on this real-routes approach. Bare
 * `/capabilities` just redirects to the first category so old
 * links/the "Mis Cursos" parent nav item still go somewhere sensible.
 * These static category paths safely coexist with the dynamic
 * `/capabilities/:capabilityId` route below it — react-router ranks
 * static path segments higher than dynamic ones, so a real capability
 * GUID never collides with a category slug (same pattern as avoiding
 * reserved route words — see /memories/repo, azure-functions-gotchas
 * "runtime reserved route prefix" note, same class of issue). */
export const router = createBrowserRouter([
  {
    element: <EmployeeAppLayout />,
    children: [
      {
        index: true,
        lazy: () => import('@/features/today').then((m) => ({ Component: m.TodayPage })),
      },
      {
        path: 'today',
        loader: () => redirect('/'),
      },
      {
        path: 'start-in-your-role',
        lazy: () => import('@/features/start-in-your-role').then((m) => ({ Component: m.StartInYourRolePage })),
      },
      {
        path: 'in-your-role',
        lazy: () => import('@/features/in-your-role').then((m) => ({ Component: m.InYourRolePage })),
      },
      {
        path: 'advance-your-knowledge',
        lazy: () => import('@/features/advance-your-knowledge').then((m) => ({ Component: m.AdvanceYourKnowledgePage })),
      },
      {
        path: 'master-your-job',
        lazy: () => import('@/features/master-your-job').then((m) => ({ Component: m.MasterYourJobPage })),
      },
      {
        path: 'tec-contigo',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecContigoIndexPage) })),
      },
      {
        path: 'tec-contigo/mientras-estudias',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecMientrasEstudiasPage) })),
      },
      {
        path: 'tec-contigo/comienza-tu-carrera',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecComienzaTuCarreraPage) })),
      },
      {
        path: 'tec-contigo/mantente-vigente',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecMantenteVigentePage) })),
      },
      {
        path: 'tec-contigo/practica-y-refuerza',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecPracticaYRefuerzaPage) })),
      },
      {
        path: 'tec-contigo/conecta-y-contribuye',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecConectaYContribuyePage) })),
      },
      {
        path: 'tec-contigo/semestre-actual',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecSemestreActualPage) })),
      },
      {
        path: 'tec-contigo/industria-monterrey',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecIndustriaMonterreyPage) })),
      },
      {
        path: 'tec-contigo/objetivo-profesional',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecObjetivoProfesionalPage) })),
      },
      {
        path: 'tec-contigo/fortalecer-conocimientos',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecFortalecerConocimientosPage) })),
      },
      {
        path: 'tec-contigo/capacidades-humanas',
        lazy: () => import('@/features/tec-contigo').then((m) => ({ Component: withAcademicGuard(m.TecCapacidadesHumanasPage) })),
      },
      {
        path: 'brain',
        lazy: () => import('@/features/brain').then((m) => ({ Component: m.BrainPage })),
      },
      {
        // "Mi Evolución" (2026-08-08) — cumulative engrams-mastered growth
        // chart, ported from human-os-web's /my-evolution.
        path: 'my-evolution',
        lazy: () => import('@/features/my-evolution').then((m) => ({ Component: m.MyEvolutionPage })),
      },
      {
        // "Mis Logros" / "My Achievements" — Gamification badges, milestones and habits
        path: 'achievements',
        lazy: () => import('@/features/achievements').then((m) => ({ Component: m.AchievementsPage })),
      },
      {
        path: 'logros',
        loader: () => redirect('/achievements'),
      },
      {
        path: 'my-achievements',
        loader: () => redirect('/achievements'),
      },
      {
        path: 'mis-logros',
        loader: () => redirect('/achievements'),
      },
      {
        // "Mi Perfil" (2026-08-02) — self-service profile view/edit, ported
        // pattern-wise from human-os-enterprise's EmployeeDetailPage resume
        // section, but scoped to only the signed-in employee's own
        // PersonProfile (contact info/bio), never the HR-only org
        // assignment fields (Department/JobRole/OrgRole).
        path: 'profile',
        lazy: () => import('@/features/profile').then((m) => ({ Component: m.ProfilePage })),
      },
      {
        // "Objetivos & Mi Puesto" — Full Job Description + SMART Employee Objectives
        // aligned with Department Objectives and Company Strategy.
        path: 'objectives',
        lazy: () => import('@/features/objectives').then((m) => ({ Component: m.ObjectivesPage })),
      },
      {
        path: 'objetivos',
        loader: () => redirect('/objectives'),
      },
      {
        // "Programas" — Programs catalog and chronological progression management
        path: 'programs',
        lazy: () => import('@/features/programs').then((m) => ({ Component: m.ProgramsPage })),
      },
      {
        path: 'programs/:programId',
        lazy: () => import('@/features/programs').then((m) => ({ Component: m.ProgramDetailPage })),
      },
      {
        path: 'programs/:programId/entities',
        lazy: () => import('@/features/program-entities').then((m) => ({ Component: m.ProgramEntitiesDetailPage })),
      },
      {
        path: 'program-entities',
        lazy: () => import('@/features/program-entities').then((m) => ({ Component: m.ProgramEntitiesIndexPage })),
      },
      {
        path: 'program-entities/:programId',
        lazy: () => import('@/features/program-entities').then((m) => ({ Component: m.ProgramEntitiesDetailPage })),
      },
      {
        path: 'programas',
        loader: () => redirect('/programs'),
      },
      {
        // "Learning Labs" (2026-09-14) — read-only catalog of published
        // Simulation Labs (see Human OS Studio /labs), enroll-only for now.
        path: 'learning-labs',
        lazy: () => import('@/features/learning-labs').then((m) => ({ Component: m.LearningLabsPage })),
      },
      {
        // Attempt run page (2026-09-15) — full page (no modal), one per attempt:
        // conversation -> evaluation -> results. idAttempt is optional on first visit;
        // the page creates the attempt then replaces the URL with its real id.
        path: 'learning-labs/:idLab/attempts/:idAttempt?',
        lazy: () => import('@/features/learning-labs').then((m) => ({ Component: m.AttemptPage })),
      },
      {
        // Same page, reached from "My Attempts" history links which only know the attempt id.
        path: 'learning-labs/attempts/:idAttempt',
        lazy: () => import('@/features/learning-labs').then((m) => ({ Component: m.AttemptPage })),
      },
      {
        // "Program AI Agents" — Multi-course intelligence across all engrams of a program
        path: 'program-agents',
        lazy: () => import('@/features/program-agents').then((m) => ({ Component: m.ProgramAgentsPage })),
      },
      {
        path: 'program-agents/:programId',
        lazy: () => import('@/features/program-agents').then((m) => ({ Component: m.ProgramAgentDetailPage })),
      },
      {
        // "Cursos Disponibles" — Only courses and engrams that are ready & available in the catalog
        path: 'available-courses',
        lazy: () => import('@/features/available-courses').then((m) => ({ Component: m.AvailableCoursesPage })),
      },
      {
        path: 'cursos-disponibles',
        loader: () => redirect('/available-courses'),
      },
      {
        // "AI Agents" (2026-09-04) — one specialized chat agent per
        // enrolled course, grounded only in that course's own engrams.
        path: 'ai-agents',
        lazy: () => import('@/features/ai-agents').then((m) => ({ Component: m.AiAgentsPage })),
      },
      {
        path: 'ai-agents/:documentId',
        lazy: () => import('@/features/ai-agents').then((m) => ({ Component: m.AiAgentCoursePage })),
      },
      {
        // "CQI Workspace" (HermanScience, 2026-09-21) — upload a CQI
        // assessment report PDF and view the AI-extracted structured profile.
        path: 'cqi-workspace',
        lazy: () => import('@/features/cqi-workspace').then((m) => ({ Component: m.CqiWorkspacePage })),
      },
      {
        path: 'capabilities',
        loader: () => redirect('/capabilities/job-skills'),
      },
      {
        path: 'capabilities/job-skills',
        lazy: () => import('@/features/career-plan').then((m) => ({ Component: m.CareerPlanPage })),
      },
      {
        path: 'capabilities/soft-skills',
        lazy: () => import('@/features/career-plan').then((m) => ({ Component: m.CareerPlanPage })),
      },
      {
        path: 'capabilities/department-processes',
        lazy: () => import('@/features/career-plan').then((m) => ({ Component: m.CareerPlanPage })),
      },
      {
        path: 'capabilities/hr-processes',
        lazy: () => import('@/features/career-plan').then((m) => ({ Component: m.CareerPlanPage })),
      },
      {
        path: 'capabilities/:capabilityId',
        lazy: () => import('@/features/capabilities').then((m) => ({ Component: m.CapabilityGraphPage })),
      },
      {
        path: 'capabilities/:capabilityId/nodes/:nodeId',
        lazy: () => import('@/features/capabilities').then((m) => ({ Component: m.CapabilityNodePage })),
      },
      {
        path: 'structured-learning/:documentId',
        lazy: () => import('@/features/capabilities/pages/StructuredLearningCoursePage').then((m) => ({ Component: m.default })),
      },
      {
        path: 'structured-learning/:documentId/sections/:sectionId',
        lazy: () => import('@/features/capabilities/pages/StructuredLearningSectionPage').then((m) => ({ Component: m.default })),
      },
    ],
  },
]);
