import { createBrowserRouter } from 'react-router';
import { EnterpriseAppLayout } from '@/layouts/EnterpriseAppLayout';

export const router = createBrowserRouter([
  {
    element: <EnterpriseAppLayout />,
    children: [
      { index: true, lazy: () => import('@/pages/OverviewPage').then((m) => ({ Component: m.OverviewPage })) },
      {
        path: 'organizacion',
        lazy: () => import('@/pages/OrganizationPage').then((m) => ({ Component: m.OrganizationPage })),
      },
      {
        path: 'organizacion/importar',
        lazy: () => import('@/pages/OrgChartImportPage').then((m) => ({ Component: m.OrgChartImportPage })),
      },
      { path: 'personal', lazy: () => import('@/pages/PeoplePage').then((m) => ({ Component: m.PeoplePage })) },
      {
        path: 'personal/:personId',
        lazy: () => import('@/pages/EmployeeDetailPage').then((m) => ({ Component: m.EmployeeDetailPage })),
      },
      {
        path: 'personal/:personId/plan-capacitacion',
        lazy: () =>
          import('@/pages/EmployeeCareerPlanPage').then((m) => ({ Component: m.EmployeeCareerPlanPage })),
      },
      { path: 'roles', lazy: () => import('@/pages/RolesPage').then((m) => ({ Component: m.RolesPage })) },
      {
        path: 'roles/nuevo',
        lazy: () => import('@/pages/RoleDetailPage').then((m) => ({ Component: m.RoleDetailPage })),
      },
      {
        path: 'roles/:jobRoleId',
        lazy: () => import('@/pages/RoleDetailPage').then((m) => ({ Component: m.RoleDetailPage })),
      },
      { path: 'skills', lazy: () => import('@/pages/SkillsPage').then((m) => ({ Component: m.SkillsPage })) },
      {
        path: 'skills/job-skills',
        lazy: () => import('@/pages/SkillsPage').then((m) => ({ Component: m.SkillsPage })),
      },
      {
        path: 'skills/soft-skills',
        lazy: () => import('@/pages/SkillsPage').then((m) => ({ Component: m.SkillsPage })),
      },
      {
        path: 'skills/department-processes',
        lazy: () => import('@/pages/SkillsPage').then((m) => ({ Component: m.SkillsPage })),
      },
      {
        path: 'skills/hr-processes',
        lazy: () => import('@/pages/SkillsPage').then((m) => ({ Component: m.SkillsPage })),
      },
      { path: 'roadmaps', lazy: () => import('@/pages/RoadmapsPage').then((m) => ({ Component: m.RoadmapsPage })) },
      { path: 'reportes', lazy: () => import('@/pages/ReportsPage').then((m) => ({ Component: m.ReportsPage })) },
    ],
  },
]);
