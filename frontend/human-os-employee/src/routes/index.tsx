import { createBrowserRouter, redirect } from 'react-router';
import { EmployeeAppLayout } from '@/layouts/EmployeeAppLayout';

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
        path: 'brain',
        lazy: () => import('@/features/brain').then((m) => ({ Component: m.BrainPage })),
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
    ],
  },
]);
