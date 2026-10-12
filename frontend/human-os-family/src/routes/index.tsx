import { createBrowserRouter, Navigate } from 'react-router';
import { FamilyAppLayout } from '../components/layout/FamilyAppLayout';
import { OverviewPage } from '../pages/OverviewPage';
import { FamilyPage } from '../pages/FamilyPage';
import { NewFamilyMemberPage } from '../pages/NewFamilyMemberPage';
import { PersonalDevelopmentPage } from '../pages/PersonalDevelopmentPage';
import { CapabilitiesPage } from '../pages/CapabilitiesPage';
import { LearningPage } from '../pages/LearningPage';
import { InsightsPage } from '../pages/InsightsPage';
import { AdminPage } from '../pages/AdminPage';

export const router = createBrowserRouter([
  {
    element: <FamilyAppLayout />,
    children: [
      {
        index: true,
        element: <OverviewPage />,
      },
      {
        path: 'overview',
        element: <Navigate to="/" replace />,
      },
      {
        path: 'family',
        element: <FamilyPage />,
      },
      {
        path: 'family/new',
        element: <NewFamilyMemberPage />,
      },
      {
        path: 'family/nuevo',
        element: <Navigate to="/family/new" replace />,
      },
      {
        path: 'personal-development',
        element: <PersonalDevelopmentPage />,
      },
      {
        path: 'capabilities',
        element: <CapabilitiesPage />,
      },
      {
        path: 'skills',
        element: <Navigate to="/capabilities" replace />,
      },
      {
        path: 'learning',
        element: <LearningPage />,
      },
      {
        path: 'insights',
        element: <InsightsPage />,
      },
      {
        path: 'admin',
        element: <AdminPage />,
      },
      {
        path: '*',
        element: <Navigate to="/" replace />,
      },
    ],
  },
]);
