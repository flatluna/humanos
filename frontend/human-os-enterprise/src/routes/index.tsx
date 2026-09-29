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
        path: 'organizacion/editar',
        lazy: () => import('@/pages/CompanyProfilePage').then((m) => ({ Component: m.CompanyProfilePage })),
      },
      {
        path: 'organizacion/importar',
        lazy: () => import('@/pages/OrgChartImportPage').then((m) => ({ Component: m.OrgChartImportPage })),
      },
      {
        path: 'departamentos',
        lazy: () => import('@/pages/DepartmentsPage').then((m) => ({ Component: m.DepartmentsPage })),
      },
      {
        path: 'departamentos/nuevo',
        lazy: () => import('@/pages/DepartmentDetailPage').then((m) => ({ Component: m.DepartmentDetailPage })),
      },
      {
        path: 'departamentos/:departmentId',
        lazy: () => import('@/pages/DepartmentDetailPage').then((m) => ({ Component: m.DepartmentDetailPage })),
      },
      {
        path: 'objetivos',
        lazy: () => import('@/pages/BusinessGoalsPage').then((m) => ({ Component: m.BusinessGoalsPage })),
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
      {
        path: 'personal/:personId/intereses',
        lazy: () => import('@/pages/EmployeeAcademicInterestsPage').then((m) => ({ Component: m.EmployeeAcademicInterestsPage })),
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
        path: 'proceso-aprendizaje',
        lazy: () =>
          import('@/pages/ProcessLearningWizardPage').then((m) => ({ Component: m.ProcessLearningWizardPage })),
      },
      {
        path: 'procesos',
        lazy: () =>
          import('@/pages/ProcessLearningWizardPage').then((m) => ({ Component: m.ProcessLearningWizardPage })),
      },
      {
        path: 'process-documents',
        lazy: () =>
          import('@/pages/ProcessLearningWizardPage').then((m) => ({ Component: m.ProcessLearningWizardPage })),
      },
      {
        path: 'skills/nuevo',
        lazy: () => import('@/pages/SkillDetailPage').then((m) => ({ Component: m.SkillDetailPage })),
      },
      {
        path: 'skills/:skillId/edit',
        lazy: () => import('@/pages/SkillDetailPage').then((m) => ({ Component: m.SkillDetailPage })),
      },
      {
        path: 'skills/edit/:skillId',
        lazy: () => import('@/pages/SkillDetailPage').then((m) => ({ Component: m.SkillDetailPage })),
      },
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
      {
        path: 'responsibilities',
        lazy: () => import('@/pages/ResponsibilitiesPage').then((m) => ({ Component: m.ResponsibilitiesPage })),
      },
      {
        path: 'responsibilities/nuevo',
        lazy: () =>
          import('@/pages/ResponsibilityDetailPage').then((m) => ({ Component: m.ResponsibilityDetailPage })),
      },
      {
        path: 'responsibilities/:responsibilityId/edit',
        lazy: () =>
          import('@/pages/ResponsibilityDetailPage').then((m) => ({ Component: m.ResponsibilityDetailPage })),
      },
      {
        path: 'certifications',
        lazy: () => import('@/pages/CertificationsPage').then((m) => ({ Component: m.CertificationsPage })),
      },
      {
        path: 'certifications/nuevo',
        lazy: () =>
          import('@/pages/CertificationDetailPage').then((m) => ({ Component: m.CertificationDetailPage })),
      },
      {
        path: 'certifications/:certificationId/edit',
        lazy: () =>
          import('@/pages/CertificationDetailPage').then((m) => ({ Component: m.CertificationDetailPage })),
      },
      { path: 'roadmaps', lazy: () => import('@/pages/RoadmapsPage').then((m) => ({ Component: m.RoadmapsPage })) },
      { path: 'reportes', lazy: () => import('@/pages/ReportsPage').then((m) => ({ Component: m.ReportsPage })) },
      {
        path: 'perfil',
        lazy: () =>
          import('@/pages/ProfileRedirectPage').then((m) => ({ Component: m.ProfileRedirectPage })),
      },
      {
        path: 'profile',
        lazy: () =>
          import('@/pages/ProfileRedirectPage').then((m) => ({ Component: m.ProfileRedirectPage })),
      },
    ],
  },
]);
