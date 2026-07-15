import { createBrowserRouter } from 'react-router';
import { RootLayout } from '@/layouts/RootLayout';
import { AuthenticatedAppLayout } from '@/layouts/AuthenticatedAppLayout';

export const router = createBrowserRouter([
  {
    path: '/',
    lazy: () => import('@/pages/LandingPage').then((m) => ({ Component: m.default })),
  },
  {
    // TODO: Gate this behind Microsoft Entra ID once credentials are
    // configured (see src/auth/index.ts and AuthenticatedAppLayout).
    // It is unauthenticated for now so the enterprise experience can be
    // built and previewed without a real Azure AD app registration.
    element: <AuthenticatedAppLayout />,
    children: [
      { path: 'today', lazy: () => import('@/features/today').then((m) => ({ Component: m.TodayPage })) },
      {
        path: 'growth-plan',
        lazy: () => import('@/features/growth-plan').then((m) => ({ Component: m.GrowthPlanLayout })),
        children: [
          {
            index: true,
            lazy: () => import('@/features/growth-plan').then((m) => ({ Component: m.GrowthPlanPage })),
          },
          {
            path: 'currentrole',
            lazy: () => import('@/features/enterprise-context').then((m) => ({ Component: m.WorkContextPage })),
          },
          {
            path: 'role-experience',
            lazy: () => import('@/features/role-requirements').then((m) => ({ Component: m.RoleExperiencePage })),
          },
          {
            path: 'role-experience/alignment-guide',
            lazy: () =>
              import('@/features/role-requirements/roleAlignmentGuide').then((m) => ({
                Component: m.RoleAlignmentGuidePage,
              })),
          },
        ],
      },
      {
        path: 'capabilities',
        lazy: () => import('@/features/capabilities').then((m) => ({ Component: m.CapabilitiesPage })),
      },
      { path: 'goals', lazy: () => import('@/features/goals').then((m) => ({ Component: m.GoalsPage })) },
      {
        path: 'evidence',
        lazy: () => import('@/features/evidence').then((m) => ({ Component: m.EvidencePage })),
      },
      {
        path: 'growth',
        lazy: () => import('@/features/my-evolution').then((m) => ({ Component: m.MyEvolutionPage })),
      },
      { path: 'agents', lazy: () => import('@/features/agents').then((m) => ({ Component: m.AgentsPage })) },
      {
        path: 'profile',
        lazy: () => import('@/features/profile').then((m) => ({ Component: m.ProfilePage })),
      },
      {
        path: 'settings',
        lazy: () => import('@/features/settings').then((m) => ({ Component: m.SettingsPage })),
      },
      { path: 'privacy', lazy: () => import('@/features/privacy').then((m) => ({ Component: m.PrivacyPage })) },
      { path: 'help', lazy: () => import('@/features/help').then((m) => ({ Component: m.HelpPage })) },
    ],
  },
  {
    path: '/app',
    element: <RootLayout />,
    children: [
      {
        index: true,
        lazy: () => import('@/features/onboarding').then((m) => ({ Component: m.OnboardingPage })),
      },
      {
        path: 'profile',
        lazy: () => import('@/features/profile').then((m) => ({ Component: m.ProfilePage })),
      },
      {
        path: 'capabilities',
        lazy: () => import('@/features/capabilities').then((m) => ({ Component: m.CapabilitiesPage })),
      },
      {
        path: 'practice',
        lazy: () => import('@/features/practice').then((m) => ({ Component: m.PracticePage })),
      },
      {
        path: 'recall',
        lazy: () => import('@/features/recall').then((m) => ({ Component: m.RecallPage })),
      },
      {
        path: 'application',
        lazy: () => import('@/features/application').then((m) => ({ Component: m.ApplicationPage })),
      },
      {
        path: 'evidence',
        lazy: () => import('@/features/evidence').then((m) => ({ Component: m.EvidencePage })),
      },
    ],
  },
]);
